using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChromaBlast
{
    [RequireComponent(typeof(CanvasGroup))]
    public class PieceView : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform blockRoot;
        [SerializeField] private BlockView blockPrefab;
        [SerializeField] private float dragLiftPixels = 82f;
        [SerializeField] private float minimumTouchSize = 128f;

        private const float TrayPieceVisualFill = 0.98f;

        private CanvasGroup canvasGroup;
        private Image hitArea;
        private Image moveBadgeBackground;
        private TMP_Text moveBadgeText;
        private RectTransform dragLayer;
        private RectTransform originalParent;
        private Vector2 originalAnchoredPosition;
        private GameManager gameManager;
        private float currentCellSize;
        private bool dragging;
        private bool canCurrentlyFit = true;
        private bool canClearLineNow;
        private int clearOpportunityCount;
        private int lastPreviewLineCount = -1;
        private int lastPreviewPureLineCount = -1;
        private Coroutine invalidDropRoutine;

        public PieceInstance Instance { get; private set; }
        public TraySlot SourceSlot { get; private set; }
        public RectTransform RectTransform { get; private set; }

        private void Awake()
        {
            RectTransform = (RectTransform)transform;
            canvasGroup = GetComponent<CanvasGroup>();
            EnsureHitArea();
            if (blockRoot == null)
            {
                blockRoot = RectTransform;
            }
        }

        private void OnDestroy()
        {
            gameManager?.Board?.ClearPreview();
            StopInvalidDropFlash();
        }

        private void OnDisable()
        {
            gameManager?.Board?.ClearPreview();
        }

        public void Initialize(
            PieceInstance instance,
            TraySlot sourceSlot,
            GameManager owner,
            BlockView visualPrefab,
            RectTransform dragLayerRoot,
            float sharedPreviewCellSize)
        {
            Instance = instance;
            SourceSlot = sourceSlot;
            gameManager = owner;
            blockPrefab = visualPrefab;
            dragLayer = dragLayerRoot;
            currentCellSize = Mathf.Max(1f, sharedPreviewCellSize);
            RebuildBlocks(currentCellSize);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Reinforce the per-piece zero-threshold setting at press time in case
            // an input module refreshes PointerEventData between initialization and drag.
            eventData.useDragThreshold = false;
            AudioManager.Instance?.PlayClick();
            if (!canCurrentlyFit)
            {
                ShakeUnavailable();
                gameManager?.ShowNoFitPieceHint();
                Haptics.Light();
                return;
            }

            // The drag threshold is already zero, so there is no positional tween to
            // remove. Give the touch an immediate pickup response without starting a
            // drag on a simple tap; OnBeginDrag restores the board-drag scale.
            if (RectTransform != null && invalidDropRoutine == null && !dragging)
            {
                RectTransform.DOKill();
                RectTransform.localScale = Vector3.one * 1.04f;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!dragging && RectTransform != null && invalidDropRoutine == null)
            {
                RectTransform.localScale = Vector3.one;
            }
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            // The EventSystem is already configured with a 1 px global threshold.
            // Disable that final threshold only for tray pieces so their visual drag
            // begins on the first pointer movement without affecting other UI controls.
            eventData.useDragThreshold = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Instance == null || gameManager == null || !gameManager.CanInteract || invalidDropRoutine != null)
            {
                return;
            }

            if (!canCurrentlyFit)
            {
                ShakeUnavailable();
                gameManager.ShowNoFitPieceHint();
                Haptics.Light();
                return;
            }

            gameManager.ClearMoveHint();
            gameManager.Board.ClearPreview();
            dragging = true;
            SetMoveBadgeVisible(false);
            lastPreviewLineCount = -1;
            lastPreviewPureLineCount = -1;
            originalParent = (RectTransform)transform.parent;
            originalAnchoredPosition = RectTransform.anchoredPosition;

            if (dragLayer != null)
            {
                dragLayer.SetAsLastSibling();
                transform.SetParent(dragLayer, true);
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 1f;
            RectTransform.SetAsLastSibling();
            SetDragGlow(false, true);
            ApplyBoardDragLayout();
            RectTransform.localScale = Vector3.one;
            MoveToPointer(eventData);
            UpdatePreview(eventData);
            UpdateDragVisibility(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            MoveToPointer(eventData);
            UpdatePreview(eventData);
            UpdateDragVisibility(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            gameManager.Board.ClearPreview();
            lastPreviewLineCount = -1;
            lastPreviewPureLineCount = -1;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            Vector2 liftedPosition = GetLiftedScreenPosition(eventData);
            if (!gameManager.Board.ContainsScreenPoint(liftedPosition, eventData.pressEventCamera))
            {
                ReturnToSlot();
                PlayInvalidDropFlash();
                return;
            }

            Vector2Int origin = gameManager.Board.GetSnappedOriginFromScreenPoint(Instance, liftedPosition, eventData.pressEventCamera);
            bool placed = gameManager.TryPlacePiece(this, origin);
            if (!placed)
            {
                ReturnToSlot();
                PlayInvalidDropFlash();
            }
        }

        public void SetCanFitNow(bool canFit)
        {
            SetCanFitNow(canFit, false);
        }

        public void SetCanFitNow(bool canFit, bool clearsLineNow)
        {
            SetCanFitNow(canFit, clearsLineNow ? 1 : 0);
        }

        public void SetCanFitNow(bool canFit, int clearOpportunities)
        {
            canCurrentlyFit = canFit;
            clearOpportunityCount = Mathf.Max(0, clearOpportunities);
            canClearLineNow = canFit && clearOpportunityCount > 0;
            if (!dragging)
            {
                ApplyFitVisual();
            }
        }

        public void PlayHintPulse()
        {
            if (RectTransform == null || dragging || !canCurrentlyFit)
            {
                return;
            }

            RectTransform.DOKill();
            RectTransform.localScale = Vector3.one;
            RectTransform.DOPunchScale(Vector3.one * 0.12f, 0.28f, 7, 0.55f).SetEase(Ease.OutQuad);
        }

        public void PlaySpawnReveal(float delay)
        {
            if (RectTransform == null)
            {
                return;
            }

            RectTransform.DOKill();
            RectTransform.localScale = Vector3.one;
            ApplyFitVisual();
        }

        public void ReturnToSlot()
        {
            gameManager?.Board.ClearPreview();
            transform.DOKill();
            transform.SetParent(originalParent != null ? originalParent : SourceSlot.PieceContainer, true);
            ApplyBlockLayout(currentCellSize, currentCellSize * TrayPieceVisualFill);
            RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            RectTransform.pivot = new Vector2(0.5f, 0.5f);
            RectTransform.DOAnchorPos(originalAnchoredPosition, 0.10f).SetEase(Ease.OutQuad);
            RectTransform.DOScale(1f, 0.10f).SetEase(Ease.OutQuad);
            SetDragGlow(false, true);
            ApplyFitVisual();
        }

        public void Consume()
        {
            SourceSlot?.ClearReference(this);
            gameManager?.Board.ClearPreview();
            transform.DOKill();
            StopInvalidDropFlash();
            Destroy(gameObject);
        }

        private void MoveToPointer(PointerEventData eventData)
        {
            Vector2 liftedPosition = GetLiftedScreenPosition(eventData);
            if (dragLayer == null)
            {
                RectTransform.position = liftedPosition;
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, liftedPosition, eventData.pressEventCamera, out Vector2 localPoint);
            RectTransform.anchoredPosition = localPoint;
        }

        private void UpdatePreview(PointerEventData eventData)
        {
            Vector2 liftedPosition = GetLiftedScreenPosition(eventData);
            Vector2Int origin = gameManager.Board.GetSnappedOriginFromScreenPoint(Instance, liftedPosition, eventData.pressEventCamera);
            bool overBoard = gameManager.Board.ContainsScreenPoint(liftedPosition, eventData.pressEventCamera);
            if (overBoard)
            {
                gameManager.Board.ShowPreview(Instance, origin);
            }
            else
            {
                gameManager.Board.ClearPreview();
            }

            int pureLines;
            int lineCount = gameManager.Board.GetPlacementClearPreview(Instance, origin, out pureLines);
            if (lineCount > 0 && (lineCount != lastPreviewLineCount || pureLines != lastPreviewPureLineCount))
            {
                gameManager.ShowPlacementPreview(lineCount, pureLines);
            }

            lastPreviewLineCount = lineCount;
            lastPreviewPureLineCount = pureLines;
        }

        private void UpdateDragVisibility(PointerEventData eventData)
        {
            if (gameManager == null || gameManager.Board == null)
            {
                return;
            }

            Vector2 liftedPosition = GetLiftedScreenPosition(eventData);
            bool overBoard = gameManager.Board.ContainsScreenPoint(liftedPosition, eventData.pressEventCamera);
            bool canPlace = false;
            if (overBoard && Instance != null)
            {
                Vector2Int origin = gameManager.Board.GetSnappedOriginFromScreenPoint(Instance, liftedPosition, eventData.pressEventCamera);
                canPlace = gameManager.Board.CanPlace(Instance, origin);
            }

            canvasGroup.alpha = 1f;
            SetDragGlow(overBoard, canPlace);
        }

        private Vector2 GetLiftedScreenPosition(PointerEventData eventData)
        {
            return eventData.position + Vector2.up * dragLiftPixels;
        }

        private void ApplyFitVisual()
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = 1f;
            RefreshMoveBadge();
            SetIdleGlow();
        }

        private void ShakeUnavailable()
        {
            if (RectTransform == null || dragging)
            {
                return;
            }

            RectTransform.DOKill();
            RectTransform.DOShakePosition(0.12f, 8f, 8, 0.45f);
        }

        private void SetIdleGlow()
        {
            if (hitArea == null || dragging)
            {
                return;
            }

            hitArea.color = Color.clear;
        }

        private void SetDragGlow(bool overBoard, bool validPlacement)
        {
            if (hitArea == null)
            {
                return;
            }

            hitArea.color = Color.clear;
        }

        private void PlayInvalidDropFlash()
        {
            if (hitArea == null)
            {
                return;
            }

            StopInvalidDropFlash();
            invalidDropRoutine = StartCoroutine(InvalidDropFlashRoutine());
        }

        private void StopInvalidDropFlash()
        {
            if (invalidDropRoutine != null)
            {
                StopCoroutine(invalidDropRoutine);
                invalidDropRoutine = null;
            }
        }

        private IEnumerator InvalidDropFlashRoutine()
        {
            Image[] tintTargets = blockRoot == null
                ? GetComponentsInChildren<Image>(true)
                : blockRoot.GetComponentsInChildren<Image>(true);
            Color[] originalColors = new Color[tintTargets.Length];
            for (int i = 0; i < tintTargets.Length; i++)
            {
                originalColors[i] = tintTargets[i] == null ? Color.white : tintTargets[i].color;
            }

            Color softRed = new Color(1f, 0.24f, 0.30f, 1f);
            float elapsed = 0f;
            const float returnDuration = 0.10f;
            const float tintDuration = 0.12f;
            const float shakeDuration = 0.18f;
            const float totalDuration = returnDuration + shakeDuration;
            bool shakeStarted = false;

            while (elapsed < totalDuration && RectTransform != null)
            {
                elapsed += Time.deltaTime;

                float tintT = Mathf.Clamp01(elapsed / tintDuration);
                float tintAmount = (1f - tintT) * 0.34f;
                for (int i = 0; i < tintTargets.Length; i++)
                {
                    if (tintTargets[i] != null && tintTargets[i] != hitArea)
                    {
                        tintTargets[i].color = Color.Lerp(originalColors[i], softRed, tintAmount);
                    }
                }

                if (elapsed >= returnDuration)
                {
                    if (!shakeStarted)
                    {
                        shakeStarted = true;
                        RectTransform.DOKill();
                        RectTransform.anchoredPosition = originalAnchoredPosition;
                        RectTransform.localScale = Vector3.one;
                    }

                    float shakeT = Mathf.Clamp01((elapsed - returnDuration) / shakeDuration);
                    float offset = Mathf.Sin(shakeT * Mathf.PI * 7f) * 8f * (1f - shakeT);
                    RectTransform.anchoredPosition = originalAnchoredPosition + Vector2.right * offset;
                }

                yield return null;
            }

            for (int i = 0; i < tintTargets.Length; i++)
            {
                if (tintTargets[i] != null && tintTargets[i] != hitArea)
                {
                    tintTargets[i].color = originalColors[i];
                }
            }

            if (RectTransform != null)
            {
                RectTransform.anchoredPosition = originalAnchoredPosition;
                RectTransform.localScale = Vector3.one;
            }

            invalidDropRoutine = null;
            ApplyFitVisual();
        }

        private void RebuildBlocks(float cellSize)
        {
            for (int i = blockRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(blockRoot.GetChild(i).gameObject);
            }

            if (Instance == null || blockPrefab == null)
            {
                return;
            }

            PieceData data = Instance.Data;
            Vector2 visualSize = new Vector2(data.width * cellSize, data.height * cellSize);
            Vector2 hitSize = new Vector2(Mathf.Max(minimumTouchSize, visualSize.x), Mathf.Max(minimumTouchSize, visualSize.y));
            Vector2 visualOffset = (hitSize - visualSize) * 0.5f;
            RectTransform.sizeDelta = hitSize;

            for (int i = 0; i < data.cells.Length; i++)
            {
                Vector2Int cell = data.cells[i];
                BlockView block = Instantiate(blockPrefab, blockRoot);
                block.gameObject.SetActive(true);
                block.name = $"PieceBlock_{cell.x}_{cell.y}";
                RectTransform blockRect = (RectTransform)block.transform;
                blockRect.anchorMin = Vector2.zero;
                blockRect.anchorMax = Vector2.zero;
                blockRect.pivot = new Vector2(0.5f, 0.5f);
                blockRect.sizeDelta = Vector2.one * (cellSize * TrayPieceVisualFill);
                blockRect.anchoredPosition = visualOffset + new Vector2((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize);
                block.Initialize(Instance.color, false);
                block.SetTrayShadowVisible(true);
            }

            EnsureMoveBadge();
            RefreshMoveBadge();
        }

        private void ApplyBoardDragLayout()
        {
            if (gameManager == null || gameManager.Board == null)
            {
                return;
            }

            ApplyBlockLayout(gameManager.Board.CellSize, gameManager.Board.CellVisualSize);
        }

        private void ApplyBlockLayout(float cellPitch, float blockSize)
        {
            if (Instance == null || blockRoot == null)
            {
                return;
            }

            float safePitch = Mathf.Max(1f, cellPitch);
            float safeBlockSize = Mathf.Clamp(blockSize, 1f, safePitch);
            PieceData data = Instance.Data;
            Vector2 visualSize = new Vector2(data.width * safePitch, data.height * safePitch);
            Vector2 hitSize = new Vector2(Mathf.Max(minimumTouchSize, visualSize.x), Mathf.Max(minimumTouchSize, visualSize.y));
            Vector2 visualOffset = (hitSize - visualSize) * 0.5f;
            RectTransform.sizeDelta = hitSize;

            for (int i = 0; i < data.cells.Length; i++)
            {
                Vector2Int cell = data.cells[i];
                Transform blockTransform = blockRoot.Find($"PieceBlock_{cell.x}_{cell.y}");
                RectTransform blockRect = blockTransform as RectTransform;
                if (blockRect == null)
                {
                    continue;
                }

                blockRect.sizeDelta = Vector2.one * safeBlockSize;
                blockRect.anchoredPosition = visualOffset + new Vector2((cell.x + 0.5f) * safePitch, (cell.y + 0.5f) * safePitch);
            }
        }

        private void EnsureHitArea()
        {
            hitArea = GetComponent<Image>();
            if (hitArea == null)
            {
                hitArea = gameObject.AddComponent<Image>();
            }

            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
        }

        private void EnsureMoveBadge()
        {
            Transform existing = transform.Find("MoveBadge");
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
            moveBadgeBackground = null;
            moveBadgeText = null;
        }

        private void RefreshMoveBadge()
        {
            EnsureMoveBadge();
            if (moveBadgeBackground == null || moveBadgeText == null)
            {
                return;
            }

            if (dragging)
            {
                SetMoveBadgeVisible(false);
                return;
            }

            if (!canCurrentlyFit)
            {
                moveBadgeText.text = "X";
                moveBadgeText.color = new Color(1f, 0.92f, 0.95f, 1f);
                moveBadgeBackground.color = new Color(1f, 0.12f, 0.24f, 0.72f);
                SetMoveBadgeVisible(true);
                return;
            }

            if (canClearLineNow)
            {
                moveBadgeText.text = clearOpportunityCount > 1 ? $"x{Mathf.Min(9, clearOpportunityCount)}" : "LINIE";
                moveBadgeText.color = new Color(0.02f, 0.03f, 0.08f, 1f);
                moveBadgeBackground.color = clearOpportunityCount > 1
                    ? new Color(1f, 0.31f, 0.85f, 0.88f)
                    : new Color(0.1f, 0.9f, 1f, 0.88f);
                SetMoveBadgeVisible(true);
                return;
            }

            SetMoveBadgeVisible(false);
        }

        private void SetMoveBadgeVisible(bool visible)
        {
            if (moveBadgeBackground == null)
            {
                return;
            }

            moveBadgeBackground.gameObject.SetActive(visible);
            if (visible)
            {
                moveBadgeBackground.transform.SetAsLastSibling();
            }
        }
    }
}
