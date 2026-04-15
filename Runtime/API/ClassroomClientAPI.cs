using UnityEngine;
using ClassroomClient.Core;

namespace ClassroomClient.API
{
    public static class ClassroomClientAPI
    {
        private static ClassroomClientManager _manager;
        private static ClassroomClientManager Manager
        {
            get
            {
                if (_manager == null) _manager = Object.FindAnyObjectByType<ClassroomClientManager>();
                return _manager;
            }
        }

        public static void SetStatus(SessionStatus status)
            => Manager?.SendSessionStatus(status.ToString());

        public static void SetAvatarUrl(string url)
            => Manager?.SetAvatarUrl(url);

        public static void ReportCurrentScene(string sceneKey)
            => Manager?.ReportCurrentScene(sceneKey);

        public static void ReportSceneLoaded(string sceneKey)
            => Manager?.ReportSceneLoaded(sceneKey);

        public static void ReportSceneLoadFailed(string sceneKey, string reason = "")
            => Manager?.ReportSceneLoadFailed(sceneKey, reason);

        public static Camera GetStreamCamera() => Manager?.GetStreamCamera();
        public static void SetStreamCamera(Camera camera) => Manager?.SetStreamCamera(camera);

        public static bool IsConnected() => Manager != null && Manager.CurrentState != ConnectionState.Disconnected;
        public static bool IsInSession() => Manager != null && Manager.CurrentState == ConnectionState.InSession;
        public static ConnectionState GetConnectionState() => Manager?.CurrentState ?? ConnectionState.Disconnected;
    }
}
