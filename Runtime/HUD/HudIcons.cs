using UnityEngine;
using UnityEngine.Rendering;

namespace ClassroomClient.HUD
{
    /// <summary>
    /// Procedurally drawn HUD icons (no texture assets to import or lose). White shapes on a
    /// transparent background — tint them with Image.color.
    /// </summary>
    internal static class HudIcons
    {
        private const int Size = 64;

        /// <summary>"Cast" icon: a screen outline with broadcast waves in its lower-left corner.</summary>
        public static Sprite CreateCastSprite()
        {
            return Draw((x, y) =>
            {
                // Screen outline, open in the lower-left corner where the waves sit.
                float screen = RectOutline(x, y, 10f, 14f, 58f, 52f, 4f);
                if (x < 28f && y < 32f) screen = 0f;

                // Three quarter-circle waves around the lower-left corner + a dot.
                float d = Mathf.Sqrt((x - 8f) * (x - 8f) + (y - 10f) * (y - 10f));
                bool inQuadrant = x >= 8f && y >= 10f;
                float waves = 0f;
                if (inQuadrant)
                {
                    waves = Mathf.Max(Ring(d, 12f, 2.5f), Ring(d, 21f, 2.5f));
                }
                float dot = Mathf.Clamp01(5f - d);
                return Mathf.Max(screen, Mathf.Max(waves, dot));
            });
        }

        /// <summary>Microphone icon: capsule, holder arc and stand.</summary>
        public static Sprite CreateMicrophoneSprite()
        {
            return Draw((x, y) =>
            {
                // Capsule (rounded rectangle) centred at x=32, from y=30 to y=56, radius 9.5.
                float cy = Mathf.Clamp(y, 39.5f, 46.5f);
                float capsuleDist = Mathf.Sqrt((x - 32f) * (x - 32f) + (y - cy) * (y - cy));
                float capsule = Mathf.Clamp01(9.5f - capsuleDist);

                // Holder: lower half of a ring around the capsule.
                float rd = Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 36f) * (y - 36f));
                float holder = y <= 36f ? Ring(rd, 16f, 2.5f) : 0f;

                // Stand + base.
                float stand = (Mathf.Abs(x - 32f) <= 2.5f && y >= 12f && y <= 20f) ? 1f : 0f;
                float baseBar = (Mathf.Abs(x - 32f) <= 11f && y >= 9f && y <= 13f) ? 1f : 0f;
                return Mathf.Max(Mathf.Max(capsule, holder), Mathf.Max(stand, baseBar));
            });
        }

        /// <summary>UI material that always draws on top (ZTest Always), like the rest of the HUD.</summary>
        public static Material CreateOverlayMaterial()
        {
            var mat = new Material(Shader.Find("UI/Default"));
            if (mat.HasProperty("_ZTestMode"))
                mat.SetInt("_ZTestMode", (int)CompareFunction.Always);
            else
                mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            return mat;
        }

        private static float Ring(float distance, float radius, float halfWidth)
        {
            return Mathf.Clamp01(halfWidth + 0.5f - Mathf.Abs(distance - radius));
        }

        private static float RectOutline(float x, float y, float x0, float y0, float x1, float y1, float width)
        {
            bool inside = x >= x0 && x <= x1 && y >= y0 && y <= y1;
            if (!inside) return 0f;
            float edge = Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(y - y0, y1 - y));
            return Mathf.Clamp01(width + 0.5f - edge);
        }

        private static Sprite Draw(System.Func<float, float, float> alphaAt)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float alpha = Mathf.Clamp01(alphaAt(x + 0.5f, y + 0.5f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
