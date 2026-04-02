using UnityEngine;
using ClassroomClient.Core;

namespace ClassroomClient.HUD
{
    public class ClassroomHUD : MonoBehaviour
    {
        private StatusIndicator statusIndicator;
        private NotificationPanel notificationPanel;

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var rt = GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1.2f, 0.7f);
            rt.localScale = Vector3.one * 0.001f;

            var siGo = new GameObject("StatusIndicator");
            siGo.transform.SetParent(transform, false);
            statusIndicator = siGo.AddComponent<StatusIndicator>();
            var siRect = siGo.GetComponent<RectTransform>();
            siRect.anchorMin = new Vector2(0f, 1f);
            siRect.anchorMax = new Vector2(0f, 1f);
            siRect.pivot = new Vector2(0f, 1f);
            siRect.anchoredPosition = new Vector2(20f, -20f);

            var npGo = new GameObject("NotificationPanel");
            npGo.transform.SetParent(transform, false);
            notificationPanel = npGo.AddComponent<NotificationPanel>();
            var npRect = npGo.GetComponent<RectTransform>();
            npRect.anchorMin = new Vector2(0.5f, 1f);
            npRect.anchorMax = new Vector2(0.5f, 1f);
            npRect.pivot = new Vector2(0.5f, 1f);
            npRect.sizeDelta = new Vector2(600f, 120f);
            npRect.anchoredPosition = new Vector2(0f, -20f);
        }

        void LateUpdate()
        {
            if (Camera.main == null) return;
            var target = Camera.main.transform.position + Camera.main.transform.forward * 2.5f;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 5f);
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0, 180f, 0);
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
