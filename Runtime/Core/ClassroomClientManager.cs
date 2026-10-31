using UnityEngine;
using Unity.WebRTC;
using System;
using System.Collections;
using ClassroomClient.API;
using ClassroomClient.HUD;
using ClassroomClient.Networking;

namespace ClassroomClient.Core
{
    public class ClassroomClientManager : MonoBehaviour
    {
        [Header("Manual Network Configuration")]
        [HideInInspector] [SerializeField] private string serverUrl = "";
        [HideInInspector] [SerializeField] private string serverToken = "";
        [SerializeField] private string appName = "VR Training App";

        public enum CameraSetupMode { Automatic, ApiControlled }

        [Header("Camera Setup")]
        [Tooltip("Automatic: ClassroomClient creates a dedicated streaming camera that follows the main/XR camera (normal projects). API Controlled: call ClassroomClientAPI.SetStreamCamera(camera) at runtime to choose the streamed viewpoint.")]
        [SerializeField] private CameraSetupMode cameraSetupMode = CameraSetupMode.Automatic;
        // The DEDICATED capture camera (ClassroomClient-owned, marked, renders to its own RenderTexture).
        // The ONLY camera WebRTC captures — never the HMD/eye/source camera.
        [SerializeField] private Camera streamCamera;
        // The source/viewpoint being mirrored (may be the eye camera). Never captured, never disabled.
        private Camera streamSourceCamera;
        // True when streaming was requested before a source camera existed (API Controlled mode).
        private bool _streamRequested;

        [Header("Streamed Audio")]
        [Tooltip("Send the app's sound (what the user hears) to the dashboard together with the video. The headset's own sound is never changed.")]
        [SerializeField] private bool streamAppAudio = true;
        [Tooltip("Also send the headset microphone to the dashboard. Requires the CLASSROOMCLIENT_MICROPHONE scripting define — use the Setup Wizard's 'Stream microphone' option. A microphone icon is shown in the HUD while it is streamed.")]
        [SerializeField] private bool streamMicrophone = false;
        private Audio.StreamAudioCapture _audioCapture;

        [Header("Components")]
        [SerializeField] private WebSocketClient webSocketClient;
        [SerializeField] private WebRTCConnection webRTCConnection;

        [Header("HUD")]
        [SerializeField] private ClassroomHUD hud;

        [Header("Device Status")]
        [SerializeField] private float deviceStatusUpdateInterval = 10f;
        private Utilities.DeviceStatusProvider deviceStatusProvider;
        private Coroutine deviceStatusCoroutine;
        private Coroutine createVideoTrackDelayCoroutine;
        private Coroutine pendingApprovalCoroutine;
        private Coroutine reconnectCoroutine;

        private bool suppressAutoReconnect;
        private int reconnectAttempt;
        // Persists across pending-approval retries so a waiting headset backs off instead of
        // reconnecting every few seconds (which used to exhaust the server's rate limit).
        private int pendingApprovalAttempt;

        private const float ReconnectBaseDelay = 1f;
        private const float ReconnectMaxDelay = 30f;
        private const float ConnectionLostDebounceSeconds = 0.25f;

        private float lastConnectionLostRealtime = -999f;

        private bool isInitialized = false;
        private bool isStreaming = false;

        private ConnectionState currentState = ConnectionState.Disconnected;
        private string currentSessionId;

        [Header("Scene Management")]
        [SerializeField] private SceneLibrary sceneLibrary;

        private string _avatarUrl = null;
        private string _currentSceneName = "";
        private ClassroomSceneManager _classroomSceneManager;
        private StreamCameraWatchdog _streamCameraWatchdog;

        public ConnectionState CurrentState => currentState;
        public SceneLibrary SceneLibrary => sceneLibrary;

        // The one persistent ClassroomClient. A scene that is loaded again (e.g. the app returning to
        // its menu/bootstrap scene) brings another copy, which must remove itself.
        public static ClassroomClientManager Instance { get; private set; }

        public Action OnConnected;
        public Action OnDisconnected;
        public Action OnStreamingStarted;
        public Action OnStreamingStopped;

        [System.Serializable]
        private class ServerMessage
        {
            public string type;
            public string reason;
            public string sessionId;
            public string text;
            public string color;
            public string category;
        }

        [System.Serializable]
        private class SdpJsonMessage
        {
            public string type;
            public string sdp;
        }

        [System.Serializable]
        private class CandidateJsonMessage
        {
            public string candidate;
            public string sdpMid;
            public int sdpMLineIndex;
        }

        [System.Serializable]
        private class SessionStatusMessage
        {
            public string type;
            public string status;
        }

