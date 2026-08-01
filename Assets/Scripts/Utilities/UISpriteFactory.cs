using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    /// <summary>
    /// Generates and caches procedural UI sprites at runtime so the game can have
    /// glossy, rounded, premium blocks/cells/frames without shipping any art assets.
    /// All sprites are pure white (tint them via Image.color) and use 9-slice borders
    /// so corners stay crisp at any size. Textures are generated once and reused.
    /// </summary>
    public static class UISpriteFactory
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // ---- Public helpers that also set the correct Image.type ----

        public static void ApplyRounded(Image image, float radius01 = 0.28f)
        {
            if (image == null) return;
            image.sprite = RoundedRect(radius01);
            image.type = Image.Type.Sliced;
        }

        public static void ApplySoftCircle(Image image)
        {
            if (image == null) return;
            image.sprite = SoftCircle();
            image.type = Image.Type.Simple;
        }

        public static void ApplyFrame(Image image, float radius01 = 0.22f, float thickness01 = 0.12f)
        {
            if (image == null) return;
            image.sprite = RoundedFrame(radius01, thickness01);
            image.type = Image.Type.Sliced;
        }

        public static void ApplySoftShadow(Image image, float radius01 = 0.30f)
        {
            if (image == null) return;
            image.sprite = SoftRoundedRect(radius01);
            image.type = Image.Type.Sliced;
        }

        // ---- Sprite generators (cached) ----

        public static Sprite RoundedRect(float radius01 = 0.28f, int size = 64)
        {
            string key = $"rr_{size}_{radius01:0.00}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            float radius = Mathf.Clamp01(radius01) * half;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxSdf(x, y, half, radius);
                    float a = Mathf.Clamp01(0.5f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            float border = Mathf.Min(radius, size * 0.5f - 1f);
            Sprite sprite = Build(pixels, size, key, new Vector4(border, border, border, border));
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite RoundedFrame(float radius01 = 0.22f, float thickness01 = 0.12f, int size = 96)
        {
            string key = $"frame_{size}_{radius01:0.00}_{thickness01:0.00}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            float radius = Mathf.Clamp01(radius01) * half;
            float thickness = Mathf.Clamp01(thickness01) * half;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxSdf(x, y, half, radius);
                    float outer = Mathf.Clamp01(0.5f - d);            // 1 inside outer edge
                    float inner = Mathf.Clamp01(0.5f - (d + thickness)); // 1 inside hollow
                    float a = Mathf.Clamp01(outer - inner);          // border band only
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            float border = Mathf.Min(radius + thickness, size * 0.5f - 1f);
            Sprite sprite = Build(pixels, size, key, new Vector4(border, border, border, border));
            Cache[key] = sprite;
            return sprite;
        }

        // Rounded rect with a wide blurred edge, for drop shadows. The shape is
        // inset by the feather width so the blur fully fades out inside the
        // texture and the 9-slice never clips it.
        public static Sprite SoftRoundedRect(float radius01 = 0.30f, float feather01 = 0.35f, int size = 96)
        {
            string key = $"softrr_{size}_{radius01:0.00}_{feather01:0.00}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            float feather = Mathf.Max(1f, Mathf.Clamp01(feather01) * half);
            float extent = half - feather;
            float radius = Mathf.Clamp01(radius01) * extent;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - half) - (extent - radius);
                    float py = Mathf.Abs(y + 0.5f - half) - (extent - radius);
                    float ox = Mathf.Max(px, 0f);
                    float oy = Mathf.Max(py, 0f);
                    float outside = Mathf.Sqrt(ox * ox + oy * oy);
                    float inside = Mathf.Min(Mathf.Max(px, py), 0f);
                    float d = outside + inside - radius;
                    float a = Mathf.Clamp01(0.5f - d / feather);
                    a = a * a;                                        // softer falloff tail
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            float border = Mathf.Min(radius + feather, half - 1f);
            Sprite sprite = Build(pixels, size, key, new Vector4(border, border, border, border));
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite SoftCircle(int size = 64)
        {
            string key = $"soft_{size}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float t = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - t);
                    a = a * a;                                        // softer falloff
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            Sprite sprite = Build(pixels, size, key, Vector4.zero);
            Cache[key] = sprite;
            return sprite;
        }

        // Signed distance to a rounded box centered in a 'size' texture.
        private static float RoundedBoxSdf(int x, int y, float half, float radius)
        {
            float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
            float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
            float ox = Mathf.Max(px, 0f);
            float oy = Mathf.Max(py, 0f);
            float outside = Mathf.Sqrt(ox * ox + oy * oy);
            float inside = Mathf.Min(Mathf.Max(px, py), 0f);
            return outside + inside - radius;
        }

        private static Sprite Build(Color32[] pixels, int size, string name, Vector4 border)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
