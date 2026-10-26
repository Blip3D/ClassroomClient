using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ClassroomClient.API;
using ClassroomClient.Networking;

#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

namespace ClassroomClient.Core
{
    public class ClassroomSceneManager : MonoBehaviour
    {
        private ClassroomClientManager _manager;
        private Coroutine _activeLoadCoroutine;
        private bool _isLoading;

        // The content scene ClassroomClient itself loaded (the ONLY scene it will ever unload).
        // App scenes (the startup/bootstrap scene, scenes the app or a custom loader loaded) are
        // never unloaded by ClassroomClient, so the app's camera rig and managers are never destroyed.
        private Scene _ownedScene;
        private bool _hasOwnedScene;

        private bool _ownedIsAddressable;

#if UNITY_ADDRESSABLES
        private AsyncOperationHandle<SceneInstance> _currentAddressableHandle;
        private bool _hasAddressableHandle;
#endif

        public event Action<SceneInfo> OnLoadSceneRequested;
        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadCompleted;
        public event Action<string, string> OnSceneLoadFailed;

        public void Initialize(WebSocketClient wsClient, ClassroomClientManager manager)
        {
            _manager = manager;
        }

        void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            // Deactivating the object stops its coroutines without running their finally blocks.
            _isLoading = false;
            _activeLoadCoroutine = null;
        }

        // The app changed scene by itself (not a ClassroomClient load): forget our scene if the app
        // unloaded it, and tell the dashboard what is showing now.
        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (_isLoading || !next.IsValid()) return;
            if (_hasOwnedScene && !_ownedScene.isLoaded)
            {
                ForgetOwnedScene(releaseAddressable: true);
            }
            _manager?.ReportCurrentScene(KeyForScene(next));
        }

        public void LoadScene(SceneInfo info)
        {
            if (info == null)
            {
                Debug.LogWarning("[ClassroomSceneManager] LoadScene called with null SceneInfo — scene key not in library");
                return;
            }

            if (_isLoading)
            {
                Fail(info.sceneKey, "Another scene is still loading — try again when it has finished");
                return;
            }

            if (info.customLoading)
            {
                RequestCustomLoad(info);
                return;
            }

            if (IsAlreadyLoaded(info))
            {
                // Loading it again would add a second copy (two environments, two rigs).
                Completed(info.sceneKey);
                return;
            }

            switch (info.loadType)
            {
                case SceneLoadType.Standard:
                    _activeLoadCoroutine = StartCoroutine(RunLoad(info.sceneKey, LoadStandardScene(info)));
                    break;
                case SceneLoadType.Addressable:
                    _activeLoadCoroutine = StartCoroutine(RunLoad(info.sceneKey, LoadAddressableScene(info)));
                    break;
                default:
                    Fail(info.sceneKey, $"Unknown loadType: {info.loadType}");
                    break;
            }
        }

        // Custom loading: the app loads the scene itself and reports the result via
        // ClassroomClientAPI.ReportSceneLoaded / ReportSceneLoadFailed.
        private void RequestCustomLoad(SceneInfo info)
        {
            if (OnLoadSceneRequested == null && !ClassroomEvents.HasLoadSceneRequestedHandler)
            {
                Fail(info.sceneKey, "Custom loading is enabled for this scene but the app has no loader registered (subscribe to ClassroomEvents.OnLoadSceneRequested)");
                return;
            }

            try
            {
                OnLoadSceneRequested?.Invoke(info);
                ClassroomEvents.FireOnLoadSceneRequested(info);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Fail(info.sceneKey, $"Custom scene loader threw: {e.Message}");
            }
        }

        // Steps the load by hand so an exception inside it is reported to the teacher instead of
        // silently ending the coroutine with _isLoading stuck.
        private IEnumerator RunLoad(string sceneKey, IEnumerator load)
        {
            _isLoading = true;
            while (true)
            {
                object current;
                try
                {
                    if (!load.MoveNext()) break;
                    current = load.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Fail(sceneKey, e.Message);
                    break;
                }
                yield return current;
            }
            _isLoading = false;
            _activeLoadCoroutine = null;
        }

        private bool IsAlreadyLoaded(SceneInfo info)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(info.sceneKey);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                if (scene.path == info.sceneKey || scene.name == info.sceneKey || scene.name == name) return true;
            }
            return false;
        }

        // ── Standard scene loading ────────────────────────────────────────────

        private IEnumerator LoadStandardScene(SceneInfo info)
        {
            Started(info.sceneKey);
            Debug.Log($"[ClassroomSceneManager] Loading standard scene: {info.sceneKey}");

            AsyncOperation op = null;
            try
            {
                op = SceneManager.LoadSceneAsync(info.sceneKey, LoadSceneMode.Additive);
            }
            catch (Exception e)
            {
                Fail(info.sceneKey, e.Message);
                yield break;
            }

            if (op == null)
            {
                Fail(info.sceneKey, $"Scene '{info.sceneKey}' not found in Build Settings");
                yield break;
            }

            yield return op;

            // Do NOT call SetActiveScene: calling it while the OVR Camera Rig is moving to
            // DontDestroyOnLoad fires activeSceneChanged on OVRManager before the rig is stable,
            // causing the compositor to lose its frame submission path (HMD freezes).
            // The app's active scene (and its lighting settings) therefore stays as it is.
            // Unique by name: IsAlreadyLoaded() prevents a second copy being loaded.
            Scene newScene = SceneManager.GetSceneByPath(info.sceneKey);
            if (!newScene.IsValid())
                newScene = SceneManager.GetSceneByName(System.IO.Path.GetFileNameWithoutExtension(info.sceneKey));
            yield return UnloadOwnedScene();

            _ownedScene = newScene;
            _hasOwnedScene = true;
            _ownedIsAddressable = false;
            Completed(info.sceneKey);
        }

        // ── Addressable scene loading ─────────────────────────────────────────

        private IEnumerator LoadAddressableScene(SceneInfo info)
        {
#if UNITY_ADDRESSABLES
            Started(info.sceneKey);
            Debug.Log($"[ClassroomSceneManager] Loading addressable scene: {info.sceneKey}");

            var handle = Addressables.LoadSceneAsync(info.sceneKey, LoadSceneMode.Additive);
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                string reason = handle.OperationException?.Message ?? "Addressable load failed";
                Addressables.Release(handle);
                Fail(info.sceneKey, reason);
                yield break;
            }

            // Do NOT call SetActiveScene (see LoadStandardScene).
            Scene newScene = handle.Result.Scene;
            yield return UnloadOwnedScene();

            _ownedScene = newScene;
            _hasOwnedScene = true;
            _ownedIsAddressable = true;
            _currentAddressableHandle = handle;
            _hasAddressableHandle = true;
            Completed(info.sceneKey);
