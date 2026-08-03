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
            ShowInternal(message, color, fontSize, anchoredPosition, 0.92f, 100f, 0f, 1.12f, false);
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
            bool emphasized = chain >= 3 || result.linesCleared >= 2;
            Color scoreColor = Color.Lerp(placedPieceColor, Color.white, emphasized ? 0.42f : 0.72f);
            scoreColor.a = 1f;
            int scoreSize = emphasized
                ? Mathf.Clamp(58 + result.linesCleared * 4 + Mathf.Max(0, chain - 2) * 3, 62, 76)
                : result.pureLines > 0 ? 58 : 52;
            ShowInternal(
                $"+{Mathf.Max(0, scoreAdded)}",
                scoreColor,
                scoreSize,
                popupPosition,
                emphasized ? 1.06f : 0.90f,
                emphasized ? 84f : 70f,
                0f,
                emphasized ? 1.24f : 1.14f,
                false,
                emphasized);

            if (chain >= 2)
            {
                ShowChain(chain, result.linesCleared, popupPosition + Vector2.up * 84f, placedPieceColor);
            }
        }

        public void ShowPop(ChromaColor color, int popped, int scoreAdded)
        {
            Color popColor = Color.Lerp(ChromaPalette.GetColor(color), Color.white, 0.42f);
            string message = popped > 0 && scoreAdded > 0
                ? $"POP!\n+{scoreAdded}"
                : "POP!";
            bool emphasized = popped >= 8;
            int fontSize = popped >= 8 ? 70 : 62;
            ShowInternal(
                message,
                popColor,
                fontSize,
                new Vector2(0f, 285f),
                emphasized ? 1.12f : 0.96f,
                emphasized ? 112f : 92f,
                0f,
                emphasized ? 1.28f : 1.18f,
                false,
                emphasized);
        }

        private void ShowChain(int chain, int linesCleared, Vector2 anchoredPosition, Color accentColor)
        {
            if (activeChainPopup != null)
            {
                activeChainPopup.gameObject.SetActive(false);
                Destroy(activeChainPopup.gameObject);
                activeChainPopup = null;
            }

            bool emphasized = chain >= 3 || linesCleared >= 2;
            Color color = chain >= 4
                ? Color.Lerp(accentColor, Hex("#FFE59A"), 0.42f)
                : Color.Lerp(accentColor, Color.white, emphasized ? 0.34f : 0.62f);
            color.a = 1f;
            int fontSize = emphasized
                ? Mathf.Min(78, 50 + chain * 6 + Mathf.Max(0, linesCleared - 1) * 4)
                : Mathf.Min(60, 42 + chain * 4);
            ShowInternal(
                $"CHAIN x{chain}",
                color,
                fontSize,
                anchoredPosition,
                emphasized ? 1.12f : 0.94f,
                emphasized ? 96f : 76f,
                0f,
                emphasized ? 1.30f : 1.16f,
                true,
                emphasized);
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
            bool chainPopup,
            bool emphasized = false)
        {
            bool fullJuice = MobilePerformance.UseFullJuice();
            int popupLimit = fullJuice ? maxPopups : Mathf.Min(3, maxPopups);

            if (!chainPopup && activePopups >= popupLimit)
            {
                return;
            }

            if (!fullJuice)
            {
                duration = Mathf.Clamp(duration, 0.80f, 1f);
                travel *= 0.82f;
                peakScale = Mathf.Min(peakScale, emphasized ? 1.20f : 1.12f);
            }

            duration = Mathf.Clamp(duration, 0.80f, 1.20f);

            GameObject textObject = new GameObject(
                chainPopup ? "JuicePopup_Chain" : "JuicePopup",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(760f, emphasized ? 190f : 150f);
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
            text.outlineWidth = emphasized ? 0.22f : 0.10f;
            Color textOutlineColor = Color.Lerp(color, Color.white, 0.18f);
            textOutlineColor.a = emphasized ? 0.78f : 0.48f;
            text.outlineColor = textOutlineColor;

            Outline accentGlow = null;
            if (emphasized)
            {
                accentGlow = textObject.AddComponent<Outline>();
                Color glowColor = color;
                glowColor.a = 0.62f;
                accentGlow.effectColor = glowColor;
                accentGlow.effectDistance = new Vector2(2.5f, -2.5f);
                accentGlow.useGraphicAlpha = true;
            }

            Outline depthOutline = textObject.AddComponent<Outline>();
            depthOutline.effectColor = new Color(0f, 0.025f, 0.10f, 0.76f);
            depthOutline.effectDistance = new Vector2(0.75f, -0.75f);
            depthOutline.useGraphicAlpha = true;

            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.015f, 0.08f, 0.72f);
            shadow.effectDistance = new Vector2(0f, -2f);
            shadow.useGraphicAlpha = true;

            activePopups++;
            if (chainPopup)
            {
                activeChainPopup = textRect;
            }

            StartCoroutine(AnimatePopup(
                text,
                textRect,
                duration,
                travel,
                startScale,
                peakScale,
                chainPopup,
                emphasized,
                accentGlow));
        }

        private IEnumerator AnimatePopup(
            TMP_Text text,
            RectTransform textRect,
            float duration,
            float travel,
            float startScale,
            float peakScale,
            bool chainPopup,
            bool emphasized,
            Outline accentGlow)
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
                Vector2 animatedPosition = Vector2.Lerp(start, end, eased);
                if (emphasized)
                {
                    float shakeEnvelope = 1f - Mathf.SmoothStep(0.18f, 0.68f, t);
                    animatedPosition.x += Mathf.Sin(t * Mathf.PI * 12f) * 4f * shakeEnvelope;
                    animatedPosition.y += Mathf.Sin(t * Mathf.PI * 9f) * 1.5f * shakeEnvelope;
                }

                textRect.anchoredPosition = animatedPosition;

                float scale;
                if (t < 0.18f)
                {
                    float punchT = 1f - Mathf.Pow(1f - t / 0.18f, 3f);
                    scale = Mathf.Lerp(startScale, peakScale, punchT);
                }
                else if (t < 0.38f)
                {
                    float settleT = Mathf.SmoothStep(0f, 1f, (t - 0.18f) / 0.20f);
                    scale = Mathf.Lerp(peakScale, 1f, settleT);
                }
                else
                {
                    float residualPunch = emphasized
                        ? Mathf.Sin((t - 0.38f) * Mathf.PI * 5f) * 0.025f * (1f - t)
                        : 0f;
                    scale = 1f + residualPunch;
                }

                textRect.localScale = Vector3.one * scale;

                Color animatedColor = startColor;
                float fade = 1f - Mathf.SmoothStep(0.60f, 1f, t);
                animatedColor.a = fade;
                text.color = animatedColor;

                if (accentGlow != null)
                {
                    Color glowColor = accentGlow.effectColor;
                    float glowPulse = 0.5f + Mathf.Sin(Time.unscaledTime * 18f) * 0.5f;
                    glowColor.a = Mathf.Lerp(0.34f, 0.82f, glowPulse) * fade;
                    accentGlow.effectColor = glowColor;
                }

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
