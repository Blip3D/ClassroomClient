using UnityEditor;
using UnityEngine;


namespace ClassroomClient.Editor
{
    public class SimpleSetupWizard : EditorWindow
    {
        private string serverUrl = "wss://blip3d.com";
        private string deviceSecret = "";
        private string appName = "VR Training App";
        private bool showSecret = false;

        static SimpleSetupWizard()
        {
            // Silent initialization
        }

        [MenuItem("Tools/Classroom Client/Quick Setup")]
        public static void ShowWindow()
        {
            GetWindow<SimpleSetupWizard>("Classroom Client Setup");
        }

        void OnGUI()
        {
            GUILayout.Label("ClassroomClient Setup", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Enter the server URL provided by your IT department.\n" +
                "Local classroom: ws://192.168.x.x:9000\n" +
                "Cloud / university server: wss://blip3d.com",
                MessageType.Info);
            EditorGUILayout.Space();

            serverUrl = EditorGUILayout.TextField("Server URL", serverUrl);

            EditorGUILayout.BeginHorizontal();
            if (showSecret)
                deviceSecret = EditorGUILayout.TextField("Device Secret", deviceSecret);
            else
                deviceSecret = EditorGUILayout.PasswordField("Device Secret", deviceSecret);
            if (GUILayout.Button(showSecret ? "Hide" : "Show", GUILayout.Width(45)))
                showSecret = !showSecret;
            EditorGUILayout.EndHorizontal();

            appName = EditorGUILayout.TextField("App Name", appName);

            EditorGUILayout.Space();

            bool configValid = !string.IsNullOrEmpty(serverUrl) && !string.IsNullOrEmpty(deviceSecret);
            if (!configValid)
                EditorGUILayout.HelpBox("Server URL and Device Secret are required.", MessageType.Warning);

            EditorGUI.BeginDisabledGroup(!configValid);
            if (GUILayout.Button("Setup ClassroomClient", GUILayout.Height(40)))
            {
                SetupClassroomClient();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This will:\n" +
                "• Create ClassroomClient as a persistent root GameObject\n" +
                "• Add ClassroomClientManager, WebRTCConnection, WebSocketClient\n" +
                "• Create StreamingCamera as child of main camera\n" +
                "• Wire all component references automatically\n" +
                "• Pre-fill server URL, device secret, and app name\n\n" +
                "The VR application is never modified. ClassroomClient runs silently underneath.",
                MessageType.Info);
        }

        private void SetupClassroomClient()
        {
            // Find camera for reference
            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindAnyObjectByType<Camera>();
            }

            if (targetCamera == null)
            {
                EditorUtility.DisplayDialog("Error", "No camera found! Please add a camera to the scene.", "OK");
                return;
            }

            // Create ClassroomClient as ROOT GameObject (persists across scene loads)
            GameObject classroomClientGO = new GameObject("ClassroomClient");
            classroomClientGO.transform.position = Vector3.zero;
            classroomClientGO.transform.rotation = Quaternion.identity;

            try
            {
                // 1. Add ClassroomClientManager
                var managerType = System.Type.GetType("ClassroomClient.Core.ClassroomClientManager, ClassroomClient");
                if (managerType != null)
                {
                    var manager = classroomClientGO.AddComponent(managerType);
                }
                else
                {
                    Debug.LogError("[ClassroomClient] ClassroomClientManager type not found!");
                    EditorUtility.DisplayDialog("Error", "ClassroomClientManager component not found. Please check compilation errors.", "OK");
                    return;
                }

                // 2. Add WebRTCConnection
                var webRTCType = System.Type.GetType("ClassroomClient.Networking.WebRTCConnection, ClassroomClient");
                if (webRTCType != null)
                {
                    var webRTC = classroomClientGO.AddComponent(webRTCType);
                }
                else
                {
                    Debug.LogError("[ClassroomClient] WebRTCConnection type not found!");
                }

                // 3. Add WebSocketClient
                var webSocketType = System.Type.GetType("ClassroomClient.Networking.WebSocketClient, ClassroomClient");
                if (webSocketType != null)
                {
                    var webSocket = classroomClientGO.AddComponent(webSocketType);
                }
                else
                {
                    Debug.LogError("[ClassroomClient] WebSocketClient type not found!");
                }

                // 4. Create StreamingCamera as child of main camera
                Camera streamingCamera = CreateStreamingCamera(targetCamera);

                // 5. Create HUD as child of ClassroomClient GameObject
                GameObject hudGO = new GameObject("ClassroomClient_HUD");
                hudGO.transform.SetParent(classroomClientGO.transform, false);
                var hudType = System.Type.GetType("ClassroomClient.HUD.ClassroomHUD, ClassroomClient");
                if (hudType != null)
                {
                    hudGO.AddComponent(hudType);
                }
                else
                {
                    Debug.LogError("[ClassroomClient] ClassroomHUD type not found!");
                }

                // 6. Auto-reference all components
                SetupComponentReferences(classroomClientGO, streamingCamera, hudGO);

                Debug.Log("[ClassroomClient] Setup complete!");
                EditorUtility.DisplayDialog("Setup Complete",
                    "ClassroomClient is ready.\n\n" +
                    "Configured:\n" +
                    "  Server URL: " + serverUrl + "\n" +
                    "  App Name:   " + appName + "\n\n" +
                    "All components created and wired automatically.\n" +
                    "Build and deploy to Quest — the package connects silently on app start.", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ClassroomClient] Error adding components: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to add components: {e.Message}", "OK");
            }

