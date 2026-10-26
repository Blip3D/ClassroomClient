using System;
using UnityEngine;
using Unity.WebRTC;
using ClassroomClient.HUD;
#if CLASSROOMCLIENT_MICROPHONE && UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace ClassroomClient.Audio
{
    /// <summary>
    /// Builds the audio track that is streamed to the dashboard together with the video:
    /// the app/game sound (copied from the active AudioListener, never altered) mixed with the headset
    /// microphone (only when enabled). Nothing here changes what the user hears in the headset.
    /// The microphone code is compiled only with the CLASSROOMCLIENT_MICROPHONE scripting define
    /// (set by the Setup Wizard's "Stream microphone" option), because Unity adds the Android
    /// microphone permission to every app whose code references the Microphone API.
    /// </summary>
    public class StreamAudioCapture : MonoBehaviour
    {
        private const float ListenerCheckIntervalSeconds = 0.5f;

        private bool _streamAppAudio = true;
        private bool _streamMicrophone;
        private ClassroomHUD _hud;

        // The track is used on the audio thread; _trackLock guards it against disposal mid-write.
        private readonly object _trackLock = new object();
        private AudioStreamTrack _track;
        private int _outputSampleRate;
        private float[] _sendBuffer = new float[0]; // audio thread only

        private ClassroomAudioTap _tap;
        private float _nextListenerCheck;
        private bool _micIndicatorShown;

#if CLASSROOMCLIENT_MICROPHONE
        private const float MicRingSeconds = 1f;
        private const float MicMaxLatencySeconds = 0.2f;
        private const float MicTargetLatencySeconds = 0.1f;

        private bool _micWanted;
        // Read on the audio thread (Unity objects must not be touched there).
        private volatile bool _micActive;
#if UNITY_ANDROID && !UNITY_EDITOR
        private bool _micPermissionRequested;
#endif
        private AudioClip _micClip;
        private int _micRate;
        private int _micReadPos;
        private int _micChunk;
        private float[] _micChunkBuffer;
        private float _resampleCursor;

        // Mono ring buffer at the output sample rate: written on the main thread, read on the audio thread.
        private readonly object _ringLock = new object();
        private float[] _ring = new float[0];
        private int _ringRead;
        private int _ringCount;
#endif

        public void Configure(bool streamAppAudio, bool streamMicrophone, ClassroomHUD hud)
        {
            _streamAppAudio = streamAppAudio;
            _streamMicrophone = streamMicrophone;
            _hud = hud;
#if !CLASSROOMCLIENT_MICROPHONE
            if (streamMicrophone)
            {
                Debug.LogWarning("[StreamAudioCapture] 'Stream microphone' is on, but the microphone code is not compiled in. Re-run the ClassroomClient Setup Wizard with 'Stream microphone' ticked (it adds the CLASSROOMCLIENT_MICROPHONE scripting define).");
            }
#endif
        }

        /// <summary>
        /// Creates the audio track for a new stream (null when audio streaming is off). Called by
        /// WebRTCConnection before the offer is created.
        /// </summary>
        public AudioStreamTrack BeginStream()
        {
            EndStream();
            if (!_streamAppAudio && !MicrophoneCompiledAndWanted) return null;

            _outputSampleRate = AudioSettings.outputSampleRate;
            var track = new AudioStreamTrack();
            lock (_trackLock)
            {
                _track = track;
            }
            AttachTapToListener();
#if CLASSROOMCLIENT_MICROPHONE
            _micWanted = _streamMicrophone;
            TryStartMicrophone();
#endif
            return track;
        }

        /// <summary>Stops the microphone and releases the track. Called when the stream ends.</summary>
        public void EndStream()
        {
            AudioStreamTrack track;
            lock (_trackLock)
            {
                track = _track;
                _track = null;
            }
#if CLASSROOMCLIENT_MICROPHONE
            _micWanted = false;
            StopMicrophone();
#endif
            if (track != null)
            {
                track.Stop();
                track.Dispose();
            }
            UpdateMicIndicator();
        }

        void OnEnable()
        {
            // Unity WebRTC disposes its native context on quit, before OnDestroy — stop feeding it first.
            Application.quitting += EndStream;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        void OnDisable()
        {
            Application.quitting -= EndStream;
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        // e.g. Bluetooth headphones connected mid-stream: send at the new rate (wrong rate = wrong pitch).
        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            lock (_trackLock)
            {
                _outputSampleRate = AudioSettings.outputSampleRate;
            }
        }

        void Update()
        {
            if (!HasTrack) return;

            // The AudioListener usually lives on the app's camera rig and is replaced on scene change.
            if (Time.unscaledTime >= _nextListenerCheck)
            {
                _nextListenerCheck = Time.unscaledTime + ListenerCheckIntervalSeconds;
                AttachTapToListener();
            }
#if CLASSROOMCLIENT_MICROPHONE
            if (_micWanted && _micClip == null) TryStartMicrophone();
            PumpMicrophone();
#endif
            UpdateMicIndicator();
        }

        void OnDestroy()
        {
            EndStream();
            RemoveTap();
        }

        // ── Game audio (audio thread) ───────────────────────────────────────────

        internal void OnGameAudio(float[] data, int channels)
        {
            lock (_trackLock)
            {
                if (_track == null) return;
                int length = data.Length;
                if (_sendBuffer.Length != length) _sendBuffer = new float[length];
                if (_streamAppAudio) Array.Copy(data, _sendBuffer, length);
                else Array.Clear(_sendBuffer, 0, length);
#if CLASSROOMCLIENT_MICROPHONE
                MixMicrophone(_sendBuffer, channels);
#endif
                _track.SetData(_sendBuffer, channels, _outputSampleRate);
            }
        }

        private bool HasTrack
        {
            get { lock (_trackLock) { return _track != null; } }
        }

        private void AttachTapToListener()
        {
            AudioListener listener = FindActiveListener();
            if (listener == null) return;
            if (_tap != null && _tap.gameObject == listener.gameObject) return;

            RemoveTap();
            _tap = listener.GetComponent<ClassroomAudioTap>();
            if (_tap == null) _tap = listener.gameObject.AddComponent<ClassroomAudioTap>();
            _tap.capture = this;
        }

        // The listener Unity actually uses: normally on the main camera; otherwise any enabled one.
        private static AudioListener FindActiveListener()
        {
            Camera main = Camera.main;
            if (main != null)
            {
                var onCamera = main.GetComponent<AudioListener>();
                if (onCamera != null && onCamera.isActiveAndEnabled) return onCamera;
            }

            var any = FindAnyObjectByType<AudioListener>();
            if (any != null && any.isActiveAndEnabled) return any;

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var candidate in root.GetComponentsInChildren<AudioListener>())
                    {
                        if (candidate.isActiveAndEnabled) return candidate;
                    }
                }
            }
            return null;
        }

        private void RemoveTap()
        {
            if (_tap != null)
            {
                _tap.capture = null;
                Destroy(_tap);
            }
            _tap = null;
        }

        // ── Microphone ──────────────────────────────────────────────────────────

