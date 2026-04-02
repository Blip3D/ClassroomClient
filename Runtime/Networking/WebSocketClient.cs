using UnityEngine;
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using Meta.Net.NativeWebSocket;

namespace ClassroomClient.Networking
{
    /// <summary>
    /// WebSocket client for ClassroomClient streaming
    /// </summary>
    public class WebSocketClient : MonoBehaviour
    {
        [Header("Device Registration")]
        [SerializeField] private string deviceId;
        [SerializeField] private string deviceName = "HMD Client";
        
        // App info (set by ClassroomClientManager)
        private string appName = "";
        private string bundleId = "";
        private string currentScene = "";
        private string deviceSecret = "";
        
        // WebSocket connection
        private WebSocket ws;
        private bool isConnected = false;
        private bool isConnecting = false;
        private float lastPingTime = 0f;
        private const float PING_INTERVAL = 15f; // Send ping every 15 seconds
        
        // Thread-safe message queue (NativeWebSocket may invoke OnMessage off main thread)
        private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
        
        // Events
        public System.Action OnConnected;
        public System.Action OnDisconnected;
        public System.Action<string> OnMessageReceived;
        public System.Action<string> OnConnectionFailed;
        
        void Start()
        {
            // Don't auto-connect - let ClassroomClientManager handle this
        }
        
        void Update()
        {
            // Process message queue on main thread (thread-safe)
            ProcessMessageQueue();
            
            // Send keep-alive ping
            if (isConnected && Time.time - lastPingTime > PING_INTERVAL)
            {
                SendPing();
                lastPingTime = Time.time;
            }
        }
        
        private void ProcessMessageQueue()
        {
            // Process all queued messages on main thread
            int processedCount = 0;
            while (messageQueue.TryDequeue(out string message) && processedCount < 10) // Limit to prevent blocking
            {
                try
                {
                    if (message.StartsWith("ERROR:"))
                    {
                        // Handle error messages
                        string errorMessage = message.Substring(6); // Remove "ERROR:" prefix
                        Debug.LogError($"[WebSocketClient] Error from background thread: {errorMessage}");
                    }
                    else
                    {
                        // Handle normal messages
                        OnMessageReceived?.Invoke(message);
                    }
                    processedCount++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebSocketClient] Error processing queued message: {e.Message}");
                    processedCount++;
                }
            }
        }
        
        public async void ConnectToServer(string serverAddress)
        {
            if (isConnecting)
            {
                Debug.Log("[WebSocketClient] Already connecting, ignoring duplicate request");
                return; // Prevent spam
            }
            
            if (isConnected)
            {
                Debug.Log("[WebSocketClient] Already connected");
                return; // Don't allow reconnect if already connected
            }
            
            // Close existing connection if any
            if (ws != null)
            {
                try
                {
                    await ws.Close();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WebSocketClient] Error closing existing connection: {e.Message}");
                }
                ws = null;
            }
            
            // IMPROVED: Explicitly reset connection states before attempting new connection
            isConnected = false;
            isConnecting = true;
            
