using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ClassroomClient.Core;

namespace ClassroomClient.Editor
{
    /// <summary>
    /// Handles the "Configure Scene Library" final step of the Setup Wizard.
    /// Call DrawGUI() from SimpleSetupWizard.OnGUI() once the main setup is done.
    /// Returns true when the user clicks "Finish" and the step is complete.
    /// </summary>
    public class SceneLibraryStep
    {
        // ── State ─────────────────────────────────────────────────────────────
        private List<BuildSceneRow> _buildRows = new List<BuildSceneRow>();
        private List<AddressableRow> _addressableRows = new List<AddressableRow>();
        private bool _showAddressableForm;
        private AddressableFormData _newAddressable = new AddressableFormData();
        private bool _initialised;
        private Vector2 _scroll;

        private static readonly string[] CategoryOptions =
            { "General", "Training", "Simulation", "Assessment", "Lab", "Other" };

        // ─────────────────────────────────────────────────────────────────────

        private class BuildSceneRow
        {
            public bool include = true;
            public string scenePath; // Build Settings path
            public string displayName;
            public int categoryIndex;
            public bool customLoading;
        }

        private class AddressableRow
        {
            public string displayName;
            public string addressableKey;
            public int categoryIndex;
            public bool customLoading;
        }

        private class AddressableFormData
        {
            public string displayName = "";
            public string addressableKey = "";
            public int categoryIndex;
            public bool customLoading;
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Render the scene library configuration step.
        /// Returns true when setup is complete (Finish clicked).
        /// </summary>
        public bool DrawGUI(Component managerComponent)
        {
            if (!_initialised) Initialise();

            GUILayout.Label("Configure Scene Library", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select which scenes to include in the content library.",
                MessageType.Info);
            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(280));

            // ── Build Settings scenes ────────────────────────────────────────
            if (_buildRows.Count > 0)
            {
                EditorGUILayout.LabelField("Scenes from Build Settings", EditorStyles.miniBoldLabel);
                foreach (var row in _buildRows)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    row.include = EditorGUILayout.Toggle(row.include, GUILayout.Width(16));
                    EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(row.scenePath),
                        GUILayout.Width(160));
                    EditorGUILayout.LabelField("Display name:", GUILayout.Width(80));
                    row.displayName = EditorGUILayout.TextField(row.displayName);
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.BeginDisabledGroup(!row.include);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField("Category:", GUILayout.Width(60));
                    row.categoryIndex = EditorGUILayout.Popup(row.categoryIndex, CategoryOptions,
                        GUILayout.Width(110));
                    GUILayout.Space(12);
                    row.customLoading = EditorGUILayout.ToggleLeft(
                        new GUIContent("Custom Loading", "Check this if the scene is loaded via Unity Addressables (Addressables.LoadSceneAsync). Leave unchecked for standard SceneManager.LoadScene loading."),
                        row.customLoading);
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();

                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.Space();
            }
            else
            {
                EditorGUILayout.HelpBox("No scenes found in Build Settings.", MessageType.None);
            }

