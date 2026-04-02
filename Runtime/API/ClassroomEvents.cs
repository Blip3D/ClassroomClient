using System;

namespace ClassroomClient.API
{
    public static class ClassroomEvents
    {
        public static event Action OnConnected;
        public static event Action OnSessionStarted;
        public static event Action OnSessionEnded;
        public static event Action<string, string, string> OnMessageReceived;
        public static event Action<bool> OnMuteChanged;
        public static event Action OnDisconnected;

        internal static void FireOnConnected() => OnConnected?.Invoke();
        internal static void FireOnSessionStarted() => OnSessionStarted?.Invoke();
        internal static void FireOnSessionEnded() => OnSessionEnded?.Invoke();
        internal static void FireOnMessageReceived(string text, string color, string category) => OnMessageReceived?.Invoke(text, color, category);
        internal static void FireOnMuteChanged(bool muted) => OnMuteChanged?.Invoke(muted);
        internal static void FireOnDisconnected() => OnDisconnected?.Invoke();
    }
}
