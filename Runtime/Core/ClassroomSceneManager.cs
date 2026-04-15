using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private WebSocketClient _webSocketClient;
        private ClassroomClientManager _manager;
        private Coroutine _activeLoadCoroutine;

        // Name of the scene that was active when Initialize() was called (the persistent/bootstrap scene)
        private string _startupSceneName;

        // Name of the scene currently displayed as content
        private string _currentContentSceneName;

        // Whether the current content scene was loaded via Addressables
        private bool _currentIsAddressable;

#if UNITY_ADDRESSABLES
        private AsyncOperationHandle<SceneInstance> _currentAddressableHandle;
        private bool _hasAddressableHandle;
#endif

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<SceneInfo> OnLoadSceneRequested;
        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadCompleted;
        public event Action<string, string> OnSceneLoadFailed;

        // ─────────────────────────────────────────────────────────────────────

        public void Initialize(WebSocketClient wsClient, ClassroomClientManager manager)
        {
            _webSocketClient = wsClient;
            _manager = manager;
            _startupSceneName = SceneManager.GetActiveScene().name;
            _currentContentSceneName = _startupSceneName;
            _currentIsAddressable = false;
        }

        public void LoadScene(SceneInfo info)
        {
            if (info == null)
            {
                Debug.LogWarning("[ClassroomSceneManager] LoadScene called with null SceneInfo — scene key not in library");
                return;
            }

            if (info.customLoading)
            {
                OnLoadSceneRequested?.Invoke(info);
                return;
            }

            if (_activeLoadCoroutine != null)
            {
                StopCoroutine(_activeLoadCoroutine);
                _activeLoadCoroutine = null;
            }

            switch (info.loadType)
            {
                case SceneLoadType.Standard:
                    _activeLoadCoroutine = StartCoroutine(LoadStandardScene(info));
                    break;
                case SceneLoadType.Addressable:
                    _activeLoadCoroutine = StartCoroutine(LoadAddressableScene(info));
                    break;
                default:
                    Debug.LogWarning($"[ClassroomSceneManager] Unknown loadType: {info.loadType}");
                    break;
            }
        }

        // ── Standard scene loading ────────────────────────────────────────────

        private IEnumerator LoadStandardScene(SceneInfo info)
        {
            OnSceneLoadStarted?.Invoke(info.sceneKey);
            Debug.Log($"[ClassroomSceneManager] Loading standard scene: {info.sceneKey}");

            // Snapshot previous scene state before loading
            string previousSceneName = _currentContentSceneName;
            bool previousWasAddressable = _currentIsAddressable;
#if UNITY_ADDRESSABLES
            AsyncOperationHandle<SceneInstance> previousAddressableHandle = default;
            bool hasPreviousAddressableHandle = false;
            if (previousWasAddressable && _hasAddressableHandle)
            {
                previousAddressableHandle = _currentAddressableHandle;
                hasPreviousAddressableHandle = true;
            }
#endif

            AsyncOperation op = null;
            try
            {
                op = SceneManager.LoadSceneAsync(info.sceneKey, LoadSceneMode.Additive);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ClassroomSceneManager] Failed to start loading '{info.sceneKey}': {e.Message}");
                SendSceneLoadFailed(info.sceneKey, e.Message);
                OnSceneLoadFailed?.Invoke(info.sceneKey, e.Message);
                yield break;
            }

            if (op == null)
            {
                string reason = $"Scene '{info.sceneKey}' not found in Build Settings";
                Debug.LogError($"[ClassroomSceneManager] {reason}");
                SendSceneLoadFailed(info.sceneKey, reason);
                OnSceneLoadFailed?.Invoke(info.sceneKey, reason);
                yield break;
            }

            yield return op;

            // Record the new scene name but do NOT call SetActiveScene.
            // Calling SetActiveScene while the OVR Camera Rig is moving to DontDestroyOnLoad
            // fires activeSceneChanged on OVRManager before the Camera Rig is stable,
            // causing the compositor to lose its frame submission path (HMD freezes).
            // Unity will automatically promote the new scene to active when the old one unloads.
            string newSceneName = System.IO.Path.GetFileNameWithoutExtension(info.sceneKey);

            _currentContentSceneName = newSceneName;
            _currentIsAddressable = false;
#if UNITY_ADDRESSABLES
            _hasAddressableHandle = false;
#endif

            // Unload the previous scene.
            // Before unloading, hide its renderers and lights so the old and new
            // environments are never visible simultaneously (prevents flicker).
#if UNITY_ADDRESSABLES
            if (hasPreviousAddressableHandle)
            {
                yield return Addressables.UnloadSceneAsync(previousAddressableHandle);
                Debug.Log($"[ClassroomSceneManager] Unloaded previous addressable scene: {previousSceneName}");
            }
            else
