using UnityEditor;
using UnityEngine;


namespace ClassroomClient.Editor
{
    public class SimpleSetupWizard : EditorWindow
    {
        private string serverUrl = "ws://192.168.50.1:8080";
        private string serverToken = "";
        private string appName = "VR Training App";
        private bool showToken = false;
        private int cameraSetupModeIndex = 0; // 0 = Automatic, 1 = API Controlled
        private float hudDistance = 0.6f;
        private float hudVerticalOffset = -0.10f;
        private float hudIconHeight = 0.22f;
        private float hudIconSideOffset = 0.25f;
        private bool streamAppAudio = true;
        private bool streamMicrophone = false;
        private const string MicrophoneDefine = "CLASSROOMCLIENT_MICROPHONE";

        // Server URL presets (hardcoded). Index 2 = Custom (free-text).
        private static readonly string[] ServerPresetLabels = { "Raspberry Pi classroom", "blip3d.com cloud", "Custom" };
        private static readonly string[] ServerPresetUrls = { "ws://192.168.50.1:8080", "wss://blip3d.com", "" };
        private const int ServerPresetCustom = 2;
        private int serverPresetIndex = 0; // 0 = Raspberry Pi (matches the serverUrl default)

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
                    "Choose a server preset, or pick Custom to enter the URL from your IT department.\n" +
                    "Raspberry Pi classroom: ws://192.168.50.1:8080\n" +
                    "blip3d.com cloud: wss://blip3d.com",
                    MessageType.Info);
                EditorGUILayout.Space();

                int newServerPreset = EditorGUILayout.Popup("Server", serverPresetIndex, ServerPresetLabels);
                if (newServerPreset != serverPresetIndex)
                {
                    serverPresetIndex = newServerPreset;
                    if (serverPresetIndex != ServerPresetCustom)
                        serverUrl = ServerPresetUrls[serverPresetIndex];
                    GUI.FocusControl(null); // force the URL field to refresh even if it is focused
                }

                serverUrl = EditorGUILayout.TextField("Server URL", serverUrl);

