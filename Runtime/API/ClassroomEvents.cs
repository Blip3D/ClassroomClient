using System;
using ClassroomClient.Core;

namespace ClassroomClient.API
{
    public static class ClassroomEvents
    {
        public static event Action OnConnected;
        public static event Action OnSessionStarted;
        public static event Action OnSessionEnded;
        public static event Action<string, string, string> OnMessageReceived;
#pragma warning disable 0067 // kept only so existing integrations still compile
        [Obsolete("Removed in 2.75.0: the dashboard mute button now mutes only the stream in the browser and never affects the headset. This event is never raised.")]
        public static event Action<bool> OnMuteChanged;
#pragma warning restore 0067
        public static event Action OnDisconnected;
        /// <summary>Raised in API Controlled mode when the streamed camera is destroyed or deactivated (e.g. the app changed scene). Call ClassroomClientAPI.SetStreamCamera(camera) with the new camera.</summary>
        public static event Action OnStreamCameraLost;
        /// <summary>Raised when the teacher loads a scene marked "Custom Loading". Load it yourself, then call ClassroomClientAPI.ReportSceneLoaded or ReportSceneLoadFailed.</summary>
        public static event Action<SceneInfo> OnLoadSceneRequested;
        /// <summary>Raised after ClassroomClient finished loading a scene (scene key).</summary>
        public static event Action<string> OnSceneLoadCompleted;
        /// <summary>Raised when a teacher-triggered scene load failed (scene key, reason).</summary>
        public static event Action<string, string> OnSceneLoadFailed;
        /// <summary>Raised when the teacher picks a language for THIS headset in the dashboard (locale code, e.g. "de", "en", "pt-BR"). Switch and save the app's language, then call ClassroomClientAPI.ReportLanguage — or ReportLanguageFailed if it cannot be done.</summary>
        public static event Action<string> OnLanguageChangeRequested;

        internal static void FireOnConnected() => OnConnected?.Invoke();
        internal static void FireOnSessionStarted() => OnSessionStarted?.Invoke();
        internal static void FireOnSessionEnded() => OnSessionEnded?.Invoke();
        internal static void FireOnMessageReceived(string text, string color, string category) => OnMessageReceived?.Invoke(text, color, category);
        internal static void FireOnDisconnected() => OnDisconnected?.Invoke();
        internal static void FireOnStreamCameraLost() => OnStreamCameraLost?.Invoke();
        internal static bool HasLoadSceneRequestedHandler => OnLoadSceneRequested != null;
        internal static void FireOnLoadSceneRequested(SceneInfo info) => OnLoadSceneRequested?.Invoke(info);
        internal static void FireOnSceneLoadCompleted(string sceneKey) => OnSceneLoadCompleted?.Invoke(sceneKey);
        internal static void FireOnSceneLoadFailed(string sceneKey, string reason) => OnSceneLoadFailed?.Invoke(sceneKey, reason);
        internal static bool HasLanguageChangeRequestedHandler => OnLanguageChangeRequested != null;
        internal static void FireOnLanguageChangeRequested(string localeCode) => OnLanguageChangeRequested?.Invoke(localeCode);
    }
}