            EditorUtility.SetDirty(classroomClientGO);
        }

        private Camera CreateStreamingCamera(Camera mainCamera)
        {
            // Check if StreamingCamera already exists
            Transform existingStreamCam = mainCamera.transform.Find("StreamingCamera");
            if (existingStreamCam != null)
            {
                Camera existingCam = existingStreamCam.GetComponent<Camera>();
                if (existingCam != null)
                {
                    Debug.Log("[ClassroomClient] Using existing StreamingCamera");
                    return existingCam;
                }
            }

            // Create StreamingCamera as child of main camera
            GameObject streamCamGO = new GameObject("StreamingCamera");
            streamCamGO.transform.SetParent(mainCamera.transform);
            streamCamGO.transform.localPosition = Vector3.zero;
            streamCamGO.transform.localRotation = Quaternion.identity;
            streamCamGO.transform.localScale = Vector3.one;

            Camera streamCam = streamCamGO.AddComponent<Camera>();
            
            // Copy settings from main camera
            streamCam.clearFlags = mainCamera.clearFlags;
            streamCam.backgroundColor = mainCamera.backgroundColor;
            streamCam.cullingMask = mainCamera.cullingMask;
            streamCam.fieldOfView = mainCamera.fieldOfView;
            streamCam.nearClipPlane = mainCamera.nearClipPlane;
            streamCam.farClipPlane = mainCamera.farClipPlane;
            streamCam.depth = mainCamera.depth - 1; // Render before main camera
            
            // Critical: Don't render to any display (WebRTC captures internally)
            streamCam.targetDisplay = 7; // Unused display
            streamCam.enabled = true;

            Debug.Log($"[ClassroomClient] Created StreamingCamera under {mainCamera.name}");
            return streamCam;
        }

        private void SetupComponentReferences(GameObject classroomClientGO, Camera streamingCamera, GameObject hudGO)
        {
            var manager = classroomClientGO.GetComponent("ClassroomClient.Core.ClassroomClientManager");
            var webRTC = classroomClientGO.GetComponent("ClassroomClient.Networking.WebRTCConnection");
            var webSocket = classroomClientGO.GetComponent("ClassroomClient.Networking.WebSocketClient");
            var hud = hudGO != null ? hudGO.GetComponent("ClassroomClient.HUD.ClassroomHUD") : null;

            bool allComponentsFound = true;

            if (manager == null)
            {
                Debug.LogError("[ClassroomClient] ClassroomClientManager component not found!");
                allComponentsFound = false;
            }

            if (webRTC == null)
            {
                Debug.LogError("[ClassroomClient] WebRTCConnection component not found!");
                allComponentsFound = false;
            }

            if (webSocket == null)
            {
                Debug.LogError("[ClassroomClient] WebSocketClient component not found!");
                allComponentsFound = false;
            }

            if (streamingCamera == null)
            {
                Debug.LogError("[ClassroomClient] StreamingCamera not created!");
                allComponentsFound = false;
            }

            if (!allComponentsFound)
            {
                Debug.LogError("[ClassroomClient] Cannot set up component references because some components are missing.");
                return;
            }

            try
            {
                var managerSO = new SerializedObject(manager);
                var webSocketField = managerSO.FindProperty("webSocketClient");
                var webRTCField = managerSO.FindProperty("webRTCConnection");
                var cameraField = managerSO.FindProperty("streamCamera");
                var hudField = managerSO.FindProperty("hud");

                if (webSocketField != null)
                {
                    webSocketField.objectReferenceValue = webSocket;
                }
                else
                {
                    Debug.LogError("[ClassroomClient] webSocketClient field not found in ClassroomClientManager");
                }

                if (webRTCField != null)
                {
                    webRTCField.objectReferenceValue = webRTC;
                }
                else
                {
                    Debug.LogError("[ClassroomClient] webRTCConnection field not found in ClassroomClientManager");
                }

                if (cameraField != null && streamingCamera != null)
                {
                    cameraField.objectReferenceValue = streamingCamera;
                }
                if (hudField != null && hud != null)
                {
                    hudField.objectReferenceValue = hud;
                }

                var serverUrlField = managerSO.FindProperty("serverUrl");
                if (serverUrlField != null)
                    serverUrlField.stringValue = serverUrl;

                var deviceSecretField = managerSO.FindProperty("deviceSecret");
                if (deviceSecretField != null)
                    deviceSecretField.stringValue = deviceSecret;

                var appNameField = managerSO.FindProperty("appName");
                if (appNameField != null)
                    appNameField.stringValue = appName;

                managerSO.ApplyModifiedProperties();

                var webRTCSO = new SerializedObject(webRTC);
                var webRTCCameraField = webRTCSO.FindProperty("streamCamera");

                if (webRTCCameraField != null && streamingCamera != null)
                {
                    webRTCCameraField.objectReferenceValue = streamingCamera;
                }

                webRTCSO.ApplyModifiedProperties();
                
                Debug.Log($"[ClassroomClient] Component references set. StreamingCamera: {streamingCamera.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ClassroomClient] Error setting up component references: {e.Message}");
            }
        }

    }
}