                // Editing a preset URL by hand switches the dropdown to Custom.
                if (serverPresetIndex != ServerPresetCustom && serverUrl != ServerPresetUrls[serverPresetIndex])
                    serverPresetIndex = ServerPresetCustom;

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
                cameraSetupModeIndex = EditorGUILayout.Popup("Camera Setup", cameraSetupModeIndex,
                    new[] { "Automatic (Recommended)", "API Controlled (Advanced)" });
                EditorGUILayout.HelpBox(
                    cameraSetupModeIndex == 0
                        ? "Automatic: ClassroomClient creates a dedicated streaming camera that follows the main camera. Use this for normal projects."
                        : "API Controlled: no streaming camera is set up here. Call ClassroomClientAPI.SetStreamCamera(camera) at runtime when your camera (e.g. a runtime/Addressable-loaded rig) exists. The HMD stays protected either way.",
                    MessageType.None);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Headset HUD", EditorStyles.boldLabel);
                hudDistance = EditorGUILayout.Slider(
                    new GUIContent("Distance (m)", "How far in front of the user's eyes teacher messages float."),
                    hudDistance, 0.3f, 1.5f);
                hudVerticalOffset = EditorGUILayout.Slider(
                    new GUIContent("Height (m)", "Height of the message panel relative to eye level. Negative = below eye level (default -0.10 m)."),
                    hudVerticalOffset, -0.4f, 0.3f);
                hudIconHeight = EditorGUILayout.Slider(
                    new GUIContent("Icon height (m)", "Height of the status icons (cast / microphone) relative to eye level. Positive = above eye level, out of the way of the VR scene (default +0.22 m)."),
                    hudIconHeight, -0.2f, 0.4f);
                hudIconSideOffset = EditorGUILayout.Slider(
                    new GUIContent("Icon side (m)", "Sideways position of the status icons. Positive = to the right (default +0.25 m → upper-right corner)."),
                    hudIconSideOffset, -0.4f, 0.4f);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Streamed Audio", EditorStyles.boldLabel);
                streamAppAudio = EditorGUILayout.Toggle(
                    new GUIContent("Stream app audio", "Send the app's sound to the dashboard with the video. The headset's own sound is never changed."),
                    streamAppAudio);
                streamMicrophone = EditorGUILayout.Toggle(
                    new GUIContent("Stream microphone", "Also send the headset microphone to the dashboard (a mic icon shows in the HUD while it is streamed). Adds the Android microphone permission to your app."),
                    streamMicrophone);
                if (streamMicrophone)
                {
                    EditorGUILayout.HelpBox(
                        "Adds the scripting define " + MicrophoneDefine + " (Android + current platform) so the microphone code is compiled in. Your app will then declare the Android microphone permission and ask the user for it.",
                        MessageType.Info);
                }

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
                    (cameraSetupModeIndex == 0
                        ? "• Create StreamingCamera as child of main camera\n"
                        : "• Skip camera setup — call ClassroomClientAPI.SetStreamCamera(camera) at runtime\n") +
                    "• Wire all component references automatically\n" +
                    "• Pre-fill server URL, server token, and app name\n\n" +
                    "The VR application is never modified. ClassroomClient runs silently underneath.",
                    MessageType.Info);
            }
            else
            {
                // A script recompile (domain reload) resets the wizard's in-memory state.
                if (_sceneLibraryStep == null) _sceneLibraryStep = new SceneLibraryStep();
                if (_createdManager == null)
                    _createdManager = FindAnyObjectByType<ClassroomClient.Core.ClassroomClientManager>();

                bool done = _sceneLibraryStep.DrawGUI(_createdManager);
                if (done)
                {
                    // Applied last: changing a scripting define recompiles scripts, which would
                    // interrupt the wizard if done earlier.
                    SetMicrophoneDefine(streamMicrophone);
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
            // Find camera for reference (only required in Automatic mode).
            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindAnyObjectByType<Camera>();
            }

            if (cameraSetupModeIndex == 0 && targetCamera == null)
            {
                EditorUtility.DisplayDialog("Error", "Automatic camera mode requires a camera in the scene. Add a camera, or choose API Controlled mode.", "OK");
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

                var webRTCType = System.Type.GetType("ClassroomClient.Networking.WebRTCConnection, ClassroomClient");
                if (webRTCType != null)
                {
                    var webRTC = classroomClientGO.AddComponent(webRTCType);
                }
                else
                {
                    Debug.LogError("[ClassroomClient] WebRTCConnection type not found!");
                }

                var webSocketType = System.Type.GetType("ClassroomClient.Networking.WebSocketClient, ClassroomClient");
                if (webSocketType != null)
                {
                    var webSocket = classroomClientGO.AddComponent(webSocketType);
                }
                else
                {
                    Debug.LogError("[ClassroomClient] WebSocketClient type not found!");
                }

                var sceneManagerType = System.Type.GetType("ClassroomClient.Core.ClassroomSceneManager, ClassroomClient");
                if (sceneManagerType != null)
                {
                    classroomClientGO.AddComponent(sceneManagerType);
                }
                else
                {
                    Debug.LogWarning("[ClassroomClient] ClassroomSceneManager type not found — scene management unavailable");
                }

                // 4. Create StreamingCamera (Automatic mode only). In API Controlled mode the developer
                // assigns the viewpoint at runtime via ClassroomClientAPI.SetStreamCamera(camera).
                Camera streamingCamera = (cameraSetupModeIndex == 0) ? CreateStreamingCamera(targetCamera) : null;

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

                SetupComponentReferences(classroomClientGO, streamingCamera, hudGO);

                Debug.Log("[ClassroomClient] Setup complete — configure scene library next.");

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
            Transform existingStreamCam = mainCamera.transform.Find("StreamingCamera");
            if (existingStreamCam != null)
            {
                Camera existingCam = existingStreamCam.GetComponent<Camera>();
                if (existingCam != null)
                {
                    var reuseMarker = System.Type.GetType("ClassroomClient.Core.ClassroomStreamCameraMarker, ClassroomClient");
                    if (reuseMarker != null)
                    {
                        if (existingCam.GetComponent(reuseMarker) == null)
                            existingCam.gameObject.AddComponent(reuseMarker);
                    }
                    else
                    {
                        Debug.LogWarning("[ClassroomClient] ClassroomStreamCameraMarker type not found — the dedicated camera will be marked at runtime instead.");
                    }
                    Debug.Log("[ClassroomClient] Using existing StreamingCamera");
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
            streamCam.depth = mainCamera.depth - 1; // Render before main camera
            
            // Off-display while idle. targetDisplay (a serialized int) survives scene save/reload —
            // unlike a RenderTexture, which would be lost. At runtime WebRTC assigns the real stream
            // RenderTexture (which overrides targetDisplay), so the dedicated camera never hits the HMD.
            streamCam.targetDisplay = 7; // Unused display
            streamCam.enabled = true;

            // Internal marker so ClassroomClient recognizes this as its own dedicated capture camera.
            var markerType = System.Type.GetType("ClassroomClient.Core.ClassroomStreamCameraMarker, ClassroomClient");
            if (markerType != null)
            {
                if (streamCamGO.GetComponent(markerType) == null)
                    streamCamGO.AddComponent(markerType);
            }
            else
            {
                Debug.LogWarning("[ClassroomClient] ClassroomStreamCameraMarker type not found — the dedicated camera will be marked at runtime instead.");
            }

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

            // streamingCamera is intentionally null in API Controlled mode — not an error.

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

                var cameraModeField = managerSO.FindProperty("cameraSetupMode");
                if (cameraModeField != null)
                    cameraModeField.enumValueIndex = cameraSetupModeIndex;

                var appAudioField = managerSO.FindProperty("streamAppAudio");
                if (appAudioField != null)
                    appAudioField.boolValue = streamAppAudio;

                var microphoneField = managerSO.FindProperty("streamMicrophone");
                if (microphoneField != null)
                    microphoneField.boolValue = streamMicrophone;

                managerSO.ApplyModifiedProperties();

                var webSocketSO = new SerializedObject(webSocket);
                var deviceNameField = webSocketSO.FindProperty("deviceName");
                // No name is baked into the build: every headset running the same build would share
                // it. The server shows a short device id; the admin names headsets in the dashboard
                // (Admin → Devices → Rename). Re-running the wizard clears a name set by older versions.
                if (deviceNameField != null)
                    deviceNameField.stringValue = "";
                webSocketSO.ApplyModifiedProperties();

                var webRTCSO = new SerializedObject(webRTC);
                var webRTCCameraField = webRTCSO.FindProperty("streamCamera");

                if (webRTCCameraField != null && streamingCamera != null)
                {
                    webRTCCameraField.objectReferenceValue = streamingCamera;
                }

                webRTCSO.ApplyModifiedProperties();

                if (hud != null)
                {
                    var hudSO = new SerializedObject(hud);
                    var distanceField = hudSO.FindProperty("hudDistance");
                    if (distanceField != null)
                        distanceField.floatValue = hudDistance;
                    var heightField = hudSO.FindProperty("hudVerticalOffset");
                    if (heightField != null)
                        heightField.floatValue = hudVerticalOffset;
                    var iconHeightField = hudSO.FindProperty("hudIconHeight");
                    if (iconHeightField != null)
                        iconHeightField.floatValue = hudIconHeight;
                    var iconSideField = hudSO.FindProperty("hudIconSideOffset");
                    if (iconSideField != null)
                        iconSideField.floatValue = hudIconSideOffset;
                    hudSO.ApplyModifiedProperties();
                }
                
                Debug.Log($"[ClassroomClient] Component references set. StreamingCamera: {(streamingCamera != null ? streamingCamera.name : "(none — API Controlled mode)")}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ClassroomClient] Error setting up component references: {e.Message}");
            }
        }

        // The microphone code is compiled only with this define, so apps that do not stream the
        // microphone do not get the Android RECORD_AUDIO permission added by Unity.
        private static void SetMicrophoneDefine(bool enabled)
        {
            var targets = new System.Collections.Generic.HashSet<UnityEditor.Build.NamedBuildTarget>
            {
                UnityEditor.Build.NamedBuildTarget.Android,
                UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup),
            };
            foreach (var target in targets)
            {
                var defines = new System.Collections.Generic.List<string>(
                    PlayerSettings.GetScriptingDefineSymbols(target).Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
                bool has = defines.Contains(MicrophoneDefine);
                if (enabled == has) continue;
                if (enabled) defines.Add(MicrophoneDefine);
                else defines.Remove(MicrophoneDefine);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }
        }
    }
}
