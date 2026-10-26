using UnityEngine;
using Unity.WebRTC;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ClassroomClient.Networking
{
    public class WebRTCConnection : MonoBehaviour
    {
        [Header("Camera Setup")]
        [SerializeField] public Camera streamCamera;

        [Header("Stream Quality")]
        [SerializeField] private StreamResolution streamResolution = StreamResolution.Resolution720p;

        public enum StreamResolution
        {
            [InspectorName("360p")] Resolution360p,
            [InspectorName("720p")] Resolution720p,
            [InspectorName("1080p")] Resolution1080p,
        }

        private (int width, int height, ulong bitrate) GetStreamConfig()
        {
            return streamResolution switch
            {
                StreamResolution.Resolution360p => (640, 360, 500_000UL),
                StreamResolution.Resolution1080p => (1920, 1080, 4_000_000UL),
                _ => (1280, 720, 2_000_000UL) // 720p default
            };
        }

        private RTCPeerConnection peerConnection;
        private VideoStreamTrack videoTrack;
        // The RenderTexture the video track encodes. It follows whichever dedicated camera is
        // currently streamed (see SwitchCamera), so a camera change never replaces the track.
        private RenderTexture streamTexture;
        private bool trackCreationInProgress;
        // Audio track + the MediaStream that groups it with the video (one tile, picture and sound).
        private MediaStream sendStream;
        private const ulong AudioMaxBitrate = 64_000;

        // STUN/TURN servers advertised by the server (/api/ice-config); null = default Google STUN.
        private RTCIceServer[] configuredIceServers;

        public void SetIceServers(RTCIceServer[] servers)
        {
            configuredIceServers = servers;
        }

        /// <summary>Provides the streamed audio track (game sound + optional microphone). Set by ClassroomClientManager.</summary>
        [NonSerialized] public ClassroomClient.Audio.StreamAudioCapture audioCapture;
        private Coroutine offerCoroutine;
        private Coroutine webRtcUpdateCoroutine;
        
        private RTCIceConnectionState currentIceState = RTCIceConnectionState.New;
        private bool isInitialized = false;
        
        public System.Action<RTCIceConnectionState> OnConnectionStateChanged;
        public System.Action<string> OnOfferCreated;
        public System.Action<string> OnAnswerReceived;
        public System.Action<string, string, int> OnIceCandidateReceived;
        public System.Action OnDisposeRequested;

        void Start()
        {
            InitializeWebRTC();
        }
        
        public void InitializeWebRTC()
        {
            if (streamCamera == null || streamCamera.GetComponent<ClassroomClient.Core.ClassroomStreamCameraMarker>() == null)
            {
                // Only a dedicated, ClassroomClient-owned (marked) camera may be initialized/captured.
                // Protects against an unmarked/eye camera serialized directly into this field, and
                // against API Controlled mode before SetStreamCamera. ReinitializeIfNeeded() will
                // initialize once a marked camera is set.
                Debug.Log("[WebRTCConnection] No dedicated (marked) stream camera yet — deferring WebRTC init.");
                return;
            }

            isInitialized = true;
        }
        
        /// <summary>
        /// Re-initialize WebRTC if camera becomes available after initial Start()
        /// </summary>
        public void ReinitializeIfNeeded()
        {
            if (!isInitialized && streamCamera != null)
            {
                Debug.Log("[WebRTCConnection] Re-initializing WebRTC now that camera is available");
                InitializeWebRTC();
            }
        }
        
        public void CreatePeerConnection()
        {
            if (!isInitialized)
            {
                Debug.LogError("[WebRTCConnection] WebRTC not initialized!");
                return;
            }

            if (offerCoroutine != null)
            {
                StopCoroutine(offerCoroutine);
                offerCoroutine = null;
            }

            ClosePeerAndTrack();

            currentIceState = RTCIceConnectionState.New;

            if (webRtcUpdateCoroutine != null)
            {
                StopCoroutine(webRtcUpdateCoroutine);
            }
            webRtcUpdateCoroutine = StartCoroutine(WebRTC.Update());

            RTCConfiguration config = new RTCConfiguration
            {
                iceServers = configuredIceServers != null && configuredIceServers.Length > 0
                    ? configuredIceServers
                    : new RTCIceServer[]
                    {
                        new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } }
                    }
            };
            
            peerConnection = new RTCPeerConnection(ref config);
            
            peerConnection.OnIceCandidate = OnIceCandidate;
            peerConnection.OnIceConnectionChange = OnIceConnectionChange;
            peerConnection.OnTrack = OnTrack;
        }
        
        public IEnumerator CreateVideoTrack()
        {
            if (peerConnection == null)
            {
                Debug.LogError("[WebRTCConnection] Peer connection not created!");
                yield break;
            }
            
            if (streamCamera == null)
            {
                Debug.LogError("[WebRTCConnection] Camera not available!");
                yield break;
            }
            
            if (videoTrack != null || trackCreationInProgress)
            {
                yield break;
            }

            trackCreationInProgress = true;
            RTCPeerConnection connection = peerConnection;
            streamCamera.gameObject.SetActive(true);

            var (width, height, bitrate) = GetStreamConfig();
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.BGRA32);
            rt.useMipMap = false;
            rt.autoGenerateMips = false;
            rt.Create();

            // Release the dedicated camera's "parking" RenderTexture (or a previous stream texture)
            // before swapping in the stream-sized one, so no RenderTexture is orphaned.
            RenderTexture oldTarget = streamCamera.targetTexture;
            streamCamera.targetTexture = null;
            ReleaseTexture(oldTarget);
            if (streamTexture != oldTarget)
            {
                ReleaseTexture(streamTexture);
            }
            streamCamera.targetTexture = rt;
            streamTexture = rt;
            streamCamera.enabled = true;

            yield return new WaitForSeconds(0.5f);
            trackCreationInProgress = false;

            // The stream was stopped or restarted during the wait — this connection is gone.
            if (peerConnection == null || peerConnection != connection)
            {
                yield break;
            }

            try
            {
                videoTrack = new VideoStreamTrack(rt);
                
                if (videoTrack != null)
                {
                    sendStream = new MediaStream();
                    peerConnection.AddTrack(videoTrack, sendStream);
                    AddAudioTrack();

                    foreach (var sender in peerConnection.GetSenders())
                    {
                        if (sender.Track?.Kind == TrackKind.Video)
                        {
                            var parameters = sender.GetParameters();
                            foreach (var encoding in parameters.encodings)
                            {
                                encoding.maxBitrate = (ulong)bitrate;
                                encoding.maxFramerate = 30;
                            }
                            sender.SetParameters(parameters);
                            break;
                        }
                    }

                    foreach (var transceiver in peerConnection.GetTransceivers())
                    {
                        if (transceiver.Sender?.Track?.Kind == TrackKind.Video)
                        {
                            var codecs = RTCRtpSender.GetCapabilities(TrackKind.Video).codecs;
                            var selectedCodecs = SelectOptimalCodecsForDevice(codecs);
                            if (selectedCodecs.Count > 0)
                                transceiver.SetCodecPreferences(selectedCodecs.ToArray());
                            break;
                        }
                    }
                    
                    CreateOffer();
                }
                else
                {
                    Debug.LogError("[WebRTCConnection] Failed to create video track!");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebRTCConnection] Exception creating video track: {e.Message}");
            }
        }

        /// <summary>
        /// Moves the stream to another dedicated camera WITHOUT replacing the WebRTC video track:
        /// the stream RenderTexture (what the track encodes) is handed to the new camera, so no
        /// renegotiation is needed and the teacher's tile keeps playing. Safe in every state
        /// (idle, track being created, streaming, ICE temporarily disconnected).
        /// </summary>
        public void SwitchCamera(Camera newCamera)
        {
            if (newCamera == null || newCamera == streamCamera)
            {
                return;
            }

            Camera oldCamera = streamCamera;
            streamCamera = newCamera;

            // Only ever touch a camera ClassroomClient created itself — a legacy setup may have the
            // app's eye camera in this field, and disabling that would black out the headset.
            bool oldIsDedicated = oldCamera != null &&
                oldCamera.GetComponent<ClassroomClient.Core.ClassroomStreamCameraMarker>() != null;

            if (streamTexture != null)
            {
                RenderTexture parking = newCamera.targetTexture;
                newCamera.targetTexture = streamTexture;
                if (oldIsDedicated && oldCamera.targetTexture == streamTexture)
                {
                    // The old camera keeps an off-display target in case it is streamed again later.
                    oldCamera.targetTexture = parking;
                }
                else if (parking != streamTexture)
                {
                    ReleaseTexture(parking);
                }
            }

            if (oldIsDedicated)
            {
                oldCamera.enabled = false;
            }

            if (peerConnection == null)
            {
                return;
            }

            if (streamTexture != null)
            {
                // Only render once the camera owns the stream texture (never onto the HMD display).
                newCamera.gameObject.SetActive(true);
                newCamera.enabled = true;
            }

            // Streaming was requested but the track could not be built yet (e.g. the camera died
            // during startup) — build it now that a camera exists.
            if (videoTrack == null && !trackCreationInProgress)
            {
                StartCoroutine(CreateVideoTrack());
            }
        }

        void OnDestroy()
        {
            ReleaseTexture(streamTexture);
            streamTexture = null;
        }

        private void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }
            texture.Release();
            Destroy(texture);
        }

        // Audio must be added before the offer is created so it is part of the same negotiation.
        // A failure here only costs the audio — the video offer must still go out.
        private void AddAudioTrack()
        {
            try
            {
                AudioStreamTrack audioTrack = audioCapture != null ? audioCapture.BeginStream() : null;
                if (audioTrack == null) return;

                RTCRtpSender audioSender = peerConnection.AddTrack(audioTrack, sendStream);
                if (audioSender == null) return;
                var parameters = audioSender.GetParameters();
                foreach (var encoding in parameters.encodings)
                {
                    encoding.maxBitrate = AudioMaxBitrate;
                }
                audioSender.SetParameters(parameters);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebRTCConnection] Audio track setup failed — streaming video only: {e.Message}");
                audioCapture?.EndStream();
            }
        }

        private void ClosePeerAndTrack()
        {
            trackCreationInProgress = false;
            audioCapture?.EndStream();

            if (peerConnection != null)
            {
                peerConnection.Close();
                peerConnection.Dispose();
                peerConnection = null;
            }

            if (videoTrack != null)
            {
                videoTrack.Stop();
                videoTrack.Dispose();
                videoTrack = null;
            }

            if (sendStream != null)
            {
                sendStream.Dispose();
                sendStream = null;
            }
        }

        public void SetStreamCamera(Camera camera)
        {
            streamCamera = camera;
        }

        public Camera GetStreamCamera()
        {
            return streamCamera;
        }
        
        public void CreateOffer()
        {
            if (peerConnection == null)
            {
                Debug.LogError("[WebRTCConnection] Peer connection not created!");
                return;
            }

            if (offerCoroutine != null)
            {
                StopCoroutine(offerCoroutine);
                offerCoroutine = null;
            }

            offerCoroutine = StartCoroutine(CreateOfferCoroutine());
        }

        private IEnumerator CreateOfferCoroutine()
        {
            try
            {
                var offerOperation = peerConnection.CreateOffer();
                yield return offerOperation;

                if (offerOperation.IsError)
                {
                    Debug.LogError($"[WebRTCConnection] Failed to create offer: {offerOperation.Error.message}");
                    yield break;
                }

                RTCSessionDescription offer = offerOperation.Desc;

                var setLocalOperation = peerConnection.SetLocalDescription(ref offer);
                yield return setLocalOperation;

                if (setLocalOperation.IsError)
                {
                    Debug.LogError($"[WebRTCConnection] Failed to set local description: {setLocalOperation.Error.message}");
                    yield break;
                }

                Debug.Log($"[WebRTCConnection] Offer SDP:\n{offer.sdp}");
                OnOfferCreated?.Invoke(offer.sdp);
            }
            finally
            {
                offerCoroutine = null;
            }
        }
        
        public void HandleAnswer(string sdp)
        {
            if (peerConnection == null)
            {
                Debug.LogError("[WebRTCConnection] Peer connection not created!");
                return;
            }
            
            StartCoroutine(HandleAnswerCoroutine(sdp));
        }
        
        private IEnumerator HandleAnswerCoroutine(string sdp)
        {
            if (peerConnection.SignalingState != RTCSignalingState.HaveLocalOffer)
            {
                yield return new WaitForSeconds(0.3f);
                if (peerConnection.SignalingState != RTCSignalingState.HaveLocalOffer)
                {
                    Debug.LogWarning($"[WebRTCConnection] HandleAnswerCoroutine — unexpected signaling state: {peerConnection.SignalingState}, discarding answer");
                    yield break;
                }
            }
            
            RTCSessionDescription answer = new RTCSessionDescription
            {
                type = RTCSdpType.Answer,
                sdp = sdp
            };
            
            var setRemoteOperation = peerConnection.SetRemoteDescription(ref answer);
            yield return setRemoteOperation;
            
            if (setRemoteOperation.IsError)
            {
                Debug.LogError($"[WebRTCConnection] Failed to set remote description: {setRemoteOperation.Error.message}");
            }
        }
        
        public void HandleIceCandidate(string candidate, string sdpMid, int sdpMLineIndex)
        {
            if (peerConnection == null)
            {
                return;
            }
            
            try
            {
                RTCIceCandidateInit init = new RTCIceCandidateInit
                {
                    candidate = candidate,
                    sdpMid = sdpMid,
                    sdpMLineIndex = sdpMLineIndex
                };
                
                RTCIceCandidate iceCandidate = new RTCIceCandidate(init);
                peerConnection.AddIceCandidate(iceCandidate);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebRTCConnection] Failed to add ICE candidate: {e.Message}");
            }
        }
        
        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            OnIceCandidateReceived?.Invoke(candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex ?? 0);
        }
        
        private void OnIceConnectionChange(RTCIceConnectionState state)
        {
            currentIceState = state;
            OnConnectionStateChanged?.Invoke(state);
        }
        
        private void OnTrack(RTCTrackEvent e) { }
        
        public void Disconnect()
        {
            OnDisposeRequested?.Invoke();

            if (offerCoroutine != null)
            {
                StopCoroutine(offerCoroutine);
                offerCoroutine = null;
            }

            if (webRtcUpdateCoroutine != null)
            {
                StopCoroutine(webRtcUpdateCoroutine);
                webRtcUpdateCoroutine = null;
            }

            ClosePeerAndTrack();
        }
        
        public bool IsInitialized => isInitialized;
        public bool IsConnected => currentIceState == RTCIceConnectionState.Connected;
        public string GetConnectionStatus() => currentIceState.ToString();

        private List<RTCRtpCodecCapability> SelectOptimalCodecsForDevice(RTCRtpCodecCapability[] availableCodecs)
        {
            var selectedCodecs = new List<RTCRtpCodecCapability>();
            var deviceType = DetectDeviceType();
            var codecPriority = GetCodecPriorityForDevice(deviceType);

            foreach (var priority in codecPriority)
            {
                var matchingCodecs = availableCodecs.Where(codec => codec.mimeType.ToUpper().Contains(priority)).ToList();
                if (matchingCodecs.Any())
                {
                    selectedCodecs.Add(matchingCodecs.First());
                }
            }

            return selectedCodecs;
        }

        private string DetectDeviceType()
        {
            if (SystemInfo.deviceModel.ToLower().Contains("quest") || 
                SystemInfo.deviceModel.ToLower().Contains("meta"))
            {
                return "Quest";
            }
            else if (SystemInfo.deviceType == DeviceType.Desktop)
            {
                return "PC";
            }
            else if (SystemInfo.deviceType == DeviceType.Handheld)
            {
                return "Mobile";
            }
            return "Unknown";
        }

        private List<string> GetCodecPriorityForDevice(string deviceType)
        {
            switch (deviceType)
            {
                case "Quest":
                    return new List<string> { "H264", "VP8", "VP9" };
                case "PC":
                    return new List<string> { "H264", "VP8", "VP9" };
                case "Mobile":
                    return new List<string> { "VP8", "H264", "VP9" };
                default:
                    return new List<string> { "VP8", "H264", "VP9" };
            }
        }
    }
}
