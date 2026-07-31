using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class OceanRescueUI : MonoBehaviour
    {
        private const float OpenDuration = 0.25f;
        private const float CloseDuration = 0.18f;
        private const float FeedbackDuration = 2.0f;
        private const float PreviewTileSize = 106f;
        private const float PreviewTilePitch = 112f;

        [SerializeField] private GameObject root;
        [SerializeField] private Image dimBackground;
        [SerializeField] private RectTransform popupRoot;
        [SerializeField] private CanvasGroup popupCanvasGroup;
        [SerializeField] private RectTransform[] previewPieceRoots;
        [SerializeField] private Button watchAdButton;
        [SerializeField] private Button noThanksButton;
        [SerializeField] private TMP_Text feedbackText;

        private OceanRescueController controller;
        private Coroutine transitionRoutine;
        private Coroutine feedbackRoutine;
        private bool listenersAdded;
        private float dimTargetAlpha = 0.65f;

        public bool IsVisible => root != null && root.activeSelf;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (dimBackground != null)
            {
                dimTargetAlpha = dimBackground.color.a;
            }
        }

        private void OnDestroy()
        {
            if (!listenersAdded)
            {
                return;
            }

            if (watchAdButton != null)
            {
                watchAdButton.onClick.RemoveListener(HandleWatchAdClicked);
            }

            if (noThanksButton != null)
            {
                noThanksButton.onClick.RemoveListener(HandleNoThanksClicked);
            }
        }

        public void Initialize(OceanRescueController owner)
        {
            controller = owner;
            if (root == null)
            {
                root = gameObject;
            }

            if (dimBackground != null)
            {
                dimTargetAlpha = dimBackground.color.a;
            }

            WireButtonsOnce();
        }

        public void Show(PieceInstance[] rescueSet)
        {
            StopTransition();
            StopFeedback();

            if (root == null)
            {
                root = gameObject;
            }

            root.SetActive(true);
            root.transform.SetAsLastSibling();
            RenderPreview(rescueSet);
            SetButtonsInteractable(true);
            SetFeedbackVisible(false);

            SetImageAlpha(dimBackground, 0f);
            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.alpha = 0f;
                popupCanvasGroup.interactable = true;
                popupCanvasGroup.blocksRaycasts = true;
            }

            if (popupRoot != null)
            {
                popupRoot.localScale = Vector3.one * 0.88f;
            }

            transitionRoutine = StartCoroutine(OpenRoutine());
        }

        public void CloseAnimated(Action onComplete)
        {
            StopTransition();
            StopFeedback();
            SetButtonsInteractable(false);

            if (root == null || !root.activeSelf)
            {
                onComplete?.Invoke();
                return;
            }

            transitionRoutine = StartCoroutine(CloseRoutine(onComplete));
        }

        public void HideImmediate()
        {
            StopTransition();
            StopFeedback();
            SetFeedbackVisible(false);
            SetButtonsInteractable(false);

            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.alpha = 1f;
                popupCanvasGroup.interactable = false;
                popupCanvasGroup.blocksRaycasts = false;
            }

            if (popupRoot != null)
            {
                popupRoot.localScale = Vector3.one;
            }

            SetImageAlpha(dimBackground, dimTargetAlpha);
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void SetButtonsInteractable(bool interactable)
        {
            if (watchAdButton != null)
            {
                watchAdButton.interactable = interactable;
            }

            if (noThanksButton != null)
            {
                noThanksButton.interactable = interactable;
            }
        }

        public void ShowAdUnavailable()
        {
            SetButtonsInteractable(true);
            StopFeedback();
            if (feedbackText == null)
            {
                return;
            }

            feedbackText.text = "Ad unavailable. Try again.";
            SetFeedbackVisible(true);
            feedbackRoutine = StartCoroutine(HideFeedbackAfterDelay());
        }

        private IEnumerator OpenRoutine()
        {
            float elapsed = 0f;
            while (elapsed < OpenDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / OpenDuration);
                float eased = EaseOutCubic(t);
                SetImageAlpha(dimBackground, Mathf.Lerp(0f, dimTargetAlpha, eased));

                if (popupCanvasGroup != null)
                {
                    popupCanvasGroup.alpha = eased;
                }

                if (popupRoot != null)
                {
                    popupRoot.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, eased);
                }

                yield return null;
            }

            SetImageAlpha(dimBackground, dimTargetAlpha);
            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.alpha = 1f;
            }

            if (popupRoot != null)
            {
                popupRoot.localScale = Vector3.one;
            }

            transitionRoutine = null;
        }

        private IEnumerator CloseRoutine(Action onComplete)
        {
            float startDimAlpha = dimBackground == null ? dimTargetAlpha : dimBackground.color.a;
            float startPopupAlpha = popupCanvasGroup == null ? 1f : popupCanvasGroup.alpha;
            Vector3 startScale = popupRoot == null ? Vector3.one : popupRoot.localScale;
            float elapsed = 0f;

            while (elapsed < CloseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CloseDuration);
                float eased = t * t;
                SetImageAlpha(dimBackground, Mathf.Lerp(startDimAlpha, 0f, eased));

                if (popupCanvasGroup != null)
                {
                    popupCanvasGroup.alpha = Mathf.Lerp(startPopupAlpha, 0f, eased);
                }

                if (popupRoot != null)
                {
                    popupRoot.localScale = Vector3.Lerp(startScale, Vector3.one * 0.92f, eased);
                }

                yield return null;
            }

            transitionRoutine = null;
            HideImmediate();
            onComplete?.Invoke();
        }

        private IEnumerator HideFeedbackAfterDelay()
        {
            float elapsed = 0f;
            while (elapsed < FeedbackDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetFeedbackVisible(false);
            feedbackRoutine = null;
        }

        private void RenderPreview(PieceInstance[] rescueSet)
        {
            if (previewPieceRoots == null)
            {
                return;
            }

            for (int i = 0; i < previewPieceRoots.Length; i++)
            {
                RectTransform pieceRoot = previewPieceRoots[i];
                ClearPreviewChildren(pieceRoot);
                PieceInstance piece = rescueSet != null && i < rescueSet.Length
                    ? rescueSet[i]
                    : null;
                if (pieceRoot == null || piece == null)
                {
                    continue;
                }

                PieceData data = piece.Data;
                float centerX = (data.width - 1) * PreviewTilePitch * 0.5f;
                float centerY = (data.height - 1) * PreviewTilePitch * 0.5f;
                Sprite tileSprite = ChromaPalette.GetTileSprite(piece.color);

                for (int cellIndex = 0; cellIndex < data.cells.Length; cellIndex++)
                {
                    Vector2Int cell = data.cells[cellIndex];
                    GameObject tileObject = new GameObject(
                        $"RescueTile_{cellIndex}",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image));
                    RectTransform tileRect = tileObject.GetComponent<RectTransform>();
                    tileRect.SetParent(pieceRoot, false);
                    tileRect.anchorMin = new Vector2(0.5f, 0.5f);
                    tileRect.anchorMax = new Vector2(0.5f, 0.5f);
                    tileRect.pivot = new Vector2(0.5f, 0.5f);
                    tileRect.sizeDelta = new Vector2(
                        PreviewTileSize,
                        PreviewTileSize);
                    tileRect.anchoredPosition = new Vector2(
                        cell.x * PreviewTilePitch - centerX,
                        cell.y * PreviewTilePitch - centerY);

                    Image tileImage = tileObject.GetComponent<Image>();
                    tileImage.sprite = tileSprite;
                    tileImage.type = Image.Type.Simple;
                    tileImage.preserveAspect = true;
                    tileImage.color = Color.white;
                    tileImage.raycastTarget = false;
                }
            }
        }

        private static void ClearPreviewChildren(RectTransform pieceRoot)
        {
            if (pieceRoot == null)
            {
                return;
            }

            for (int i = pieceRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = pieceRoot.GetChild(i);
                if (child.name.StartsWith("RescueTile_", StringComparison.Ordinal))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void WireButtonsOnce()
        {
            if (listenersAdded)
            {
                return;
            }

            if (watchAdButton != null)
            {
                watchAdButton.onClick.AddListener(HandleWatchAdClicked);
            }

            if (noThanksButton != null)
            {
                noThanksButton.onClick.AddListener(HandleNoThanksClicked);
            }

            listenersAdded = true;
        }

        private void HandleWatchAdClicked()
        {
            controller?.RequestRewardedRescue();
        }

        private void HandleNoThanksClicked()
        {
            controller?.DeclineRescue();
        }

        private void StopTransition()
        {
            if (transitionRoutine == null)
            {
                return;
            }

            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        private void StopFeedback()
        {
            if (feedbackRoutine == null)
            {
                return;
            }

            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }

        private void SetFeedbackVisible(bool visible)
        {
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(visible);
            }
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }
    }
}
