using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace ChromaBlast
{
    public class BoardManager : MonoBehaviour
    {
        private const float FinalBoardGridPaddingX = 16f;
        private const float FinalBoardGridPaddingY = 9.5f;
        // Matches the fixed board tile artwork used by BlockView and the regular
        // placement preview: Cyan, Magenta/Pink, Lime/Blue and Amber/Yellow.
        private static readonly Color32[] CompletionPreviewColors =
        {
            new Color32(6, 213, 233, 255),
            new Color32(233, 5, 43, 255),
            new Color32(5, 73, 233, 255),
            new Color32(233, 178, 0, 255)
        };

        [Header("UI")]
        [SerializeField] private RectTransform boardRoot;
        [SerializeField] private RectTransform blockLayer;
        [SerializeField] private BoardCell cellPrefab;
        [SerializeField] private BlockView blockPrefab;
        [SerializeField] private float cellPadding = 1f;

        [Header("Effects")]
        [SerializeField] private ParticleSystem[] clearParticlesByColor;
        [SerializeField] private CameraShake cameraShake;

        private readonly BoardCell[,] cells = new BoardCell[GameConstants.BoardSize, GameConstants.BoardSize];
        private readonly BlockView[,] blocks = new BlockView[GameConstants.BoardSize, GameConstants.BoardSize];
        private readonly List<BoardCell> generatedCells = new List<BoardCell>();
        private readonly List<BoardCell> previewCells = new List<BoardCell>();
        private readonly List<CompletionFillVisual> completionPreviewPool = new List<CompletionFillVisual>();
        private readonly List<LineGlowVisual> lineGlowPool = new List<LineGlowVisual>();
        private readonly List<IntersectionFlareVisual> intersectionFlarePool = new List<IntersectionFlareVisual>();

        private RectTransform lineClearEffectLayer;
        private RectTransform completionPreviewLayer;
        private Coroutine completionPreviewPulseRoutine;
        private int activeCompletionPreviewFills;
        private int lineClearEffectGeneration;
        private string previewShapeId;
        private Vector2Int previewOrigin = new Vector2Int(int.MinValue, int.MinValue);

        public RectTransform BoardRoot => boardRoot;
        public float CellSize => boardRoot == null
            ? 72f
            : Mathf.Min(
                (boardRoot.rect.width - FinalBoardGridPaddingX * 2f) / GameConstants.BoardSize,
                (boardRoot.rect.height - FinalBoardGridPaddingY * 2f) / GameConstants.BoardSize);
        public float CellVisualSize => Mathf.Max(1f, CellSize - cellPadding * 2f);
        public Vector2 LastClearScreenPosition { get; private set; }

        private void Awake()
        {
            EnsureLayers();
            EnsureRuntimePrefabs();
            BuildCells();
        }

        public void BuildCells()
        {
            if (boardRoot == null || cellPrefab == null)
            {
                return;
            }

            for (int i = generatedCells.Count - 1; i >= 0; i--)
            {
                if (generatedCells[i] != null)
                {
                    Destroy(generatedCells[i].gameObject);
                }
            }

            generatedCells.Clear();

            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    BoardCell cell = Instantiate(cellPrefab, boardRoot);
                    cell.gameObject.SetActive(true);
                    cell.name = $"Cell_{x}_{y}";
                    ConfigureBoardRect((RectTransform)cell.transform, x, y);
                    cell.Configure();
                    cells[x, y] = cell;
                    generatedCells.Add(cell);
                }
            }

            if (blockLayer != null)
            {
                blockLayer.SetAsLastSibling();
            }

            if (lineClearEffectLayer != null)
            {
                lineClearEffectLayer.SetAsLastSibling();
            }

            if (completionPreviewLayer != null)
            {
                completionPreviewLayer.SetAsLastSibling();
            }

            UpdateOpportunityHints();
        }

        public void ClearBoard(bool animated = false)
        {
            ClearPreview();
            ResetLineClearEffects();
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    if (blocks[x, y] == null)
                    {
                        continue;
                    }

                    if (animated)
                    {
                        blocks[x, y].PlayClear(Random.Range(0f, 0.08f));
                    }
                    else
                    {
                        Destroy(blocks[x, y].gameObject);
                    }

                    blocks[x, y] = null;
                }
            }

            UpdateOpportunityHints();
        }

        public Vector2Int GetOriginFromDraggedPiece(PieceInstance piece, RectTransform pieceRect, Camera eventCamera)
        {
            if (boardRoot == null || piece == null || pieceRect == null)
            {
                return new Vector2Int(int.MinValue, int.MinValue);
            }

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, pieceRect.position);
            return GetOriginFromScreenPoint(piece, screenPoint, eventCamera);
        }

        public Vector2Int GetOriginFromScreenPoint(PieceInstance piece, Vector2 screenPoint, Camera eventCamera)
        {
            if (boardRoot == null || piece == null)
            {
                return new Vector2Int(int.MinValue, int.MinValue);
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRoot, screenPoint, eventCamera, out Vector2 localPoint);

            Rect rect = boardRoot.rect;
            float gridWidth = rect.width - FinalBoardGridPaddingX * 2f;
            float gridHeight = rect.height - FinalBoardGridPaddingY * 2f;
            float cellWidth = gridWidth / GameConstants.BoardSize;
            float cellHeight = gridHeight / GameConstants.BoardSize;
            PieceData data = piece.Data;

            float gridX = (localPoint.x - rect.xMin - FinalBoardGridPaddingX) / cellWidth - data.width * 0.5f;
            float gridY = (localPoint.y - rect.yMin - FinalBoardGridPaddingY) / cellHeight - data.height * 0.5f;
            return new Vector2Int(Mathf.RoundToInt(gridX), Mathf.RoundToInt(gridY));
        }

        public Vector2Int GetSnappedOriginFromScreenPoint(PieceInstance piece, Vector2 screenPoint, Camera eventCamera)
        {
            Vector2Int origin = GetOriginFromScreenPoint(piece, screenPoint, eventCamera);
            if (CanPlace(piece, origin))
            {
                return origin;
            }

            Vector2Int bestOrigin = origin;
            float bestDistance = float.MaxValue;
            for (int radius = 1; radius <= 3; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                        {
                            continue;
                        }

                        Vector2Int candidate = new Vector2Int(origin.x + dx, origin.y + dy);
                        if (!CanPlace(piece, candidate))
                        {
                            continue;
                        }

                        float distance = dx * dx + dy * dy;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestOrigin = candidate;
                        }
                    }
                }

                if (bestDistance < float.MaxValue)
                {
                    return bestOrigin;
                }
            }

            return origin;
        }

        public bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            return boardRoot != null && RectTransformUtility.RectangleContainsScreenPoint(boardRoot, screenPoint, eventCamera);
        }

        public bool CanPlace(PieceInstance piece, Vector2Int origin)
        {
            if (piece == null)
            {
                return false;
            }

            Vector2Int[] shapeCells = piece.Data.cells;
            for (int i = 0; i < shapeCells.Length; i++)
            {
                int x = origin.x + shapeCells[i].x;
                int y = origin.y + shapeCells[i].y;
                if (!IsInside(x, y) || blocks[x, y] != null)
                {
                    return false;
                }
            }

            return true;
        }

        public bool CanAnyPieceFit(PieceInstance piece)
        {
            if (piece == null)
            {
                return false;
            }

            PieceData data = piece.Data;
            for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
            {
                for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                {
                    if (CanPlace(piece, new Vector2Int(x, y)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public int CountPlacementOptions(PieceInstance piece)
        {
            if (piece == null)
            {
                return 0;
            }

            int options = 0;
            PieceData data = piece.Data;
            for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
            {
                for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                {
                    if (CanPlace(piece, new Vector2Int(x, y)))
                    {
                        options++;
                    }
                }
            }

            return options;
        }

        public int CountClearOpportunities(PieceInstance piece)
        {
            if (piece == null)
            {
                return 0;
            }

            int opportunities = 0;
            PieceData data = piece.Data;
            for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
            {
                for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                {
                    Vector2Int origin = new Vector2Int(x, y);
                    if (CanPlace(piece, origin))
                    {
                        opportunities += CountLinesCompletedByPlacement(piece, origin);
                    }
                }
            }

            return opportunities;
        }

        public int ScoreBestSetupOpportunity(PieceInstance piece)
        {
            if (piece == null)
            {
                return 0;
            }

            int bestScore = 0;
            PieceData data = piece.Data;
            for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
            {
                for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                {
                    Vector2Int origin = new Vector2Int(x, y);
                    if (!CanPlace(piece, origin))
                    {
                        continue;
                    }

                    int score = CountLinesCompletedByPlacement(piece, origin) * 120;
                    score += ScoreNearLinesAfterPlacement(piece, origin);
                    if (score > bestScore)
                    {
                        bestScore = score;
                    }
                }
            }

            return bestScore;
        }

        public int ScorePlacementSetup(PieceInstance piece, Vector2Int origin)
        {
            if (piece == null || !CanPlace(piece, origin))
            {
                return 0;
            }

            return CountLinesCompletedByPlacement(piece, origin) * 120
                + ScoreNearLinesAfterPlacement(piece, origin);
        }

        public int GetPlacementClearPreview(PieceInstance piece, Vector2Int origin, out int pureLines)
        {
            pureLines = 0;
            if (piece == null || !CanPlace(piece, origin))
            {
                return 0;
            }

            bool[] touchedRows = new bool[GameConstants.BoardSize];
            bool[] touchedColumns = new bool[GameConstants.BoardSize];
            Vector2Int[] shapeCells = piece.Data.cells;
            for (int i = 0; i < shapeCells.Length; i++)
            {
                int x = origin.x + shapeCells[i].x;
                int y = origin.y + shapeCells[i].y;
                if (IsInside(x, y))
                {
                    touchedRows[y] = true;
                    touchedColumns[x] = true;
                }
            }

            int completed = 0;
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                if (touchedRows[y] && WouldRowBeFullAfterPlacement(y, piece, origin))
                {
                    completed++;
                    if (WouldRowBePureAfterPlacement(y, piece, origin))
                    {
                        pureLines++;
                    }
                }
            }

            for (int x = 0; x < GameConstants.BoardSize; x++)
            {
                if (touchedColumns[x] && WouldColumnBeFullAfterPlacement(x, piece, origin))
                {
                    completed++;
                    if (WouldColumnBePureAfterPlacement(x, piece, origin))
                    {
                        pureLines++;
                    }
                }
            }

            return completed;
        }

        public bool TryFindBestPlacement(PieceInstance piece, out Vector2Int bestOrigin, out int bestScore)
        {
            bestOrigin = new Vector2Int(int.MinValue, int.MinValue);
            bestScore = int.MinValue;
            if (piece == null)
            {
                return false;
            }

            bool found = false;
            PieceData data = piece.Data;
            for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
            {
                for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                {
                    Vector2Int origin = new Vector2Int(x, y);
                    if (!CanPlace(piece, origin))
                    {
                        continue;
                    }

                    int completedLines = CountLinesCompletedByPlacement(piece, origin);
                    int nearLineScore = ScoreNearLinesAfterPlacement(piece, origin);
                    int score = completedLines * 2400 + nearLineScore * 28 + data.cells.Length * 16;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestOrigin = origin;
                        found = true;
                    }
                }
            }

            return found;
        }

        private void ShowCompletionLinePreview(PieceInstance piece, Vector2Int origin)
        {
            Color completionColor = GetCompletionPreviewColor(piece.color);
            bool[] touchedRows = new bool[GameConstants.BoardSize];
            bool[] touchedColumns = new bool[GameConstants.BoardSize];
            Vector2Int[] shapeCells = piece.Data.cells;
            for (int i = 0; i < shapeCells.Length; i++)
            {
                int x = origin.x + shapeCells[i].x;
                int y = origin.y + shapeCells[i].y;
                if (IsInside(x, y))
                {
                    touchedRows[y] = true;
                    touchedColumns[x] = true;
                }
            }

            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                if (!touchedRows[y] || !WouldRowBeFullAfterPlacement(y, piece, origin))
                {
                    continue;
                }

                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    ShowCompletionLineFill(x, y, completionColor);
                }
            }

            for (int x = 0; x < GameConstants.BoardSize; x++)
            {
                if (!touchedColumns[x] || !WouldColumnBeFullAfterPlacement(x, piece, origin))
                {
                    continue;
                }

                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    ShowCompletionLineFill(x, y, completionColor);
                }
            }

            if (activeCompletionPreviewFills > 0 && completionPreviewPulseRoutine == null)
            {
                completionPreviewPulseRoutine = StartCoroutine(CompletionPreviewPulseRoutine());
            }
        }

        private void ShowCompletionLineFill(int x, int y, Color color)
        {
            if (!IsInside(x, y) || completionPreviewLayer == null)
            {
                return;
            }

            CompletionFillVisual fill = GetCompletionFill(activeCompletionPreviewFills++);
            ConfigureBoardRect(fill.root, x, y);
            fill.baseColor = color;
            color.a = 0.78f;
            fill.image.color = color;
            fill.root.gameObject.SetActive(true);
            fill.root.SetAsLastSibling();
        }

        public int CountEmptyCells()
        {
            int empty = 0;
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    if (blocks[x, y] == null)
                    {
                        empty++;
                    }
                }
            }

            return empty;
        }

        public bool CanAnyOfPiecesFit(PieceInstance[] pieces)
        {
            if (pieces == null)
            {
                return false;
            }

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] != null && CanAnyPieceFit(pieces[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public void ShowPreview(PieceInstance piece, Vector2Int origin)
        {
            if (piece == null || !CanPlace(piece, origin))
            {
                ClearPreview();
                return;
            }

            if (previewShapeId == piece.shapeId && previewOrigin == origin)
            {
                return;
            }

            ClearPreview();
            previewShapeId = piece.shapeId;
            previewOrigin = origin;

            bool completesLine = CountLinesCompletedByPlacement(piece, origin) > 0;
            Vector2Int[] shapeCells = piece.Data.cells;
            for (int i = 0; i < shapeCells.Length; i++)
            {
                int x = origin.x + shapeCells[i].x;
                int y = origin.y + shapeCells[i].y;
                if (!IsInside(x, y) || cells[x, y] == null)
                {
                    continue;
                }

                cells[x, y].SetPreview(true, piece.color, completesLine);
                previewCells.Add(cells[x, y]);
            }

            if (completesLine)
            {
                ShowCompletionLinePreview(piece, origin);
            }
        }

        private bool IsCellInCompletedLineAfterPlacement(PieceInstance piece, Vector2Int origin, int x, int y)
        {
            if (piece == null || !IsInside(x, y))
            {
                return false;
            }

            return WouldRowBeFullAfterPlacement(y, piece, origin)
                || WouldColumnBeFullAfterPlacement(x, piece, origin);
        }

        public void ClearPreview()
        {
            ClearCompletionLinePreview();

            for (int i = 0; i < previewCells.Count; i++)
            {
                if (previewCells[i] != null)
                {
                    previewCells[i].ClearPreview();
                }
            }

            previewCells.Clear();
            previewShapeId = null;
            previewOrigin = new Vector2Int(int.MinValue, int.MinValue);
        }

        private CompletionFillVisual GetCompletionFill(int index)
        {
            while (completionPreviewPool.Count <= index)
            {
                GameObject fillObject = new GameObject(
                    $"CompletionLineFill_{completionPreviewPool.Count}",
                    typeof(RectTransform),
                    typeof(UnityEngine.UI.Image));
                RectTransform fillRect = (RectTransform)fillObject.transform;
                fillRect.SetParent(completionPreviewLayer, false);
                UnityEngine.UI.Image fillImage = fillObject.GetComponent<UnityEngine.UI.Image>();
                UISpriteFactory.ApplyRounded(fillImage, 0.20f);
                fillImage.raycastTarget = false;
                fillImage.fillCenter = true;
                completionPreviewPool.Add(new CompletionFillVisual
                {
                    root = fillRect,
                    image = fillImage
                });
            }

            return completionPreviewPool[index];
        }

        private void ClearCompletionLinePreview()
        {
            if (completionPreviewPulseRoutine != null)
            {
                StopCoroutine(completionPreviewPulseRoutine);
                completionPreviewPulseRoutine = null;
            }

            for (int i = 0; i < activeCompletionPreviewFills && i < completionPreviewPool.Count; i++)
            {
                CompletionFillVisual fill = completionPreviewPool[i];
                if (fill != null && fill.root != null)
                {
                    fill.root.gameObject.SetActive(false);
                }
            }

            activeCompletionPreviewFills = 0;
        }

        private IEnumerator CompletionPreviewPulseRoutine()
        {
            while (activeCompletionPreviewFills > 0)
            {
                float wave = 0.5f + Mathf.Sin(Time.unscaledTime * 8.5f) * 0.5f;
                float alpha = Mathf.Lerp(0.62f, 0.98f, Mathf.SmoothStep(0f, 1f, wave));
                for (int i = 0; i < activeCompletionPreviewFills && i < completionPreviewPool.Count; i++)
                {
                    CompletionFillVisual fill = completionPreviewPool[i];
                    if (fill == null || fill.image == null)
                    {
                        continue;
                    }

                    Color color = fill.baseColor;
                    color.a = alpha;
                    fill.image.color = color;
                }

                yield return null;
            }

            completionPreviewPulseRoutine = null;
        }

        private static Color GetCompletionPreviewColor(ChromaColor color)
        {
            int colorIndex = Mathf.Clamp((int)color, 0, CompletionPreviewColors.Length - 1);
            return CompletionPreviewColors[colorIndex];
        }

        public void PlacePiece(PieceInstance piece, Vector2Int origin)
        {
            Vector2Int[] shapeCells = piece.Data.cells;
            for (int i = 0; i < shapeCells.Length; i++)
            {
                int x = origin.x + shapeCells[i].x;
                int y = origin.y + shapeCells[i].y;
                float placementDelay = CalculatePlacementDelay(i, shapeCells[i], piece.Data.width, piece.Data.height);
                BlockView block = Instantiate(blockPrefab, blockLayer);
                block.gameObject.SetActive(true);
                block.name = $"Block_{x}_{y}_{piece.color}";
                ConfigureBoardRect((RectTransform)block.transform, x, y);
                block.Initialize(piece.color, false);
                cells[x, y]?.PlayFlash(ChromaPalette.GetColor(piece.color), placementDelay * 0.45f);
                block.PlayPlaced(placementDelay);
                PlayPlacementImpact(x, y, piece.color, placementDelay * 0.65f);
                blocks[x, y] = block;
            }

            UpdateOpportunityHints();
        }

        private float CalculatePlacementDelay(int orderIndex, Vector2Int localCell, int pieceWidth, int pieceHeight)
        {
            if (!MobilePerformance.UseFullJuice())
            {
                return 0f;
            }

            float centerX = Mathf.Max(0f, (pieceWidth - 1) * 0.5f);
            float centerY = Mathf.Max(0f, (pieceHeight - 1) * 0.5f);
            float distanceFromCenter = Mathf.Abs(localCell.x - centerX) + Mathf.Abs(localCell.y - centerY);
            return Mathf.Min(0.09f, orderIndex * 0.019f + distanceFromCenter * 0.006f);
        }

        public ClearResult ResolveClears()
        {
            return ResolveClears(ChromaColor.Cyan);
        }

        public ClearResult ResolveClears(ChromaColor completionColor)
        {
            ClearResult result = new ClearResult();
            LastClearScreenPosition = boardRoot == null
                ? Vector2.zero
                : RectTransformUtility.WorldToScreenPoint(null, boardRoot.position);
            List<int> rows = new List<int>();
            List<int> columns = new List<int>();

            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                if (IsRowFull(y))
                {
                    rows.Add(y);
                    AddPureIfNeeded(result, y, true);
                }
            }

            for (int x = 0; x < GameConstants.BoardSize; x++)
            {
                if (IsColumnFull(x))
                {
                    columns.Add(x);
                    AddPureIfNeeded(result, x, false);
                }
            }

            result.linesCleared = rows.Count + columns.Count;
            if (result.linesCleared == 0)
            {
                return result;
            }

            Color pieceColor = ChromaPalette.GetColor(completionColor);
            Color anticipationColor = Color.Lerp(pieceColor, Color.white, 0.34f);
            FlashCompletedLines(rows, columns, anticipationColor);
            PlayCompletedLineGlow(rows, columns, pieceColor);

            HashSet<Vector2Int> toClear = new HashSet<Vector2Int>();
            for (int i = 0; i < rows.Count; i++)
            {
                int y = rows[i];
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    toClear.Add(new Vector2Int(x, y));
                }
            }

            for (int i = 0; i < columns.Count; i++)
            {
                int x = columns[i];
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    toClear.Add(new Vector2Int(x, y));
                }
            }

            UpdateLastClearScreenPosition(toClear);

            foreach (Vector2Int cell in toClear)
            {
                BlockView block = blocks[cell.x, cell.y];
                if (block == null)
                {
                    continue;
                }

                float clearDelay = CalculateLineClearDelay(cell, rows, columns);
                cells[cell.x, cell.y]?.PlayFlash(anticipationColor, clearDelay);
                result.AddClearedCell(block.Color);
                block.PlayClear(clearDelay);
                blocks[cell.x, cell.y] = null;
            }

            ShakeForLineClear(result);
            UpdateOpportunityHints();
            return result;
        }

        private float CalculateLineClearDelay(Vector2Int cell, List<int> rows, List<int> columns)
        {
            if (!MobilePerformance.UseFullJuice())
            {
                return 0f;
            }

            const float sweepLead = 0.085f;
            const float cellStep = 0.021f;
            const float lineStep = 0.006f;
            const float jitter = 0.003f;
            float center = (GameConstants.BoardSize - 1) * 0.5f;
            float delay = float.MaxValue;

            int rowIndex = rows.IndexOf(cell.y);
            if (rowIndex >= 0)
            {
                delay = Mathf.Min(delay, Mathf.Abs(cell.x - center) * cellStep + rowIndex * lineStep);
            }

            int columnIndex = columns.IndexOf(cell.x);
            if (columnIndex >= 0)
            {
                delay = Mathf.Min(delay, Mathf.Abs(cell.y - center) * cellStep + columnIndex * lineStep);
            }

            if (delay == float.MaxValue)
            {
                delay = 0f;
            }

            return sweepLead + delay + Random.Range(0f, jitter);
        }

        private void ShakeForLineClear(ClearResult result)
        {
            if (result == null || result.linesCleared < 1)
            {
                return;
            }

            float strength = Mathf.Clamp(
                0.028f + result.linesCleared * 0.012f + result.pureLines * 0.008f + result.cellsCleared * 0.0007f,
                0.045f,
                0.11f);
            float duration = Mathf.Clamp(0.10f + result.linesCleared * 0.012f, 0.10f, 0.16f);
            cameraShake?.Shake(strength, duration);
        }

        public void PlayComboShake(int chain)
        {
            // The clear itself already emits one move-level camera impulse.
            // Avoid stacking a second shake for the same completed move.
        }

        public int PopColor(ChromaColor color)
        {
            List<Vector2Int> toPop = new List<Vector2Int>();
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    if (blocks[x, y] != null && blocks[x, y].Color == color)
                    {
                        toPop.Add(new Vector2Int(x, y));
                    }
                }
            }

            for (int i = 0; i < toPop.Count; i++)
            {
                Vector2Int cell = toPop[i];
                BlockView block = blocks[cell.x, cell.y];
                if (block == null)
                {
                    continue;
                }

                SpawnClearParticles(block, i * 0.006f, 1.22f);
                Color popFlash = Color.Lerp(ChromaPalette.GetColor(color), Color.white, 0.66f);
                cells[cell.x, cell.y]?.PlayFlash(popFlash, i * 0.006f);
                block.PlayClear(i * 0.009f);
                blocks[cell.x, cell.y] = null;
            }

            if (toPop.Count > 0)
            {
                float strength = Mathf.Clamp(0.16f + toPop.Count * 0.003f, 0.18f, 0.30f);
                cameraShake?.Shake(strength, 0.22f);
            }

            UpdateOpportunityHints();
            return toPop.Count;
        }

        public int CountCellsOfColor(ChromaColor color)
        {
            int count = 0;
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    if (blocks[x, y] != null && blocks[x, y].Color == color)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public int ClearRandomCells(int count, System.Random random)
        {
            List<Vector2Int> occupied = new List<Vector2Int>();
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    if (blocks[x, y] != null)
                    {
                        occupied.Add(new Vector2Int(x, y));
                    }
                }
            }

            int cleared = 0;
            while (occupied.Count > 0 && cleared < count)
            {
                int index = random.Next(occupied.Count);
                Vector2Int cell = occupied[index];
                occupied.RemoveAt(index);
                BlockView block = blocks[cell.x, cell.y];
                if (block == null)
                {
                    continue;
                }

                SpawnClearParticles(block);
                cells[cell.x, cell.y]?.PlayFlash(ChromaPalette.GetColor(block.Color), cleared * 0.01f);
                block.PlayClear(cleared * 0.012f);
                blocks[cell.x, cell.y] = null;
                cleared++;
            }

            if (cleared > 0)
            {
                cameraShake?.Shake(0.16f, 0.2f);
            }

            UpdateOpportunityHints();
            return cleared;
        }

        public void ApplyDailyPrefill(int seed)
        {
            System.Random random = new System.Random(seed);
            Vector2Int[] clusterA = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) };
            Vector2Int[] clusterB = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) };
            Vector2Int[] clusterC = { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2) };

            TryPlaceDailyCluster(random, clusterA, random.Next(0, 2), random.Next(0, 3));
            TryPlaceDailyCluster(random, clusterB, random.Next(3, 5), random.Next(1, 4));
            TryPlaceDailyCluster(random, clusterC, random.Next(1, 4), random.Next(4, 6));

            int partialRow = random.Next(1, GameConstants.BoardSize - 1);
            int partialColumn = random.Next(1, GameConstants.BoardSize - 1);
            for (int i = 0; i < 4; i++)
            {
                TryPlaceDailyBlock(i, partialRow, (ChromaColor)(i % GameConstants.ColorCount));
            }

            for (int i = 4; i < 7; i++)
            {
                TryPlaceDailyBlock(partialColumn, i, (ChromaColor)((i + 1) % GameConstants.ColorCount));
            }

            UpdateOpportunityHints();
        }

        private void TryPlaceDailyCluster(System.Random random, Vector2Int[] shape, int originX, int originY)
        {
            ChromaColor color = (ChromaColor)random.Next(GameConstants.ColorCount);
            for (int i = 0; i < shape.Length; i++)
            {
                TryPlaceDailyBlock(originX + shape[i].x, originY + shape[i].y, color);
            }
        }

        private bool TryPlaceDailyBlock(int x, int y, ChromaColor color)
        {
            if (!IsInside(x, y) || blocks[x, y] != null)
            {
                return false;
            }

            BlockView block = Instantiate(blockPrefab, blockLayer);
            block.gameObject.SetActive(true);
            block.name = $"DailyBlock_{x}_{y}_{color}";
            ConfigureBoardRect((RectTransform)block.transform, x, y);
            block.Initialize(color, false);
            blocks[x, y] = block;
            return true;
        }

        public BoardSnapshot CreateSnapshot()
        {
            BoardSnapshot snapshot = new BoardSnapshot();
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    int index = y * GameConstants.BoardSize + x;
                    snapshot.colors[index] = blocks[x, y] == null ? -1 : (int)blocks[x, y].Color;
                }
            }

            return snapshot;
        }

        public void Restore(BoardSnapshot snapshot)
        {
            ClearBoard(false);
            if (snapshot == null || snapshot.colors == null)
            {
                return;
            }

            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    int index = y * GameConstants.BoardSize + x;
                    if (index >= snapshot.colors.Length || snapshot.colors[index] < 0)
                    {
                        continue;
                    }

                    ChromaColor color = (ChromaColor)snapshot.colors[index];
                    BlockView block = Instantiate(blockPrefab, blockLayer);
                    block.gameObject.SetActive(true);
                    block.name = $"RestoredBlock_{x}_{y}_{color}";
                    ConfigureBoardRect((RectTransform)block.transform, x, y);
                    block.Initialize(color, false);
                    blocks[x, y] = block;
                }
            }

            UpdateOpportunityHints();
        }

        private void UpdateOpportunityHints()
        {
            int[] rowFill = new int[GameConstants.BoardSize];
            int[] columnFill = new int[GameConstants.BoardSize];

            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    if (blocks[x, y] != null)
                    {
                        rowFill[y]++;
                        columnFill[x]++;
                    }
                }
            }

            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    BoardCell cell = cells[x, y];
                    if (cell == null)
                    {
                        continue;
                    }

                    if (blocks[x, y] != null)
                    {
                        cell.SetOpportunityHint(0);
                        continue;
                    }

                    bool rowAlmostReady = rowFill[y] >= GameConstants.BoardSize - 2 && rowFill[y] < GameConstants.BoardSize;
                    bool columnAlmostReady = columnFill[x] >= GameConstants.BoardSize - 2 && columnFill[x] < GameConstants.BoardSize;
                    int level = 0;

                    if (rowAlmostReady)
                    {
                        level = Mathf.Max(level, rowFill[y] >= GameConstants.BoardSize - 1 ? 2 : 1);
                    }

                    if (columnAlmostReady)
                    {
                        level = Mathf.Max(level, columnFill[x] >= GameConstants.BoardSize - 1 ? 2 : 1);
                    }

                    if (rowAlmostReady && columnAlmostReady)
                    {
                        level = 2;
                    }

                    cell.SetOpportunityHint(level);
                }
            }
        }

        private bool IsRowFull(int y)
        {
            for (int x = 0; x < GameConstants.BoardSize; x++)
            {
                if (blocks[x, y] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsColumnFull(int x)
        {
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                if (blocks[x, y] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private void AddPureIfNeeded(ClearResult result, int index, bool row)
        {
            BlockView first = row ? blocks[0, index] : blocks[index, 0];
            if (first == null)
            {
                return;
            }

            ChromaColor color = first.Color;
            for (int i = 1; i < GameConstants.BoardSize; i++)
            {
                BlockView block = row ? blocks[i, index] : blocks[index, i];
                if (block == null || block.Color != color)
                {
                    return;
                }
            }

            result.AddPureLine(color);
        }

        private int CountLinesCompletedByPlacement(PieceInstance piece, Vector2Int origin)
        {
            return GetPlacementClearPreview(piece, origin, out _);
        }

        private bool WouldRowBeFullAfterPlacement(int y, PieceInstance piece, Vector2Int origin)
        {
            for (int x = 0; x < GameConstants.BoardSize; x++)
            {
                if (blocks[x, y] == null && !PieceOccupiesCell(piece, origin, x, y))
                {
                    return false;
                }
            }

            return true;
        }

        private bool WouldColumnBeFullAfterPlacement(int x, PieceInstance piece, Vector2Int origin)
        {
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                if (blocks[x, y] == null && !PieceOccupiesCell(piece, origin, x, y))
                {
                    return false;
                }
            }

            return true;
        }

        private bool WouldRowBePureAfterPlacement(int y, PieceInstance piece, Vector2Int origin)
        {
            bool hasColor = false;
            ChromaColor firstColor = ChromaColor.Cyan;
            for (int x = 0; x < GameConstants.BoardSize; x++)
            {
                if (!TryGetCellColorAfterPlacement(x, y, piece, origin, out ChromaColor color))
                {
                    return false;
                }

                if (!hasColor)
                {
                    firstColor = color;
                    hasColor = true;
                }
                else if (color != firstColor)
                {
                    return false;
                }
            }

            return hasColor;
        }

        private bool WouldColumnBePureAfterPlacement(int x, PieceInstance piece, Vector2Int origin)
        {
            bool hasColor = false;
            ChromaColor firstColor = ChromaColor.Cyan;
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                if (!TryGetCellColorAfterPlacement(x, y, piece, origin, out ChromaColor color))
                {
                    return false;
                }

                if (!hasColor)
                {
                    firstColor = color;
                    hasColor = true;
                }
                else if (color != firstColor)
                {
                    return false;
                }
            }

            return hasColor;
        }

        private bool TryGetCellColorAfterPlacement(int x, int y, PieceInstance piece, Vector2Int origin, out ChromaColor color)
        {
            BlockView block = blocks[x, y];
            if (block != null)
            {
                color = block.Color;
                return true;
            }

            if (PieceOccupiesCell(piece, origin, x, y))
            {
                color = piece.color;
                return true;
            }

            color = ChromaColor.Cyan;
            return false;
        }

        private bool PieceOccupiesCell(PieceInstance piece, Vector2Int origin, int boardX, int boardY)
        {
            Vector2Int[] shapeCells = piece.Data.cells;
            for (int i = 0; i < shapeCells.Length; i++)
            {
                if (origin.x + shapeCells[i].x == boardX && origin.y + shapeCells[i].y == boardY)
                {
                    return true;
                }
            }

            return false;
        }

        private int ScoreNearLinesAfterPlacement(PieceInstance piece, Vector2Int origin)
        {
            bool[] touchedRows = new bool[GameConstants.BoardSize];
            bool[] touchedColumns = new bool[GameConstants.BoardSize];
            Vector2Int[] shapeCells = piece.Data.cells;
            for (int i = 0; i < shapeCells.Length; i++)
            {
                int x = origin.x + shapeCells[i].x;
                int y = origin.y + shapeCells[i].y;
                if (IsInside(x, y))
                {
                    touchedRows[y] = true;
                    touchedColumns[x] = true;
                }
            }

            int score = 0;
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                if (touchedRows[y])
                {
                    score += ScoreLineFill(CountFilledInRowAfterPlacement(y, piece, origin));
                }
            }

            for (int x = 0; x < GameConstants.BoardSize; x++)
            {
                if (touchedColumns[x])
                {
                    score += ScoreLineFill(CountFilledInColumnAfterPlacement(x, piece, origin));
                }
            }

            return score;
        }

        private int CountFilledInRowAfterPlacement(int y, PieceInstance piece, Vector2Int origin)
        {
            int filled = 0;
            for (int x = 0; x < GameConstants.BoardSize; x++)
            {
                if (blocks[x, y] != null || PieceOccupiesCell(piece, origin, x, y))
                {
                    filled++;
                }
            }

            return filled;
        }

        private int CountFilledInColumnAfterPlacement(int x, PieceInstance piece, Vector2Int origin)
        {
            int filled = 0;
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                if (blocks[x, y] != null || PieceOccupiesCell(piece, origin, x, y))
                {
                    filled++;
                }
            }

            return filled;
        }

        private int ScoreLineFill(int filledCells)
        {
            if (filledCells >= GameConstants.BoardSize)
            {
                return 90;
            }

            if (filledCells == GameConstants.BoardSize - 1)
            {
                return 54;
            }

            if (filledCells == GameConstants.BoardSize - 2)
            {
                return 22;
            }

            if (filledCells == GameConstants.BoardSize - 3)
            {
                return 8;
            }

            return 0;
        }

        private void FlashCompletedLines(List<int> rows, List<int> columns, Color color)
        {
            if (!MobilePerformance.UseFullJuice())
            {
                return;
            }

            int totalLines = rows.Count + columns.Count;
            bool strong = totalLines > 1;
            int lineOrder = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                int y = rows[i];
                float lineDelay = lineOrder * 0.024f;
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    cells[x, y]?.PlaySweep(color, lineDelay + x * 0.011f, strong);
                }

                lineOrder++;
            }

            for (int i = 0; i < columns.Count; i++)
            {
                int x = columns[i];
                float lineDelay = lineOrder * 0.024f;
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    cells[x, y]?.PlaySweep(color, lineDelay + y * 0.011f, strong);
                }

                lineOrder++;
            }
        }

        private void PlayCompletedLineGlow(List<int> rows, List<int> columns, Color color)
        {
            if (!MobilePerformance.UseFullJuice() || lineClearEffectLayer == null)
            {
                return;
            }

            int totalLines = rows.Count + columns.Count;
            float intensity = Mathf.Clamp(1f + Mathf.Max(0, totalLines - 1) * 0.045f, 1f, 1.18f);
            int effectGeneration = lineClearEffectGeneration;
            int order = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                PlayLineGlow(rows[i], true, color, order++ * 0.018f, intensity, effectGeneration);
            }

            for (int i = 0; i < columns.Count; i++)
            {
                PlayLineGlow(columns[i], false, color, order++ * 0.018f, intensity, effectGeneration);
            }

            PlayIntersectionFlares(rows, columns, color, intensity, effectGeneration);
        }

        private void PlayLineGlow(
            int lineIndex,
            bool horizontal,
            Color color,
            float delay,
            float intensity,
            int effectGeneration)
        {
            LineGlowVisual visual = AcquireLineGlowVisual();
            RectTransform root = visual.root;
            float minX = GetGridAnchorX(lineIndex);
            float maxX = GetGridAnchorX(lineIndex + 1f);
            float minY = GetGridAnchorY(lineIndex);
            float maxY = GetGridAnchorY(lineIndex + 1f);

            root.anchorMin = horizontal
                ? new Vector2(GetGridAnchorX(0f), minY)
                : new Vector2(minX, GetGridAnchorY(0f));
            root.anchorMax = horizontal
                ? new Vector2(GetGridAnchorX(GameConstants.BoardSize), maxY)
                : new Vector2(maxX, GetGridAnchorY(GameConstants.BoardSize));
            root.offsetMin = horizontal ? new Vector2(-8f, -3f) : new Vector2(-3f, -8f);
            root.offsetMax = horizontal ? new Vector2(8f, 3f) : new Vector2(3f, 8f);
            root.localScale = Vector3.one;
            root.localRotation = Quaternion.identity;
            visual.outerGlow.color = Color.clear;
            visual.coreBand.color = Color.clear;
            visual.movingBeam.color = Color.clear;
            for (int i = 0; i < visual.sparkles.Length; i++)
            {
                visual.sparkles[i].color = Color.clear;
            }

            for (int i = 0; i < visual.bubbles.Length; i++)
            {
                visual.bubbles[i].color = Color.clear;
            }

            root.gameObject.SetActive(true);
            root.SetAsLastSibling();

            ConfigureLineBand(visual.outerGlow.rectTransform, horizontal, 0.06f, 0.94f);
            ConfigureLineBand(visual.coreBand.rectTransform, horizontal, 0.42f, 0.58f);
            ConfigureMovingBeam(visual.movingBeam.rectTransform, horizontal, -0.18f);
            for (int i = 0; i < visual.sparkles.Length; i++)
            {
                RectTransform sparkle = visual.sparkles[i].rectTransform;
                sparkle.anchorMin = horizontal ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0f);
                sparkle.anchorMax = sparkle.anchorMin;
                sparkle.anchoredPosition = Vector2.zero;
                float size = i % 2 == 0 ? 13f : 9f;
                sparkle.sizeDelta = Vector2.one * size;
                sparkle.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 45f : 0f);
                sparkle.localScale = Vector3.zero;
            }

            for (int i = 0; i < visual.bubbles.Length; i++)
            {
                RectTransform bubble = visual.bubbles[i].rectTransform;
                bubble.anchorMin = horizontal ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0f);
                bubble.anchorMax = bubble.anchorMin;
                bubble.anchoredPosition = Vector2.zero;
                float size = i == 1 ? 18f : 12f;
                bubble.sizeDelta = Vector2.one * size;
                bubble.localScale = Vector3.zero;
            }

            StartCoroutine(AnimateLineGlow(visual, color, horizontal, delay, intensity, effectGeneration));
        }

        private static void ConfigureLineBand(RectTransform rect, bool horizontal, float crossMin, float crossMax)
        {
            rect.anchorMin = horizontal ? new Vector2(0f, crossMin) : new Vector2(crossMin, 0f);
            rect.anchorMax = horizontal ? new Vector2(1f, crossMax) : new Vector2(crossMax, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void ConfigureMovingBeam(RectTransform rect, bool horizontal, float progress)
        {
            const float halfLength = 0.18f;
            rect.anchorMin = horizontal
                ? new Vector2(progress - halfLength, 0.18f)
                : new Vector2(0.18f, progress - halfLength);
            rect.anchorMax = horizontal
                ? new Vector2(progress + halfLength, 0.82f)
                : new Vector2(0.82f, progress + halfLength);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private IEnumerator AnimateLineGlow(
            LineGlowVisual visual,
            Color sourceColor,
            bool horizontal,
            float delay,
            float intensityMultiplier,
            int effectGeneration)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (effectGeneration != lineClearEffectGeneration || visual.root == null)
            {
                yield break;
            }

            Color outerColor = sourceColor;
            Color coreColor = Color.Lerp(sourceColor, Color.white, 0.68f);
            Color beamColor = Color.Lerp(sourceColor, Color.white, 0.86f);
            const float duration = 0.23f;
            float elapsed = 0f;
            while (elapsed < duration
                && visual.root != null
                && effectGeneration == lineClearEffectGeneration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.22f));
                float fade = 1f - Mathf.SmoothStep(0.62f, 1f, t);
                float intensity = rise * fade;
                float sweep = Mathf.SmoothStep(-0.16f, 1.16f, t);

                outerColor.a = Mathf.Clamp01(intensity * 0.34f * intensityMultiplier);
                coreColor.a = Mathf.Clamp01(intensity * 0.66f * intensityMultiplier);
                beamColor.a = Mathf.Clamp01(intensity * 0.90f * intensityMultiplier);
                visual.outerGlow.color = outerColor;
                visual.coreBand.color = coreColor;
                visual.movingBeam.color = beamColor;
                ConfigureMovingBeam(visual.movingBeam.rectTransform, horizontal, sweep);

                for (int i = 0; i < visual.sparkles.Length; i++)
                {
                    float along = Mathf.Clamp01(sweep - 0.12f + (i - 1.5f) * 0.075f);
                    float cross = Mathf.Sin((t * 8f + i * 0.68f) * Mathf.PI) * (7f + i * 1.5f);
                    RectTransform sparkle = visual.sparkles[i].rectTransform;
                    sparkle.anchorMin = horizontal ? new Vector2(along, 0.5f) : new Vector2(0.5f, along);
                    sparkle.anchorMax = sparkle.anchorMin;
                    sparkle.anchoredPosition = horizontal ? new Vector2(0f, cross) : new Vector2(cross, 0f);

                    float particlePulse = Mathf.Sin(Mathf.Clamp01(t * 1.3f + i * 0.08f) * Mathf.PI);
                    Color sparkleColor = Color.Lerp(sourceColor, Color.white, 0.52f);
                    sparkleColor.a = Mathf.Clamp01(intensity * particlePulse * (0.68f - i * 0.06f) * intensityMultiplier);
                    visual.sparkles[i].color = sparkleColor;
                    sparkle.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.22f, particlePulse) * fade;
                }

                for (int i = 0; i < visual.bubbles.Length; i++)
                {
                    float bubbleT = Mathf.Clamp01((t - i * 0.055f) / 0.78f);
                    float along = Mathf.Clamp01(sweep - 0.09f - i * 0.08f);
                    float cross = Mathf.Lerp(-11f + i * 4f, 13f + i * 3f, bubbleT);
                    RectTransform bubble = visual.bubbles[i].rectTransform;
                    bubble.anchorMin = horizontal ? new Vector2(along, 0.5f) : new Vector2(0.5f, along);
                    bubble.anchorMax = bubble.anchorMin;
                    bubble.anchoredPosition = horizontal ? new Vector2(0f, cross) : new Vector2(cross, 0f);

                    float bubbleFade = Mathf.Sin(bubbleT * Mathf.PI) * fade;
                    Color bubbleColor = Color.Lerp(sourceColor, Color.white, 0.76f);
                    bubbleColor.a = Mathf.Clamp01(bubbleFade * 0.38f * intensityMultiplier);
                    visual.bubbles[i].color = bubbleColor;
                    bubble.localScale = Vector3.one * Mathf.Lerp(0.48f, 1.08f, bubbleT);
                }

                yield return null;
            }

            if (visual.root != null && effectGeneration == lineClearEffectGeneration)
            {
                visual.root.gameObject.SetActive(false);
            }
        }

        private void PlayIntersectionFlares(
            List<int> rows,
            List<int> columns,
            Color color,
            float intensity,
            int effectGeneration)
        {
            if (rows.Count == 0 || columns.Count == 0)
            {
                return;
            }

            int intersectionCount = rows.Count * columns.Count;
            int flareCount = Mathf.Min(8, intersectionCount);
            for (int i = 0; i < flareCount; i++)
            {
                int flatIndex = flareCount == 1
                    ? 0
                    : Mathf.RoundToInt(i * (intersectionCount - 1f) / (flareCount - 1f));
                int rowListIndex = flatIndex / columns.Count;
                int columnListIndex = flatIndex % columns.Count;
                PlayIntersectionFlare(
                    columns[columnListIndex],
                    rows[rowListIndex],
                    color,
                    0.052f + i * 0.006f,
                    intensity,
                    effectGeneration);
            }
        }

        private void PlayPlacementImpact(int column, int row, ChromaColor color, float delay)
        {
            if (!MobilePerformance.UseFullJuice() || lineClearEffectLayer == null)
            {
                return;
            }

            float size = Mathf.Clamp(CellSize * 0.46f, 24f, 48f);
            PlayIntersectionFlare(
                column,
                row,
                ChromaPalette.GetColor(color),
                delay,
                0.46f,
                lineClearEffectGeneration,
                size);
        }

        private void PlayIntersectionFlare(
            int column,
            int row,
            Color color,
            float delay,
            float intensity,
            int effectGeneration,
            float size = 62f)
        {
            IntersectionFlareVisual flare = AcquireIntersectionFlareVisual();
            float anchorX = GetGridAnchorX(column + 0.5f);
            float anchorY = GetGridAnchorY(row + 0.5f);
            flare.root.anchorMin = new Vector2(anchorX, anchorY);
            flare.root.anchorMax = flare.root.anchorMin;
            flare.root.pivot = new Vector2(0.5f, 0.5f);
            flare.root.anchoredPosition = Vector2.zero;
            flare.root.sizeDelta = Vector2.one * size;
            flare.root.localScale = Vector3.one;
            flare.outerGlow.color = Color.clear;
            flare.core.color = Color.clear;
            flare.root.gameObject.SetActive(true);
            flare.root.SetAsLastSibling();
            StartCoroutine(AnimateIntersectionFlare(flare, color, delay, intensity, effectGeneration));
        }

        private IEnumerator AnimateIntersectionFlare(
            IntersectionFlareVisual flare,
            Color sourceColor,
            float delay,
            float intensityMultiplier,
            int effectGeneration)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (effectGeneration != lineClearEffectGeneration || flare.root == null)
            {
                yield break;
            }

            Color outerColor = Color.Lerp(sourceColor, Color.white, 0.42f);
            Color coreColor = Color.Lerp(sourceColor, Color.white, 0.90f);
            const float duration = 0.19f;
            float elapsed = 0f;
            while (elapsed < duration
                && flare.root != null
                && effectGeneration == lineClearEffectGeneration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                float fade = 1f - Mathf.SmoothStep(0.58f, 1f, t);
                outerColor.a = Mathf.Clamp01(pulse * fade * 0.42f * intensityMultiplier);
                coreColor.a = Mathf.Clamp01(pulse * fade * 0.82f * intensityMultiplier);
                flare.outerGlow.color = outerColor;
                flare.core.color = coreColor;
                flare.root.localScale = Vector3.one * Mathf.Lerp(0.66f, 1.20f, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            if (flare.root != null && effectGeneration == lineClearEffectGeneration)
            {
                flare.root.gameObject.SetActive(false);
                flare.root.localScale = Vector3.one;
            }
        }

        private IntersectionFlareVisual AcquireIntersectionFlareVisual()
        {
            for (int i = 0; i < intersectionFlarePool.Count; i++)
            {
                if (!intersectionFlarePool[i].root.gameObject.activeSelf)
                {
                    return intersectionFlarePool[i];
                }
            }

            GameObject rootObject = new GameObject("LineIntersectionFlare", typeof(RectTransform));
            RectTransform root = (RectTransform)rootObject.transform;
            root.SetParent(lineClearEffectLayer, false);
            IntersectionFlareVisual flare = new IntersectionFlareVisual
            {
                root = root,
                outerGlow = CreateLineBubbleImage(root, "OuterGlow"),
                core = CreateLineBubbleImage(root, "Core")
            };
            ConfigureFlareImage(flare.outerGlow.rectTransform, Vector3.one);
            ConfigureFlareImage(flare.core.rectTransform, Vector3.one * 0.42f);
            rootObject.SetActive(false);
            intersectionFlarePool.Add(flare);
            return flare;
        }

        private static void ConfigureFlareImage(RectTransform rect, Vector3 scale)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = scale;
        }

        private LineGlowVisual AcquireLineGlowVisual()
        {
            for (int i = 0; i < lineGlowPool.Count; i++)
            {
                if (!lineGlowPool[i].root.gameObject.activeSelf)
                {
                    return lineGlowPool[i];
                }
            }

            GameObject rootObject = new GameObject("LineClearGlow", typeof(RectTransform));
            RectTransform root = (RectTransform)rootObject.transform;
            root.SetParent(lineClearEffectLayer, false);

            LineGlowVisual visual = new LineGlowVisual
            {
                root = root,
                outerGlow = CreateLineEffectImage(root, "OuterGlow", 0.50f),
                coreBand = CreateLineEffectImage(root, "CoreBand", 0.50f),
                movingBeam = CreateLineEffectImage(root, "MovingBeam", 0.50f),
                sparkles = new UnityEngine.UI.Image[3],
                bubbles = new UnityEngine.UI.Image[2]
            };

            for (int i = 0; i < visual.sparkles.Length; i++)
            {
                visual.sparkles[i] = CreateLineEffectImage(root, $"Sparkle_{i}", 0.22f);
            }

            for (int i = 0; i < visual.bubbles.Length; i++)
            {
                visual.bubbles[i] = CreateLineBubbleImage(root, $"Bubble_{i}");
            }

            rootObject.SetActive(false);
            lineGlowPool.Add(visual);
            return visual;
        }

        private static UnityEngine.UI.Image CreateLineEffectImage(RectTransform parent, string name, float radius)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            RectTransform rect = (RectTransform)imageObject.transform;
            rect.SetParent(parent, false);
            UnityEngine.UI.Image image = imageObject.GetComponent<UnityEngine.UI.Image>();
            UISpriteFactory.ApplyRounded(image, radius);
            image.color = Color.clear;
            image.raycastTarget = false;
            return image;
        }

        private static UnityEngine.UI.Image CreateLineBubbleImage(RectTransform parent, string name)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            RectTransform rect = (RectTransform)imageObject.transform;
            rect.SetParent(parent, false);
            UnityEngine.UI.Image image = imageObject.GetComponent<UnityEngine.UI.Image>();
            UISpriteFactory.ApplySoftCircle(image);
            image.color = Color.clear;
            image.raycastTarget = false;
            return image;
        }

        private Color GetClearFlashColor(ClearResult result)
        {
            if (result != null && result.pureLines > 0 && result.pureLinesByColor != null)
            {
                for (int i = 0; i < result.pureLinesByColor.Length; i++)
                {
                    if (result.pureLinesByColor[i] > 0)
                    {
                        return ChromaPalette.GetColor((ChromaColor)i);
                    }
                }
            }

            return new Color(0.1f, 0.9f, 1f, 1f);
        }

        private void SpawnClearParticles(BlockView block, float delay = 0f, float scaleMultiplier = 1f)
        {
            if (block == null)
            {
                return;
            }

            SpawnClearParticles(block, block.Color, delay, scaleMultiplier);
        }

        private void SpawnClearParticles(BlockView block, ChromaColor effectColor, float delay = 0f, float scaleMultiplier = 1f)
        {
            if (!MobilePerformance.UseFullJuice() || block == null)
            {
                return;
            }

            Vector3 screen = RectTransformUtility.WorldToScreenPoint(null, block.RectTransform.position);

            if (delay <= 0.01f)
            {
                SpawnClearParticlesAt(effectColor, screen, scaleMultiplier);
                return;
            }

            StartCoroutine(SpawnClearParticlesDelayed(effectColor, screen, delay, scaleMultiplier));
        }

        private IEnumerator SpawnClearParticlesDelayed(ChromaColor color, Vector3 screen, float delay, float scaleMultiplier)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            SpawnClearParticlesAt(color, screen, scaleMultiplier);
        }

        private void SpawnClearParticlesAt(ChromaColor color, Vector3 screen, float scaleMultiplier)
        {
            if (!MobilePerformance.UseFullJuice())
            {
                return;
            }

            if (clearParticlesByColor == null || clearParticlesByColor.Length <= (int)color)
            {
                return;
            }

            ParticleSystem prefab = clearParticlesByColor[(int)color];
            if (prefab == null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(camera.transform.position.z)));
            ParticleSystem particles = Instantiate(prefab, world, Quaternion.identity);
            particles.transform.localScale *= Mathf.Clamp(scaleMultiplier, 0.75f, 1.35f);
            particles.Play();
            Destroy(particles.gameObject, 2f);
        }

        private void UpdateLastClearScreenPosition(HashSet<Vector2Int> clearedCells)
        {
            if (clearedCells == null || clearedCells.Count == 0)
            {
                return;
            }

            Vector3 worldCenter = Vector3.zero;
            int count = 0;
            foreach (Vector2Int cell in clearedCells)
            {
                BlockView block = blocks[cell.x, cell.y];
                if (block == null || block.RectTransform == null)
                {
                    continue;
                }

                worldCenter += block.RectTransform.position;
                count++;
            }

            if (count > 0)
            {
                LastClearScreenPosition = RectTransformUtility.WorldToScreenPoint(null, worldCenter / count);
            }
        }

        private void ResetLineClearEffects()
        {
            lineClearEffectGeneration++;
            for (int i = 0; i < lineGlowPool.Count; i++)
            {
                if (lineGlowPool[i]?.root != null)
                {
                    lineGlowPool[i].root.gameObject.SetActive(false);
                }
            }

            for (int i = 0; i < intersectionFlarePool.Count; i++)
            {
                if (intersectionFlarePool[i]?.root != null)
                {
                    intersectionFlarePool[i].root.gameObject.SetActive(false);
                }
            }
        }

        private void ConfigureBoardRect(RectTransform rectTransform, int x, int y)
        {
            float minX = GetGridAnchorX(x);
            float minY = GetGridAnchorY(y);
            float maxX = GetGridAnchorX(x + 1f);
            float maxY = GetGridAnchorY(y + 1f);

            rectTransform.anchorMin = new Vector2(minX, minY);
            rectTransform.anchorMax = new Vector2(maxX, maxY);
            rectTransform.offsetMin = Vector2.one * cellPadding;
            rectTransform.offsetMax = Vector2.one * -cellPadding;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localRotation = Quaternion.identity;
        }

        private float GetGridAnchorX(float cellCoordinate)
        {
            if (boardRoot == null || boardRoot.rect.width <= 0f)
            {
                return cellCoordinate / GameConstants.BoardSize;
            }

            float gridWidth = boardRoot.rect.width - FinalBoardGridPaddingX * 2f;
            return (FinalBoardGridPaddingX + gridWidth * (cellCoordinate / GameConstants.BoardSize)) / boardRoot.rect.width;
        }

        private float GetGridAnchorY(float cellCoordinate)
        {
            if (boardRoot == null || boardRoot.rect.height <= 0f)
            {
                return cellCoordinate / GameConstants.BoardSize;
            }

            float gridHeight = boardRoot.rect.height - FinalBoardGridPaddingY * 2f;
            return (FinalBoardGridPaddingY + gridHeight * (cellCoordinate / GameConstants.BoardSize)) / boardRoot.rect.height;
        }

        private bool IsInside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < GameConstants.BoardSize && y < GameConstants.BoardSize;
        }

        private void EnsureLayers()
        {
            if (boardRoot == null)
            {
                boardRoot = (RectTransform)transform;
            }

            if (blockLayer == null && boardRoot != null)
            {
                GameObject layer = new GameObject("BlockLayer", typeof(RectTransform));
                blockLayer = (RectTransform)layer.transform;
                blockLayer.SetParent(boardRoot, false);
                blockLayer.anchorMin = Vector2.zero;
                blockLayer.anchorMax = Vector2.one;
                blockLayer.offsetMin = Vector2.zero;
                blockLayer.offsetMax = Vector2.zero;
            }

            if (lineClearEffectLayer == null && boardRoot != null)
            {
                Transform existing = boardRoot.Find("LineClearEffectLayer");
                lineClearEffectLayer = existing as RectTransform;
                if (lineClearEffectLayer == null)
                {
                    GameObject layer = new GameObject("LineClearEffectLayer", typeof(RectTransform));
                    lineClearEffectLayer = (RectTransform)layer.transform;
                    lineClearEffectLayer.SetParent(boardRoot, false);
                }

                lineClearEffectLayer.anchorMin = Vector2.zero;
                lineClearEffectLayer.anchorMax = Vector2.one;
                lineClearEffectLayer.offsetMin = Vector2.zero;
                lineClearEffectLayer.offsetMax = Vector2.zero;
                lineClearEffectLayer.localScale = Vector3.one;
                lineClearEffectLayer.SetAsLastSibling();
            }

            if (completionPreviewLayer == null && boardRoot != null)
            {
                Transform existing = boardRoot.Find("CompletionLinePreviewLayer");
                completionPreviewLayer = existing as RectTransform;
                if (completionPreviewLayer == null)
                {
                    GameObject layer = new GameObject("CompletionLinePreviewLayer", typeof(RectTransform));
                    completionPreviewLayer = (RectTransform)layer.transform;
                    completionPreviewLayer.SetParent(boardRoot, false);
                }

                completionPreviewLayer.anchorMin = Vector2.zero;
                completionPreviewLayer.anchorMax = Vector2.one;
                completionPreviewLayer.offsetMin = Vector2.zero;
                completionPreviewLayer.offsetMax = Vector2.zero;
                completionPreviewLayer.localScale = Vector3.one;
                completionPreviewLayer.SetAsLastSibling();
            }
        }

        private sealed class CompletionFillVisual
        {
            public RectTransform root;
            public UnityEngine.UI.Image image;
            public Color baseColor;
        }

        private sealed class LineGlowVisual
        {
            public RectTransform root;
            public UnityEngine.UI.Image outerGlow;
            public UnityEngine.UI.Image coreBand;
            public UnityEngine.UI.Image movingBeam;
            public UnityEngine.UI.Image[] sparkles;
            public UnityEngine.UI.Image[] bubbles;
        }

        private sealed class IntersectionFlareVisual
        {
            public RectTransform root;
            public UnityEngine.UI.Image outerGlow;
            public UnityEngine.UI.Image core;
        }

        private void EnsureRuntimePrefabs()
        {
            if (cellPrefab == null)
            {
                GameObject cellObject = new GameObject("RuntimeBoardCellTemplate", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(BoardCell));
                cellObject.transform.SetParent(transform, false);
                cellObject.SetActive(false);
                cellPrefab = cellObject.GetComponent<BoardCell>();
                cellPrefab.Configure();
            }

            if (blockPrefab == null)
            {
                GameObject blockObject = new GameObject("RuntimeBlockTemplate", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(BlockView));
                blockObject.transform.SetParent(transform, false);
                blockObject.SetActive(false);
                blockPrefab = blockObject.GetComponent<BlockView>();
            }
        }
    }
}