        [System.Serializable]
        private class DisconnectMessage
        {
            public string type;
            public string deviceId;
            public string reason;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Deactivate first so none of this copy's components start (no second WebSocket,
                // HUD, watchdog or microphone), then remove it.
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        void Start()
        {
            AutoSetup();
            hud?.UpdateStatus(currentState);

            if (!string.IsNullOrEmpty(serverUrl))
            {
                currentState = ConnectionState.Connecting;
                hud?.UpdateStatus(currentState);
                ConnectToServer(serverUrl);
            }
            else
            {
                Debug.LogWarning("[ClassroomClientManager] No serverUrl configured.");
            }
        }

        void Update()
        {
        }

        public void AutoSetup()
        {
            SetupWebSocketClient();
            SetupWebRTCConnection();
            SetupDeviceStatusProvider();
            SetupClassroomSceneManager();
            SetupStreamCameraWatchdog();
            isInitialized = true;
        }

        private void SetupDeviceStatusProvider()
        {
            if (deviceStatusProvider == null)
            {
                deviceStatusProvider = GetComponent<Utilities.DeviceStatusProvider>();
                if (deviceStatusProvider == null)
                {
                    deviceStatusProvider = gameObject.AddComponent<Utilities.DeviceStatusProvider>();
                }
            }
        }

        private void SetupClassroomSceneManager()
        {
            _classroomSceneManager = GetComponent<ClassroomSceneManager>();
            if (_classroomSceneManager != null)
            {
                _classroomSceneManager.Initialize(webSocketClient, this);
            }
        }

        private void SetupStreamCameraWatchdog()
        {
            _streamCameraWatchdog = GetComponent<StreamCameraWatchdog>();
            if (_streamCameraWatchdog == null)
            {
                _streamCameraWatchdog = gameObject.AddComponent<StreamCameraWatchdog>();
            }
            _streamCameraWatchdog.Initialize(this);
        }

        /// <summary>Asks the watchdog to verify the stream camera now (e.g. right after a scene load).</summary>
        public void RequestStreamCameraCheck()
        {
            _streamCameraWatchdog?.RequestCheck();
        }

        private Camera CreateStreamingCamera(Camera source)
        {
            Transform existingStreamCam = source.transform.Find("StreamingCamera");
            if (existingStreamCam != null)
            {
                Camera existingCam = existingStreamCam.GetComponent<Camera>();
                if (existingCam != null)
                {
                    if (existingCam.GetComponent<ClassroomStreamCameraMarker>() == null)
                        existingCam.gameObject.AddComponent<ClassroomStreamCameraMarker>();
                    return existingCam;
                }
            }

            GameObject streamCamGO = new GameObject("StreamingCamera");
            streamCamGO.transform.SetParent(source.transform);
            streamCamGO.transform.localPosition = Vector3.zero;
            streamCamGO.transform.localRotation = Quaternion.identity;
            streamCamGO.transform.localScale = Vector3.one;

            Camera streamCam = streamCamGO.AddComponent<Camera>();
            streamCam.clearFlags = source.clearFlags;
            streamCam.backgroundColor = source.backgroundColor;
            streamCam.cullingMask = source.cullingMask;
            streamCam.fieldOfView = source.fieldOfView;
            streamCam.nearClipPlane = source.nearClipPlane;
            streamCam.farClipPlane = source.farClipPlane;
            streamCam.depth = source.depth - 1;
            // Parking RenderTexture keeps the dedicated camera OFF the HMD display (never the eye
            // display). Unity 6's Render Graph requires a depth buffer on a camera's output texture,
            // so create it with 24-bit depth — a depthless RT logs an error every rendered frame.
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.useMipMap = false;
            rt.autoGenerateMips = false;
            rt.Create();
            streamCam.targetTexture = rt;
            // Render only while streaming. StartStreaming() enables it; StopStreaming() disables it.
            // Avoids rendering the scene to the parking RT every frame while idle (Quest perf).
            streamCam.enabled = false;

            streamCamGO.AddComponent<ClassroomStreamCameraMarker>();
            return streamCam;
        }

        private void SetupWebSocketClient()
        {
            if (webSocketClient == null)
            {
                Debug.LogError("[ClassroomClientManager] WebSocket client not assigned!");
                return;
            }

            webSocketClient.OnConnected += OnWebSocketConnected;
            webSocketClient.OnDisconnected += OnWebSocketDisconnected;
            webSocketClient.OnConnectionFailed += OnWebSocketConnectionFailed;
            webSocketClient.OnMessageReceived += OnWebSocketMessageReceived;
            webSocketClient.SetServerToken(serverToken);
            webSocketClient.SetAppInfo(appName, Application.identifier, "");
            webSocketClient.SetAvatarUrl(_avatarUrl ?? "");
        }

