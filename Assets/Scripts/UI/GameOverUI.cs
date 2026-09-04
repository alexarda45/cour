using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class GameOverUI : MonoBehaviour
    {
        public event Action PresentationCompleted;

        // Keep the loss feedback decisive: the player can restart within 0.46 seconds
        // of confirmation (impact + panel entrance + button reveal).
        private const float InitialImpactDuration = 0.12f;
        private const float ScreenImpactDuration = 0.16f;
        private const float BoardReactionDuration = 0.14f;
        private const float PanelEntryDuration = 0.20f;
        private const float ButtonRevealDuration = 0.14f;
        private const float ScreenImpactAlpha = 0.18f;
        private const float BoardCompressionScale = 0.985f;
        // The theme capsule files share the original 1536x1024 padded canvas.
        // Game Over uses a wide cropped capsule RectTransform, so use the same
        // visible-art bounds (alpha >= 8) without stretching the source artwork.
        private static readonly Rect ThemeCapsuleVisibleRectNormalized = new Rect(
            67f / 1536f,
            250f / 1024f,
            1402f / 1536f,
            533f / 1024f);

        private static readonly string[] LegacyObjectNames =
        {
            "ScoreCaption",
            "RoundSummaryText",
            "XPText",
            "RankHintText",
            "RankProgress",
            "ReviveButton",
            "MenuButton"
        };

        [SerializeField] private GameObject root;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image newBestAccent;
        [SerializeField] private TMP_Text scoreValueText;
        [SerializeField] private TMP_Text bestLabelText;
        [SerializeField] private CanvasGroup bestScoreCapsuleGroup;
        [SerializeField] private TMP_Text bestValueText;
        [SerializeField] private Button restartButton;
        [SerializeField] private CanvasGroup restartButtonGroup;
        [SerializeField] private Vector2 normalScorePosition = new Vector2(0f, 105f);
        [SerializeField] private Vector2 newBestScorePosition = new Vector2(0f, 35f);

        private GameManager gameManager;
        private Coroutine entranceRoutine;
        private bool runtimeRestartListenerAdded;
        private Vector2 restartFinalPosition;
        private bool restartFinalPositionCached;
        private Image themedBestScoreCapsuleImage;
        private Image themedBestScoreCrownImage;
        private Sprite defaultBestScoreCapsuleSprite;
        private Sprite defaultBestScoreCrownSprite;
        private Sprite gameOverThemeCapsuleSprite;
        private Sprite gameOverThemeCapsuleSource;
        private Image impactOverlayImage;
        private RectTransform panelRect;
        private RectTransform boardReactionRect;
        private Vector3 boardReactionBaseScale = Vector3.one;
        private bool boardReactionBaseScaleCached;
        private CameraShake cameraShake;
        private readonly Vector3[] fullscreenCanvasCorners = new Vector3[4];

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            DisableLegacyVisuals();
            ApplyCurrentThemeVisuals();
            CacheRestartFinalPosition();
        }

        private void OnEnable()
        {
            ThemeCatalog.ThemeChanged -= HandleThemeChanged;
            ThemeCatalog.ThemeChanged += HandleThemeChanged;
            ApplyCurrentThemeVisuals();
        }

        private void OnDisable()
        {
            ThemeCatalog.ThemeChanged -= HandleThemeChanged;
            SetIosFullscreenBackgroundVisible(false);
        }

        private void OnDestroy()
        {
            ThemeCatalog.ThemeChanged -= HandleThemeChanged;
            DestroyRuntimeThemeCapsuleSprite();
        }

        public void Initialize(GameManager owner)
        {
            gameManager = owner;
            CachePresentationTargets();
            WireRestartButtonOnce();
        }

        public void Show(
            GameMode mode,
            int score,
            int highScore,
            bool canRevive,
            int xpGained,
            int rankPoints,
            int coinsEarned,
            int totalCoins,
            int linesCleared,
            int pureLines,
            int popsUsed,
            int bestChain,
            int previousRankPoints = -1,
            int dailyMedalCoins = 0,
            string dailyMedalName = "",
            bool newBest = false)
        {
            if (root == null)
            {
                root = gameObject;
            }

            DisableLegacyVisuals();
            ApplyCurrentThemeVisuals();
            CachePresentationTargets();
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            SetIosFullscreenBackgroundVisible(true);
            WireRestartButtonOnce();

            if (entranceRoutine != null)
            {
                StopCoroutine(entranceRoutine);
                entranceRoutine = null;
            }

            int finalScore = Mathf.Max(0, score);
            int displayedBest = Mathf.Max(finalScore, highScore);
            bool showNewBest = newBest && finalScore > 0;

            if (scoreValueText != null)
            {
                scoreValueText.rectTransform.anchoredPosition =
                    showNewBest ? newBestScorePosition : normalScorePosition;
                SetScore(finalScore);
            }

            if (bestValueText != null)
            {
                bestValueText.text = displayedBest.ToString(CultureInfo.InvariantCulture);
            }

            if (newBestAccent != null)
            {
                newBestAccent.gameObject.SetActive(showNewBest);
            }

            CacheRestartFinalPosition();
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(true);
                restartButton.interactable = false;
            }

            PrepareEntranceState();
            BeginImpact();
            entranceRoutine = StartCoroutine(AnimateEntrance(finalScore));
        }

        public void Hide()
        {
            if (entranceRoutine != null)
            {
                StopCoroutine(entranceRoutine);
                entranceRoutine = null;
            }

            CacheRestartFinalPosition();
            RestoreFinalVisualState();
            SetIosFullscreenBackgroundVisible(false);
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private IEnumerator AnimateEntrance(int finalScore)
        {
            float elapsed = 0f;

            while (elapsed < InitialImpactDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                UpdateImpact(elapsed);
                yield return null;
            }

            SetScore(finalScore);

            elapsed = 0f;
            while (elapsed < PanelEntryDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float panelT = Mathf.Clamp01(elapsed / PanelEntryDuration);
                float panelEase = EaseOutCubic(panelT);
                SetPanelVisualAlpha(panelEase);
                SetPanelScale(EvaluatePanelScale(panelT));

                SetScale(
                    scoreValueText == null ? null : scoreValueText.rectTransform,
                    1f + Mathf.Sin(panelT * Mathf.PI) * 0.045f);

                UpdateImpact(InitialImpactDuration + elapsed);

                yield return null;
            }

            SetPanelVisualAlpha(1f);
            SetPanelScale(1f);
            RestoreBoardReactionScale();
            SetImpactOverlayAlpha(0f);

            elapsed = 0f;
            while (elapsed < ButtonRevealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float buttonProgress = Mathf.Clamp01(elapsed / ButtonRevealDuration);
                float buttonT = EaseOutCubic(buttonProgress);
                SetCanvasGroupAlpha(restartButtonGroup, buttonT);

                RectTransform restartRect =
                    restartButton == null ? null : restartButton.transform as RectTransform;
                if (restartRect != null)
                {
                    restartRect.localScale =
                        Vector3.one * Mathf.Lerp(0.92f, 1f, buttonT);
                }

                yield return null;
            }

            SetScore(finalScore);
            RestoreFinalVisualState();
            entranceRoutine = null;
            PresentationCompleted?.Invoke();
        }

        private void PrepareEntranceState()
        {
            SetPanelVisualAlpha(0f);
            SetPanelScale(0.95f);
            SetScale(scoreValueText == null ? null : scoreValueText.rectTransform, 1f);
            SetCanvasGroupAlpha(restartButtonGroup, 0f);

            if (restartButtonGroup != null)
            {
                restartButtonGroup.blocksRaycasts = false;
                restartButtonGroup.interactable = false;
            }

            RectTransform restartRect =
                restartButton == null ? null : restartButton.transform as RectTransform;
            if (restartRect != null)
            {
                restartRect.anchoredPosition = restartFinalPosition;
                restartRect.localScale = Vector3.one * 0.92f;
            }
        }

        private void RestoreFinalVisualState()
        {
            SetImpactOverlayAlpha(0f);
            RestoreBoardReactionScale();
            SetPanelVisualAlpha(1f);
            SetPanelScale(1f);
            SetGraphicAlpha(scoreValueText, 1f);
            SetScale(scoreValueText == null ? null : scoreValueText.rectTransform, 1f);
            SetCanvasGroupAlpha(restartButtonGroup, 1f);
            if (restartButtonGroup != null)
            {
                restartButtonGroup.blocksRaycasts = true;
                restartButtonGroup.interactable = true;
            }

            RectTransform restartRect =
                restartButton == null ? null : restartButton.transform as RectTransform;
            if (restartRect != null && restartFinalPositionCached)
            {
                restartRect.anchoredPosition = restartFinalPosition;
                restartRect.localScale = Vector3.one;
            }

            if (restartButton != null)
            {
                restartButton.interactable = true;
            }
        }

        private void CacheRestartFinalPosition()
        {
            if (restartFinalPositionCached || restartButton == null)
            {
                return;
            }

            RectTransform restartRect = restartButton.transform as RectTransform;
            if (restartRect != null)
            {
                restartFinalPosition = restartRect.anchoredPosition;
                restartFinalPositionCached = true;
            }
        }

        private void SetScore(int value)
        {
            if (scoreValueText != null)
            {
                scoreValueText.text =
                    Mathf.Max(0, value).ToString(CultureInfo.InvariantCulture);
            }
        }

        private void HandleThemeChanged(ThemeType requestedTheme, ThemeAssetSet resolvedTheme)
        {
            ApplyThemeBackground(resolvedTheme);
            ApplyBestScoreTheme(resolvedTheme);
        }

        private void ApplyCurrentThemeVisuals()
        {
            ThemeAssetSet theme = ThemeCatalog.Current;
            ApplyThemeBackground(theme);
            ApplyBestScoreTheme(theme);
        }

        private void ApplyBestScoreTheme(ThemeAssetSet theme)
        {
            CacheBestScoreThemeGraphics();

            if (themedBestScoreCapsuleImage != null)
            {
                themedBestScoreCapsuleImage.material = null;
                themedBestScoreCapsuleImage.color = Color.white;
                themedBestScoreCapsuleImage.preserveAspect = true;
                themedBestScoreCapsuleImage.sprite = theme != null && theme.CapsuleSprite != null
                    ? GetGameOverCapsuleSprite(theme.CapsuleSprite)
                    : defaultBestScoreCapsuleSprite;
            }

            if (themedBestScoreCrownImage != null)
            {
                themedBestScoreCrownImage.material = null;
                themedBestScoreCrownImage.color = Color.white;
                themedBestScoreCrownImage.preserveAspect = true;
                themedBestScoreCrownImage.sprite = defaultBestScoreCrownSprite;
            }

            ApplyGameOverAccentColors(theme);
        }

        private void ApplyGameOverAccentColors(ThemeAssetSet theme)
        {
            Color accent = GetBrightGameOverAccent(theme);

            SetSolidTextColor(titleText, Color.white);
            SetSolidTextColor(scoreValueText, Color.white);
            SetSolidTextColor(bestValueText, Color.white);
            SetSolidTextColor(bestLabelText, accent);

            ApplyTextMaterialAccent(titleText, accent);
            ApplyTextMaterialAccent(scoreValueText, accent);
            ApplyTextMaterialAccent(bestValueText, accent);
            ApplyTextMaterialAccent(bestLabelText, accent);
        }

        private static Color GetBrightGameOverAccent(ThemeAssetSet theme)
        {
            Color source = theme == null
                ? new Color(0.20f, 0.90f, 1f, 1f)
                : theme.CapsuleTintColor;
            Color.RGBToHSV(source, out float hue, out float saturation, out _);
            Color bright = Color.HSVToRGB(hue, Mathf.Max(0.55f, saturation), 1f);
            bright.a = 1f;
            return bright;
        }

        private static void SetSolidTextColor(TMP_Text text, Color color)
        {
            if (text == null)
            {
                return;
            }

            color.a = 1f;
            text.enableVertexGradient = false;
            text.color = color;
            text.faceColor = Color.white;
        }

        private static void ApplyTextMaterialAccent(TMP_Text text, Color accent)
        {
            if (text == null)
            {
                return;
            }

            Material material = text.fontMaterial;
            if (material == null)
            {
                return;
            }

            accent.a = 1f;
            if (material.HasProperty(ShaderUtilities.ID_OutlineColor))
            {
                material.SetColor(ShaderUtilities.ID_OutlineColor, accent);
            }

            if (material.HasProperty(ShaderUtilities.ID_GlowColor))
            {
                Color glow = accent;
                glow.a = material.GetColor(ShaderUtilities.ID_GlowColor).a;
                material.SetColor(ShaderUtilities.ID_GlowColor, glow);
            }

            text.UpdateMeshPadding();
        }

        private Sprite GetGameOverCapsuleSprite(Sprite source)
        {
            if (source == null)
            {
                DestroyRuntimeThemeCapsuleSprite();
                return defaultBestScoreCapsuleSprite;
            }

            if (gameOverThemeCapsuleSprite != null && gameOverThemeCapsuleSource == source)
            {
                return gameOverThemeCapsuleSprite;
            }

            DestroyRuntimeThemeCapsuleSprite();
            Rect sourceRect = source.rect;
            Rect crop = new Rect(
                sourceRect.x + sourceRect.width * ThemeCapsuleVisibleRectNormalized.x,
                sourceRect.y + sourceRect.height * ThemeCapsuleVisibleRectNormalized.y,
                sourceRect.width * ThemeCapsuleVisibleRectNormalized.width,
                sourceRect.height * ThemeCapsuleVisibleRectNormalized.height);
            gameOverThemeCapsuleSprite = Sprite.Create(
                source.texture,
                crop,
                new Vector2(0.5f, 0.5f),
                source.pixelsPerUnit,
                0u,
                SpriteMeshType.FullRect,
                source.border);
            gameOverThemeCapsuleSprite.name = $"{source.name}_GameOverVisibleCrop";
            gameOverThemeCapsuleSprite.hideFlags = HideFlags.HideAndDontSave;
            gameOverThemeCapsuleSource = source;
            return gameOverThemeCapsuleSprite;
        }

        private void DestroyRuntimeThemeCapsuleSprite()
        {
            if (gameOverThemeCapsuleSprite != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameOverThemeCapsuleSprite);
                }
                else
                {
                    DestroyImmediate(gameOverThemeCapsuleSprite);
                }
            }

            gameOverThemeCapsuleSprite = null;
            gameOverThemeCapsuleSource = null;
        }

        private void CacheBestScoreThemeGraphics()
        {
            if (bestScoreCapsuleGroup == null)
            {
                return;
            }

            if (themedBestScoreCapsuleImage == null)
            {
                themedBestScoreCapsuleImage = bestScoreCapsuleGroup.GetComponent<Image>();
                if (themedBestScoreCapsuleImage != null)
                {
                    defaultBestScoreCapsuleSprite = themedBestScoreCapsuleImage.sprite;
                }
            }

            if (themedBestScoreCrownImage == null)
            {
                Transform crown = bestScoreCapsuleGroup.transform.Find("CrownIcon");
                if (crown != null)
                {
                    themedBestScoreCrownImage = crown.GetComponent<Image>();
                    if (themedBestScoreCrownImage != null)
                    {
                        defaultBestScoreCrownSprite = themedBestScoreCrownImage.sprite;
                    }
                }
            }
        }

        private void ApplyThemeBackground(ThemeAssetSet theme)
        {
            if (backgroundImage == null || theme == null || theme.GameOverBackground == null)
            {
                return;
            }

            backgroundImage.sprite = theme.GameOverBackground;
            backgroundImage.color = Color.white;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = true;

            AspectRatioFitter fitter = backgroundImage.GetComponent<AspectRatioFitter>();
            if (fitter == null)
            {
                fitter = backgroundImage.gameObject.AddComponent<AspectRatioFitter>();
            }

            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = theme.GameOverBackground.rect.width / theme.GameOverBackground.rect.height;

            ConfigureIosFullscreenBackground();
        }

        private static bool UseFullscreenIosGameOverBackground
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        private void ConfigureIosFullscreenBackground()
        {
            if (!UseFullscreenIosGameOverBackground || backgroundImage == null || root == null)
            {
                return;
            }

            Canvas canvas = root.GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas == null ? null : canvas.transform as RectTransform;
            RectTransform rootRect = root.transform as RectTransform;
            if (canvasRect == null || rootRect == null)
            {
                return;
            }

            RectTransform backgroundRect = backgroundImage.rectTransform;
            if (backgroundRect.parent != rootRect)
            {
                backgroundRect.gameObject.SetActive(false);
                backgroundRect.SetParent(rootRect, false);
            }

            AspectRatioFitter fitter = backgroundImage.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }

            Canvas.ForceUpdateCanvases();
            canvasRect.GetWorldCorners(fullscreenCanvasCorners);
            Vector2 localMin = rootRect.InverseTransformPoint(fullscreenCanvasCorners[0]);
            Vector2 localMax = rootRect.InverseTransformPoint(fullscreenCanvasCorners[2]);
            Vector2 canvasSize = localMax - localMin;
            float spriteAspect = backgroundImage.sprite == null
                ? canvasSize.x / Mathf.Max(1f, canvasSize.y)
                : backgroundImage.sprite.rect.width / backgroundImage.sprite.rect.height;
            float targetAspect = canvasSize.x / Mathf.Max(1f, canvasSize.y);
            Vector2 coverSize = canvasSize;
            if (targetAspect > spriteAspect)
            {
                coverSize.y = canvasSize.x / spriteAspect;
            }
            else
            {
                coverSize.x = canvasSize.y * spriteAspect;
            }

            backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = (localMin + localMax) * 0.5f;
            backgroundRect.sizeDelta = coverSize;
            backgroundRect.localScale = Vector3.one;

            // Keep the entire Game Over root above gameplay. Only this decorative
            // first child extends beyond SafeArea to cover the physical display.
            backgroundRect.SetAsFirstSibling();
        }

        private void SetIosFullscreenBackgroundVisible(bool visible)
        {
            if (!UseFullscreenIosGameOverBackground || backgroundImage == null)
            {
                return;
            }

            backgroundImage.gameObject.SetActive(visible);
        }

        private void CachePresentationTargets()
        {
            if (root != null)
            {
                impactOverlayImage ??= root.GetComponent<Image>();

                if (panelRect == null)
                {
                    panelRect = root.transform.Find("GameOverPanel") as RectTransform;
                }
            }

            if (boardReactionRect == null && gameManager != null && gameManager.Board != null)
            {
                boardReactionRect = gameManager.Board.BoardRoot;
                if (boardReactionRect != null)
                {
                    boardReactionBaseScale = boardReactionRect.localScale;
                    boardReactionBaseScaleCached = true;
                }
            }

            cameraShake ??= FindFirstObjectByType<CameraShake>();
        }

        private void BeginImpact()
        {
            CachePresentationTargets();
            SetImpactOverlayAlpha(ScreenImpactAlpha);
            cameraShake?.Shake(0.095f, 0.12f);
        }

        private void UpdateImpact(float elapsed)
        {
            float screenT = Mathf.Clamp01(elapsed / ScreenImpactDuration);
            SetImpactOverlayAlpha(ScreenImpactAlpha * (1f - screenT));

            if (boardReactionRect == null || !boardReactionBaseScaleCached)
            {
                return;
            }

            float boardT = Mathf.Clamp01(elapsed / BoardReactionDuration);
            float compression = Mathf.Lerp(
                1f,
                BoardCompressionScale,
                Mathf.Sin(boardT * Mathf.PI));
            boardReactionRect.localScale = boardReactionBaseScale * compression;
        }

        private void RestoreBoardReactionScale()
        {
            if (boardReactionRect != null && boardReactionBaseScaleCached)
            {
                boardReactionRect.localScale = boardReactionBaseScale;
            }
        }

        private void SetPanelVisualAlpha(float alpha)
        {
            SetGraphicAlpha(backgroundImage, alpha);
            SetGraphicAlpha(titleText, alpha);
            SetGraphicAlpha(newBestAccent, alpha);
            SetGraphicAlpha(scoreValueText, alpha);
            SetGraphicAlpha(bestLabelText, alpha);
            SetCanvasGroupAlpha(bestScoreCapsuleGroup, alpha);
        }

        private void SetPanelScale(float scale)
        {
            if (panelRect != null)
            {
                panelRect.localScale = Vector3.one * scale;
            }
        }

        private void SetImpactOverlayAlpha(float alpha)
        {
            if (impactOverlayImage == null)
            {
                return;
            }

            impactOverlayImage.color = new Color(0.035f, 0.05f, 0.085f, Mathf.Clamp01(alpha));
        }

        private static float EvaluatePanelScale(float value)
        {
            const float peakTime = 0.72f;
            if (value <= peakTime)
            {
                return Mathf.Lerp(0.95f, 1.02f, EaseOutCubic(value / peakTime));
            }

            return Mathf.Lerp(1.02f, 1f, EaseOutCubic((value - peakTime) / (1f - peakTime)));
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            Color color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }

        private static void SetCanvasGroupAlpha(
            CanvasGroup canvasGroup,
            float alpha)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        private static void SetScale(RectTransform rect, float scale)
        {
            if (rect != null)
            {
                rect.localScale = Vector3.one * scale;
            }
        }

        private void DisableLegacyVisuals()
        {
            if (root == null)
            {
                return;
            }

            Transform panel = root.transform.Find("GameOverPanel");
            if (panel != null)
            {
                for (int i = 0; i < LegacyObjectNames.Length; i++)
                {
                    Transform legacy = FindDeep(panel, LegacyObjectNames[i]);
                    if (legacy != null)
                    {
                        legacy.gameObject.SetActive(false);
                    }
                }
            }

            if (restartButton == null)
            {
                return;
            }

            Transform restartTransform = restartButton.transform;
            for (int i = 0; i < restartTransform.childCount; i++)
            {
                Transform child = restartTransform.GetChild(i);
                if (child.name != "PlayIcon")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static Transform FindDeep(Transform parent, string objectName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == objectName)
                {
                    return child;
                }

                Transform nested = FindDeep(child, objectName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void WireRestartButtonOnce()
        {
            if (restartButton == null || runtimeRestartListenerAdded)
            {
                return;
            }

            restartButton.onClick.AddListener(HandleRestartClicked);
            runtimeRestartListenerAdded = true;
        }

        private void HandleRestartClicked()
        {
            if (gameManager == null)
            {
                return;
            }

            restartButton.interactable = false;
            gameManager.RestartFromGameOver();
        }
    }
}