#endif
            if (!string.IsNullOrEmpty(previousSceneName))
            {
                Scene prevScene = SceneManager.GetSceneByName(previousSceneName);
                if (prevScene.IsValid() && prevScene.isLoaded)
                {
                    foreach (var root in prevScene.GetRootGameObjects())
                    {
                        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                            r.enabled = false;
                        foreach (var l in root.GetComponentsInChildren<Light>(true))
                            l.enabled = false;
                    }
                    yield return SceneManager.UnloadSceneAsync(prevScene);
                    Debug.Log($"[ClassroomSceneManager] Unloaded previous scene: {previousSceneName}");
                }
            }

            FindAndUpdateStreamingCamera();
            SendSceneLoaded(info.sceneKey);
            OnSceneLoadCompleted?.Invoke(info.sceneKey);
            Debug.Log($"[ClassroomSceneManager] Scene loaded: {info.sceneKey}");
        }

        // ── Addressable scene loading ─────────────────────────────────────────

        private IEnumerator LoadAddressableScene(SceneInfo info)
        {
#if UNITY_ADDRESSABLES
            OnSceneLoadStarted?.Invoke(info.sceneKey);
            Debug.Log($"[ClassroomSceneManager] Loading addressable scene: {info.sceneKey}");

            // Snapshot previous scene state before loading
            string previousSceneName = _currentContentSceneName;
            bool previousWasAddressable = _currentIsAddressable;
            AsyncOperationHandle<SceneInstance> previousAddressableHandle = default;
            bool hasPreviousAddressableHandle = false;
            if (previousWasAddressable && _hasAddressableHandle)
            {
                previousAddressableHandle = _currentAddressableHandle;
                hasPreviousAddressableHandle = true;
            }

            var handle = Addressables.LoadSceneAsync(info.sceneKey, LoadSceneMode.Additive);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                // Activate the newly loaded scene
                Scene newScene = handle.Result.Scene;
                if (newScene.IsValid())
                    SceneManager.SetActiveScene(newScene);

                _currentContentSceneName = newScene.name;
                _currentIsAddressable = true;
                _currentAddressableHandle = handle;
                _hasAddressableHandle = true;

                // Unload the previous scene
                if (hasPreviousAddressableHandle)
                {
                    yield return Addressables.UnloadSceneAsync(previousAddressableHandle);
                    Debug.Log($"[ClassroomSceneManager] Unloaded previous addressable scene: {previousSceneName}");
                }
                else if (!string.IsNullOrEmpty(previousSceneName))
                {
                    Scene prevScene = SceneManager.GetSceneByName(previousSceneName);
                    if (prevScene.IsValid() && prevScene.isLoaded)
                    {
                        yield return SceneManager.UnloadSceneAsync(prevScene);
                        Debug.Log($"[ClassroomSceneManager] Unloaded previous scene: {previousSceneName}");
                    }
                }

                FindAndUpdateStreamingCamera();
                SendSceneLoaded(info.sceneKey);
                OnSceneLoadCompleted?.Invoke(info.sceneKey);
                Debug.Log($"[ClassroomSceneManager] Addressable scene loaded: {info.sceneKey}");
            }
            else
            {
                string reason = handle.OperationException?.Message ?? "Addressable load failed";
                Debug.LogError($"[ClassroomSceneManager] Addressable load failed for '{info.sceneKey}': {reason}");
                SendSceneLoadFailed(info.sceneKey, reason);
                OnSceneLoadFailed?.Invoke(info.sceneKey, reason);
            }
#else
            Debug.LogWarning("[ClassroomSceneManager] Addressable loading requires the Addressables package (com.unity.addressables). Install it or change loadType to Standard.");
            SendSceneLoadFailed(info.sceneKey, "Addressables package not installed");
            OnSceneLoadFailed?.Invoke(info.sceneKey, "Addressables package not installed");
            yield break;
#endif
        }

        // ── Post-load camera maintenance ──────────────────────────────────────

        private void FindAndUpdateStreamingCamera()
        {
            if (_manager == null) return;

            // If the existing streaming camera is still alive, leave it alone.
            // In setup wizard projects the streaming camera lives in DontDestroyOnLoad
            // as a child of the Camera Rig and survives all scene changes — this check
            // returns immediately every time in that architecture.
            Camera existing = _manager.GetStreamCamera();
            if (existing != null && existing.isActiveAndEnabled) return;

            // Streaming camera was destroyed (Camera Rig not in DontDestroyOnLoad).
            // Search for a replacement. Prefer a camera with a RenderTexture target —
            // that is the dedicated streaming camera created by the setup wizard.
            Camera streamCam = null;

            foreach (var cam in Camera.allCameras)
            {
                if (cam.targetTexture != null)
                {
                    streamCam = cam;
                    break;
                }
            }

            if (streamCam == null)
                streamCam = Camera.main;

            if (streamCam == null)
                streamCam = FindAnyObjectByType<Camera>();

            if (streamCam == null)
            {
                Debug.LogWarning("[ClassroomSceneManager] No camera found after scene load — streaming camera not updated");
                return;
            }

            _manager.SetStreamCamera(streamCam);
        }

        // ── Wire protocol helpers ─────────────────────────────────────────────

        private void SendSceneLoaded(string sceneKey)
        {
            if (_webSocketClient == null || !_webSocketClient.IsConnected) return;
            string deviceId = UnityEngine.SystemInfo.deviceUniqueIdentifier;
            _webSocketClient.SendMessage($"SCENE_LOADED|{deviceId}|SERVER|{sceneKey}");
        }

        private void SendSceneLoadFailed(string sceneKey, string reason)
        {
            if (_webSocketClient == null || !_webSocketClient.IsConnected) return;
            string deviceId = UnityEngine.SystemInfo.deviceUniqueIdentifier;
            _webSocketClient.SendMessage($"SCENE_LOAD_FAILED|{deviceId}|SERVER|{sceneKey}|{reason}");
        }
    }
}
