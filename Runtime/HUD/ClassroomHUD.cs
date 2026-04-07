using UnityEngine;
using UnityEngine.UI;
using ClassroomClient.Core;

namespace ClassroomClient.HUD
{
    public class ClassroomHUD : MonoBehaviour
    {
        private StatusIndicator statusIndicator;
        private NotificationPanel notificationPanel;
        private Camera trackedCamera;

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999;

            var rt = GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000f, 600f);
            rt.localScale = Vector3.one * 0.001f;

            // Status pill — top center
            var siGo = new GameObject("StatusIndicator");
            siGo.transform.SetParent(transform, false);
            statusIndicator = siGo.AddComponent<StatusIndicator>();
            var siRect = siGo.GetComponent<RectTransform>();
            siRect.anchorMin = new Vector2(0.5f, 1f);
            siRect.anchorMax = new Vector2(0.5f, 1f);
            siRect.pivot = new Vector2(0.5f, 1f);
            siRect.anchoredPosition = new Vector2(0f, -16f);

            // Notification panel — top center below pill
            var npGo = new GameObject("NotificationPanel");
            npGo.transform.SetParent(transform, false);
            notificationPanel = npGo.AddComponent<NotificationPanel>();
            var npRect = npGo.GetComponent<RectTransform>();
            npRect.anchorMin = new Vector2(0.5f, 1f);
            npRect.anchorMax = new Vector2(0.5f, 1f);
            npRect.pivot = new Vector2(0.5f, 1f);
            npRect.sizeDelta = new Vector2(560f, 110f);
            npRect.anchoredPosition = new Vector2(0f, -42f);
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
                + cam.forward * 0.6f
                + cam.up * 0.15f;
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
        }

        public void ShowNotification(string text, string color, string category)
        {
            notificationPanel?.Show(text, color, category);
        }
    }
}
