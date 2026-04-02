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
                if (_manager == null) _manager = Object.FindFirstObjectByType<ClassroomClientManager>();
                return _manager;
            }
        }

        public static void SetStatus(SessionStatus status)
        {
            Manager?.SendSessionStatus(status.ToString().ToLower());
        }

        public static bool IsConnected() => Manager != null && Manager.CurrentState != ConnectionState.Disconnected;
        public static bool IsInSession() => Manager != null && Manager.CurrentState == ConnectionState.InSession;
        public static ConnectionState GetConnectionState() => Manager?.CurrentState ?? ConnectionState.Disconnected;

        public static void ReportCurrentScene(string sceneKey) => Debug.Log("[ClassroomClientAPI] ReportCurrentScene — future scope");
        public static void ReportSceneLoaded(string sceneKey) => Debug.Log("[ClassroomClientAPI] ReportSceneLoaded — future scope");
        public static void ReportSceneLoadFailed(string sceneKey) => Debug.Log("[ClassroomClientAPI] ReportSceneLoadFailed — future scope");
    }
}
