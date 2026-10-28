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

        /// <summary>
        /// Tells the dashboard the app's current language and the languages it can switch to (locale codes,
        /// e.g. "de", new[] { "de", "en", "pt-BR" }). Call it at startup and after every language change —
        /// it may be called before the headset is connected; the report is sent once it is registered.
        /// </summary>
        public static void ReportLanguage(string currentCode, string[] availableCodes)
        {
            ClassroomLanguageBridge.SetReport(currentCode, availableCodes);
            Manager?.SendLanguageReport();
        }

        /// <summary>Tells the dashboard that switching to the requested language failed (e.g. reason "not_available").</summary>
        public static void ReportLanguageFailed(string requestedCode, string reason = "")
            => Manager?.ReportLanguageFailed(requestedCode, reason);

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
