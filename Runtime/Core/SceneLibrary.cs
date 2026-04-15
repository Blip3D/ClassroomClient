using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClassroomClient.Core
{
    [CreateAssetMenu(fileName = "SceneLibrary", menuName = "ClassroomClient/Scene Library")]
    public class SceneLibrary : ScriptableObject
    {
        public List<SceneInfo> scenes = new List<SceneInfo>();

        /// <summary>Returns the first scene whose sceneKey matches, or null.</summary>
        public SceneInfo GetSceneByKey(string key)
            => scenes.FirstOrDefault(s => s.sceneKey == key);

        /// <summary>
        /// Serialises the library to the ContentLibraryData envelope used by CONTENT_LIBRARY pipe message.
        /// </summary>
        public ContentLibraryData ToContentLibraryData(string appName, string bundleId, string currentScene)
        {
            return new ContentLibraryData
            {
                appName = appName ?? "",
                bundleId = bundleId ?? "",
                currentScene = currentScene ?? "",
                scenes = scenes.Select(s => s.ToData()).ToArray(),
            };
        }
    }
}