#else
            Fail(info.sceneKey, "Addressables package not installed");
            yield break;
#endif
        }

        // ── Unloading the previous content scene ──────────────────────────────

        /// <summary>
        /// Called when the app reports that it loaded a Custom Loading scene: the scene ClassroomClient
        /// loaded earlier is no longer the content, so it is unloaded.
        /// </summary>
        public void ReleaseOwnedScene()
        {
            if (!_hasOwnedScene || _isLoading) return;
            StartCoroutine(UnloadOwnedScene());
        }

        // Unloads only the scene ClassroomClient loaded last time, never an app scene.
        private IEnumerator UnloadOwnedScene()
        {
            if (!_hasOwnedScene) yield break;
            Scene previous = _ownedScene;
            if (!previous.IsValid() || !previous.isLoaded)
            {
                ForgetOwnedScene(releaseAddressable: true);
                yield break;
            }

            // Hide the old scene first so old and new environments are never visible together.
            foreach (var root in previous.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                foreach (var l in root.GetComponentsInChildren<Light>(true)) l.enabled = false;
            }

#if UNITY_ADDRESSABLES
            if (_ownedIsAddressable && _hasAddressableHandle && _currentAddressableHandle.IsValid())
            {
                yield return Addressables.UnloadSceneAsync(_currentAddressableHandle);
                ForgetOwnedScene(releaseAddressable: false);
                Debug.Log($"[ClassroomSceneManager] Unloaded previous addressable scene: {previous.name}");
                yield break;
            }
#endif
            yield return SceneManager.UnloadSceneAsync(previous);
            ForgetOwnedScene(releaseAddressable: false);
            Debug.Log($"[ClassroomSceneManager] Unloaded previous scene: {previous.name}");
        }

        private void ForgetOwnedScene(bool releaseAddressable)
        {
#if UNITY_ADDRESSABLES
            if (releaseAddressable && _hasAddressableHandle && _currentAddressableHandle.IsValid())
            {
                Addressables.Release(_currentAddressableHandle);
            }
            _hasAddressableHandle = false;
#endif
            _hasOwnedScene = false;
            _ownedIsAddressable = false;
        }

        private string KeyForScene(Scene scene)
        {
            SceneLibrary library = _manager != null ? _manager.SceneLibrary : null;
            if (library != null)
            {
                foreach (var info in library.scenes)
                {
                    if (info == null || string.IsNullOrEmpty(info.sceneKey)) continue;
                    if (info.sceneKey == scene.path || info.sceneKey == scene.name ||
                        System.IO.Path.GetFileNameWithoutExtension(info.sceneKey) == scene.name)
                        return info.sceneKey;
                }
            }
            return scene.name;
        }

        // ── Result reporting ──────────────────────────────────────────────────

        private void Started(string sceneKey)
        {
            OnSceneLoadStarted?.Invoke(sceneKey);
        }

        private void Completed(string sceneKey)
        {
            _manager?.RequestStreamCameraCheck();
            _manager?.ReportCurrentScene(sceneKey);
            _manager?.ReportSceneLoaded(sceneKey);
            OnSceneLoadCompleted?.Invoke(sceneKey);
            ClassroomEvents.FireOnSceneLoadCompleted(sceneKey);
            Debug.Log($"[ClassroomSceneManager] Scene loaded: {sceneKey}");
        }

        private void Fail(string sceneKey, string reason)
        {
            Debug.LogError($"[ClassroomSceneManager] Scene load failed for '{sceneKey}': {reason}");
            _manager?.ReportSceneLoadFailed(sceneKey, reason);
            OnSceneLoadFailed?.Invoke(sceneKey, reason);
            ClassroomEvents.FireOnSceneLoadFailed(sceneKey, reason);
        }
    }
}
