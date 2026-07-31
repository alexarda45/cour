using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class JuicePopupLayer : MonoBehaviour
    {
        [SerializeField] private int maxPopups = 8;

        private int activePopups;
        private RectTransform activeChainPopup;

        private void OnDisable()
        {
            StopAllCoroutines();
            activePopups = 0;
            activeChainPopup = null;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name.StartsWith("JuicePopup"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        public void Show(string message, Color color, int fontSize, Vector2 anchoredPosition)
        {
            ShowInternal(message, color, fontSize, anchoredPosition, 0.86f, 100f, 0.82f, 1.08f, false);
        }

        public void ShowClear(ClearResult result, int chain, int scoreAdded)
        {
            ShowClear(result, chain, scoreAdded, 0, Vector2.zero, new Color(0.1f, 0.9f, 1f, 1f));
        }

        public void ShowClear(ClearResult result, int chain, int scoreAdded, int styleBonus)
        {
            ShowClear(result, chain, scoreAdded, styleBonus, Vector2.zero, new Color(0.1f, 0.9f, 1f, 1f));
        }

        public void ShowClear(ClearResult result, int chain, int scoreAdded, int styleBonus, Vector2 clearScreenPosition)
        {
            ShowClear(result, chain, scoreAdded, styleBonus, clearScreenPosition, new Color(0.1f, 0.9f, 1f, 1f));
        }

        public void ShowClear(
            ClearResult result,
            int chain,
            int scoreAdded,
            int styleBonus,
            Vector2 clearScreenPosition,
            Color placedPieceColor)
        {
            if (result == null || result.linesCleared <= 0)
            {
                return;
            }

            Vector2 popupPosition = ResolveClearPosition(clearScreenPosition);
            Color scoreColor = Color.Lerp(placedPieceColor, Color.white, 0.78f);
            scoreColor.a = 1f;
            int scoreSize = result.linesCleared >= 3 || result.pureLines > 0 ? 58 : 52;
            ShowInternal($"+{Mathf.Max(0, scoreAdded)}", scoreColor, scoreSize, popupPosition, 0.66f, 62f, 0.92f, 1.05f, false);

            if (chain >= 2)
            {
                ShowChain(chain, popupPosition + Vector2.up * 84f, placedPieceColor);
            }
        }

        public void ShowPop(ChromaColor color, int popped, int scoreAdded)
        {
            Color popColor = Color.Lerp(ChromaPalette.GetColor(color), Color.white, 0.42f);
            string message = popped > 0 && scoreAdded > 0
                ? $"POP!\n+{scoreAdded}"
                : "POP!";
            int fontSize = popped >= 8 ? 70 : 62;
            ShowInternal(message, popColor, fontSize, new Vector2(0f, 285f), 0.80f, 96f, 0.72f, 1.16f, false);
        }

        private void ShowChain(int chain, Vector2 anchoredPosition, Color accentColor)
        {
            if (activeChainPopup != null)
            {
                activeChainPopup.gameObject.SetActive(false);
                Destroy(activeChainPopup.gameObject);
                activeChainPopup = null;
            }

            Color color = chain >= 4
                ? Color.Lerp(accentColor, Hex("#FFE59A"), 0.62f)
                : Color.Lerp(accentColor, Color.white, 0.66f);
            color.a = 1f;
            int fontSize = Mathf.Min(60, 42 + chain * 4);
            ShowInternal($"CHAIN x{chain}", color, fontSize, anchoredPosition, 0.76f, 66f, 0.90f, 1.07f, true);
        }

        private void ShowInternal(
            string message,
            Color color,
            int fontSize,
            Vector2 anchoredPosition,
            float duration,
            float travel,
            float startScale,
            float peakScale,
            bool chainPopup)
        {
            // Floating gameplay labels are intentionally disabled. The score
            // and reward systems still run, but this layer spawns no text.
            return;
#pragma warning disable CS0162
            bool fullJuice = MobilePerformance.UseFullJuice();
            int popupLimit = fullJuice ? maxPopups : Mathf.Min(3, maxPopups);

            if (!chainPopup && activePopups >= popupLimit)
            {
                return;
            }

            if (!fullJuice)
            {
                duration = Mathf.Min(duration, 0.58f);
                travel *= 0.68f;
                startScale = Mathf.Max(0.88f, startScale);
                peakScale = Mathf.Min(1.07f, peakScale);
            }

            GameObject textObject = new GameObject(
                chainPopup ? "JuicePopup_Chain" : "JuicePopup",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(760f, 150f);
            textRect.anchoredPosition = anchoredPosition;

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.text = message;
            text.color = color;
            text.fontSize = fontSize;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(18f, fontSize * 0.58f);
            text.enableAutoSizing = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.025f, 0.10f, 0.76f);
            outline.effectDistance = new Vector2(0.75f, -0.75f);
            outline.useGraphicAlpha = true;

            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.015f, 0.08f, 0.72f);
            shadow.effectDistance = new Vector2(0f, -2f);
            shadow.useGraphicAlpha = true;

            activePopups++;
            if (chainPopup)
            {
                activeChainPopup = textRect;
            }

            StartCoroutine(AnimatePopup(text, textRect, duration, travel, startScale, peakScale, chainPopup));
#pragma warning restore CS0162
        }

        private IEnumerator AnimatePopup(
            TMP_Text text,
            RectTransform textRect,
            float duration,
            float travel,
            float startScale,
            float peakScale,
            bool chainPopup)
        {
            Vector2 start = textRect.anchoredPosition;
            Vector2 end = start + Vector2.up * travel;
            Color startColor = text.color;
            textRect.localScale = Vector3.one * startScale;

            float elapsed = 0f;
            while (elapsed < duration && text != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                textRect.anchoredPosition = Vector2.Lerp(start, end, eased);

                float scale = t < 0.28f
                    ? Mathf.Lerp(startScale, peakScale, t / 0.28f)
                    : Mathf.Lerp(peakScale, 1f, (t - 0.28f) / 0.72f);
                textRect.localScale = Vector3.one * scale;

                Color animatedColor = startColor;
                animatedColor.a = 1f - Mathf.SmoothStep(0.56f, 1f, t);
                text.color = animatedColor;
                yield return null;
            }

            activePopups = Mathf.Max(0, activePopups - 1);
            if (chainPopup && activeChainPopup == textRect)
            {
                activeChainPopup = null;
            }

            if (textRect != null)
            {
                Destroy(textRect.gameObject);
            }
        }

        private Vector2 ResolveClearPosition(Vector2 screenPosition)
        {
            RectTransform layerRect = transform as RectTransform;
            if (layerRect == null || screenPosition.sqrMagnitude < 1f)
            {
                return new Vector2(0f, 190f);
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layerRect, screenPosition, eventCamera, out Vector2 localPoint))
            {
                return new Vector2(0f, 190f);
            }

            localPoint += Vector2.up * 68f;
            Rect bounds = layerRect.rect;
            localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin + 150f, bounds.xMax - 150f);
            localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin + 180f, bounds.yMax - 220f);
            return localPoint;
        }

        private Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out Color color);
            return color;
        }
    }
}
