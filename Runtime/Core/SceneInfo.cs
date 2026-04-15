using System;
using UnityEngine;

namespace ClassroomClient.Core
{
    public enum SceneLoadType { Standard, Addressable }

    [Serializable]
    public class SceneInfo
    {
    [Tooltip("The name shown in the PWA dashboard for this scene.")]
    public string displayName;

    [Tooltip("The scene path (Build Settings scenes) or Addressable address key used to load this scene at runtime.")]
    public string sceneKey;

    [Tooltip("Standard: loaded via SceneManager.LoadScene. Addressable: loaded via Addressables.LoadSceneAsync. Set automatically based on Custom Loading.")]
    public SceneLoadType loadType;

    [Tooltip("Groups scenes in the PWA content view for easier navigation.")]
    public string category;

    [Tooltip("Check if this scene is loaded via Unity Addressables. Leave unchecked for standard SceneManager loading.")]
    public bool customLoading;

        public SceneInfoData ToData() => new SceneInfoData
        {
            displayName = displayName,
            sceneKey = sceneKey,
            loadType = loadType.ToString(),
            category = category ?? "",
            customLoading = customLoading,
        };
    }

    /// <summary>
    /// Plain serializable data class — used for JSON via JsonUtility when sending CONTENT_LIBRARY.
    /// Field names match the wire protocol exactly.
    /// </summary>
    [Serializable]
    public class SceneInfoData
    {
        public string displayName;
        public string sceneKey;
        public string loadType;
        public string category;
        public bool customLoading;
    }

    /// <summary>
    /// Outer envelope for the CONTENT_LIBRARY JSON payload.
    /// </summary>
    [Serializable]
    public class ContentLibraryData
    {
        public string appName;
        public string bundleId;
        public string currentScene;
        public SceneInfoData[] scenes;
    }
}