        private void SetupWebRTCConnection()
        {
            if (webRTCConnection == null)
            {
                Debug.LogError("[ClassroomClientManager] WebRTC connection not assigned!");
                return;
            }

            webRTCConnection.OnConnectionStateChanged += OnWebRTCStateChanged;
            webRTCConnection.OnOfferCreated += OnOfferCreated;
            webRTCConnection.OnIceCandidateReceived += OnIceCandidateReceived;
            webRTCConnection.OnDisposeRequested += OnWebRTCDisposeRequested;
            webRTCConnection.audioCapture = GetOrAddAudioCapture();

            // Only ever hand WebRTC a dedicated, ClassroomClient-owned (marked) capture camera.
            bool dedicatedReady = streamCamera != null && streamCamera.GetComponent<ClassroomStreamCameraMarker>() != null;
            if (dedicatedReady && webRTCConnection.streamCamera == null)
            {
                webRTCConnection.streamCamera = streamCamera;
            }
            if (dedicatedReady)
            {
                webRTCConnection.InitializeWebRTC();
            }
        }

        // Picks up the server's STUN/TURN servers (e.g. the Pi's coturn relay) for the next stream.
        private async void FetchIceServers()
        {
            var servers = await IceConfigFetcher.FetchAsync(serverUrl);
            if (servers != null && webRTCConnection != null)
            {
                webRTCConnection.SetIceServers(servers);
            }
        }

        private Audio.StreamAudioCapture GetOrAddAudioCapture()
        {
            if (_audioCapture == null)
            {
                _audioCapture = GetComponent<Audio.StreamAudioCapture>();
                if (_audioCapture == null) _audioCapture = gameObject.AddComponent<Audio.StreamAudioCapture>();
                _audioCapture.Configure(streamAppAudio, streamMicrophone, hud);
            }
            return _audioCapture;
        }

        private void OnWebSocketConnected()
        {
            // reconnectAttempt is reset on "registered", not here: a socket the server closes right
            // after opening (rate limit) must keep backing off instead of retrying every second.
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }

            OnConnected?.Invoke();
            currentState = ConnectionState.Connecting;
            hud?.UpdateStatus(currentState);
        }

        private void OnWebSocketDisconnected()
        {
            Debug.Log("[ClassroomClientManager] WebSocket disconnected");
            HandleConnectionLost();
        }

        private void OnWebSocketConnectionFailed(string err)
        {
            Debug.LogWarning($"[ClassroomClientManager] WebSocket connection failed: {err}");
            HandleConnectionLost();
        }

        private void HandleConnectionLost()
        {
            if (this == null) return; // destroyed while the socket was still closing
            float now = Time.realtimeSinceStartup;
            if (now - lastConnectionLostRealtime < ConnectionLostDebounceSeconds)
            {
                return;
            }

            lastConnectionLostRealtime = now;

            OnDisconnected?.Invoke();

            if (isStreaming)
            {
                StopStreaming();
            }

            StopDeviceStatusUpdates();

            if (currentState == ConnectionState.PendingApproval)
            {
                // Still waiting for approval: keep retrying with the pending backoff. A running retry /
                // re-register coroutine owns the reconnect (its own Disconnect() lands here).
                if (pendingApprovalCoroutine == null && !suppressAutoReconnect)
                {
                    pendingApprovalCoroutine = StartCoroutine(PendingApprovalRetryLoop());
                }
                return;
            }

            if (suppressAutoReconnect)
            {
                suppressAutoReconnect = false;
                reconnectAttempt = 0;
                if (reconnectCoroutine != null)
                {
                    StopCoroutine(reconnectCoroutine);
                    reconnectCoroutine = null;
                }
                currentState = ConnectionState.Disconnected;
                ClassroomEvents.FireOnDisconnected();
                hud?.UpdateStatus(currentState);
                return;
            }

            currentState = ConnectionState.Reconnecting;
            ClassroomEvents.FireOnDisconnected();
            hud?.UpdateStatus(currentState);
            ScheduleReconnect();
        }

        private void ScheduleReconnect()
        {
            if (string.IsNullOrEmpty(serverUrl))
            {
                return;
            }

            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }

            float delay = Mathf.Min(ReconnectBaseDelay * Mathf.Pow(2f, reconnectAttempt), ReconnectMaxDelay);
            reconnectCoroutine = StartCoroutine(ReconnectAfterDelay(delay));
        }

        private IEnumerator ReconnectAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            reconnectCoroutine = null;
            if (suppressAutoReconnect)
            {
                yield break;
            }

