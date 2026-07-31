using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class TraySlot : MonoBehaviour
    {
        [SerializeField] private RectTransform pieceContainer;
        [SerializeField] private Image slotGlow;
        [SerializeField] private Image slotPanel;
        [SerializeField] private Image slotInnerShadow;
        [SerializeField] private Image slotRim;

        public PieceView CurrentPiece { get; private set; }
        public RectTransform PieceContainer => pieceContainer;
        public bool IsEmpty => CurrentPiece == null;

        private void Awake()
        {
            if (pieceContainer == null)
            {
                pieceContainer = (RectTransform)transform;
            }

            EnsureTrayVisuals();
        }

        public void SetPiece(PieceView piece)
        {
            RemoveStalePieceVisuals(piece);
            CurrentPiece = piece;
            if (piece == null)
            {
                return;
            }

            piece.transform.SetParent(pieceContainer, false);
            RectTransform rectTransform = piece.RectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.SetAsLastSibling();
        }

        public PieceInstance GetPieceSnapshot()
        {
            return CurrentPiece == null ? null : CurrentPiece.Instance.Clone();
        }

        public void ClearReference(PieceView piece)
        {
            if (CurrentPiece == piece)
            {
                CurrentPiece = null;
            }
        }

        public void ClearAndDestroy()
        {
            PieceView removedPiece = CurrentPiece;
            if (CurrentPiece != null)
            {
                CurrentPiece.gameObject.SetActive(false);
                Destroy(CurrentPiece.gameObject);
                CurrentPiece = null;
            }

            RemoveStalePieceVisuals(removedPiece);
        }

        private void RemoveStalePieceVisuals(PieceView pieceToKeep)
        {
            if (pieceContainer == null)
            {
                return;
            }

            PieceView[] pieceViews = pieceContainer.GetComponentsInChildren<PieceView>(true);
            for (int i = 0; i < pieceViews.Length; i++)
            {
                PieceView pieceView = pieceViews[i];
                if (pieceView == null || pieceView == pieceToKeep)
                {
                    continue;
                }

                pieceView.gameObject.SetActive(false);
                Destroy(pieceView.gameObject);
            }
        }

        private void EnsureTrayVisuals()
        {
            RectTransform slotRect = transform as RectTransform;
            if (slotRect == null)
            {
                return;
            }

            slotGlow = FindSlotLayer(slotGlow, "TraySlotGlow");
            slotPanel = FindSlotLayer(slotPanel, "TraySlotPanel");
            slotInnerShadow = FindSlotLayer(slotInnerShadow, "TraySlotInnerShadow");
            slotRim = FindSlotLayer(slotRim, "TraySlotRim");

            ConfigureSlotRoot(slotRect);
            SetSlotLayerVisible(slotGlow, false);
            SetSlotLayerVisible(slotPanel, false);
            SetSlotLayerVisible(slotInnerShadow, false);
            SetSlotLayerVisible(slotRim, false);
        }

        private static void SetSlotLayerVisible(Image image, bool visible)
        {
            if (image != null)
            {
                image.gameObject.SetActive(visible);
                image.raycastTarget = false;
            }
        }

        private static void ConfigureSlotRoot(RectTransform slotRect)
        {
            slotRect.localScale = Vector3.one;

            ContentSizeFitter fitter = slotRect.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }

            Image rootImage = slotRect.GetComponent<Image>();
            if (rootImage != null)
            {
                UISpriteFactory.ApplyRounded(rootImage, 0.34f);
                rootImage.color = Color.clear;
                rootImage.raycastTarget = false;
            }

            Outline outline = slotRect.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = Color.clear;
                outline.effectDistance = Vector2.zero;
                outline.useGraphicAlpha = true;
            }

            Shadow shadow = FindPlainShadow(slotRect.gameObject);
            if (shadow != null)
            {
                shadow.effectColor = Color.clear;
                shadow.effectDistance = Vector2.zero;
                shadow.useGraphicAlpha = true;
            }
        }

        private static Shadow FindPlainShadow(GameObject target)
        {
            Shadow[] shadows = target == null ? null : target.GetComponents<Shadow>();
            if (shadows == null)
            {
                return null;
            }

            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    return shadows[i];
                }
            }

            return null;
        }

        private Image EnsureSlotLayer(Image image, string layerName)
        {
            if (image == null)
            {
                Transform existing = transform.Find(layerName);
                image = existing == null ? null : existing.GetComponent<Image>();
            }

            if (image != null)
            {
                image.gameObject.SetActive(true);
                image.raycastTarget = false;
                return image;
            }

            GameObject layer = new GameObject(layerName, typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(transform, false);
            image = layer.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private Image FindSlotLayer(Image image, string layerName)
        {
            if (image != null)
            {
                return image;
            }

            Transform existing = transform.Find(layerName);
            return existing == null ? null : existing.GetComponent<Image>();
        }

        private void ConfigureSlotLayer(Image image, Color color, Vector2 offsetMin, Vector2 offsetMax, float radius, bool frameOnly)
        {
            if (image == null)
            {
                return;
            }

            if (frameOnly)
            {
                UISpriteFactory.ApplyFrame(image, radius, 0.035f);
                image.fillCenter = false;
            }
            else
            {
                UISpriteFactory.ApplyRounded(image, radius);
                image.fillCenter = true;
            }

            image.color = color;
            image.raycastTarget = false;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