#if CLASSROOMCLIENT_MICROPHONE
        private bool MicrophoneCompiledAndWanted => _streamMicrophone;

        private bool MicrophoneRunning => _micClip != null;

        private void TryStartMicrophone()
        {
            if (!_micWanted || _micClip != null) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                if (!_micPermissionRequested)
                {
                    _micPermissionRequested = true;
                    Permission.RequestUserPermission(Permission.Microphone);
                }
                return; // retried from Update once the user has answered
            }
#endif
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[StreamAudioCapture] No microphone found — streaming game audio only.");
                _micWanted = false;
                return;
            }
            if (Microphone.IsRecording(null))
            {
                // Starting it again would restart the app's own recording (e.g. voice chat).
                Debug.LogWarning("[StreamAudioCapture] The app is already using the microphone — not streaming it to the dashboard.");
                _micWanted = false;
                return;
            }

            Microphone.GetDeviceCaps(null, out int minRate, out int maxRate);
            _micRate = _outputSampleRate;
            if (minRate != 0 || maxRate != 0) _micRate = Mathf.Clamp(_micRate, minRate, maxRate);

            _micClip = Microphone.Start(null, true, 1, _micRate);
            if (_micClip == null)
            {
                Debug.LogWarning("[StreamAudioCapture] Microphone.Start failed — streaming game audio only.");
                _micWanted = false;
                return;
            }

            _micReadPos = 0;
            _micChunk = LargestDivisorUpTo(_micClip.samples, 1024);
            _micActive = true;
            _micChunkBuffer = new float[_micChunk];
            _resampleCursor = 0f;
            lock (_ringLock)
            {
                _ring = new float[Mathf.CeilToInt(_outputSampleRate * MicRingSeconds)];
                _ringRead = 0;
                _ringCount = 0;
            }
        }

        private void StopMicrophone()
        {
            if (_micClip == null) return;
            _micActive = false;
            Microphone.End(null);
            _micClip = null;
            lock (_ringLock)
            {
                _ringCount = 0;
            }
        }

        // Main thread: move newly recorded samples into the ring (resampled to the output rate).
        private void PumpMicrophone()
        {
            if (_micClip == null) return;
            int position = Microphone.GetPosition(null);
            if (position < 0) return;

            int total = _micClip.samples;
            int available = (position - _micReadPos + total) % total;
            float step = (float)_micRate / _outputSampleRate;

            while (available >= _micChunk)
            {
                // Chunks divide the clip length exactly, so a read never crosses the loop point.
                _micClip.GetData(_micChunkBuffer, _micReadPos);
                _micReadPos = (_micReadPos + _micChunk) % total;
                available -= _micChunk;

                lock (_ringLock)
                {
                    while (_resampleCursor < _micChunk)
                    {
                        PushToRing(_micChunkBuffer[(int)_resampleCursor]);
                        _resampleCursor += step;
                    }
                    _resampleCursor -= _micChunk;

                    // Keep the delay to the dashboard bounded.
                    int maxSamples = (int)(_outputSampleRate * MicMaxLatencySeconds);
                    if (_ringCount > maxSamples)
                    {
                        int drop = _ringCount - (int)(_outputSampleRate * MicTargetLatencySeconds);
                        _ringRead = (_ringRead + drop) % _ring.Length;
                        _ringCount -= drop;
                    }
                }
            }
        }

        private void PushToRing(float sample)
        {
            int write = (_ringRead + _ringCount) % _ring.Length;
            _ring[write] = sample;
            if (_ringCount < _ring.Length) _ringCount++;
            else _ringRead = (_ringRead + 1) % _ring.Length;
        }

        // Audio thread: add the microphone (mono) to every channel of the outgoing buffer.
        private void MixMicrophone(float[] buffer, int channels)
        {
            if (!_micActive || channels <= 0) return;
            lock (_ringLock)
            {
                if (_ring.Length == 0) return;
                int frames = buffer.Length / channels;
                int count = Mathf.Min(frames, _ringCount);
                for (int frame = 0; frame < count; frame++)
                {
                    float mic = _ring[_ringRead];
                    _ringRead = (_ringRead + 1) % _ring.Length;
                    int baseIndex = frame * channels;
                    for (int c = 0; c < channels; c++)
                    {
                        buffer[baseIndex + c] = Mathf.Clamp(buffer[baseIndex + c] + mic, -1f, 1f);
                    }
                }
                _ringCount -= count;
            }
        }

        private static int LargestDivisorUpTo(int value, int limit)
        {
            for (int candidate = Mathf.Min(limit, value); candidate > 1; candidate--)
            {
                if (value % candidate == 0) return candidate;
            }
            return 1;
        }
#else
        private bool MicrophoneCompiledAndWanted => false;

        private bool MicrophoneRunning => false;
#endif

        // The HUD shows the microphone icon only while the microphone really is being streamed.
        private void UpdateMicIndicator()
        {
            bool active = HasTrack && MicrophoneRunning;
            if (active == _micIndicatorShown) return;
            _micIndicatorShown = active;
            if (_hud != null) _hud.SetMicrophoneActive(active);
        }
    }
}
