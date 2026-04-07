using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;

namespace ClassroomClient.HUD
{
    public class NotificationPanel : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI categoryLabel;
        private TextMeshProUGUI messageLabel;
        private Image background;
        private Coroutine hideCoroutine;

        void Awake()
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            background = gameObject.AddComponent<Image>();
            background.color = new Color(0.11f, 0.11f, 0.11f, 0.88f);
            background.material = CreateOverlayMaterial();

            // Category label
            var catGo = new GameObject("CategoryLabel");
            catGo.transform.SetParent(transform, false);
            categoryLabel = catGo.AddComponent<TextMeshProUGUI>();
            categoryLabel.fontSize = 14f;
            categoryLabel.fontStyle = FontStyles.Bold;
            categoryLabel.color = new Color(1f, 1f, 1f, 0.55f);
            categoryLabel.alignment = TextAlignmentOptions.Left;
            var catRect = catGo.GetComponent<RectTransform>();
            catRect.anchorMin = new Vector2(0f, 1f);
            catRect.anchorMax = new Vector2(1f, 1f);
            catRect.pivot = new Vector2(0.5f, 1f);
            catRect.sizeDelta = new Vector2(0f, 24f);
            catRect.anchoredPosition = new Vector2(0f, -12f);
            catRect.offsetMin = new Vector2(20f, catRect.offsetMin.y);
            catRect.offsetMax = new Vector2(-20f, catRect.offsetMax.y);
            ApplyTMPOverlayMaterial(categoryLabel);

            // Message label
            var msgGo = new GameObject("MessageLabel");
            msgGo.transform.SetParent(transform, false);
            messageLabel = msgGo.AddComponent<TextMeshProUGUI>();
            messageLabel.fontSize = 22f;
            messageLabel.color = Color.white;
            messageLabel.alignment = TextAlignmentOptions.Left;
            messageLabel.textWrappingMode = TextWrappingModes.Normal;
            var msgRect = msgGo.GetComponent<RectTransform>();
            msgRect.anchorMin = Vector2.zero;
            msgRect.anchorMax = Vector2.one;
            msgRect.offsetMin = new Vector2(20f, 14f);
            msgRect.offsetMax = new Vector2(-20f, -38f);
            ApplyTMPOverlayMaterial(messageLabel);
        }

        public void Show(string text, string color, string category)
        {
            categoryLabel.text = category?.ToUpper() ?? "";
            messageLabel.text = text ?? "";
            background.color = ParseColor(color);

            if (hideCoroutine != null) StopCoroutine(hideCoroutine);
            StopAllCoroutines();
            hideCoroutine = StartCoroutine(ShowAndFade(6f));
        }

        private IEnumerator ShowAndFade(float displayDuration)
        {
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(t / 0.2f);
                yield return null;
            }
            canvasGroup.alpha = 1f;

            yield return new WaitForSeconds(displayDuration);

            t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(t / 0.8f);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        private Material CreateOverlayMaterial()
        {
            var mat = new Material(Shader.Find("UI/Default"));
            if (mat.HasProperty("_ZTestMode"))
                mat.SetInt("_ZTestMode", (int)CompareFunction.Always);
            else
                mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            return mat;
        }

        private void ApplyTMPOverlayMaterial(TextMeshProUGUI label)
        {
            var mat = new Material(label.fontMaterial);
            if (mat.HasProperty("_ZTestMode"))
                mat.SetInt("_ZTestMode", (int)CompareFunction.Always);
            else
                mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            label.fontMaterial = mat;
        }

        private Color ParseColor(string color)
        {
            return color?.ToLower() switch
            {
                "red"   => new Color(0.55f, 0.09f, 0.09f, 0.92f),
                "green" => new Color(0.07f, 0.30f, 0.12f, 0.92f),
                _       => new Color(0.11f, 0.11f, 0.11f, 0.88f)
            };
        }
    }
}