            if (currentState == ConnectionState.PendingApproval)
            {
                yield break;
            }

            if (string.IsNullOrEmpty(serverUrl) || webSocketClient == null)
            {
                yield break;
            }

            if (webSocketClient.IsConnected || webSocketClient.IsConnecting)
            {
                yield break;
            }

            ConnectToServer(serverUrl);
            reconnectAttempt++;
        }

        void OnDestroy()
        {
            suppressAutoReconnect = true;
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }
            // The socket can still report "closed" after this object is gone — stop listening. Compared by
            // reference: the WebSocketClient component may already be destroyed, but its events still fire.
            if (!ReferenceEquals(webSocketClient, null))
            {
                webSocketClient.OnConnected -= OnWebSocketConnected;
                webSocketClient.OnDisconnected -= OnWebSocketDisconnected;
                webSocketClient.OnConnectionFailed -= OnWebSocketConnectionFailed;
                webSocketClient.OnMessageReceived -= OnWebSocketMessageReceived;
            }
            // Only the persistent instance may release the slot — a discarded duplicate's OnDestroy
            // used to reset it, so the NEXT reload of the scene kept a second ClassroomClient alive.
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit()
        {
            suppressAutoReconnect = true;
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }
        }

        private void OnWebSocketMessageReceived(string message)
        {
            ProcessWebSocketMessage(message);
        }

        private void ProcessWebSocketMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (message.StartsWith("{"))
            {
                try
                {
                    var msg = JsonUtility.FromJson<ServerMessage>(message);
                    switch (msg.type)
                    {
                        case "registered":
                            FetchIceServers();
                            reconnectAttempt = 0;
                            pendingApprovalAttempt = 0;
                            // Before registration the server ignores device messages ("deviceId mismatch").
                            StartDeviceStatusUpdates();
                            currentState = ConnectionState.InLobby;
                            ClassroomEvents.FireOnConnected();
                            hud?.UpdateStatus(currentState);
                            SendContentLibrary();
                            SendLanguageReport();
                            break;

                        case "rejected":
                            if (msg.reason == "pending_approval")
                            {
                                Debug.Log("[ClassroomClientManager] Device pending approval — will retry");
                                currentState = ConnectionState.PendingApproval;
                                hud?.UpdateStatus(currentState);
                                if (pendingApprovalCoroutine != null) StopCoroutine(pendingApprovalCoroutine);
                                pendingApprovalCoroutine = StartCoroutine(PendingApprovalRetryLoop());
                            }
                            else
                            {
                                Debug.LogError($"[ClassroomClientManager] Registration rejected: {msg.reason}");
                                currentState = ConnectionState.Disconnected;
                                hud?.UpdateStatus(currentState);
                            }
                            break;

                        case "approved":
                            // An admin approved this headset while it was waiting — register now
                            // instead of waiting for the next retry.
                            if (currentState == ConnectionState.PendingApproval)
                            {
                                if (pendingApprovalCoroutine != null) StopCoroutine(pendingApprovalCoroutine);
                                pendingApprovalCoroutine = StartCoroutine(ReRegisterAfterApproval());
                            }
                            break;

                        case "session_assigned":
                            if (currentState == ConnectionState.InSession)
                            {
                                // Teacher reconnected — server re-sent session_assigned mid-session.
                                // Treat the same as stream_request: restart WebRTC so a fresh OFFER is sent.
                                Debug.Log("[ClassroomClientManager] session_assigned received while already InSession — restarting stream");
                                StopStreaming();
                                StartStreaming();
                            }
                            else
                            {
                                currentState = ConnectionState.InSession;
                                currentSessionId = msg.sessionId;
                                ClassroomEvents.FireOnSessionStarted();
                                hud?.UpdateStatus(currentState);
                                EnsureStreamingSetup();
                                StartStreaming();
                            }
                            break;

                        case "session_ended":
                            currentState = ConnectionState.InLobby;
                            currentSessionId = null;
                            ClassroomEvents.FireOnSessionEnded();
                            hud?.UpdateStatus(currentState);
                            StopStreaming();
                            break;

                        case "stream_request":
                            StopStreaming();
                            StartStreaming();
                            break;

                        case "message":
                            ClassroomEvents.FireOnMessageReceived(msg.text, msg.color, msg.category);
                            Debug.Log($"[ClassroomClient] MESSAGE received — text: {msg.text} | color: {msg.color} | category: {msg.category}");
                            hud?.ShowNotification(msg.text, msg.color, msg.category);
                            break;

                        case "pong":
                            break;

                        default:
                            Debug.Log($"[ClassroomClientManager] Unhandled JSON type: {msg.type}");
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ClassroomClientManager] JSON parse error: {e.Message}");
                }
                return;
            }

            string[] parts = message.Split('|');
            if (parts.Length < 4) return;

            string messageType = parts[0];
            switch (messageType)
            {
                case "ANSWER":
                    if (webRTCConnection != null)
                    {
                        try
                        {
                            var sdpMsg = JsonUtility.FromJson<SdpJsonMessage>(parts[3]);
                            webRTCConnection.HandleAnswer(sdpMsg.sdp);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[ClassroomClientManager] Failed to parse ANSWER JSON: {e.Message}");
                        }
                    }
                    break;

                case "CANDIDATE":
                    if (webRTCConnection != null)
                    {
                        try
                        {
                            var candMsg = JsonUtility.FromJson<CandidateJsonMessage>(parts[3]);
                            webRTCConnection.HandleIceCandidate(candMsg.candidate, candMsg.sdpMid, candMsg.sdpMLineIndex);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[ClassroomClientManager] Failed to parse CANDIDATE JSON: {e.Message}");
                        }
                    }
                    break;

                case "LOAD_SCENE":
                    // Format: LOAD_SCENE|SERVER|{deviceId}|{sceneKey}|{loadType}
                    if (parts.Length >= 4)
                    {
                        string sceneKey = parts[3];
                        SceneInfo info = sceneLibrary != null ? sceneLibrary.GetSceneByKey(sceneKey) : null;
                        if (_classroomSceneManager == null)
                        {
                            ReportSceneLoadFailed(sceneKey, "ClassroomSceneManager component missing on the ClassroomClient object");
                        }
                        else if (info == null)
                        {
                            ReportSceneLoadFailed(sceneKey, "Scene is not in this app's Scene Library");
                        }
                        else
                        {
                            _classroomSceneManager.LoadScene(info);
                        }
                    }
                    break;

                case "REQUEST_CONTENT_LIBRARY":
                    SendContentLibrary();
                    break;

                case "SET_LANGUAGE":
                    // Format: SET_LANGUAGE|SERVER|{deviceId}|{localeCode}
                    ClassroomLanguageBridge.HandleRequest(parts[3], ReportLanguageFailed, SendLanguageAck);
                    break;

                default:
                    Debug.Log($"[ClassroomClientManager] Unhandled pipe message: {messageType}");
                    break;
            }
        }

        private void OnWebRTCStateChanged(RTCIceConnectionState state)
        {
            switch (state)
            {
                case RTCIceConnectionState.Connected:
                    isStreaming = true;
                    OnStreamingStarted?.Invoke();
                    break;
                case RTCIceConnectionState.Disconnected:
                case RTCIceConnectionState.Failed:
                case RTCIceConnectionState.Closed:
                    isStreaming = false;
                    OnStreamingStopped?.Invoke();
                    break;
            }
        }

        private void OnOfferCreated(string offerSdp)
        {
            if (webSocketClient != null)
            {
                webSocketClient.SendSignalingMessage("OFFER", offerSdp);
            }
        }

        private void OnIceCandidateReceived(string candidate, string sdpMid, int sdpMLineIndex)
        {
            if (webSocketClient != null)
            {
                webSocketClient.SendSignalingMessage("CANDIDATE", "", candidate, sdpMid, sdpMLineIndex);
            }
        }

        private void OnWebRTCDisposeRequested()
        {
            if (webSocketClient != null)
            {
                webSocketClient.SendSignalingMessage("DISPOSE");
                Debug.Log("[ClassroomClientManager] Sent DISPOSE message to server");
            }
        }

        // Guarantees `streamCamera` is a dedicated, ClassroomClient-owned (marked) capture camera.
        // Handles an unsafe camera serialized into the field (e.g. the eye camera) or an older
        // wizard-created "StreamingCamera" child that predates the marker.
        private void NormalizeStreamCamera()
        {
            if (streamCamera == null) return;

            if (streamCamera.GetComponent<ClassroomStreamCameraMarker>() != null)
            {
                if (streamSourceCamera == null)
                {
                    Transform p = streamCamera.transform.parent;
                    streamSourceCamera = (p != null && p.GetComponent<Camera>() != null) ? p.GetComponent<Camera>() : streamCamera;
                }
                return;
            }

            Transform parentT = streamCamera.transform.parent;
            Camera parentCam = parentT != null ? parentT.GetComponent<Camera>() : null;
            if (streamCamera.gameObject.name == "StreamingCamera" && parentCam != null)
            {
                // Legacy ClassroomClient camera (named "StreamingCamera" AND a child of a Camera) — adopt it.
                // A developer camera merely named "StreamingCamera" (no parent Camera) falls through to the source branch.
                streamCamera.gameObject.AddComponent<ClassroomStreamCameraMarker>();
                streamSourceCamera = parentCam;
                return;
            }

            // Any other assigned camera (e.g. the eye camera) is a SOURCE — build a dedicated child.
            Camera source = streamCamera;
            streamSourceCamera = source;
            streamCamera = CreateStreamingCamera(source);
        }

        private void EnsureStreamingSetup()
        {
            NormalizeStreamCamera();

            if (streamCamera == null)
            {
                if (cameraSetupMode == CameraSetupMode.Automatic)
                {
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        streamSourceCamera = mainCamera;
                        streamCamera = CreateStreamingCamera(mainCamera);
                    }
                    else
                    {
                        Debug.LogWarning("[ClassroomClientManager] Automatic camera mode: no main camera found for streaming setup.");
                    }
                }
                else
                {
                    Debug.LogWarning("[ClassroomClientManager] API Controlled camera mode: no stream camera set yet. Call ClassroomClientAPI.SetStreamCamera(camera) to choose the streamed viewpoint — streaming will begin automatically once it is set.");
                }
            }

            if (webRTCConnection == null)
            {
                webRTCConnection = gameObject.AddComponent<WebRTCConnection>();
                SetupWebRTCConnection();
            }

            if (webRTCConnection != null && streamCamera != null)
            {
                // streamCamera is guaranteed marked here (NormalizeStreamCamera ran). Replace
                // whatever WebRTC currently holds (null OR an unmarked/source camera) with it.
                if (webRTCConnection.streamCamera != streamCamera)
                {
                    webRTCConnection.SwitchCamera(streamCamera);
                }
                if (!webRTCConnection.IsInitialized)
                {
                    webRTCConnection.ReinitializeIfNeeded();
                }
            }
        }

        public void StartStreaming()
        {
            if (isStreaming)
            {
                return;
            }

            if (streamCamera == null)
            {
                _streamRequested = true;
                Debug.LogWarning("[ClassroomClientManager] StartStreaming requested but no stream camera is set. Waiting for ClassroomClientAPI.SetStreamCamera(camera).");
                return;
            }

            // Render only into an off-display target. A wizard-made camera has no RenderTexture
            // until CreateVideoTrack assigns the stream texture (which then enables it).
            if (streamCamera != null && streamCamera.targetTexture != null)
            {
                streamCamera.enabled = true;
            }

            if (webRTCConnection != null && webRTCConnection.IsInitialized)
            {
                if (createVideoTrackDelayCoroutine != null)
                {
                    StopCoroutine(createVideoTrackDelayCoroutine);
                    createVideoTrackDelayCoroutine = null;
                }

                webRTCConnection.CreatePeerConnection();
                createVideoTrackDelayCoroutine = StartCoroutine(CreateVideoTrackWithDelay());
                isStreaming = true;
            }
            else
            {
                Debug.LogError("[ClassroomClientManager] WebRTC not initialized.");
            }
        }

        private IEnumerator CreateVideoTrackWithDelay()
        {
            if (streamCamera == null)
            {
                Debug.LogError("[ClassroomClientManager] Stream camera is NULL!");
                yield break;
            }

            yield return new WaitForSeconds(1f);
            if (webRTCConnection == null)
            {
                yield break;
            }

            yield return StartCoroutine(webRTCConnection.CreateVideoTrack());
        }

        public void StopStreaming()
        {
            _streamRequested = false;

            if (!isStreaming)
            {
                return;
            }

            if (createVideoTrackDelayCoroutine != null)
            {
                StopCoroutine(createVideoTrackDelayCoroutine);
                createVideoTrackDelayCoroutine = null;
            }

            if (streamCamera != null && streamCamera.GetComponent<ClassroomStreamCameraMarker>() != null)
            {
                streamCamera.enabled = false;
            }

            if (webRTCConnection != null)
            {
                webRTCConnection.Disconnect();
            }

            isStreaming = false;
            OnStreamingStopped?.Invoke();
        }

        public void ConnectToServer(string serverAddress)
        {
            // New explicit connection (including reconnect backoff) — allow auto-reconnect after future drops.
            suppressAutoReconnect = false;
            if (webSocketClient != null)
            {
                webSocketClient.ConnectToServer(serverAddress);
            }
        }

        public void DisconnectFromServer()
        {
            suppressAutoReconnect = true;
            reconnectAttempt = 0;
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }

            if (webSocketClient != null && webSocketClient.IsConnected)
            {
                SendDisconnectNotification();
                StartCoroutine(DelayedDisconnect());
            }
        }

        private void SendDisconnectNotification()
        {
            if (webSocketClient != null && webSocketClient.IsConnected)
            {
                var disconnectMessage = new DisconnectMessage
                {
                    type = "disconnect",
                    deviceId = SystemInfo.deviceUniqueIdentifier,
                    reason = "client_disconnect"
                };

                string json = JsonUtility.ToJson(disconnectMessage);
                webSocketClient.SendMessage(json);
            }
        }

        private IEnumerator DelayedDisconnect()
        {
            yield return new WaitForSeconds(0.2f);
            webSocketClient.Disconnect();
        }

        public void ToggleStreaming()
        {
            if (isStreaming)
            {
                StopStreaming();
            }
            else if (webSocketClient != null && webSocketClient.IsConnected)
            {
                StartStreaming();
            }
        }

        public void SendDeviceStatus()
        {
            if (webSocketClient == null || !webSocketClient.IsConnected || deviceStatusProvider == null)
            {
                return;
            }

            try
            {
                deviceStatusProvider.UpdateStatus();
                string deviceId = SystemInfo.deviceUniqueIdentifier;
                int battery = deviceStatusProvider.BatteryLevel;
                int wifiLevel = deviceStatusProvider.WifiSignalLevel;
                bool charging = deviceStatusProvider.IsCharging;
                string message = $"DEVICE_STATUS|{deviceId}|SERVER|{battery}|{wifiLevel}|{(charging ? "1" : "0")}";
                webSocketClient.SendMessage(message);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ClassroomClientManager] Error sending device status: {e.Message}");
            }
        }

        private void StartDeviceStatusUpdates()
        {
            if (deviceStatusCoroutine != null)
            {
                StopCoroutine(deviceStatusCoroutine);
            }
            deviceStatusCoroutine = StartCoroutine(DeviceStatusUpdateLoop());
        }

        private void StopDeviceStatusUpdates()
        {
            if (deviceStatusCoroutine != null)
            {
                StopCoroutine(deviceStatusCoroutine);
                deviceStatusCoroutine = null;
            }
        }

        private IEnumerator DeviceStatusUpdateLoop()
        {
            SendDeviceStatus();

            while (true)
            {
                yield return new WaitForSeconds(deviceStatusUpdateInterval);
                if (webSocketClient != null && webSocketClient.IsConnected)
                {
                    SendDeviceStatus();
                }
                else
                {
                    break;
                }
            }
        }

        private IEnumerator PendingApprovalRetryLoop()
        {
            // Fallback only: the server sends "approved" the moment an admin approves this headset.
            float[] delays = { 15f, 30f, 60f };
            float delay = delays[Mathf.Min(pendingApprovalAttempt, delays.Length - 1)];
            pendingApprovalAttempt++;
            Debug.Log($"[ClassroomClientManager] Waiting for admin approval — retrying registration in {delay}s (attempt {pendingApprovalAttempt})");
            yield return new WaitForSeconds(delay);
            if (currentState != ConnectionState.PendingApproval) { pendingApprovalCoroutine = null; yield break; }
            webSocketClient?.Disconnect();
            yield return new WaitForSeconds(0.5f);
            pendingApprovalCoroutine = null;
            if (currentState != ConnectionState.PendingApproval) yield break;
            ConnectToServer(serverUrl);
        }

        private IEnumerator ReRegisterAfterApproval()
        {
            Debug.Log("[ClassroomClientManager] Approved by admin — registering now");
            webSocketClient?.Disconnect();
            yield return new WaitForSeconds(0.5f);
            pendingApprovalAttempt = 0;
            pendingApprovalCoroutine = null;
            currentState = ConnectionState.Connecting;
            hud?.UpdateStatus(currentState);
            ConnectToServer(serverUrl);
        }

        public void SendSessionStatus(string status)
        {
            if (webSocketClient == null || !webSocketClient.IsConnected)
            {
                return;
            }

            var message = new SessionStatusMessage
            {
                type = "session_status",
                status = status
            };

            webSocketClient.SendMessage(JsonUtility.ToJson(message));
        }

        public void SetAvatarUrl(string url)
        {
            _avatarUrl = url ?? "";
            webSocketClient?.SetAvatarUrl(_avatarUrl);
        }

        public Camera GetStreamCamera() => streamCamera;

        public bool IsApiControlledCameraMode => cameraSetupMode == CameraSetupMode.ApiControlled;

        public Camera GetStreamSourceCamera() => streamSourceCamera;

        public void SetStreamCamera(Camera cam)
        {
            if (cam == null) return;

            // The passed camera is always a SOURCE/viewpoint unless it is a camera ClassroomClient
            // itself created (carries the internal marker). The source is never given a RenderTexture
            // and never disabled — only the dedicated capture camera is.
            Camera dedicated;
            if (cam.GetComponent<ClassroomStreamCameraMarker>() != null)
            {
                dedicated = cam;
                Transform p = cam.transform.parent;
                streamSourceCamera = (p != null && p.GetComponent<Camera>() != null) ? p.GetComponent<Camera>() : cam;
            }
            else
            {
                streamSourceCamera = cam;
                dedicated = CreateStreamingCamera(cam);
            }

            streamCamera = dedicated;

            if (webRTCConnection != null && webRTCConnection.streamCamera != dedicated)
            {
                // Hands the existing stream texture to the new camera — the video track is kept,
                // so this works in every state (idle, starting, streaming, ICE reconnecting).
                webRTCConnection.SwitchCamera(dedicated);
                webRTCConnection.ReinitializeIfNeeded();
            }

            // If a session requested streaming before a camera existed (API Controlled mode), start now.
            if (_streamRequested && !isStreaming)
            {
                _streamRequested = false;
                EnsureStreamingSetup();
                StartStreaming();
            }
        }

        public void NotifySceneDownloaded()
        {
            SendContentLibrary();
        }

        public void ReportCurrentScene(string sceneKey)
        {
            _currentSceneName = sceneKey ?? "";
            webSocketClient?.SetCurrentScene(_currentSceneName);
            if (webSocketClient == null || !webSocketClient.IsConnected) return;
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            webSocketClient.SendMessage($"CURRENT_SCENE|{deviceId}|SERVER|{sceneKey}");
        }

        public void ReportSceneLoaded(string sceneKey)
        {
            // The app finished a "Custom Loading" scene: the scene ClassroomClient loaded before is
            // no longer the content and must go, otherwise both stay visible.
            SceneInfo info = sceneLibrary != null ? sceneLibrary.GetSceneByKey(sceneKey) : null;
            if (info != null && info.customLoading)
            {
                _classroomSceneManager?.ReleaseOwnedScene();
            }

            if (webSocketClient == null || !webSocketClient.IsConnected) return;
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            webSocketClient.SendMessage($"SCENE_LOADED|{deviceId}|SERVER|{sceneKey}");
        }

        public void ReportSceneLoadFailed(string sceneKey, string reason)
        {
            if (webSocketClient == null || !webSocketClient.IsConnected) return;
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            webSocketClient.SendMessage($"SCENE_LOAD_FAILED|{deviceId}|SERVER|{sceneKey}|{reason}");
        }

        /// <summary>Sends the language the app reported (WS12). Before registration the server would drop it — "registered" sends it again.</summary>
        public void SendLanguageReport()
        {
            if (!ClassroomLanguageBridge.HasReport) return;
            if (currentState != ConnectionState.InLobby && currentState != ConnectionState.InSession) return;
            if (webSocketClient == null || !webSocketClient.IsConnected) return;
            webSocketClient.SendMessage(ClassroomLanguageBridge.BuildLanguageMessage(SystemInfo.deviceUniqueIdentifier));
        }

        private void SendLanguageAck(string requestedCode)
        {
            if (webSocketClient == null || !webSocketClient.IsConnected) return;
            webSocketClient.SendMessage(ClassroomLanguageBridge.BuildAckMessage(SystemInfo.deviceUniqueIdentifier, requestedCode));
        }

        public void ReportLanguageFailed(string requestedCode, string reason)
        {
            if (webSocketClient == null || !webSocketClient.IsConnected) return;
            webSocketClient.SendMessage(ClassroomLanguageBridge.BuildFailedMessage(SystemInfo.deviceUniqueIdentifier, requestedCode, reason));
        }

        private void SendContentLibrary()
        {
            if (webSocketClient == null || !webSocketClient.IsConnected) return;

            ContentLibraryData data;
            if (sceneLibrary != null)
            {
                data = sceneLibrary.ToContentLibraryData(appName, Application.identifier, _currentSceneName);
            }
            else
            {
                data = new ContentLibraryData
                {
                    appName = appName ?? "",
                    bundleId = Application.identifier,
                    currentScene = _currentSceneName,
                    scenes = new SceneInfoData[0],
                };
            }

            string json = JsonUtility.ToJson(data);
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            webSocketClient.SendMessage($"CONTENT_LIBRARY|{deviceId}|SERVER|{json}");
        }

        public bool IsInitialized => isInitialized;
        public bool IsStreaming => isStreaming;
        public bool IsConnected => webSocketClient?.IsConnected ?? false;
        public string GetConnectionStatus() => webSocketClient?.GetConnectionStatus() ?? "Disconnected";
    }
}
