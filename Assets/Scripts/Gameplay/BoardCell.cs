using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class BoardCell : MonoBehaviour
    {
        private const float PreviewVisualScale = 1f;
        private const float PreviewPulseMin = 0.99f;
        private const float PreviewPulseMax = 1.035f;

        [SerializeField] private Image background;
        [SerializeField] private Image insetShadow;
        [SerializeField] private Image topSheen;
        [SerializeField] private Image preview;

        private static readonly Color BaseColor = Color.clear;
        private static readonly Color CompletionGlow = new Color(0.12f, 0.58f, 0.72f, 1f);
        private static readonly Color CompletionGlowPeak = new Color(0.25f, 0.94f, 1f, 1f);
        private const float ValidPreviewAlpha = 0.50f;
        private const float ValidPreviewStrongAlpha = 0.65f;
        private Coroutine flashRoutine;
        private Coroutine sweepRoutine;
        private Coroutine linePulseRoutine;
        private Coroutine previewPulseRoutine;
        private Coroutine invalidPreviewRoutine;
        private RectTransform previewRect;
        private Color previewBaseColor;

        private void Awake()
        {
            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (preview == null)
            {
                Transform previewChild = transform.Find("Preview");
                if (previewChild != null)
                {
                    preview = previewChild.GetComponent<Image>();
                }
            }

            if (preview != null)
            {
                previewRect = preview.rectTransform;
            }

            EnsureSurfaceLayers();
        }

        public void Configure()
        {
            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (preview == null)
            {
                EnsurePreviewImage();
            }

            EnsureSurfaceLayers();

            if (linePulseRoutine != null)
            {
                StopCoroutine(linePulseRoutine);
                linePulseRoutine = null;
            }

            if (sweepRoutine != null)
            {
                StopCoroutine(sweepRoutine);
                sweepRoutine = null;
            }

            StopInvalidPreviewPulse();
            if (background != null)
            {
                transform.localScale = Vector3.one;
                background.raycastTarget = false;
                background.sprite = null;
                background.type = Image.Type.Simple;
                background.preserveAspect = false;
                background.fillCenter = true;
                background.color = Color.clear;
            }

            RefreshSurfaceLayerColors();

            if (preview != null)
            {
                StopPreviewPulse();
                preview.enabled = false;
                previewRect = preview.rectTransform;
                ApplyPreviewScale(1f);
                preview.transform.SetAsLastSibling();
            }
        }

        public void SetPreview(bool canPlace)
        {
            SetPreview(canPlace, ChromaColor.Cyan);
        }

        public void SetPreview(bool canPlace, ChromaColor color)
        {
            SetPreview(canPlace, color, false);
        }

        public void SetPreview(bool canPlace, ChromaColor color, bool completesLine)
        {
            if (preview == null)
            {
                return;
            }

            if (!canPlace)
            {
                ClearPreview();
                return;
            }

            preview.enabled = true;
            UISpriteFactory.ApplyRounded(preview, 0.24f);
            preview.preserveAspect = false;
            preview.fillCenter = true;

            Color previewColor = ThemeCatalog.GetEffectColor(
                color,
                ChromaPalette.GetColor(color, ThemeType.Ocean));
            previewColor.a = completesLine ? ValidPreviewStrongAlpha : ValidPreviewAlpha;
            previewBaseColor = previewColor;
            preview.color = previewColor;
            SetPreviewInset(5f);
            ApplyPreviewBackgroundGlow(color, completesLine);
            StopPreviewPulse();
        }

        public void ClearPreview()
        {
            StopPreviewPulse();
            StopInvalidPreviewPulse();
            if (preview != null)
            {
                preview.enabled = false;
                preview.color = previewBaseColor;
                if (previewRect != null)
                {
                    ApplyPreviewScale(1f);
                }
            }

            if (background != null && flashRoutine == null && sweepRoutine == null && linePulseRoutine == null)
            {
                background.color = GetRestingColor();
            }
        }

        public void PlayFlash(Color flashColor, float delay, float duration = 0.18f)
        {
            if (background == null || !MobilePerformance.UseFullJuice())
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine(flashColor, delay, duration));
        }

        public void PlaySweep(Color sweepColor, float delay, bool strong)
        {
            if (background == null || !MobilePerformance.UseFullJuice())
            {
                return;
            }

            if (sweepRoutine != null)
            {
                StopCoroutine(sweepRoutine);
            }

            sweepRoutine = StartCoroutine(SweepRoutine(sweepColor, delay, strong));
        }

        public void SetHighlightedBorder(bool highlighted)
        {
            if (background == null)
            {
                return;
            }

            if (!highlighted)
            {
                if (linePulseRoutine != null)
                {
                    StopCoroutine(linePulseRoutine);
                    linePulseRoutine = null;
                }

                background.color = GetRestingColor();
                return;
            }

            if (MobilePerformance.UseFullJuice())
            {
                if (linePulseRoutine == null)
                {
                    linePulseRoutine = StartCoroutine(LinePulseRoutine());
                }
            }
            else
            {
                background.color = CompletionGlow;
            }
        }

        public void SetOpportunityHint(int level)
        {
            if (background == null || linePulseRoutine != null || flashRoutine != null || sweepRoutine != null)
            {
                return;
            }

            background.color = GetRestingColor();
        }

        private IEnumerator LinePulseRoutine()
        {
            while (background != null)
            {
                float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 11f) * 0.5f;
                background.color = Color.Lerp(CompletionGlow, CompletionGlowPeak, pulse);
                yield return null;
            }

            linePulseRoutine = null;
        }

        private void StartPreviewPulse()
        {
            if (previewPulseRoutine != null || preview == null)
            {
                return;
            }

            if (previewRect == null)
            {
                previewRect = preview.rectTransform;
            }

            previewPulseRoutine = StartCoroutine(PreviewPulseRoutine());
        }

        private void StopPreviewPulse()
        {
            if (previewPulseRoutine != null)
            {
                StopCoroutine(previewPulseRoutine);
                previewPulseRoutine = null;
            }

            if (previewRect != null)
            {
                ApplyPreviewScale(1f);
            }
        }

        private void PlayInvalidPreviewPulse()
        {
            if (!MobilePerformance.UseFullJuice() || previewRect == null)
            {
                ApplyPreviewScale(1.02f);
                return;
            }

            StopInvalidPreviewPulse();
            invalidPreviewRoutine = StartCoroutine(InvalidPreviewPulseRoutine());
        }

        private void StopInvalidPreviewPulse()
        {
            if (invalidPreviewRoutine != null)
            {
                StopCoroutine(invalidPreviewRoutine);
                invalidPreviewRoutine = null;
            }
        }

        private IEnumerator PreviewPulseRoutine()
        {
            while (preview != null && preview.enabled)
            {
                float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 12.5f) * 0.5f;
                if (previewRect != null)
                {
                    ApplyPreviewScale(Mathf.Lerp(PreviewPulseMin, PreviewPulseMax, pulse));
                }

                Color color = previewBaseColor;
                color.a = Mathf.Lerp(0.84f, previewBaseColor.a, pulse);
                preview.color = color;
                yield return null;
            }

            previewPulseRoutine = null;
        }

        private IEnumerator InvalidPreviewPulseRoutine()
        {
            Vector3 startScale = Vector3.one * PreviewVisualScale;
            Vector3 peakScale = Vector3.one * (PreviewVisualScale * 1.065f);
            float elapsed = 0f;
            const float duration = 0.18f;
            while (elapsed < duration && previewRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float wave = Mathf.Sin(t * Mathf.PI);
                previewRect.localScale = Vector3.Lerp(startScale, peakScale, wave);
                yield return null;
            }

            ApplyPreviewScale(1f);
            invalidPreviewRoutine = null;
        }

        private IEnumerator FlashRoutine(Color flashColor, float delay, float duration)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            Color peak = Color.Lerp(flashColor, Color.white, 0.22f);
            peak.a = 1f;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration && background != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                background.color = Color.Lerp(peak, BaseColor, t * t);
                yield return null;
            }

            flashRoutine = null;
            if (background != null)
            {
                background.color = GetRestingColor();
            }
        }

        private IEnumerator SweepRoutine(Color sweepColor, float delay, bool strong)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            Color rest = GetRestingColor();
            Color peak = Color.Lerp(sweepColor, Color.white, strong ? 0.36f : 0.24f);
            peak.a = 1f;

            float elapsed = 0f;
            const float inDuration = 0.028f;
            while (elapsed < inDuration && background != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / inDuration);
                background.color = Color.Lerp(rest, peak, t);
                yield return null;
            }

            elapsed = 0f;
            float outDuration = strong ? 0.105f : 0.085f;
            while (elapsed < outDuration && background != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / outDuration);
                background.color = Color.Lerp(peak, rest, t * t);
                yield return null;
            }

            sweepRoutine = null;
            if (background != null && flashRoutine == null && linePulseRoutine == null)
            {
                background.color = GetRestingColor();
            }
        }

        private void ApplyPreviewBackgroundGlow(ChromaColor color, bool completesLine)
        {
            if (background == null || flashRoutine != null || sweepRoutine != null || linePulseRoutine != null)
            {
                return;
            }

            background.color = Color.clear;
        }

        private Color GetRestingColor()
        {
            return Color.clear;
        }

        private void EnsurePreviewImage()
        {
            GameObject previewObject = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            previewObject.transform.SetParent(transform, false);
            RectTransform previewRect = (RectTransform)previewObject.transform;
            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.one;
            previewRect.offsetMin = Vector2.one * 4f;
            previewRect.offsetMax = Vector2.one * -4f;
            preview = previewObject.GetComponent<Image>();
            this.previewRect = previewRect;
            ApplyPreviewScale(1f);
            preview.raycastTarget = false;
            preview.enabled = false;
        }

        private void EnsureSurfaceLayers()
        {
            insetShadow = FindSurfaceImage(insetShadow, "CellInsetShadow");
            topSheen = FindSurfaceImage(topSheen, "CellTopSheen");
            DisableSurfaceImage("CellInsetShadow");
            DisableSurfaceImage("CellTopSheen");
            DisableSurfaceImage("CellRimHighlight");
        }

        private Image FindSurfaceImage(Image target, string layerName)
        {
            if (target != null)
            {
                return target;
            }

            Transform existing = transform.Find(layerName);
            return existing == null ? null : existing.GetComponent<Image>();
        }

        private Image EnsureSurfaceImage(Image target, string layerName)
        {
            if (target == null)
            {
                Transform existing = transform.Find(layerName);
                target = existing == null ? null : existing.GetComponent<Image>();
            }

            if (target != null)
            {
                target.gameObject.SetActive(true);
                target.raycastTarget = false;
                return target;
            }

            GameObject layer = new GameObject(layerName, typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(transform, false);
            target = layer.GetComponent<Image>();
            target.raycastTarget = false;
            return target;
        }

        private void DisableSurfaceImage(string layerName)
        {
            Transform existing = transform.Find(layerName);
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
        }

        private void ConfigureSurfaceFrame(Image image, Vector2 offsetMin, Vector2 offsetMax, float radius, float thickness, bool abovePreview)
        {
            if (image == null)
            {
                return;
            }

            UISpriteFactory.ApplyFrame(image, radius, thickness);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            image.fillCenter = false;
            if (abovePreview)
            {
                image.transform.SetAsLastSibling();
            }
            else
            {
                image.transform.SetAsFirstSibling();
            }
        }

        private void ConfigureTopSheen(Image image)
        {
            if (image == null)
            {
                return;
            }

            UISpriteFactory.ApplyRounded(image, 0.24f);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.08f, 0.56f);
            rect.anchorMax = new Vector2(0.92f, 0.93f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            image.fillCenter = true;
            image.raycastTarget = false;
            image.transform.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));
        }

        private void RefreshSurfaceLayerColors()
        {
            if (insetShadow != null)
            {
                insetShadow.gameObject.SetActive(false);
            }

            if (topSheen != null)
            {
                topSheen.gameObject.SetActive(false);
            }
        }

        private void SetPreviewInset(float inset)
        {
            if (previewRect == null && preview != null)
            {
                previewRect = preview.rectTransform;
            }

            if (previewRect == null)
            {
                return;
            }

            previewRect.offsetMin = Vector2.one * inset;
            previewRect.offsetMax = Vector2.one * -inset;
        }

        private void ApplyPreviewScale(float scale)
        {
            if (previewRect != null)
            {
                previewRect.localScale = Vector3.one * (PreviewVisualScale * scale);
            }
        }
    }
}
