using UnityEditor;
using UnityEngine;


namespace ClassroomClient.Editor
{
    public class SimpleSetupWizard : EditorWindow
    {
        private string serverUrl = "";
        private string serverToken = "";
        private string deviceName = "";
        private string appName = "VR Training App";
        private bool showToken = false;
        private bool _setupComplete;
        private SceneLibraryStep _sceneLibraryStep;
        private Component _createdManager;

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
            if (!_setupComplete)
            {
                GUILayout.Label("ClassroomClient Setup", EditorStyles.largeLabel);
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Enter the server URL provided by your IT department.\n" +
                    "Local classroom: ws://192.168.x.x:9000\n" +
                    "Cloud / university server: wss://your-university-server.com",
                    MessageType.Info);
                EditorGUILayout.Space();

                deviceName = EditorGUILayout.TextField("Device Name", deviceName);
                EditorGUILayout.HelpBox("A friendly name for this headset (e.g. Station 1, Quest Lab A).", MessageType.None);
                EditorGUILayout.Space();

                serverUrl = EditorGUILayout.TextField("Server URL", serverUrl);

                EditorGUILayout.BeginHorizontal();
                if (showToken)
                    serverToken = EditorGUILayout.TextField("Server Token", serverToken);
                else
                    serverToken = EditorGUILayout.PasswordField("Server Token", serverToken);
                if (GUILayout.Button(showToken ? "Hide" : "Show", GUILayout.Width(45)))
                    showToken = !showToken;
                EditorGUILayout.EndHorizontal();

                appName = EditorGUILayout.TextField("App Name", appName);

                EditorGUILayout.Space();

                bool configValid = !string.IsNullOrEmpty(serverUrl) && !string.IsNullOrEmpty(serverToken);
                if (!configValid)
                    EditorGUILayout.HelpBox("Server URL and Server Token are required.", MessageType.Warning);

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
                    "• Pre-fill server URL, server token, and app name\n\n" +
                    "The VR application is never modified. ClassroomClient runs silently underneath.",
                    MessageType.Info);
            }
            else
            {
                bool done = _sceneLibraryStep.DrawGUI(_createdManager);
                if (done)
                {
                    EditorUtility.DisplayDialog("Setup Complete",
                        "ClassroomClient and scene library are configured.\n" +
                        "Build and deploy to Quest.",
                        "OK");
                    Close();
                }
            }
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

            // Destroy any existing ClassroomClient GameObjects to prevent duplicates on re-run
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var go in rootObjects)
            {
                if (go.name == "ClassroomClient")
                {
                    DestroyImmediate(go);
                }
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

                // 3b. Add ClassroomSceneManager
                var sceneManagerType = System.Type.GetType("ClassroomClient.Core.ClassroomSceneManager, ClassroomClient");
                if (sceneManagerType != null)
                {
                    classroomClientGO.AddComponent(sceneManagerType);
                }
                else
                {
                    Debug.LogWarning("[ClassroomClient] ClassroomSceneManager type not found — scene management unavailable");
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

                Debug.Log("[ClassroomClient] Setup complete — configure scene library next.");

                // Transition to scene library step
                var managerComponent = classroomClientGO.GetComponent("ClassroomClient.Core.ClassroomClientManager") as Component;
                _createdManager = managerComponent;
                _setupComplete = true;
                _sceneLibraryStep = new SceneLibraryStep();
                Repaint();
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

                var serverTokenField = managerSO.FindProperty("serverToken");
                if (serverTokenField != null)
                    serverTokenField.stringValue = serverToken;

                var appNameField = managerSO.FindProperty("appName");
                if (appNameField != null)
                    appNameField.stringValue = appName;

                managerSO.ApplyModifiedProperties();

                var webSocketSO = new SerializedObject(webSocket);
                var deviceNameField = webSocketSO.FindProperty("deviceName");
                if (deviceNameField != null)
                    deviceNameField.stringValue = deviceName;
                webSocketSO.ApplyModifiedProperties();

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