            // ── Manually added Addressable scenes ────────────────────────────
            if (_addressableRows.Count > 0)
            {
                EditorGUILayout.LabelField("Addressable Scenes", EditorStyles.miniBoldLabel);
                int toRemove = -1;
                for (int i = 0; i < _addressableRows.Count; i++)
                {
                    var row = _addressableRows[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Display name:", GUILayout.Width(80));
                    row.displayName = EditorGUILayout.TextField(row.displayName);
                    EditorGUILayout.LabelField("Key:", GUILayout.Width(30));
                    row.addressableKey = EditorGUILayout.TextField(row.addressableKey);
                    if (GUILayout.Button("✕", GUILayout.Width(22))) toRemove = i;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField("Category:", GUILayout.Width(60));
                    row.categoryIndex = EditorGUILayout.Popup(row.categoryIndex, CategoryOptions,
                        GUILayout.Width(110));
                    GUILayout.Space(12);
                    row.customLoading = EditorGUILayout.ToggleLeft(
                        new GUIContent("Custom Loading", "Check this if the scene is loaded via Unity Addressables (Addressables.LoadSceneAsync). Leave unchecked for standard SceneManager.LoadScene loading."),
                        row.customLoading);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
                if (toRemove >= 0) _addressableRows.RemoveAt(toRemove);
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();

            // ── Add Addressable button / mini-form ───────────────────────────
            if (!_showAddressableForm)
            {
                if (GUILayout.Button("+ Add Addressable Scene"))
                {
                    _showAddressableForm = true;
                    _newAddressable = new AddressableFormData();
                }
            }
            else
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("New Addressable Scene", EditorStyles.miniBoldLabel);
                _newAddressable.displayName = EditorGUILayout.TextField("Display Name", _newAddressable.displayName);
                _newAddressable.addressableKey = EditorGUILayout.TextField("Addressable Key", _newAddressable.addressableKey);
                _newAddressable.categoryIndex = EditorGUILayout.Popup("Category",
                    _newAddressable.categoryIndex, CategoryOptions);
                _newAddressable.customLoading = EditorGUILayout.ToggleLeft(
                    new GUIContent("Custom Loading", "Check this if the scene is loaded via Unity Addressables (Addressables.LoadSceneAsync). Leave unchecked for standard SceneManager.LoadScene loading."),
                    _newAddressable.customLoading);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add"))
                {
                    if (!string.IsNullOrEmpty(_newAddressable.displayName) &&
                        !string.IsNullOrEmpty(_newAddressable.addressableKey))
                    {
                        _addressableRows.Add(new AddressableRow
                        {
                            displayName = _newAddressable.displayName,
                            addressableKey = _newAddressable.addressableKey,
                            categoryIndex = _newAddressable.categoryIndex,
                            customLoading = _newAddressable.customLoading,
                        });
                        _showAddressableForm = false;
                    }
                }
                if (GUILayout.Button("Cancel")) _showAddressableForm = false;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();

            // ── Finish button ─────────────────────────────────────────────────
            if (GUILayout.Button("Finish", GUILayout.Height(36)))
            {
                CreateAndAssignSceneLibrary(managerComponent);
                return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────

        private void Initialise()
        {
            _buildRows.Clear();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                _buildRows.Add(new BuildSceneRow
                {
                    include = true,
                    scenePath = scene.path,
                    displayName = Path.GetFileNameWithoutExtension(scene.path),
                    categoryIndex = 0,
                    customLoading = false,
                });
            }
            _initialised = true;
        }

        private void CreateAndAssignSceneLibrary(Component managerComponent)
        {
            // Collect selected scenes
            var sceneInfos = new List<SceneInfo>();

            foreach (var row in _buildRows.Where(r => r.include))
            {
                sceneInfos.Add(new SceneInfo
                {
                    displayName = string.IsNullOrEmpty(row.displayName)
                        ? Path.GetFileNameWithoutExtension(row.scenePath) : row.displayName,
                    sceneKey = row.scenePath,
                    loadType = row.customLoading ? SceneLoadType.Addressable : SceneLoadType.Standard,
                    category = CategoryOptions[row.categoryIndex],
                    customLoading = row.customLoading,
                });
            }

            foreach (var row in _addressableRows)
            {
                sceneInfos.Add(new SceneInfo
                {
                    displayName = row.displayName,
                    sceneKey = row.addressableKey,
                    loadType = SceneLoadType.Addressable,
                    category = CategoryOptions[row.categoryIndex],
                    customLoading = row.customLoading,
                });
            }

            // Create SceneLibrary asset
            const string assetFolder = "Assets/ClassroomClient";
            if (!AssetDatabase.IsValidFolder(assetFolder))
                AssetDatabase.CreateFolder("Assets", "ClassroomClient");

            const string assetPath = assetFolder + "/SceneLibrary.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SceneLibrary>(assetPath);
            SceneLibrary library;
            if (existing != null)
            {
                library = existing;
                library.scenes.Clear();
            }
            else
            {
                library = ScriptableObject.CreateInstance<SceneLibrary>();
                AssetDatabase.CreateAsset(library, assetPath);
            }

            library.scenes.AddRange(sceneInfos);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            // Assign to ClassroomClientManager
            if (managerComponent != null)
            {
                var so = new SerializedObject(managerComponent);
                var prop = so.FindProperty("sceneLibrary");
                if (prop != null)
                {
                    prop.objectReferenceValue = library;
                    so.ApplyModifiedProperties();
                }
            }

            Debug.Log($"[ClassroomClient] Scene library configured with {sceneInfos.Count} scene(s) → {assetPath}");
        }
    }
}
