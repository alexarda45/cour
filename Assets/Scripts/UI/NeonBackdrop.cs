using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class NeonBackdrop : MonoBehaviour
    {
        [SerializeField] private Color topColor = new Color(0.015f, 0.02f, 0.05f, 1f);
        [SerializeField] private Color bottomColor = new Color(0.04f, 0.015f, 0.085f, 1f);
        [SerializeField] private Color gridColor = new Color(0.08f, 0.48f, 0.72f, 0.11f);
        [SerializeField] private Color magentaAccent = new Color(1f, 0.18f, 0.72f, 0.1f);
        [SerializeField] private int width = 96;
        [SerializeField] private int height = 192;

        private Image image;
        private Texture2D texture;
        private Sprite sprite;
        private ThemeType appliedTheme = (ThemeType)(-1);

        private void OnEnable()
        {
            appliedTheme = (ThemeType)(-1);
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        public void Apply()
        {
            if (image == null)
            {
                image = GetComponent<Image>();
            }

            ThemeType theme = ChromaPalette.CurrentTheme;
            if (Application.isPlaying && appliedTheme == theme && image != null && image.sprite != null)
            {
                return;
            }

            appliedTheme = theme;
            topColor = ChromaPalette.GetThemeTopColor(theme);
            bottomColor = ChromaPalette.GetThemeBottomColor(theme);
            gridColor = ChromaPalette.GetThemeGridColor(theme);
            Color accent = ChromaPalette.GetColor(ChromaColor.Magenta, theme);
            accent.a = 0.10f;
            magentaAccent = accent;

            if (width < 16)
            {
                width = 16;
            }

            if (height < 16)
            {
                height = 16;
            }

            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < height; y++)
            {
                float vertical = y / (float)(height - 1);
                Color baseColor = Color.Lerp(bottomColor, topColor, vertical);

                for (int x = 0; x < width; x++)
                {
                    float horizontal = x / (float)(width - 1);
                    float vignette = Mathf.Abs(horizontal - 0.5f) * 1.25f + Mathf.Abs(vertical - 0.48f) * 0.45f;
                    Color pixel = Color.Lerp(baseColor, Color.black, Mathf.Clamp01(vignette) * 0.34f);

                    bool gridLine = x % 12 == 0 || y % 12 == 0;
                    if (gridLine && vertical < 0.42f)
                    {
                        pixel = Color.Lerp(pixel, gridColor, gridColor.a);
                    }

                    float cyanBand = Mathf.SmoothStep(0.08f, 0.16f, vertical) * (1f - Mathf.SmoothStep(0.24f, 0.36f, vertical));
                    float magentaBand = Mathf.SmoothStep(0.70f, 0.78f, vertical) * (1f - Mathf.SmoothStep(0.88f, 0.96f, vertical));
                    Color cyanAccent = ChromaPalette.GetColor(ChromaColor.Cyan, theme);
                    pixel = Color.Lerp(pixel, cyanAccent, cyanBand * 0.14f);
                    pixel = Color.Lerp(pixel, magentaAccent, magentaBand * 0.5f);

                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();

            if (sprite != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(sprite);
                }
                else
                {
                    DestroyImmediate(sprite);
                }
            }

            sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }
    }
}
