using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClassroomClient.HUD
{
    public class NotificationPanel : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI label;
        private Image background;
        private Coroutine hideCoroutine;

        void Awake()
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;

            background = gameObject.AddComponent<Image>();
            background.color = new Color(0.13f, 0.59f, 0.95f, 0.9f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(transform, false);
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 24;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 10f);
            textRect.offsetMax = new Vector2(-10f, -10f);
        }

        public void Show(string text, string color, string category)
        {
            label.text = $"[{category}]\n{text}";
            background.color = ParseColor(color);
            if (hideCoroutine != null) StopCoroutine(hideCoroutine);
            canvasGroup.alpha = 1f;
            hideCoroutine = StartCoroutine(FadeOut(6f));
        }

        private IEnumerator FadeOut(float delay)
        {
            yield return new WaitForSeconds(delay);
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = 1f - t;
                yield return null;
            }
            canvasGroup.alpha = 0;
        }

        private Color ParseColor(string color)
        {
            return color?.ToLower() switch
            {
                "yellow" => new Color(0.96f, 0.77f, 0.09f, 0.9f),
                "red" => new Color(0.91f, 0.25f, 0.25f, 0.9f),
                "green" => new Color(0.30f, 0.69f, 0.31f, 0.9f),
                _ => new Color(0.13f, 0.59f, 0.95f, 0.9f)
            };
        }
    }
}
