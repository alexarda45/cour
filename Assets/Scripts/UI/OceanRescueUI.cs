using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class OceanRescueUI : MonoBehaviour
    {
        private const float OpenDuration = 0.25f;
        private const float CloseDuration = 0.18f;
        private const float PreviewTileSize = 106f;
        private const float PreviewTilePitch = 112f;
        private static readonly Vector2 GardenArtworkSize = new Vector2(1080f, 1349f);
        private static readonly string[] OceanArtworkNames =
        {
            "OceanRescuePanel",
            "OceanRescueTitle",
            "ContinueText",
            "RescuePreviewPanel"
        };

        [SerializeField] private GameObject root;
        [SerializeField] private Image dimBackground;
        [SerializeField] private RectTransform popupRoot;
        [SerializeField] private CanvasGroup popupCanvasGroup;
        [SerializeField] private RectTransform[] previewPieceRoots;
        [SerializeField] private Button watchAdButton;
        [SerializeField] private Button noThanksButton;

        private OceanRescueController controller;
        private Coroutine transitionRoutine;
        private bool listenersAdded;
        private float dimTargetAlpha = 0.65f;
        private Image themedArtworkImage;
        private Image[] oceanArtworkImages;

        private readonly struct RescueArtworkFit
        {
            public readonly Vector2 Size;
            public readonly Vector2 Position;

            public RescueArtworkFit(Vector2 size, Vector2 position)
            {
                Size = size;
                Position = position;
            }
        }

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

            if (root == null)
            {
                root = gameObject;
            }

            root.SetActive(true);
            root.transform.SetAsLastSibling();
            ConfigureThemeArtwork();
            RenderPreview(rescueSet);
            SetButtonsInteractable(true);

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

        private void ConfigureThemeArtwork()
        {
            if (popupRoot == null)
            {
                return;
            }

            CacheOceanArtworkImages();
            ThemeAssetSet theme = ThemeCatalog.Current;
            Sprite themedSprite = theme == null || theme.ThemeType == ThemeType.Ocean
                ? null
                : theme.RescuePanelSprite;
            bool useThemedArtwork = themedSprite != null;

            for (int i = 0; i < oceanArtworkImages.Length; i++)
            {
                if (oceanArtworkImages[i] != null)
                {
                    oceanArtworkImages[i].enabled = !useThemedArtwork;
                }
            }

            if (!useThemedArtwork)
            {
                if (themedArtworkImage != null)
                {
                    themedArtworkImage.gameObject.SetActive(false);
                }

                return;
            }

            EnsureThemedArtworkImage();
            RescueArtworkFit artworkFit = GetArtworkFit(theme.ThemeType);
            RectTransform artworkRect = themedArtworkImage.rectTransform;
            artworkRect.sizeDelta = artworkFit.Size;
            artworkRect.anchoredPosition = artworkFit.Position;
            themedArtworkImage.sprite = themedSprite;
            themedArtworkImage.color = Color.white;
            themedArtworkImage.preserveAspect = true;
            themedArtworkImage.raycastTarget = false;
            themedArtworkImage.gameObject.SetActive(true);
            themedArtworkImage.transform.SetSiblingIndex(0);
        }

        private void CacheOceanArtworkImages()
        {
            if (oceanArtworkImages != null)
            {
                return;
            }

            oceanArtworkImages = new Image[OceanArtworkNames.Length];
            for (int i = 0; i < OceanArtworkNames.Length; i++)
            {
                Transform artwork = popupRoot.Find(OceanArtworkNames[i]);
                oceanArtworkImages[i] = artwork == null ? null : artwork.GetComponent<Image>();
            }
        }

        private void EnsureThemedArtworkImage()
        {
            if (themedArtworkImage != null)
            {
                return;
            }

            Transform existing = popupRoot.Find("ThemedRescueArtwork");
            if (existing != null)
            {
                themedArtworkImage = existing.GetComponent<Image>();
            }

            if (themedArtworkImage == null)
            {
                GameObject artworkObject = new GameObject(
                    "ThemedRescueArtwork",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                RectTransform artworkRect = artworkObject.GetComponent<RectTransform>();
                artworkRect.SetParent(popupRoot, false);
                artworkRect.anchorMin = new Vector2(0.5f, 0.5f);
                artworkRect.anchorMax = new Vector2(0.5f, 0.5f);
                artworkRect.pivot = new Vector2(0.5f, 0.5f);
                artworkRect.anchoredPosition = Vector2.zero;
                artworkRect.sizeDelta = GardenArtworkSize;
                themedArtworkImage = artworkObject.GetComponent<Image>();
                themedArtworkImage.type = Image.Type.Simple;
            }

            themedArtworkImage.raycastTarget = false;
        }

        private static RescueArtworkFit GetArtworkFit(ThemeType themeType)
        {
            switch (themeType)
            {
                case ThemeType.Neon: // Blossom
                    return new RescueArtworkFit(
                        new Vector2(1012f, 1264f),
                        new Vector2(-21f, 40f));
                case ThemeType.Gold: // Desert
                    return new RescueArtworkFit(
                        new Vector2(983f, 1228f),
                        new Vector2(-17f, 49f));
                case ThemeType.Candy:
                    return new RescueArtworkFit(
                        new Vector2(1024f, 1279f),
                        new Vector2(-15f, 24f));
                case ThemeType.Aqua: // Beach
                    return new RescueArtworkFit(
                        new Vector2(1004f, 1255f),
                        new Vector2(-20f, 46f));
                case ThemeType.Crystal: // Garden is the approved custom-art reference.
                default:
                    return new RescueArtworkFit(GardenArtworkSize, Vector2.zero);
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