            try
            {
                // Create WebSocket connection
                ws = new WebSocket(serverAddress);
                
                ws.OnOpen += () =>
                {
                    isConnected = true;
                    isConnecting = false;
                    OnConnected?.Invoke();
                    RegisterDevice();
                };
                
                ws.OnMessage += HandleMessage;

                ws.OnError += (error) =>
                {
                    Debug.LogError($"[WebSocketClient] WebSocket error: {error}");
                    isConnecting = false;
                    OnConnectionFailed?.Invoke(error);
                };
                
                ws.OnClose += (code) =>
                {
                    isConnected = false;
                    isConnecting = false;
                    OnDisconnected?.Invoke();
                };
                
                await ws.Connect();
                

            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocketClient] Connection failed: {e.Message}");
                Debug.LogError($"[WebSocketClient] Stack trace: {e.StackTrace}");
                isConnecting = false;
                OnConnectionFailed?.Invoke(e.Message);
            }
        }
        
        private void HandleMessage(byte[] bytes, int offset, int length)
        {
            HandleMessageInternal(bytes, offset, length);
        }

        private void HandleMessageInternal(byte[] bytes, int offset, int length)
        {
            try
            {
                if (bytes == null || length <= 0)
                {
                    messageQueue.Enqueue("ERROR: Received null or empty byte array");
                    return;
                }

                // Meta NativeWebSocket provides a segment view (data+offset+length).
                string message = System.Text.Encoding.UTF8.GetString(bytes, offset, length);

                // Queue message for processing on main thread (thread-safe)
                messageQueue.Enqueue(message);

                // Don't call Debug.Log here - it's called from background thread
                // Debug.Log will be called in ProcessMessageQueue() on main thread
            }
            catch (Exception e)
            {
                // Queue error message for main thread processing
                messageQueue.Enqueue($"ERROR: {e.Message}");
            }
        }
        
        private void RegisterDevice()
        {
            var registrationMessage = new RegistrationMessage
            {
                type = "register_device",
                deviceId = GetDeviceId(),
                deviceName = deviceName,
                deviceType = "XR_STREAMING_CLIENT",
                usePOVCapture = false, // Removed POV capture
                hasCaptureManager = false, // Removed capture manager
                captureStatus = "Not Available", // Removed POV capture
                appName = appName,
                bundleId = string.IsNullOrEmpty(bundleId) ? Application.identifier : bundleId,
                currentScene = currentScene,
                deviceSecret = this.deviceSecret,
                deviceModel = SystemInfo.deviceModel,
            };
            
            string json = JsonUtility.ToJson(registrationMessage);
            SendMessage(json);
            
        }
        
        /// <summary>
        /// Set app info for registration
        /// </summary>
        public void SetAppInfo(string name, string bundle, string scene)
        {
            appName = name ?? "";
            bundleId = bundle ?? Application.identifier;
            currentScene = scene ?? "";
        }
        
        /// <summary>
        /// Update current scene info
        /// </summary>
        public void SetCurrentScene(string scene)
        {
            currentScene = scene ?? "";
        }
        
        public void SetDeviceSecret(string secret)
        {
            deviceSecret = secret ?? "";
        }
        
        public new void SendMessage(string message)
        {
            if (!isConnected)
            {
                Debug.LogWarning("[WebSocketClient] Not connected, cannot send message");
                return;
            }
            
            try
            {
                ws.SendText(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocketClient] Failed to send message: {e.Message}");
            }
        }
        
        private void SendPing()
        {
            try
            {
                // Send a simple ping message to keep connection alive
                var pingMessage = new PingMessage
                {
                    type = "ping",
                    timestamp = DateTime.UtcNow.Ticks
                };
                
                string json = JsonUtility.ToJson(pingMessage);
                SendMessage(json);
                Debug.Log("[WebSocketClient] Sent keep-alive ping");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocketClient] Failed to send ping: {e.Message}");
                // Don't disconnect on ping failure - just log the error
            }
        }
        
        [System.Serializable]
        private class RegistrationMessage
        {
            public string type;
            public string deviceId;
            public string deviceName;
            public string deviceType;
            public bool usePOVCapture;
            public bool hasCaptureManager;
            public string captureStatus;
            // Content library fields
            public string appName;
            public string bundleId;
            public string currentScene;
            public string deviceSecret;
            public string deviceModel;
        }
        
        [System.Serializable]
        private class PingMessage
        {
            public string type;
            public long timestamp;
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
        
        public void SendSignalingMessage(string type, string sdp = "", string candidate = "", string sdpMid = "", int sdpMLineIndex = 0)
        {
            string deviceId = GetDeviceId();
            string message;
            
            switch (type.ToUpper())
            {
                case "OFFER":
                    var offerMsg = new SdpJsonMessage { type = "offer", sdp = sdp };
                    string offerJson = JsonUtility.ToJson(offerMsg);
                    message = $"OFFER|{deviceId}|SERVER|{offerJson}";
                    break;
                case "ANSWER":
                    message = $"ANSWER|{deviceId}|SERVER|{sdp}|{sdpMid}|{sdpMLineIndex}";
                    break;
                case "CANDIDATE":
                    var candMsg = new CandidateJsonMessage { candidate = candidate, sdpMid = sdpMid, sdpMLineIndex = sdpMLineIndex };
                    string candJson = JsonUtility.ToJson(candMsg);
                    message = $"CANDIDATE|{deviceId}|SERVER|{candJson}";
                    break;
                case "DISPOSE":
                    message = $"DISPOSE|{deviceId}|SERVER||{sdpMid}|{sdpMLineIndex}";
                    break;
                default:
                    message = $"OTHER|{deviceId}|SERVER|{sdp}|{sdpMid}|{sdpMLineIndex}";
                    break;
            }
            
            SendMessage(message);
        }
        
        private string GetDeviceId()
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = SystemInfo.deviceUniqueIdentifier;
            }
            return deviceId;
        }
        
        public void Disconnect()
        {
            if (ws != null)
            {
                try
                {
                    ws.Close();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WebSocketClient] Error during disconnect: {e.Message}");
                }
                ws = null;
            }
            
            isConnected = false;
            isConnecting = false;
        }
        
        // Public getters
        public bool IsConnected => isConnected;
        public bool IsConnecting => isConnecting;
        public string GetConnectionStatus()
        {
            if (isConnecting) return "Connecting...";
            if (isConnected) return "Connected";
            return "Disconnected";
        }
        
        void OnDestroy()
        {
            Disconnect();
        }
        
        void OnApplicationQuit()
        {
            // Send disconnect notification before quitting
            if (isConnected)
            {
                var disconnectMessage = new PingMessage
                {
                    type = "disconnect",
                    timestamp = DateTime.UtcNow.Ticks
                };
                
                string json = JsonUtility.ToJson(disconnectMessage);
                SendMessage(json);
            }
            Disconnect();
        }
    }
}
