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
        [SerializeField] private string serverUrl = "";
        [SerializeField] private string deviceSecret = "";
        [SerializeField] private string appName = "VR Training App";

        [Header("Camera Setup")]
        [SerializeField] private Camera streamCamera;

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

        private const float ReconnectBaseDelay = 1f;
        private const float ReconnectMaxDelay = 30f;
        private const float ConnectionLostDebounceSeconds = 0.25f;

        private float lastConnectionLostRealtime = -999f;

        private bool isInitialized = false;
        private bool isStreaming = false;

        private ConnectionState currentState = ConnectionState.Disconnected;
        private string currentSessionId;
        public ConnectionState CurrentState => currentState;

        private static bool isPersistentInstanceInitialized = false;

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
            public bool value;
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
            if (!isPersistentInstanceInitialized)
            {
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
                isPersistentInstanceInitialized = true;
            }
            else
            {
                Destroy(gameObject);
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

        private Camera CreateStreamingCamera(Camera mainCamera)
        {
            Transform existingStreamCam = mainCamera.transform.Find("StreamingCamera");
            if (existingStreamCam != null)
            {
                Camera existingCam = existingStreamCam.GetComponent<Camera>();
                if (existingCam != null)
                {
                    return existingCam;
                }
            }

            GameObject streamCamGO = new GameObject("StreamingCamera");
            streamCamGO.transform.SetParent(mainCamera.transform);
            streamCamGO.transform.localPosition = Vector3.zero;
            streamCamGO.transform.localRotation = Quaternion.identity;
            streamCamGO.transform.localScale = Vector3.one;

            Camera streamCam = streamCamGO.AddComponent<Camera>();
            streamCam.clearFlags = mainCamera.clearFlags;
            streamCam.backgroundColor = mainCamera.backgroundColor;
            streamCam.cullingMask = mainCamera.cullingMask;
            streamCam.fieldOfView = mainCamera.fieldOfView;
            streamCam.nearClipPlane = mainCamera.nearClipPlane;
            streamCam.farClipPlane = mainCamera.farClipPlane;
            streamCam.depth = mainCamera.depth - 1;
            var rt = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
            rt.useMipMap = false;
            rt.autoGenerateMips = false;
            rt.Create();
            streamCam.targetTexture = rt;
            streamCam.enabled = true;

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
            webSocketClient.SetDeviceSecret(deviceSecret);
            webSocketClient.SetAppInfo(appName, Application.identifier, "");
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

            if (streamCamera != null && webRTCConnection.streamCamera == null)
            {
                webRTCConnection.streamCamera = streamCamera;
            }

            webRTCConnection.InitializeWebRTC();
        }

        private void OnWebSocketConnected()
        {
            reconnectAttempt = 0;
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }

            OnConnected?.Invoke();
            currentState = ConnectionState.Connecting;
            hud?.UpdateStatus(currentState);
            StartDeviceStatusUpdates();
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
            isPersistentInstanceInitialized = false;
        }

        void OnApplicationQuit()
        {
            suppressAutoReconnect = true;
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }
            isPersistentInstanceInitialized = false;
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
                            currentState = ConnectionState.InLobby;
                            ClassroomEvents.FireOnConnected();
                            hud?.UpdateStatus(currentState);
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

                        case "mute":
                            AudioListener.volume = msg.value ? 0f : 1f;
                            ClassroomEvents.FireOnMuteChanged(msg.value);
                            break;

                        case "message":
                            ClassroomEvents.FireOnMessageReceived(msg.text, msg.color, msg.category);
                            Debug.Log($"[ClassroomClient] MESSAGE received — text: {msg.text} | color: {msg.color} | category: {msg.category}");
                            hud?.ShowNotification(msg.text, msg.color, msg.category);
                            break;

                        case "pong":
                            Debug.Log("[ClassroomClientManager] Received pong");
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
                    Debug.Log("[ClassroomClientManager] LOAD_SCENE received — future scope");
                    break;

                case "REQUEST_CONTENT_LIBRARY":
                    Debug.Log("[ClassroomClientManager] REQUEST_CONTENT_LIBRARY received — future scope");
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

        private void EnsureStreamingSetup()
        {
            if (streamCamera == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    streamCamera = CreateStreamingCamera(mainCamera);
                }
                else
                {
                    Debug.LogWarning("[ClassroomClientManager] No main camera found for streaming setup");
                }
            }

            if (webRTCConnection == null)
            {
                webRTCConnection = gameObject.AddComponent<WebRTCConnection>();
                SetupWebRTCConnection();
            }

            if (webRTCConnection != null && streamCamera != null)
            {
                if (webRTCConnection.streamCamera == null)
                {
                    webRTCConnection.streamCamera = streamCamera;
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

            if (streamCamera != null)
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
                OnStreamingStarted?.Invoke();
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
            if (!isStreaming)
            {
                return;
            }

            if (createVideoTrackDelayCoroutine != null)
            {
                StopCoroutine(createVideoTrackDelayCoroutine);
                createVideoTrackDelayCoroutine = null;
            }

            if (streamCamera != null)
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
            float[] delays = { 10f, 15f, 30f, 60f };
            int attempt = 0;
            while (currentState == ConnectionState.PendingApproval)
            {
                float delay = delays[Mathf.Min(attempt, delays.Length - 1)];
                Debug.Log($"[ClassroomClientManager] Retrying registration in {delay}s (attempt {attempt + 1})");
                yield return new WaitForSeconds(delay);
                if (currentState != ConnectionState.PendingApproval) yield break;
                webSocketClient?.Disconnect();
                yield return new WaitForSeconds(0.5f);
                if (currentState != ConnectionState.PendingApproval) yield break;
                ConnectToServer(serverUrl);
                attempt++;
            }
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

        public bool IsInitialized => isInitialized;
        public bool IsStreaming => isStreaming;
        public bool IsConnected => webSocketClient?.IsConnected ?? false;
        public string GetConnectionStatus() => webSocketClient?.GetConnectionStatus() ?? "Disconnected";
    }
}
