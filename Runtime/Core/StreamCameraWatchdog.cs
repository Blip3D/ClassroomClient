using UnityEngine;
using UnityEngine.SceneManagement;
using ClassroomClient.API;

namespace ClassroomClient.Core
{
    /// <summary>
    /// Keeps the stream alive when the VR app changes scene by itself or destroys/replaces its
    /// camera rig. Works the same however the scene was loaded (Build Settings, Addressables or a
    /// custom loader), because every load goes through SceneManager and the periodic check catches
    /// anything else.
    /// Automatic mode: re-attaches the stream to Camera.main.
    /// API Controlled mode: raises ClassroomEvents.OnStreamCameraLost so the app can call
    /// ClassroomClientAPI.SetStreamCamera(camera); falls back to Camera.main after a grace period
    /// so the stream never stays dead.
    /// </summary>
    public class StreamCameraWatchdog : MonoBehaviour
    {
        private const float CheckIntervalSeconds = 0.5f;
        private const float ApiFallbackSeconds = 3f;

        private ClassroomClientManager _manager;
        private float _nextCheckTime;
        private float _lostSince = -1f;

        public void Initialize(ClassroomClientManager manager)
        {
            _manager = manager;
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        /// <summary>Run the camera check on the next frame instead of waiting for the interval.</summary>
        public void RequestCheck()
        {
            _nextCheckTime = 0f;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RequestCheck();

        private void OnActiveSceneChanged(Scene previous, Scene next) => RequestCheck();

        void Update()
        {
            if (_manager == null || Time.unscaledTime < _nextCheckTime) return;
            _nextCheckTime = Time.unscaledTime + CheckIntervalSeconds;
            Check();
        }

        private void Check()
        {
            if (_manager.CurrentState != ConnectionState.InSession || !IsLost())
            {
                _lostSince = -1f;
                return;
            }

            // Only our capture camera was destroyed while the source is still there — rebuild it.
            Camera source = _manager.GetStreamSourceCamera();
            if (_manager.GetStreamCamera() == null && source != null && source.gameObject.activeInHierarchy)
            {
                _manager.SetStreamCamera(source);
                return;
            }

            if (!_manager.IsApiControlledCameraMode)
            {
                TryAttachMainCamera();
                return;
            }

            // API Controlled mode before the app ever chose a camera keeps the WS2 behaviour:
            // wait for SetStreamCamera without auto-picking anything.
            if (ReferenceEquals(_manager.GetStreamSourceCamera(), null)) return;

            if (_lostSince < 0f)
            {
                _lostSince = Time.unscaledTime;
                Debug.LogWarning("[StreamCameraWatchdog] Stream camera lost (API Controlled mode) — call ClassroomClientAPI.SetStreamCamera(camera). Falling back to the main camera in " + ApiFallbackSeconds + "s.");
                ClassroomEvents.FireOnStreamCameraLost();
                return;
            }

            if (Time.unscaledTime - _lostSince >= ApiFallbackSeconds)
            {
                TryAttachMainCamera();
            }
        }

        private bool IsLost()
        {
            Camera source = _manager.GetStreamSourceCamera();
            Camera dedicated = _manager.GetStreamCamera();
            // A deactivated rig counts as lost; a camera COMPONENT that is merely disabled does not
            // (apps may pass a disabled camera as a pure viewpoint anchor).
            return dedicated == null || source == null || !source.gameObject.activeInHierarchy;
        }

        private void TryAttachMainCamera()
        {
            Camera main = Camera.main;
            if (main == null || main == _manager.GetStreamSourceCamera()) return;
            Debug.Log($"[StreamCameraWatchdog] Re-attaching stream to main camera '{main.name}'.");
            _manager.SetStreamCamera(main);
        }
    }
}
