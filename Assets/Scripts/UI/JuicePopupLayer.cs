using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class JuicePopupLayer : MonoBehaviour
    {
        private const string PopupFontPath = "Fonts/Fredoka-SemiBold SDF";

        [SerializeField] private int maxPopups = 8;

        private int activePopups;
        private RectTransform activeComboPopup;
        private Coroutine activeComboRoutine;
        private RectTransform activeScorePopup;
        private Coroutine activeScorePopupRoutine;
        private TMP_FontAsset popupFont;

        private enum ComboTier
        {
            None,
            Good,
            Great,
            Amazing,
            Perfect
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            activePopups = 0;
            activeComboPopup = null;
            activeComboRoutine = null;
            activeScorePopup = null;
            activeScorePopupRoutine = null;

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
            ComboTier comboTier = ResolveComboTier(result, chain);
            bool hasComboLabel = comboTier != ComboTier.None;
            Color scoreColor = Color.Lerp(placedPieceColor, Color.white, 0.72f);
            scoreColor.a = 1f;
            int presentationTier = result.pureLines > 0 || result.linesCleared >= 3 || chain >= 4
                ? 2
                : result.linesCleared >= 2 ? 1 : 0;
            int scoreSize = result.pureLines > 0 ? 60 : hasComboLabel ? 56 : 52;
            Vector2 scorePosition = hasComboLabel
                ? popupPosition + Vector2.down * 54f
                : popupPosition;
            ShowInternal(
                $"+{Mathf.Max(0, scoreAdded)}",
                scoreColor,
                scoreSize,
                scorePosition,
                0.24f + presentationTier * 0.02f,
                58f + presentationTier * 8f,
                0.76f,
                1.12f + presentationTier * 0.03f,
                true);

            if (hasComboLabel)
            {
                ShowComboLabel(comboTier, chain, result.linesCleared, placedPieceColor);
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

        private static ComboTier ResolveComboTier(ClearResult result, int chain)
        {
            int linesCleared = result == null ? 0 : result.linesCleared;
            int pureLines = result == null ? 0 : result.pureLines;
            if (chain >= 6 || (pureLines > 0 && linesCleared >= 2))
            {
                return ComboTier.Perfect;
            }

            if (chain >= 4 || linesCleared >= 4)
            {
                return ComboTier.Amazing;
            }

            if (chain >= 3 || linesCleared >= 3)
            {
                return ComboTier.Great;
            }

            return chain >= 2 || linesCleared >= 2
                ? ComboTier.Good
                : ComboTier.None;
        }

        private void ShowComboLabel(ComboTier tier, int chain, int linesCleared, Color accentColor)
        {
            CancelActiveComboPopup();

            if (tier >= ComboTier.Amazing)
            {
                AudioManager.Instance?.PlayComboBig();
            }
            else
            {
                AudioManager.Instance?.PlayComboSmall();
            }

            AudioManager.Instance?.PlayComboVoice((int)tier);

            ConfigureComboTier(
                tier,
                accentColor,
                out string label,
                out int fontSize,
                out Color topColor,
                out Color bottomColor,
                out Color outlineColor,
                out Color glowColor,
                out int sparkleCount);

            GameObject rootObject = new GameObject(
                "JuicePopup_ComboTier",
                typeof(RectTransform),
                typeof(CanvasGroup));
            rootObject.transform.SetParent(transform, false);
            RectTransform root = (RectTransform)rootObject.transform;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = root.anchorMin;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(940f, 290f);
            root.SetAsLastSibling();

            GameObject textObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root, false);
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.pivot = new Vector2(0.5f, 0.5f);

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.text = label;
            text.font = GetPopupFont();
            text.fontSize = fontSize;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = fontSize * 0.72f;
            text.enableAutoSizing = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = -1.5f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.color = Color.white;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(topColor, topColor, bottomColor, bottomColor);
            text.outlineWidth = tier == ComboTier.Perfect
                ? 0.31f
                : tier == ComboTier.Amazing ? 0.27f : tier == ComboTier.Great ? 0.22f : 0.16f;
            outlineColor.a = 1f;
            text.outlineColor = outlineColor;

            Outline glowOutline = textObject.AddComponent<Outline>();
            glowColor.a = tier == ComboTier.Perfect
                ? 0.86f
                : tier == ComboTier.Amazing ? 0.76f : tier == ComboTier.Great ? 0.62f : 0.50f;
            glowOutline.effectColor = glowColor;
            float glowDistance = tier == ComboTier.Perfect
                ? 6f
                : tier == ComboTier.Amazing ? 5f : tier == ComboTier.Great ? 4f : 3f;
            glowOutline.effectDistance = new Vector2(glowDistance, -glowDistance);
            glowOutline.useGraphicAlpha = true;

            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.035f, 0.12f, 0.92f);
            float shadowDistance = tier == ComboTier.Perfect
                ? 9f
                : tier == ComboTier.Amazing ? 8f : tier == ComboTier.Great ? 7f : 5f;
            shadow.effectDistance = new Vector2(0f, -shadowDistance);
            shadow.useGraphicAlpha = true;

            Image[] sparkles = CreateComboSparkles(root, sparkleCount, glowColor);
            CanvasGroup canvasGroup = rootObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            activeComboPopup = root;
            activeComboRoutine = StartCoroutine(AnimateComboLabel(
                root,
                canvasGroup,
                glowOutline,
                sparkles,
                tier,
                chain,
                linesCleared));
        }

        private void ConfigureComboTier(
            ComboTier tier,
            Color accentColor,
            out string label,
            out int fontSize,
            out Color topColor,
            out Color bottomColor,
            out Color outlineColor,
            out Color glowColor,
            out int sparkleCount)
        {
            switch (tier)
            {
                case ComboTier.Perfect:
                    label = "PERFECT!";
                    fontSize = 142;
                    topColor = Hex("#FFFDF0");
                    bottomColor = Hex("#FFD447");
                    outlineColor = Hex("#F05A16");
                    glowColor = Hex("#FFF16A");
                    sparkleCount = 12;
                    break;
                case ComboTier.Amazing:
                    label = "AMAZING!";
                    fontSize = 130;
                    topColor = Hex("#FFF9D6");
                    bottomColor = Hex("#FFB62F");
                    outlineColor = Hex("#C84D12");
                    glowColor = Hex("#FFD34A");
                    sparkleCount = 9;
                    break;
                case ComboTier.Great:
                    label = "GREAT!";
                    fontSize = 118;
                    topColor = Color.white;
                    bottomColor = Color.Lerp(accentColor, Hex("#6FF7FF"), 0.62f);
                    outlineColor = Color.Lerp(accentColor, Hex("#0758B8"), 0.58f);
                    glowColor = Color.Lerp(accentColor, Color.white, 0.20f);
                    sparkleCount = 5;
                    break;
                default:
                    label = "GOOD!";
                    fontSize = 104;
                    topColor = Color.white;
                    bottomColor = Color.Lerp(accentColor, Hex("#82EEFF"), 0.68f);
                    outlineColor = Hex("#0758A8");
                    glowColor = Color.Lerp(accentColor, Hex("#35E7FF"), 0.62f);
                    sparkleCount = 0;
                    break;
            }
        }

        private Image[] CreateComboSparkles(RectTransform root, int requestedCount, Color color)
        {
            int count = MobilePerformance.UseFullJuice()
                ? requestedCount
                : Mathf.Min(4, requestedCount);
            Image[] sparkles = new Image[count];
            for (int i = 0; i < count; i++)
            {
                GameObject sparkleObject = new GameObject(
                    $"Sparkle_{i}",
                    typeof(RectTransform),
                    typeof(Image));
                sparkleObject.transform.SetParent(root, false);
                RectTransform sparkleRect = (RectTransform)sparkleObject.transform;
                sparkleRect.anchorMin = new Vector2(0.5f, 0.5f);
                sparkleRect.anchorMax = sparkleRect.anchorMin;
                sparkleRect.pivot = new Vector2(0.5f, 0.5f);
                float size = i % 3 == 0 ? 28f : i % 3 == 1 ? 20f : 14f;
                sparkleRect.sizeDelta = Vector2.one * size;
                sparkleRect.localRotation = Quaternion.Euler(0f, 0f, i * 31f);

                Image sparkle = sparkleObject.GetComponent<Image>();
                UISpriteFactory.ApplySoftCircle(sparkle);
                Color sparkleColor = i % 2 == 0 ? Color.white : color;
                sparkleColor.a = 0f;
                sparkle.color = sparkleColor;
                sparkle.raycastTarget = false;
                sparkles[i] = sparkle;
            }

            return sparkles;
        }

        private IEnumerator AnimateComboLabel(
            RectTransform root,
            CanvasGroup canvasGroup,
            Outline glowOutline,
            Image[] sparkles,
            ComboTier tier,
            int chain,
            int linesCleared)
        {
            float duration;
            float entranceDuration;
            float settleDuration;
            float peakScale;
            float floatDistance;
            float maxGlowAlpha;
            float rotationRange;
            switch (tier)
            {
                case ComboTier.Perfect:
                    duration = 0.37f;
                    entranceDuration = 0.045f;
                    settleDuration = 0.012f;
                    peakScale = 1.21f;
                    floatDistance = 30f;
                    maxGlowAlpha = 1f;
                    rotationRange = 6f;
                    break;
                case ComboTier.Amazing:
                    duration = 0.34f;
                    entranceDuration = 0.045f;
                    settleDuration = 0.012f;
                    peakScale = 1.18f;
                    floatDistance = 26f;
                    maxGlowAlpha = 0.90f;
                    rotationRange = 5f;
                    break;
                case ComboTier.Great:
                    duration = 0.31f;
                    entranceDuration = 0.045f;
                    settleDuration = 0.012f;
                    peakScale = 1.15f;
                    floatDistance = 22f;
                    maxGlowAlpha = 0.78f;
                    rotationRange = 4f;
                    break;
                default:
                    duration = 0.28f;
                    entranceDuration = 0.045f;
                    settleDuration = 0.012f;
                    peakScale = 1.11f;
                    floatDistance = 18f;
                    maxGlowAlpha = 0.66f;
                    rotationRange = 3f;
                    break;
            }

            float settleEnd = entranceDuration + settleDuration;
            const float exitDuration = 0.050f;
            float exitStart = duration - exitDuration;
            float startRotation = Random.Range(-rotationRange, rotationRange);
            Color baseGlowColor = glowOutline.effectColor;
            root.localScale = Vector3.zero;
            root.localRotation = Quaternion.Euler(0f, 0f, startRotation);

            float elapsed = 0f;
            while (elapsed < duration && root != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale;
                if (elapsed < entranceDuration)
                {
                    float enterT = 1f - Mathf.Pow(1f - elapsed / entranceDuration, 3f);
                    scale = Mathf.LerpUnclamped(0f, peakScale, enterT);
                }
                else if (elapsed < settleEnd)
                {
                    float settleT = Mathf.SmoothStep(0f, 1f, (elapsed - entranceDuration) / (settleEnd - entranceDuration));
                    scale = Mathf.Lerp(peakScale, 1f, settleT);
                }
                else if (elapsed >= exitStart)
                {
                    float exitT = Mathf.SmoothStep(0f, 1f, (elapsed - exitStart) / (duration - exitStart));
                    scale = Mathf.Lerp(1f, 1.04f, exitT);
                }
                else
                {
                    scale = 1f;
                }

                root.localScale = Vector3.one * scale;
                float rotationSettle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / settleEnd));
                root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startRotation, 0f, rotationSettle));
                root.anchoredPosition = Vector2.up * (Mathf.SmoothStep(0f, 1f, t) * floatDistance);

                float fade = elapsed < exitStart
                    ? 1f
                    : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - exitStart) / (duration - exitStart));
                canvasGroup.alpha = fade;

                float glowPulse = 0.5f + Mathf.Sin(Time.unscaledTime * (tier >= ComboTier.Amazing ? 22f : 16f)) * 0.5f;
                Color animatedGlow = baseGlowColor;
                animatedGlow.a = Mathf.Lerp(0.36f, maxGlowAlpha, glowPulse) * fade;
                glowOutline.effectColor = animatedGlow;
                AnimateComboSparkles(sparkles, t, fade, tier);
                yield return null;
            }

            if (activeComboPopup == root)
            {
                activeComboPopup = null;
                activeComboRoutine = null;
            }

            if (root != null)
            {
                Destroy(root.gameObject);
            }
        }

        private static void AnimateComboSparkles(Image[] sparkles, float t, float parentFade, ComboTier tier)
        {
            if (sparkles == null || sparkles.Length == 0)
            {
                return;
            }

            float burstT = Mathf.Clamp01(t / 0.62f);
            float burstEase = 1f - Mathf.Pow(1f - burstT, 3f);
            for (int i = 0; i < sparkles.Length; i++)
            {
                Image sparkle = sparkles[i];
                if (sparkle == null)
                {
                    continue;
                }

                float angle = (i / (float)sparkles.Length) * Mathf.PI * 2f + i * 0.37f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 start = new Vector2(direction.x * 90f, direction.y * 42f);
                float sparkleDistanceX = tier == ComboTier.Perfect
                    ? 340f
                    : tier == ComboTier.Amazing ? 300f : tier == ComboTier.Great ? 250f : 220f;
                float sparkleDistanceY = tier == ComboTier.Perfect
                    ? 130f
                    : tier == ComboTier.Amazing ? 116f : tier == ComboTier.Great ? 96f : 82f;
                Vector2 end = new Vector2(direction.x * sparkleDistanceX, direction.y * sparkleDistanceY);
                sparkle.rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, end, burstEase);
                float life = Mathf.Sin(burstT * Mathf.PI);
                float peakSparkleScale = tier == ComboTier.Perfect
                    ? 1.25f
                    : tier == ComboTier.Amazing ? 1.18f : tier == ComboTier.Great ? 1.12f : 1.05f;
                sparkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.45f, peakSparkleScale, life);
                sparkle.rectTransform.Rotate(0f, 0f, Time.unscaledDeltaTime * (90f + i * 13f));
                Color sparkleColor = sparkle.color;
                sparkleColor.a = life * parentFade * (i % 2 == 0 ? 0.95f : 0.72f);
                sparkle.color = sparkleColor;
            }
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
            bool numericScorePopup,
            bool emphasized = false)
        {
            bool fullJuice = MobilePerformance.UseFullJuice();
            int popupLimit = fullJuice ? maxPopups : Mathf.Min(3, maxPopups);

            if (numericScorePopup)
            {
                CancelActiveScorePopup();
            }

            if (!numericScorePopup && activePopups >= popupLimit)
            {
                return;
            }

            if (!fullJuice)
            {
                duration = numericScorePopup
                    ? Mathf.Clamp(duration, 0.32f, 0.36f)
                    : Mathf.Clamp(duration, 0.80f, 1f);
                travel *= 0.82f;
                peakScale = Mathf.Min(peakScale, emphasized ? 1.20f : 1.12f);
            }

            duration = numericScorePopup
                ? Mathf.Clamp(duration, 0.32f, 0.36f)
                : Mathf.Clamp(duration, 0.80f, 1.20f);

            GameObject textObject = new GameObject(
                numericScorePopup ? "JuicePopup_Score" : "JuicePopup",
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
            text.font = GetPopupFont();
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

            Coroutine popupRoutine = StartCoroutine(AnimatePopup(
                text,
                textRect,
                duration,
                travel,
                startScale,
                peakScale,
                numericScorePopup,
                emphasized,
                accentGlow));
            if (numericScorePopup)
            {
                activeScorePopup = textRect;
                activeScorePopupRoutine = popupRoutine;
            }
        }

        private IEnumerator AnimatePopup(
            TMP_Text text,
            RectTransform textRect,
            float duration,
            float travel,
            float startScale,
            float peakScale,
            bool numericScorePopup,
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

            if (numericScorePopup && activeScorePopup == textRect)
            {
                activeScorePopup = null;
                activeScorePopupRoutine = null;
            }

            if (textRect != null)
            {
                Destroy(textRect.gameObject);
            }
        }

        private void CancelActiveComboPopup()
        {
            if (activeComboRoutine != null)
            {
                StopCoroutine(activeComboRoutine);
                activeComboRoutine = null;
            }

            if (activeComboPopup != null)
            {
                activeComboPopup.gameObject.SetActive(false);
                Destroy(activeComboPopup.gameObject);
                activeComboPopup = null;
            }
        }

        public void ClearActiveComboPresentation()
        {
            CancelActiveComboPopup();
        }

        private void CancelActiveScorePopup()
        {
            if (activeScorePopupRoutine != null)
            {
                StopCoroutine(activeScorePopupRoutine);
                activeScorePopupRoutine = null;
            }

            if (activeScorePopup != null)
            {
                activeScorePopup.gameObject.SetActive(false);
                Destroy(activeScorePopup.gameObject);
                activeScorePopup = null;
                activePopups = Mathf.Max(0, activePopups - 1);
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

        private TMP_FontAsset GetPopupFont()
        {
            if (popupFont == null)
            {
                popupFont = Resources.Load<TMP_FontAsset>(PopupFontPath);
            }

            return popupFont;
        }

        private Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out Color color);
            return color;
        }
    }
}
