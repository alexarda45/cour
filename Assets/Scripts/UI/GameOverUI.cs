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
        private const float ButtonRevealDuration = 0.15f;

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

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            DisableLegacyVisuals();
            CacheRestartFinalPosition();
        }

        public void Initialize(GameManager owner)
        {
            gameManager = owner;
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
            root.SetActive(true);
            root.transform.SetAsLastSibling();
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
                scoreValueText.text = "0";
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
            if (finalScore <= 0)
            {
                SetScore(0);
                RestoreFinalVisualState();
                return;
            }

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
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private IEnumerator AnimateEntrance(int finalScore)
        {
            float scoreDuration = GetScoreDuration(finalScore);
            float elapsed = 0f;

            yield return null;

            while (elapsed < scoreDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float scoreT = Mathf.Clamp01(elapsed / scoreDuration);
                float scoreEase = EaseOutCubic(scoreT);
                int displayedScore =
                    (int)Math.Round(finalScore * (double)scoreEase);
                if (scoreT < 1f)
                {
                    displayedScore = Mathf.Min(displayedScore, finalScore - 1);
                }

                SetScore(displayedScore);

                float scoreScale = 1f + Mathf.Sin(scoreT * Mathf.PI) * 0.025f;
                SetScale(
                    scoreValueText == null ? null : scoreValueText.rectTransform,
                    scoreScale);

                if (scoreT >= 1f)
                {
                    break;
                }

                yield return null;
            }

            SetScore(finalScore);

            float buttonElapsed = 0f;
            while (buttonElapsed < ButtonRevealDuration)
            {
                buttonElapsed += Time.unscaledDeltaTime;
                float buttonProgress =
                    Mathf.Clamp01(buttonElapsed / ButtonRevealDuration);
                float buttonT = EaseOutCubic(buttonProgress);
                SetCanvasGroupAlpha(restartButtonGroup, buttonT);

                RectTransform restartRect =
                    restartButton == null ? null : restartButton.transform as RectTransform;
                if (restartRect != null)
                {
                    restartRect.localScale =
                        Vector3.one * Mathf.Lerp(0.92f, 1f, buttonT);
                }

                SetScale(
                    scoreValueText == null ? null : scoreValueText.rectTransform,
                    1f + Mathf.Sin(buttonProgress * Mathf.PI) * 0.055f);

                yield return null;
            }

            SetScore(finalScore);
            RestoreFinalVisualState();
            entranceRoutine = null;
        }

        private void PrepareEntranceState()
        {
            SetGraphicAlpha(backgroundImage, 1f);
            SetGraphicAlpha(titleText, 1f);
            SetScale(titleText == null ? null : titleText.rectTransform, 1f);

            if (newBestAccent != null)
            {
                SetGraphicAlpha(newBestAccent, 1f);
                SetScale(newBestAccent.rectTransform, 1f);
            }

            SetGraphicAlpha(scoreValueText, 1f);
            SetScale(scoreValueText == null ? null : scoreValueText.rectTransform, 1f);
            SetGraphicAlpha(bestLabelText, 1f);
            SetCanvasGroupAlpha(bestScoreCapsuleGroup, 1f);
            SetScale(
                bestScoreCapsuleGroup == null
                    ? null
                    : bestScoreCapsuleGroup.transform as RectTransform,
                1f);
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
            SetGraphicAlpha(backgroundImage, 1f);
            SetGraphicAlpha(titleText, 1f);
            SetScale(titleText == null ? null : titleText.rectTransform, 1f);
            SetGraphicAlpha(scoreValueText, 1f);
            SetScale(scoreValueText == null ? null : scoreValueText.rectTransform, 1f);
            SetGraphicAlpha(bestLabelText, 1f);
            SetCanvasGroupAlpha(bestScoreCapsuleGroup, 1f);
            SetScale(
                bestScoreCapsuleGroup == null
                    ? null
                    : bestScoreCapsuleGroup.transform as RectTransform,
                1f);

            if (newBestAccent != null)
            {
                SetGraphicAlpha(newBestAccent, 1f);
                SetScale(newBestAccent.rectTransform, 1f);
            }

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

        private static float GetScoreDuration(int finalScore)
        {
            int digits = finalScore <= 0
                ? 1
                : Mathf.FloorToInt(Mathf.Log10(finalScore)) + 1;
            return Mathf.Lerp(
                0.62f,
                0.95f,
                Mathf.InverseLerp(1f, 6f, Mathf.Clamp(digits, 1, 6)));
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
            gameManager.RestartCurrentMode();
        }
    }
}
