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
                if (_manager == null) _manager = ClassroomClientManager.Instance;
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
        public static Camera GetStreamSourceCamera() => Manager?.GetStreamSourceCamera();
        public static void SetStreamCamera(Camera camera) => Manager?.SetStreamCamera(camera);

        /// <summary>True only when the headset is registered with the server (lobby or in a session) — not while connecting, reconnecting or waiting for admin approval.</summary>
        public static bool IsConnected()
        {
            if (Manager == null) return false;
            ConnectionState state = Manager.CurrentState;
            return state == ConnectionState.InLobby || state == ConnectionState.InSession;
        }
        public static bool IsInSession() => Manager != null && Manager.CurrentState == ConnectionState.InSession;
        public static ConnectionState GetConnectionState() => Manager?.CurrentState ?? ConnectionState.Disconnected;
    }
}
