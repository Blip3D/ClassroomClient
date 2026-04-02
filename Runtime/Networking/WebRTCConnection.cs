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
        
        [Header("WebRTC Configuration")]
        [SerializeField] public string stunServer = "stun:stun.l.google.com:19302";
        [SerializeField] public int streamWidth = 1280;
        [SerializeField] public int streamHeight = 720;
        
        // WebRTC Components
        private RTCPeerConnection peerConnection;
        private VideoStreamTrack videoTrack;
        private Coroutine offerCoroutine;
        private Coroutine webRtcUpdateCoroutine;
        
        // State
        private RTCIceConnectionState currentIceState = RTCIceConnectionState.New;
        private bool isInitialized = false;
        
        // Events
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
            if (streamCamera == null)
            {
                Debug.LogError("[WebRTCConnection] Stream camera not assigned!");
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

            if (peerConnection != null)
            {
                peerConnection.Close();
                peerConnection = null;
            }

            if (videoTrack != null)
            {
                videoTrack.Stop();
                videoTrack = null;
            }

            currentIceState = RTCIceConnectionState.New;

            if (webRtcUpdateCoroutine != null)
            {
                StopCoroutine(webRtcUpdateCoroutine);
            }
            webRtcUpdateCoroutine = StartCoroutine(WebRTC.Update());

            RTCConfiguration config = new RTCConfiguration
            {
                iceServers = new RTCIceServer[]
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
            
            if (videoTrack != null)
            {
                yield break;
            }
            
            streamCamera.gameObject.SetActive(true);

            var rt = new RenderTexture(streamWidth, streamHeight, 24, RenderTextureFormat.BGRA32);
            rt.useMipMap = false;
            rt.autoGenerateMips = false;
            rt.Create();
            streamCamera.targetTexture = rt;

            yield return new WaitForSeconds(0.5f);
            
            try
            {
                videoTrack = new VideoStreamTrack(rt);
                
                if (videoTrack != null)
                {
                    peerConnection.AddTrack(videoTrack);

                    foreach (var sender in peerConnection.GetSenders())
                    {
                        if (sender.Track?.Kind == TrackKind.Video)
                        {
                            var parameters = sender.GetParameters();
                            foreach (var encoding in parameters.encodings)
                            {
                                encoding.maxBitrate = 2_000_000;
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

        public IEnumerator ReplaceVideoTrack(Camera newCamera)
        {
            if (peerConnection == null || newCamera == null)
            {
                yield break;
            }

            streamCamera = newCamera;
            streamCamera.gameObject.SetActive(true);
            
            yield return null;
            yield return new WaitForSeconds(0.3f);

            VideoStreamTrack newVideoTrack = null;
            
            try
            {
                newVideoTrack = streamCamera.CaptureStreamTrack(streamWidth, streamHeight);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebRTCConnection] Exception creating video track: {e.Message}");
                yield break;
            }
            
            if (newVideoTrack == null)
            {
                yield break;
            }

            RTCRtpSender videoSender = null;
            
            try
            {
                var senders = peerConnection.GetSenders();
                foreach (var sender in senders)
                {
                    if (sender.Track != null && sender.Track.Kind == TrackKind.Video)
                    {
                        videoSender = sender;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebRTCConnection] Exception finding video sender: {e.Message}");
            }

            if (videoSender != null)
            {
                bool replaceSuccess = false;
                
                try
                {
                    replaceSuccess = videoSender.ReplaceTrack(newVideoTrack);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebRTCConnection] Exception replacing track: {e.Message}");
                    replaceSuccess = false;
                }

                if (!replaceSuccess)
                {
                    try
                    {
                        peerConnection.RemoveTrack(videoSender);
                        peerConnection.AddTrack(newVideoTrack);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[WebRTCConnection] Fallback failed: {e.Message}");
                    }
                }
            }
            else
            {
                try
                {
                    peerConnection.AddTrack(newVideoTrack);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebRTCConnection] Exception adding track: {e.Message}");
                }
            }

            if (videoTrack != null)
            {
                try
                {
                    videoTrack.Stop();
                }
                catch { }
            }

            videoTrack = newVideoTrack;

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
                yield break;
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

            if (peerConnection != null)
            {
                peerConnection.Close();
                peerConnection = null;
            }

            if (webRtcUpdateCoroutine != null)
            {
                StopCoroutine(webRtcUpdateCoroutine);
                webRtcUpdateCoroutine = null;
            }

            if (videoTrack != null)
            {
                videoTrack.Stop();
                videoTrack = null;
            }
            
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
