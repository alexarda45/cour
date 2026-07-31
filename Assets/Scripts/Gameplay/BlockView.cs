using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    [RequireComponent(typeof(Image))]
    public class BlockView : MonoBehaviour
    {
        private const float TileVisualScale = 1.03f;

        // Index order matches the ChromaColor enum (Cyan, Magenta, Lime, Amber).
        private static readonly string[] TileResourcePaths =
        {
            "Ocean/Tiles/Tile_Reference_Cyan",
            "Ocean/Tiles/Tile_Reference_Pink",
            "Ocean/Tiles/Tile_Reference_Blue",
            "Ocean/Tiles/Tile_Reference_Yellow"
        };

        private static readonly Sprite[] TileSprites = new Sprite[TileResourcePaths.Length];

        [SerializeField] private Image image;
        [SerializeField] private Image shadowImage;
        [SerializeField] private Image glowImage;
        [SerializeField] private Image tileImage;
        [SerializeField] private Image highlightImage;
        [SerializeField] private Image innerImage;
        [SerializeField] private Image shineImage;

        public ChromaColor Color { get; private set; }
        public RectTransform RectTransform { get; private set; }

        private Coroutine visualRoutine;

        private void Awake()
        {
            RectTransform = (RectTransform)transform;
            if (image == null)
            {
                image = GetComponent<Image>();
            }

            if (image != null)
            {
                image.raycastTarget = false;
                image.enabled = false;
            }

            EnsureVisualImages();
            DisableLegacyLayers();

            if (MobilePerformance.LowEndMode)
            {
                Shadow[] effects = GetComponents<Shadow>();
                for (int i = 0; i < effects.Length; i++)
                {
                    effects[i].enabled = false;
                }
            }
        }

        public void Initialize(ChromaColor color, bool animate = true)
        {
            Color = color;
            if (image == null)
            {
                image = GetComponent<Image>();
            }

            Sprite tileSprite = GetTileSprite(color) ?? ChromaPalette.GetTileSprite(color);
            if (image != null)
            {
                image.sprite = tileSprite;
                image.color = UnityEngine.Color.white;
            }

            EnsureVisualImages();
            ApplyTileSprite(tileSprite);

            DisableLegacyLayers();

            bool shouldAnimate = animate && MobilePerformance.UseFullJuice();
            transform.localScale = shouldAnimate ? Vector3.zero : Vector3.one;
            if (shouldAnimate)
            {
                PlayPlaced(0f);
            }
        }

        public void UseCleanTrayPresentation()
        {
            // Tray pieces need one crisp representation only. These two layers
            // reuse the full tile sprite and can read as a faint duplicate
            // shape when several blocks form a piece.
            if (glowImage != null)
            {
                glowImage.enabled = false;
            }

            if (highlightImage != null)
            {
                highlightImage.enabled = false;
            }
        }

        private void EnsureVisualImages()
        {
            // shadowImage/glowImage/highlightImage are kept only as targets for the
            // placement/clear juice routines below; ApplyTileSprite keeps them
            // disabled since the tile art now bakes in its own bevel and shadow.
            shadowImage = EnsureLayerImage(shadowImage, "TileShadow", TileVisualScale, true);
            glowImage = EnsureLayerImage(glowImage, "TileGlow", TileVisualScale, true);
            tileImage = EnsureLayerImage(tileImage, "TileVisual", TileVisualScale, false);
            highlightImage = EnsureLayerImage(highlightImage, "TileHighlight", TileVisualScale, false);

            if (shadowImage != null)
            {
                shadowImage.transform.SetAsFirstSibling();
                shadowImage.rectTransform.localPosition = new Vector3(3f, -5f, 0f);
            }

            if (glowImage != null)
            {
                glowImage.transform.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));
                glowImage.rectTransform.localPosition = Vector3.zero;
            }

            if (tileImage != null)
            {
                tileImage.transform.SetAsLastSibling();
                tileImage.rectTransform.localPosition = Vector3.zero;
            }

            if (highlightImage != null)
            {
                highlightImage.transform.SetAsLastSibling();
                highlightImage.rectTransform.localPosition = new Vector3(-0.8f, 1.1f, 0f);
            }
        }

        private Image EnsureLayerImage(Image target, string layerName, float scale, bool behindTile)
        {
            if (target == null)
            {
                Transform existing = transform.Find(layerName);
                target = existing == null ? CreateLayerImage(layerName) : existing.GetComponent<Image>();
            }

            if (target == null)
            {
                return null;
            }

            RectTransform rect = target.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one * scale;
            target.raycastTarget = false;
            target.type = Image.Type.Simple;
            target.preserveAspect = true;
            if (behindTile)
            {
                target.transform.SetAsFirstSibling();
            }

            return target;
        }

        private Image CreateLayerImage(string layerName)
        {
            GameObject layer = new GameObject(layerName, typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(transform, false);
            return layer.GetComponent<Image>();
        }

        private void ApplyTileSprite(Sprite tileSprite)
        {
            if (tileImage != null)
            {
                tileImage.enabled = tileSprite != null;
                tileImage.sprite = tileSprite;
                tileImage.color = UnityEngine.Color.white;
            }

            // The tile art already bakes in its own bevel, highlight and shadow,
            // so the old procedural overlay layers stay off to avoid a duplicated look.
            SetLegacyLayerEnabled(shadowImage, false);
            SetLegacyLayerEnabled(glowImage, false);
            SetLegacyLayerEnabled(highlightImage, false);
        }

        private static Sprite GetTileSprite(ChromaColor color)
        {
            int index = Mathf.Clamp((int)color, 0, TileResourcePaths.Length - 1);
            if (TileSprites[index] == null)
            {
                TileSprites[index] = Resources.Load<Sprite>(TileResourcePaths[index]);
            }

            return TileSprites[index];
        }

        private void DisableLegacyLayers()
        {
            if (innerImage == null)
            {
                Transform inner = transform.Find("Inner");
                innerImage = inner == null ? null : inner.GetComponent<Image>();
            }

            if (shineImage == null)
            {
                Transform shine = transform.Find("Shine");
                shineImage = shine == null ? null : shine.GetComponent<Image>();
            }

            SetLegacyLayerEnabled(innerImage, false);
            SetLegacyLayerEnabled(shineImage, false);
        }

        private static void SetLegacyLayerEnabled(Image target, bool enabled)
        {
            if (target != null)
            {
                target.enabled = enabled;
            }
        }

        public void PlayPlaced(float delay = 0f)
        {
            if (!MobilePerformance.UseFullJuice())
            {
                RunVisualRoutine(QuickPlacedPulse(delay));
                return;
            }

            RunVisualRoutine(PlacedRoutine(delay));
        }

        private IEnumerator QuickPlacedPulse(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            Vector3 restingPosition = transform.localPosition;
            Vector3 restingScale = Vector3.one;
            UnityEngine.Color originalTile = tileImage == null ? UnityEngine.Color.white : tileImage.color;
            transform.localScale = restingScale;

            float elapsed = 0f;
            const float duration = 0.12f;
            while (elapsed < duration && this != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float flash = Mathf.Sin(t * Mathf.PI);
                transform.localScale = Vector3.one * EvaluatePlacementScale(t);
                if (tileImage != null)
                {
                    tileImage.color = UnityEngine.Color.Lerp(originalTile, UnityEngine.Color.white, flash * 0.08f);
                }

                yield return null;
            }

            if (this != null)
            {
                transform.localScale = restingScale;
                transform.localPosition = restingPosition;
                if (tileImage != null)
                {
                    tileImage.color = originalTile;
                }

                visualRoutine = null;
            }
        }

        public void PlayClear(float delay)
        {
            if (!MobilePerformance.UseFullJuice())
            {
                RunVisualRoutine(QuickClearRoutine(delay));
                return;
            }

            RunVisualRoutine(ClearJuicyRoutine(delay));
        }

        private IEnumerator QuickClearRoutine(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            const float duration = 0.15f;
            while (elapsed < duration && this != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = t < 0.35f
                    ? Vector3.Lerp(startScale, Vector3.one * 1.10f, t / 0.35f)
                    : Vector3.Lerp(Vector3.one * 1.10f, Vector3.zero, (t - 0.35f) / 0.65f);
                yield return null;
            }

            if (this != null)
            {
                Destroy(gameObject);
            }
        }

        private void RunVisualRoutine(IEnumerator routine)
        {
            if (visualRoutine != null)
            {
                StopCoroutine(visualRoutine);
                visualRoutine = null;
            }

            transform.DOKill();
            visualRoutine = StartCoroutine(routine);
        }

        private IEnumerator PlacedRoutine(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            Vector3 restingPosition = transform.localPosition;
            Vector3 restingScale = Vector3.one;
            UnityEngine.Color originalGlow = glowImage == null ? UnityEngine.Color.clear : glowImage.color;
            UnityEngine.Color originalHighlight = highlightImage == null ? UnityEngine.Color.clear : highlightImage.color;
            UnityEngine.Color originalTile = tileImage == null ? UnityEngine.Color.white : tileImage.color;
            transform.localScale = restingScale;

            float elapsed = 0f;
            const float duration = 0.14f;
            while (elapsed < duration && this != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float flash = Mathf.Sin(t * Mathf.PI);
                transform.localScale = Vector3.one * EvaluatePlacementScale(t);
                SetAlpha(glowImage, Mathf.Lerp(originalGlow.a, MobilePerformance.LowEndMode ? 0.12f : 0.26f, flash));
                SetAlpha(highlightImage, Mathf.Lerp(originalHighlight.a, MobilePerformance.LowEndMode ? 0.09f : 0.20f, flash));
                if (tileImage != null)
                {
                    tileImage.color = UnityEngine.Color.Lerp(originalTile, UnityEngine.Color.white, flash * 0.09f);
                }

                yield return null;
            }

            transform.localScale = restingScale;
            transform.localPosition = restingPosition;
            if (glowImage != null)
            {
                glowImage.color = originalGlow;
            }

            if (highlightImage != null)
            {
                highlightImage.color = originalHighlight;
            }

            if (tileImage != null)
            {
                tileImage.color = originalTile;
            }

            visualRoutine = null;
        }

        private IEnumerator ClearJuicyRoutine(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            SetAlpha(glowImage, MobilePerformance.LowEndMode ? 0.24f : 0.68f);
            SetAlpha(highlightImage, MobilePerformance.LowEndMode ? 0.18f : 0.56f);

            UnityEngine.Color imageColor = image == null ? UnityEngine.Color.white : image.color;
            UnityEngine.Color tileColor = tileImage == null ? UnityEngine.Color.white : tileImage.color;
            UnityEngine.Color shadowColor = shadowImage == null ? UnityEngine.Color.white : shadowImage.color;
            UnityEngine.Color glowColor = glowImage == null ? UnityEngine.Color.white : glowImage.color;
            UnityEngine.Color highlightColor = highlightImage == null ? UnityEngine.Color.white : highlightImage.color;
            UnityEngine.Color innerColor = innerImage == null ? UnityEngine.Color.white : innerImage.color;
            UnityEngine.Color shineColor = shineImage == null ? UnityEngine.Color.white : shineImage.color;

            yield return ScaleRoutine(Vector3.one, Vector3.one * 1.12f, 0.06f);

            float elapsed = 0f;
            const float duration = 0.14f;
            Vector3 startScale = Vector3.one * 1.12f;
            Vector3 endScale = Vector3.zero;
            while (elapsed < duration && this != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                transform.localScale = Vector3.Lerp(startScale, endScale, eased);
                SetAlpha(image, imageColor.a * (1f - eased));
                SetAlpha(tileImage, tileColor.a * (1f - eased));
                SetAlpha(shadowImage, shadowColor.a * (1f - eased));
                SetAlpha(glowImage, glowColor.a * (1f - eased));
                SetAlpha(highlightImage, highlightColor.a * (1f - eased));
                SetAlpha(innerImage, innerColor.a * (1f - eased));
                SetAlpha(shineImage, shineColor.a * (1f - eased));
                yield return null;
            }

            if (this != null)
            {
                Destroy(gameObject);
            }
        }

        private static float EvaluatePlacementScale(float normalizedTime)
        {
            if (normalizedTime < 0.36f)
            {
                float t = Mathf.SmoothStep(0f, 1f, normalizedTime / 0.36f);
                return Mathf.Lerp(1f, 1.08f, t);
            }

            if (normalizedTime < 0.66f)
            {
                float t = Mathf.SmoothStep(0f, 1f, (normalizedTime - 0.36f) / 0.30f);
                return Mathf.Lerp(1.08f, 0.97f, t);
            }

            float settle = Mathf.SmoothStep(0f, 1f, (normalizedTime - 0.66f) / 0.34f);
            return Mathf.Lerp(0.97f, 1f, settle);
        }

        private IEnumerator ScaleRoutine(Vector3 start, Vector3 end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && this != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                float eased = t * t * (3f - 2f * t);
                transform.localScale = Vector3.Lerp(start, end, eased);
                yield return null;
            }

            if (this != null)
            {
                transform.localScale = end;
            }
        }

        private IEnumerator FadeLayerRoutine(Image target, float startAlpha, float endAlpha, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                SetAlpha(target, Mathf.Lerp(startAlpha, endAlpha, t));
                yield return null;
            }

            SetAlpha(target, endAlpha);
        }

        private static void SetAlpha(Image target, float alpha)
        {
            if (target == null)
            {
                return;
            }

            Color color = target.color;
            color.a = Mathf.Clamp01(alpha);
            target.color = color;
        }
    }
}
