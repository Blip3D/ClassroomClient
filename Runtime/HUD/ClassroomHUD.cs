using UnityEngine;
using UnityEngine.UI;
using ClassroomClient.Core;

namespace ClassroomClient.HUD
{
    public class ClassroomHUD : MonoBehaviour
    {
        [Tooltip("How far in front of the user's eyes the HUD floats, in metres.")]
        [SerializeField, Range(0.3f, 1.5f)] private float hudDistance = 0.6f;

        [Tooltip("Height of the message panel's centre relative to eye level, in metres. Negative = below eye level (default -0.10 m, about 10 degrees down at 0.6 m).")]
        [SerializeField, Range(-0.4f, 0.3f)] private float hudVerticalOffset = -0.10f;

        [Tooltip("Height of the status icons (cast / microphone) relative to eye level, in metres. Positive = above eye level, so they stay out of the way of the VR scene (default +0.22 m, about 20 degrees up at 0.6 m).")]
        [SerializeField, Range(-0.2f, 0.4f)] private float hudIconHeight = 0.22f;

        [Tooltip("Sideways position of the status icons, in metres. Positive = to the right (default +0.25 m, about 23 degrees right at 0.6 m) — together with the height this puts them in the upper-right corner of the view.")]
        [SerializeField, Range(-0.4f, 0.4f)] private float hudIconSideOffset = 0.25f;

        // The canvas is scaled 0.001 → 1 canvas unit = 1 mm; the panel is centred on the HUD pose.
        private const float CanvasUnitsPerMetre = 1000f;
        private const float PanelHeight = 110f;

        private StatusIndicator statusIndicator;
        private NotificationPanel notificationPanel;
        private Camera trackedCamera;
        // The pending notice is shown once per wait (retries pass through Connecting in between).
        private bool pendingNoticeShown;

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999;

            var rt = GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000f, 600f);
            rt.localScale = Vector3.one * 0.001f;

            // Layout is centred on the canvas so hudVerticalOffset is the height of the message
            // panel's centre (anchoring to the canvas top edge used to put it ~0.3 m too high).

            // Status icons — upper-right corner of the view, independent of the message panel
            var siGo = new GameObject("StatusIndicator", typeof(RectTransform));
            siGo.transform.SetParent(transform, false);
            statusIndicator = siGo.AddComponent<StatusIndicator>();
            var siRect = siGo.GetComponent<RectTransform>();
            siRect.anchorMin = new Vector2(0.5f, 0.5f);
            siRect.anchorMax = new Vector2(0.5f, 0.5f);
            siRect.pivot = new Vector2(0.5f, 0.5f);
            siRect.anchoredPosition = new Vector2(
                hudIconSideOffset * CanvasUnitsPerMetre,
                (hudIconHeight - hudVerticalOffset) * CanvasUnitsPerMetre);

            // Notification panel — centred on the HUD pose
            var npGo = new GameObject("NotificationPanel");
            npGo.transform.SetParent(transform, false);
            notificationPanel = npGo.AddComponent<NotificationPanel>();
            var npRect = npGo.GetComponent<RectTransform>();
            npRect.anchorMin = new Vector2(0.5f, 0.5f);
            npRect.anchorMax = new Vector2(0.5f, 0.5f);
            npRect.pivot = new Vector2(0.5f, 0.5f);
            npRect.sizeDelta = new Vector2(560f, PanelHeight);
            npRect.anchoredPosition = Vector2.zero;
        }

        void Start()
        {
            AcquireCamera();
            Application.onBeforeRender += OnBeforeRender;
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        void LateUpdate()
        {
            if (trackedCamera == null)
                AcquireCamera();
            UpdateHUDPose();
        }

        void OnBeforeRender()
        {
            if (trackedCamera == null)
                AcquireCamera();
            UpdateHUDPose();
        }

        private void UpdateHUDPose()
        {
            if (trackedCamera == null) return;
            Transform cam = trackedCamera.transform;
            transform.position = cam.position
                + cam.forward * hudDistance
                + cam.up * hudVerticalOffset;
            transform.rotation = cam.rotation;
        }

        private void AcquireCamera()
        {
            if (Camera.main != null)
                trackedCamera = Camera.main;
        }

        public void UpdateStatus(ConnectionState state)
        {
            statusIndicator?.UpdateState(state);
            if (state == ConnectionState.PendingApproval && !pendingNoticeShown)
            {
                pendingNoticeShown = true;
                notificationPanel?.Show("Waiting for an admin to approve this headset in the ClassroomController dashboard.", null, "Classroom");
            }
            else if (state == ConnectionState.InLobby || state == ConnectionState.InSession)
            {
                pendingNoticeShown = false;
            }
        }

        /// <summary>Shows the microphone icon while the headset microphone is streamed to the dashboard.</summary>
        public void SetMicrophoneActive(bool active)
        {
            statusIndicator?.SetMicrophoneActive(active);
        }

        public void ShowNotification(string text, string color, string category)
        {
            notificationPanel?.Show(text, color, category);
        }
    }
}
