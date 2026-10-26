using UnityEngine;
using UnityEngine.UI;
using ClassroomClient.Core;

namespace ClassroomClient.HUD
{
    /// <summary>
    /// Cast icon coloured by connection state (green = in session / being streamed, yellow = lobby or
    /// connecting, blue = waiting for admin approval, red = disconnected) plus a microphone icon that
    /// is visible only while the headset microphone is being streamed to the dashboard.
    /// </summary>
    public class StatusIndicator : MonoBehaviour
    {
        private static readonly Color InSessionColor = new Color(0.20f, 0.78f, 0.35f, 1f);
        private static readonly Color WaitingColor = new Color(1f, 0.80f, 0f, 1f);
        private static readonly Color PendingApprovalColor = new Color(0.25f, 0.60f, 1f, 1f);
        private static readonly Color DisconnectedColor = new Color(0.91f, 0.25f, 0.25f, 1f);
        private static readonly Color MicrophoneColor = new Color(0.91f, 0.25f, 0.25f, 1f);

        private const float IconSize = 26f;
        private const float IconGap = 8f;

        private Image castIcon;
        private Image micIcon;

        void Awake()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(IconSize * 2f + IconGap, IconSize);

            Material overlay = HudIcons.CreateOverlayMaterial();
            castIcon = CreateIcon("CastIcon", HudIcons.CreateCastSprite(), overlay, 0f);
            castIcon.color = DisconnectedColor;

            micIcon = CreateIcon("MicrophoneIcon", HudIcons.CreateMicrophoneSprite(), overlay, IconSize + IconGap);
            micIcon.color = MicrophoneColor;
            micIcon.gameObject.SetActive(false);

            // Centre the cast icon while the mic icon is hidden.
            LayoutIcons();
        }

        public void UpdateState(ConnectionState state)
        {
            if (castIcon == null) return;
            castIcon.color = state switch
            {
                ConnectionState.InSession => InSessionColor,
                ConnectionState.PendingApproval => PendingApprovalColor,
                ConnectionState.InLobby
                or ConnectionState.Connecting
                or ConnectionState.Reconnecting => WaitingColor,
                _ => DisconnectedColor
            };
        }

        public void SetMicrophoneActive(bool active)
        {
            if (micIcon == null) return;
            micIcon.gameObject.SetActive(active);
            LayoutIcons();
        }

        private void LayoutIcons()
        {
            bool micVisible = micIcon != null && micIcon.gameObject.activeSelf;
            float castX = micVisible ? -(IconSize + IconGap) / 2f : 0f;
            castIcon.rectTransform.anchoredPosition = new Vector2(castX, 0f);
            if (micIcon != null)
                micIcon.rectTransform.anchoredPosition = new Vector2(castX + IconSize + IconGap, 0f);
        }

        private Image CreateIcon(string name, Sprite sprite, Material material, float x)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.material = material;
            image.preserveAspect = true;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(IconSize, IconSize);
            rect.anchoredPosition = new Vector2(x, 0f);
            return image;
        }
    }
}
