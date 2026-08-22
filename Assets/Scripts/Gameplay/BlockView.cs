using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    [RequireComponent(typeof(Image))]
    public class BlockView : MonoBehaviour
    {
        // The Tile_Reference_* art has a transparent margin baked into its square
        // canvas (the solid tile shape only fills ~87% of the canvas width), so
        // the sprite must be scaled up past 1.0 for tiles to read as nearly
        // touching on the board/tray, matching the reference look. Keep this
        // close to that ~1/0.87 ratio only — pushing it further crushes the
        // rounded corners into square-looking blocks. Any remaining cell gap
        // should be closed via BoardManager's cellPadding, not this scale.
        private const float TileVisualScale = 1.15f;

        // The same art also bakes in a soft drop shadow that extends further
        // below the tile than the margin above it, which pulls the solid tile
        // shape visually above true center when the sprite is centered on its
        // full canvas. This nudges it back down by that same fraction.
        private const float TileVerticalCenteringNudge = -0.024f;

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
        private Sprite completionPreviewOriginalSprite;
        private Sprite completionPreviewTargetSprite;
        private Color completionPreviewOriginalColor;
        private bool completionSpritePreviewActive;
        private bool initialized;
        private System.Action<BlockView> clearCompletionCallback;

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

        private void OnEnable()
        {
            ThemeCatalog.ThemeChanged += HandleThemeChanged;
            if (initialized)
            {
                ApplyCurrentThemeArtwork(ThemeCatalog.Current);
            }
        }

        private void OnDisable()
        {
            ThemeCatalog.ThemeChanged -= HandleThemeChanged;
        }

        // BoardManager assigns this only to its live board-block pool. Other
        // BlockView users retain the original destroy-on-clear behavior.
        public void SetClearCompletionCallback(System.Action<BlockView> callback)
        {
            clearCompletionCallback = callback;
        }

        // This is an explicit reuse boundary for pooled board blocks. Do not
        // rely on OnEnable to repair presentation state left by an interrupted
        // placement/clear routine.
        public void PrepareForPool()
        {
            if (visualRoutine != null)
            {
                StopCoroutine(visualRoutine);
                visualRoutine = null;
            }

            transform.DOKill();
            clearCompletionCallback = null;
            EndCompletionSpritePreview();

            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
            if (RectTransform != null)
            {
                RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                RectTransform.pivot = new Vector2(0.5f, 0.5f);
                RectTransform.anchoredPosition = Vector2.zero;
                RectTransform.sizeDelta = Vector2.zero;
            }

            ResetImageForPool(image);
            ResetImageForPool(shadowImage);
            ResetImageForPool(glowImage);
            ResetImageForPool(tileImage);
            ResetImageForPool(highlightImage);
            ResetImageForPool(innerImage);
            ResetImageForPool(shineImage);

            Color = default;
            initialized = false;
        }

        public void Initialize(ChromaColor color, bool animate = true)
        {
            Color = color;
            initialized = true;
            if (image == null)
            {
                image = GetComponent<Image>();
            }

            Sprite tileSprite = GetTileSprite(color);
            if (image != null)
            {
                image.sprite = tileSprite;
                image.color = UnityEngine.Color.white;
            }

            EnsureVisualImages();
            EndCompletionSpritePreview();
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

        // A soft contact shadow for pieces resting/dragging in the tray, so they
        // read as floating above the tray background instead of flat against it.
        // Reuses the shadowImage layer that ApplyTileSprite otherwise keeps off
        // (board tiles already bake their own shadow into the art).
        private static readonly Color TraySoftShadowColor = new Color(0f, 0.02f, 0.06f, 0.44f);
        private static readonly Color TraySoftShadowColorLowEnd = new Color(0f, 0.02f, 0.06f, 0.28f);

        // Down-right offset for the tray shadow. Must be applied here, after
        // Initialize, because EnsureVisualImages resets the layer to its default
        // (3,-5) board offset. Kept small enough that the shadow can never reach
        // a neighboring tray slot: adjacent pieces' edges sit >=40px apart
        // (310px slot pitch minus a max 270px piece width), while this extends
        // ~10px past the piece edge including the tile art's visual overhang.
        private static readonly Vector3 TrayShadowOffset = new Vector3(6f, -9f, 0f);

        public void SetTrayShadowVisible(bool visible)
        {
            if (shadowImage == null)
            {
                return;
            }

            // Explicit SetActive alongside .enabled: shadowImage is created inside
            // BlockView's own Awake/EnsureVisualImages, so it should already be
            // active, but this guards against it ever being parked inactive.
            shadowImage.gameObject.SetActive(true);

            if (visible)
            {
                shadowImage.sprite = tileImage != null ? tileImage.sprite : shadowImage.sprite;
                shadowImage.color = MobilePerformance.LowEndMode ? TraySoftShadowColorLowEnd : TraySoftShadowColor;
                shadowImage.rectTransform.localPosition = TrayShadowOffset;
            }

            shadowImage.enabled = visible && shadowImage.sprite != null;
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
                float verticalNudge = tileImage.rectTransform.rect.height * TileVisualScale * TileVerticalCenteringNudge;
                tileImage.rectTransform.localPosition = new Vector3(0f, verticalNudge, 0f);
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
            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            return activeTheme == null ? null : activeTheme.GetTileSprite(color);
        }

        private void HandleThemeChanged(ThemeType requestedTheme, ThemeAssetSet resolvedTheme)
        {
            if (initialized)
            {
                ApplyCurrentThemeArtwork(resolvedTheme);
            }
        }

        private void ApplyCurrentThemeArtwork(ThemeAssetSet theme)
        {
            if (theme == null)
            {
                return;
            }

            bool restoreTrayShadow = shadowImage != null && shadowImage.enabled;
            EndCompletionSpritePreview();
            Sprite tileSprite = theme.GetTileSprite(Color);
            if (image != null)
            {
                image.sprite = tileSprite;
                image.color = UnityEngine.Color.white;
            }

            ApplyTileSprite(tileSprite);
            if (restoreTrayShadow)
            {
                SetTrayShadowVisible(true);
            }
        }

        public bool BeginCompletionSpritePreview(ChromaColor targetColor)
        {
            if (tileImage == null)
            {
                EnsureVisualImages();
            }

            Sprite targetSprite = GetTileSprite(targetColor);
            if (tileImage == null || targetSprite == null)
            {
                return false;
            }

            if (!completionSpritePreviewActive)
            {
                completionPreviewOriginalSprite = tileImage.sprite;
                completionPreviewOriginalColor = tileImage.color;
            }

            completionPreviewTargetSprite = targetSprite;
            completionSpritePreviewActive = true;
            SetCompletionTargetSpriteVisible(true);
            return true;
        }

        public void SetCompletionTargetSpriteVisible(bool targetVisible)
        {
            if (!completionSpritePreviewActive || tileImage == null)
            {
                return;
            }

            tileImage.sprite = targetVisible
                ? completionPreviewTargetSprite
                : completionPreviewOriginalSprite;

            // Keep the source artwork fully opaque and untinted in both phases;
            // the pulse is the sprite swap itself, never alpha blending.
            tileImage.color = completionPreviewOriginalColor;
        }

        public void EndCompletionSpritePreview()
        {
            if (completionSpritePreviewActive && tileImage != null)
            {
                tileImage.sprite = completionPreviewOriginalSprite;
                tileImage.color = completionPreviewOriginalColor;
            }

            completionPreviewOriginalSprite = null;
            completionPreviewTargetSprite = null;
            completionSpritePreviewActive = false;
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

        private static void ResetImageForPool(Image target)
        {
            if (target == null)
            {
                return;
            }

            target.sprite = null;
            target.color = UnityEngine.Color.white;
            target.enabled = false;
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
            const float duration = 0.050f;
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

        public void PlayClear(float delay, float impactStrength = 0f)
        {
            EndCompletionSpritePreview();
            if (!MobilePerformance.UseFullJuice())
            {
                RunVisualRoutine(QuickClearRoutine(delay));
                return;
            }

            RunVisualRoutine(ClearJuicyRoutine(delay, impactStrength));
        }

        private IEnumerator QuickClearRoutine(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            const float duration = 0.052f;
            while (elapsed < duration && this != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = t < 0.22f
                    ? Vector3.Lerp(startScale, Vector3.one * 1.10f, t / 0.22f)
                    : Vector3.Lerp(Vector3.one * 1.10f, Vector3.zero, (t - 0.22f) / 0.78f);
                yield return null;
            }

            if (this != null)
            {
                CompleteClear();
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
            const float duration = 0.050f;
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

        private IEnumerator ClearJuicyRoutine(float delay, float impactStrength)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            float clearStrength = Mathf.Clamp01(impactStrength);
            SetAlpha(glowImage, MobilePerformance.LowEndMode ? 0.24f : Mathf.Lerp(0.68f, 0.80f, clearStrength));
            SetAlpha(highlightImage, MobilePerformance.LowEndMode ? 0.18f : Mathf.Lerp(0.56f, 0.68f, clearStrength));

            UnityEngine.Color imageColor = image == null ? UnityEngine.Color.white : image.color;
            UnityEngine.Color tileColor = tileImage == null ? UnityEngine.Color.white : tileImage.color;
            UnityEngine.Color shadowColor = shadowImage == null ? UnityEngine.Color.white : shadowImage.color;
            UnityEngine.Color glowColor = glowImage == null ? UnityEngine.Color.white : glowImage.color;
            UnityEngine.Color highlightColor = highlightImage == null ? UnityEngine.Color.white : highlightImage.color;
            UnityEngine.Color innerColor = innerImage == null ? UnityEngine.Color.white : innerImage.color;
            UnityEngine.Color shineColor = shineImage == null ? UnityEngine.Color.white : shineImage.color;

            float peakScale = Mathf.Lerp(1.15f, 1.19f, clearStrength);
            yield return ScaleRoutine(Vector3.one, Vector3.one * peakScale, 0.010f);

            float elapsed = 0f;
            const float duration = 0.042f;
            Vector3 startScale = Vector3.one * peakScale;
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
                CompleteClear();
            }
        }

        private void CompleteClear()
        {
            visualRoutine = null;
            System.Action<BlockView> callback = clearCompletionCallback;
            clearCompletionCallback = null;
            if (callback != null)
            {
                callback(this);
                return;
            }

            Destroy(gameObject);
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
