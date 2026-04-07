using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using ClassroomClient.Core;

namespace ClassroomClient.HUD
{
    public class StatusIndicator : MonoBehaviour
    {
        private Image dot;

        void Awake()
        {
            dot = gameObject.AddComponent<Image>();
            dot.sprite = CreateCircleSprite(64);
            dot.material = CreateOverlayMaterial();

            var rt = GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(18f, 18f);
            dot.color = new Color(0.91f, 0.25f, 0.25f, 1f);
        }

        public void UpdateState(ConnectionState state)
        {
            if (dot == null) return;
            dot.color = state switch
            {
                ConnectionState.InSession => new Color(0.20f, 0.78f, 0.35f, 1f),
                ConnectionState.InLobby => new Color(1f, 0.80f, 0f, 1f),
                ConnectionState.Connecting
                or ConnectionState.Reconnecting
                or ConnectionState.PendingApproval => new Color(1f, 0.80f, 0f, 1f),
                _ => new Color(0.91f, 0.25f, 0.25f, 1f)
            };
        }

        private Sprite CreateCircleSprite(int resolution)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            float center = resolution / 2f;
            float radius = resolution / 2f;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((radius - dist) / 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution
            );
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
    }
}
