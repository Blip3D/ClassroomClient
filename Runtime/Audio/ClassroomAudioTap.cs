using UnityEngine;

namespace ClassroomClient.Audio
{
    /// <summary>
    /// Added by ClassroomClient to the app's active AudioListener. Receives the final game mix on the
    /// audio thread and hands a COPY to StreamAudioCapture. The buffer itself is never modified, so
    /// the headset hears exactly what it heard before.
    /// (Unity WebRTC's own AudioStreamTrack(AudioListener) clears the buffer unless Loopback is set —
    /// that would silence the headset, which is why ClassroomClient uses this tap instead.)
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal class ClassroomAudioTap : MonoBehaviour
    {
        // Written on the main thread, read on the audio thread — a reference swap is atomic.
        internal volatile StreamAudioCapture capture;

        void OnAudioFilterRead(float[] data, int channels)
        {
            capture?.OnGameAudio(data, channels);
        }
    }
}
