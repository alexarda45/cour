using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Profiling;
using UnityEngine;

namespace ChromaBlast
{
    // Read-only candidate-generation data. This is intentionally separate from
    // placement and clear resolution so tray selection never mutates the board.
    public struct GenerationPlacementProfile
    {
        public int placementOptions;
        public int clearOpportunities;
        public int bestSetupScore;
        public int bestAdjacencyContacts;
        public int bestLineProgress;
        public int bestCleanlinessScore;
        public bool hasSetupOrigin;
        public Vector2Int bestSetupOrigin;
    }

    // Bounded, read-only tray-generation result. This is intentionally based on
    // a tiny virtual board only; it never writes to the live gameplay board.
    public struct GenerationFlowProjection
    {
        public bool hasSequence;
        public int finalOccupiedCells;
        public int clearedLines;
        public int largestEmptyRegion;
        public int largestOpenRectangle;
        public int emptyRegionCount;
        public int isolatedHoles;
        public int narrowCorridorCells;
        public int futurePlacementOptions;
        public int cleanlinessScore;
    }

    public class BoardManager : MonoBehaviour
    {
        private static readonly ProfilerMarker ResolveClearCalculationMarker =
            new ProfilerMarker("ChromaBlast.Board.ResolveClearCalculation");
        private static readonly ProfilerMarker ClearVisualDispatchMarker =
            new ProfilerMarker("ChromaBlast.Board.ClearVisualDispatch");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static double DebugLastResolveClearCalculationMilliseconds { get; private set; }
        public static double DebugLastClearVisualDispatchMilliseconds { get; private set; }
#endif
        private const float FinalBoardGridPaddingX = 16f;
        private const float FinalBoardGridPaddingY = 9.5f;
        private const int MaxMagneticSnapRadius = 1;
        private const int PopParticlePrewarmPerColor = 8;
        private const int PopParticleMaximumPerColor = 16;
        private const float PooledParticleReturnDelay = 0.60f;
        private const int BoardBlockPrewarmCount = 32;
        private const int BoardBlockMaximumCount = GameConstants.BoardSize * GameConstants.BoardSize * 2;
        private const int BoardBlockPrewarmPerFrame = 4;

        private sealed class PooledClearParticle
        {
            public ParticleSystem particleSystem;
            public Vector3 baseScale;
            public bool inUse;
            public float releaseTime;
        }

        private struct ScheduledParticleSpawn
        {
            public ChromaColor color;
            public Vector3 worldPosition;
            public float scaleMultiplier;
            public float spawnTime;
            public int popSequenceId;
        }

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
        private readonly List<BlockView> completionPreviewBlocks = new List<BlockView>();
        private readonly List<CompletionCellGlowVisual> completionGlowPool = new List<CompletionCellGlowVisual>();
        private readonly HashSet<Vector2Int> completionGlowCoordinates = new HashSet<Vector2Int>();
        private readonly List<LineGlowVisual> lineGlowPool = new List<LineGlowVisual>();
        private readonly List<IntersectionFlareVisual> intersectionFlarePool = new List<IntersectionFlareVisual>();
        private readonly List<BlockView> boardBlockPool = new List<BlockView>(BoardBlockMaximumCount);
        private readonly HashSet<BlockView> activeBoardBlocks = new HashSet<BlockView>();
        private readonly List<PooledClearParticle>[] clearParticlePools = new List<PooledClearParticle>[GameConstants.ColorCount];
        private readonly List<PooledClearParticle> activeClearParticles = new List<PooledClearParticle>(GameConstants.ColorCount * PopParticleMaximumPerColor);
        private readonly List<ScheduledParticleSpawn> scheduledPopParticleSpawns = new List<ScheduledParticleSpawn>(GameConstants.BoardSize * GameConstants.BoardSize);
        // Reused only while evaluating a tray candidate. Keeping these buffers on
        // the board avoids per-placement arrays during the 56-candidate search.
        private readonly int[] generationRowFill = new int[GameConstants.BoardSize];
        private readonly int[] generationColumnFill = new int[GameConstants.BoardSize];
        private readonly int[] generationRowAdd = new int[GameConstants.BoardSize];
        private readonly int[] generationColumnAdd = new int[GameConstants.BoardSize];
        // Phase 9 Relax Flow uses only the top three plausible positions at each
        // depth and evaluates at most six piece orders. These fixed buffers keep
        // the deeper read-only projection allocation-free on the generation path.
        private const int GenerationFlowPlacementShortlist = 3;
        private readonly bool[,] generationFlowBoards = new bool[GameConstants.TraySize + 1, GameConstants.BoardSize * GameConstants.BoardSize];
        private readonly int[,] generationFlowOriginX = new int[GameConstants.TraySize, GenerationFlowPlacementShortlist];
        private readonly int[,] generationFlowOriginY = new int[GameConstants.TraySize, GenerationFlowPlacementShortlist];
        private readonly int[,] generationFlowOriginScores = new int[GameConstants.TraySize, GenerationFlowPlacementShortlist];
        // A separate tiny shortlist is used for a FlowTarget-bound A -> B check.
        // It reuses the existing virtual setup/payoff simulation and never writes
        // to the live board or allocates during tray generation.
        private readonly int[] generationFlowTargetOriginX = new int[GenerationFlowPlacementShortlist];
        private readonly int[] generationFlowTargetOriginY = new int[GenerationFlowPlacementShortlist];
        private readonly int[] generationFlowTargetOriginScores = new int[GenerationFlowPlacementShortlist];
        private readonly bool[] generationFlowCompletedRows = new bool[GameConstants.BoardSize];
        private readonly bool[] generationFlowCompletedColumns = new bool[GameConstants.BoardSize];
        private readonly bool[] generationFlowVisited = new bool[GameConstants.BoardSize * GameConstants.BoardSize];
        private readonly int[] generationFlowQueue = new int[GameConstants.BoardSize * GameConstants.BoardSize];
        private readonly int[] generationFlowHistogram = new int[GameConstants.BoardSize];
        private static readonly int[,] GenerationFlowOrders =
        {
            { 0, 1, 2 },
            { 0, 2, 1 },
            { 1, 0, 2 },
            { 1, 2, 0 },
            { 2, 0, 1 },
            { 2, 1, 0 }
        };
        private static readonly string[] GenerationFlowFutureShapeIds =
        {
            "single", "line2_h", "line2_v", "line3_h", "line3_v", "square2", "corner3", "corner3_m"
        };

        private RectTransform lineClearEffectLayer;
        private RectTransform completionGlowLayer;
        private RectTransform boardBlockPoolRoot;
        private Transform clearParticlePoolRoot;
        private Coroutine completionPreviewPulseRoutine;
        private Coroutine boardBlockPoolPrewarmRoutine;
        private Coroutine particlePoolPrewarmRoutine;
        private int activeCompletionGlows;
        private int lineClearEffectGeneration;
        private int nextPopParticleSequenceId;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int LastPopPoppedTileCount { get; private set; }
        public int LastPopParticlePoolSize { get; private set; }
        public int LastPopPooledInstancesReused { get; private set; }
        public int LastPopParticlePoolExpansions { get; private set; }
        public int LastPopParticleInstantiations { get; private set; }
        public int ActivePooledParticleCount => activeClearParticles.Count;
        public int BoardBlockPoolPrewarmedCount { get; private set; }
        public int BoardBlockPoolRuntimeExpansions { get; private set; }
        public int BoardBlockPoolRuntimeInstantiations { get; private set; }
        public int BoardBlockPoolRuntimeDestroys => 0;
        public int BoardBlockPoolTotalCount => boardBlockPool.Count;
        public int BoardBlockPoolActiveCount => activeBoardBlocks.Count;
        public int BoardBlockPoolInactiveCount => boardBlockPool.Count - activeBoardBlocks.Count;
#endif

        private void Awake()
        {
            EnsureLayers();
            EnsureRuntimePrefabs();
            InitializeClearParticlePools();
            BuildCells();
        }

        private void Start()
        {
            boardBlockPoolPrewarmRoutine = StartCoroutine(PrewarmBoardBlockPool());
            if (MobilePerformance.UseFullJuice())
            {
                particlePoolPrewarmRoutine = StartCoroutine(PrewarmClearParticlePool());
            }
        }

        private void Update()
        {
            ProcessScheduledPopParticleSpawns();
            ReturnFinishedPooledParticles();
        }

        private void OnDestroy()
        {
            if (boardBlockPoolPrewarmRoutine != null)
            {
                StopCoroutine(boardBlockPoolPrewarmRoutine);
                boardBlockPoolPrewarmRoutine = null;
            }

            if (particlePoolPrewarmRoutine != null)
            {
                StopCoroutine(particlePoolPrewarmRoutine);
                particlePoolPrewarmRoutine = null;
            }

            if (clearParticlePoolRoot != null)
            {
                Destroy(clearParticlePoolRoot.gameObject);
                clearParticlePoolRoot = null;
            }

            if (boardBlockPoolRoot != null)
            {
                Destroy(boardBlockPoolRoot.gameObject);
                boardBlockPoolRoot = null;
            }
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

            if (completionGlowLayer != null)
            {
                completionGlowLayer.SetAsLastSibling();
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
                        ReturnBoardBlock(blocks[x, y]);
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
            for (int radius = 1; radius <= MaxMagneticSnapRadius; radius++)
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

        public GenerationPlacementProfile EvaluateGenerationPlacementProfile(PieceInstance piece)
        {
            GenerationPlacementProfile profile = default;
            if (piece == null)
            {
                return profile;
            }

            PopulateGenerationLineFill();
            PieceData data = piece.Data;
            int bestSetupSelectionScore = int.MinValue;

            for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
            {
                for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                {
                    Vector2Int origin = new Vector2Int(x, y);
                    if (!CanPlace(piece, origin))
                    {
                        continue;
                    }

                    profile.placementOptions++;
                    System.Array.Clear(generationRowAdd, 0, generationRowAdd.Length);
                    System.Array.Clear(generationColumnAdd, 0, generationColumnAdd.Length);

                    int adjacencyContacts = 0;
                    Vector2Int[] shapeCells = data.cells;
                    for (int cellIndex = 0; cellIndex < shapeCells.Length; cellIndex++)
                    {
                        int cellX = origin.x + shapeCells[cellIndex].x;
                        int cellY = origin.y + shapeCells[cellIndex].y;
                        generationRowAdd[cellY]++;
                        generationColumnAdd[cellX]++;
                        adjacencyContacts += CountExistingOrthogonalContacts(cellX, cellY);
                    }

                    int completedLines = 0;
                    int lineProgress = 0;
                    int setupScore = 0;
                    for (int line = 0; line < GameConstants.BoardSize; line++)
                    {
                        if (generationRowAdd[line] > 0)
                        {
                            int afterFill = generationRowFill[line] + generationRowAdd[line];
                            completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                            lineProgress += ScoreGenerationLineProgress(generationRowFill[line], afterFill);
                            setupScore += ScoreLineFill(afterFill);
                        }

                        if (generationColumnAdd[line] > 0)
                        {
                            int afterFill = generationColumnFill[line] + generationColumnAdd[line];
                            completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                            lineProgress += ScoreGenerationLineProgress(generationColumnFill[line], afterFill);
                            setupScore += ScoreLineFill(afterFill);
                        }
                    }

                    int isolatedHoles = CountGenerationIsolatedHolesAfterPlacement(piece, origin);
                    int cleanliness = adjacencyContacts * 12 - isolatedHoles * 90;
                    int setupSelectionScore = completedLines == 0
                        ? lineProgress * 18 + setupScore * 4 + adjacencyContacts * 3 - isolatedHoles * 40
                        : int.MinValue / 2;

                    profile.clearOpportunities += completedLines;
                    profile.bestAdjacencyContacts = Mathf.Max(profile.bestAdjacencyContacts, adjacencyContacts);
                    profile.bestLineProgress = Mathf.Max(profile.bestLineProgress, lineProgress);
                    profile.bestCleanlinessScore = Mathf.Max(profile.bestCleanlinessScore, cleanliness);
                    profile.bestSetupScore = Mathf.Max(profile.bestSetupScore, setupScore);

                    if (setupSelectionScore > bestSetupSelectionScore)
                    {
                        bestSetupSelectionScore = setupSelectionScore;
                        profile.hasSetupOrigin = true;
                        profile.bestSetupOrigin = origin;
                    }
                }
            }

            return profile;
        }

        // Returns the live-board fill of one line without exposing board blocks
        // to tray selection. It is used only when a completed tray captures its
        // short-lived Phase 9 continuation targets.
        public int GetGenerationLineFill(bool row, int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= GameConstants.BoardSize)
            {
                return 0;
            }

            int filled = 0;
            for (int i = 0; i < GameConstants.BoardSize; i++)
            {
                if (row ? blocks[i, lineIndex] != null : blocks[lineIndex, i] != null)
                {
                    filled++;
                }
            }

            return filled;
        }

        // A soft FlowTarget compatibility probe. It asks how much a legal move
        // can contribute to one actual row or column; it does not solve or force
        // a placement for the player.
        public int GetBestGenerationFlowTargetAdvance(
            PieceInstance piece,
            bool row,
            int lineIndex,
            out bool completesTarget)
        {
            completesTarget = false;
            if (piece == null || lineIndex < 0 || lineIndex >= GameConstants.BoardSize)
            {
                return 0;
            }

            int existingFill = GetGenerationLineFill(row, lineIndex);
            int bestAdvance = 0;
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

                    int advance = 0;
                    for (int cell = 0; cell < data.cells.Length; cell++)
                    {
                        Vector2Int offset = data.cells[cell];
                        if ((row && origin.y + offset.y == lineIndex)
                            || (!row && origin.x + offset.x == lineIndex))
                        {
                            advance++;
                        }
                    }

                    if (advance <= 0)
                    {
                        continue;
                    }

                    bool completes = existingFill + advance >= GameConstants.BoardSize;
                    if (advance > bestAdvance || (advance == bestAdvance && completes && !completesTarget))
                    {
                        bestAdvance = advance;
                        completesTarget = completes;
                    }
                }
            }

            return bestAdvance;
        }

        // Confirms a real, bounded BUILD -> PAYOFF relationship tied to an
        // actual FlowTarget. The first shape is restricted to the target line;
        // the existing virtual setup/payoff probe then verifies that the second
        // shape can clear after that exact setup. This is selection-only and
        // never changes live placement, clear, score, or board state.
        public int ScoreGenerationFlowTargetPayoff(
            PieceInstance continuationPiece,
            PieceInstance payoffPiece,
            bool row,
            int lineIndex,
            out int continuationAdvance,
            out bool continuationCompletesTarget)
        {
            continuationAdvance = 0;
            continuationCompletesTarget = false;
            if (continuationPiece == null || payoffPiece == null
                || lineIndex < 0 || lineIndex >= GameConstants.BoardSize)
            {
                return 0;
            }

            for (int i = 0; i < GenerationFlowPlacementShortlist; i++)
            {
                generationFlowTargetOriginScores[i] = int.MinValue;
            }

            int targetPlacementCount = 0;
            int existingFill = GetGenerationLineFill(row, lineIndex);
            PieceData continuationData = continuationPiece.Data;
            for (int y = 0; y <= GameConstants.BoardSize - continuationData.height; y++)
            {
                for (int x = 0; x <= GameConstants.BoardSize - continuationData.width; x++)
                {
                    Vector2Int origin = new Vector2Int(x, y);
                    if (!CanPlace(continuationPiece, origin))
                    {
                        continue;
                    }

                    int advance = CountGenerationFlowTargetCells(
                        continuationData,
                        x,
                        y,
                        row,
                        lineIndex);
                    if (advance <= 0)
                    {
                        continue;
                    }

                    // Target contribution is decisive; the setup score only
                    // breaks ties between equally readable continuations.
                    int score = advance * 10000 + ScorePlacementSetup(continuationPiece, origin);
                    int insertAt = targetPlacementCount;
                    if (insertAt >= GenerationFlowPlacementShortlist)
                    {
                        insertAt = GenerationFlowPlacementShortlist - 1;
                    }

                    while (insertAt > 0 && score > generationFlowTargetOriginScores[insertAt - 1])
                    {
                        if (insertAt < GenerationFlowPlacementShortlist)
                        {
                            generationFlowTargetOriginScores[insertAt] = generationFlowTargetOriginScores[insertAt - 1];
                            generationFlowTargetOriginX[insertAt] = generationFlowTargetOriginX[insertAt - 1];
                            generationFlowTargetOriginY[insertAt] = generationFlowTargetOriginY[insertAt - 1];
                        }

                        insertAt--;
                    }

                    if (insertAt < GenerationFlowPlacementShortlist
                        && (targetPlacementCount < GenerationFlowPlacementShortlist
                            || score > generationFlowTargetOriginScores[insertAt]))
                    {
                        generationFlowTargetOriginScores[insertAt] = score;
                        generationFlowTargetOriginX[insertAt] = x;
                        generationFlowTargetOriginY[insertAt] = y;
                        if (targetPlacementCount < GenerationFlowPlacementShortlist)
                        {
                            targetPlacementCount++;
                        }
                    }
                }
            }

            int bestPayoffScore = 0;
            for (int i = 0; i < targetPlacementCount; i++)
            {
                Vector2Int origin = new Vector2Int(
                    generationFlowTargetOriginX[i],
                    generationFlowTargetOriginY[i]);
                int payoffScore = ScoreGenerationSetupPayoff(
                    continuationPiece,
                    origin,
                    payoffPiece);
                if (payoffScore <= bestPayoffScore)
                {
                    continue;
                }

                bestPayoffScore = payoffScore;
                continuationAdvance = CountGenerationFlowTargetCells(
                    continuationData,
                    origin.x,
                    origin.y,
                    row,
                    lineIndex);
                continuationCompletesTarget = existingFill + continuationAdvance >= GameConstants.BoardSize;
            }

            return bestPayoffScore;
        }

        private static int CountGenerationFlowTargetCells(
            PieceData data,
            int originX,
            int originY,
            bool row,
            int lineIndex)
        {
            int count = 0;
            for (int i = 0; i < data.cells.Length; i++)
            {
                Vector2Int cell = data.cells[i];
                if ((row && originY + cell.y == lineIndex)
                    || (!row && originX + cell.x == lineIndex))
                {
                    count++;
                }
            }

            return count;
        }

        // Read-only development telemetry for transient tray-to-tray flow
        // targets. This observes the board just before ResolveClears removes a
        // completed line; it never changes board state or gameplay rules.
        public bool IsGenerationFlowTargetComplete(bool row, int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= GameConstants.BoardSize)
            {
                return false;
            }

            return row ? IsRowFull(lineIndex) : IsColumnFull(lineIndex);
        }

        // Evaluates only a compact set of plausible A/B/C placements. This is a
        // final-board quality estimate for tray selection, never an exhaustive
        // solver and never a mutation of the live board.
        public GenerationFlowProjection EvaluateGenerationFlowProjection(PieceInstance[] set)
        {
            GenerationFlowProjection best = default;
            if (set == null || set.Length < GameConstants.TraySize
                || set[0] == null || set[1] == null || set[2] == null)
            {
                return best;
            }

            CopyLiveBoardToGenerationFlowBoard(0);
            for (int order = 0; order < GenerationFlowOrders.GetLength(0); order++)
            {
                EvaluateGenerationFlowOrder(
                    set,
                    order,
                    0,
                    0,
                    ref best);
            }

            return best;
        }

        private void EvaluateGenerationFlowOrder(
            PieceInstance[] set,
            int orderIndex,
            int depth,
            int clearedLines,
            ref GenerationFlowProjection best)
        {
            if (depth >= GameConstants.TraySize)
            {
                GenerationFlowProjection result = EvaluateGenerationFlowBoard(depth, clearedLines);
                if (!best.hasSequence || result.cleanlinessScore > best.cleanlinessScore)
                {
                    best = result;
                }

                return;
            }

            PieceInstance piece = set[GenerationFlowOrders[orderIndex, depth]];
            int placementCount = CollectGenerationFlowPlacements(depth, piece);
            for (int placement = 0; placement < placementCount; placement++)
            {
                CopyGenerationFlowBoard(depth, depth + 1);
                int clearedByMove = PlaceOnGenerationFlowBoard(
                    depth + 1,
                    piece,
                    generationFlowOriginX[depth, placement],
                    generationFlowOriginY[depth, placement]);
                EvaluateGenerationFlowOrder(
                    set,
                    orderIndex,
                    depth + 1,
                    clearedLines + clearedByMove,
                    ref best);
            }
        }

        private int CollectGenerationFlowPlacements(int depth, PieceInstance piece)
        {
            for (int i = 0; i < GenerationFlowPlacementShortlist; i++)
            {
                generationFlowOriginScores[depth, i] = int.MinValue;
            }

            int count = 0;
            PieceData data = piece.Data;
            for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
            {
                for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                {
                    if (!CanPlaceOnGenerationFlowBoard(depth, data, x, y))
                    {
                        continue;
                    }

                    int score = ScoreGenerationFlowPlacement(depth, data, x, y);
                    int insertAt = count;
                    if (insertAt >= GenerationFlowPlacementShortlist)
                    {
                        insertAt = GenerationFlowPlacementShortlist - 1;
                    }

                    while (insertAt > 0 && score > generationFlowOriginScores[depth, insertAt - 1])
                    {
                        if (insertAt < GenerationFlowPlacementShortlist)
                        {
                            generationFlowOriginScores[depth, insertAt] = generationFlowOriginScores[depth, insertAt - 1];
                            generationFlowOriginX[depth, insertAt] = generationFlowOriginX[depth, insertAt - 1];
                            generationFlowOriginY[depth, insertAt] = generationFlowOriginY[depth, insertAt - 1];
                        }

                        insertAt--;
                    }

                    if (insertAt < GenerationFlowPlacementShortlist
                        && (count < GenerationFlowPlacementShortlist || score > generationFlowOriginScores[depth, insertAt]))
                    {
                        generationFlowOriginScores[depth, insertAt] = score;
                        generationFlowOriginX[depth, insertAt] = x;
                        generationFlowOriginY[depth, insertAt] = y;
                        if (count < GenerationFlowPlacementShortlist)
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }

        private int ScoreGenerationFlowPlacement(int boardDepth, PieceData data, int originX, int originY)
        {
            PopulateGenerationFlowLineFill(boardDepth);
            System.Array.Clear(generationRowAdd, 0, generationRowAdd.Length);
            System.Array.Clear(generationColumnAdd, 0, generationColumnAdd.Length);

            int contacts = 0;
            for (int cell = 0; cell < data.cells.Length; cell++)
            {
                int x = originX + data.cells[cell].x;
                int y = originY + data.cells[cell].y;
                generationRowAdd[y]++;
                generationColumnAdd[x]++;
                contacts += CountGenerationFlowOrthogonalContacts(boardDepth, x, y);
            }

            int completedLines = 0;
            int progress = 0;
            for (int line = 0; line < GameConstants.BoardSize; line++)
            {
                if (generationRowAdd[line] > 0)
                {
                    int afterFill = generationRowFill[line] + generationRowAdd[line];
                    completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                    progress += ScoreGenerationLineProgress(generationRowFill[line], afterFill);
                }

                if (generationColumnAdd[line] > 0)
                {
                    int afterFill = generationColumnFill[line] + generationColumnAdd[line];
                    completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                    progress += ScoreGenerationLineProgress(generationColumnFill[line], afterFill);
                }
            }

            return completedLines * 4500 + progress * 28 + contacts * 24;
        }

        private void CopyLiveBoardToGenerationFlowBoard(int destinationDepth)
        {
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    generationFlowBoards[destinationDepth, GenerationFlowIndex(x, y)] = blocks[x, y] != null;
                }
            }
        }

        private void CopyGenerationFlowBoard(int sourceDepth, int destinationDepth)
        {
            for (int index = 0; index < GameConstants.BoardSize * GameConstants.BoardSize; index++)
            {
                generationFlowBoards[destinationDepth, index] = generationFlowBoards[sourceDepth, index];
            }
        }

        private bool CanPlaceOnGenerationFlowBoard(int boardDepth, PieceData data, int originX, int originY)
        {
            for (int cell = 0; cell < data.cells.Length; cell++)
            {
                int x = originX + data.cells[cell].x;
                int y = originY + data.cells[cell].y;
                if (x < 0 || y < 0 || x >= GameConstants.BoardSize || y >= GameConstants.BoardSize
                    || generationFlowBoards[boardDepth, GenerationFlowIndex(x, y)])
                {
                    return false;
                }
            }

            return true;
        }

        private int PlaceOnGenerationFlowBoard(int boardDepth, PieceInstance piece, int originX, int originY)
        {
            PieceData data = piece.Data;
            for (int cell = 0; cell < data.cells.Length; cell++)
            {
                int x = originX + data.cells[cell].x;
                int y = originY + data.cells[cell].y;
                generationFlowBoards[boardDepth, GenerationFlowIndex(x, y)] = true;
            }

            System.Array.Clear(generationFlowCompletedRows, 0, generationFlowCompletedRows.Length);
            System.Array.Clear(generationFlowCompletedColumns, 0, generationFlowCompletedColumns.Length);
            int clearedLines = 0;
            for (int line = 0; line < GameConstants.BoardSize; line++)
            {
                bool rowComplete = true;
                bool columnComplete = true;
                for (int cell = 0; cell < GameConstants.BoardSize; cell++)
                {
                    rowComplete &= generationFlowBoards[boardDepth, GenerationFlowIndex(cell, line)];
                    columnComplete &= generationFlowBoards[boardDepth, GenerationFlowIndex(line, cell)];
                }

                generationFlowCompletedRows[line] = rowComplete;
                generationFlowCompletedColumns[line] = columnComplete;
                clearedLines += rowComplete ? 1 : 0;
                clearedLines += columnComplete ? 1 : 0;
            }

            if (clearedLines > 0)
            {
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    for (int x = 0; x < GameConstants.BoardSize; x++)
                    {
                        if (generationFlowCompletedRows[y] || generationFlowCompletedColumns[x])
                        {
                            generationFlowBoards[boardDepth, GenerationFlowIndex(x, y)] = false;
                        }
                    }
                }
            }

            return clearedLines;
        }

        private GenerationFlowProjection EvaluateGenerationFlowBoard(int boardDepth, int clearedLines)
        {
            GenerationFlowProjection result = default;
            result.hasSequence = true;
            result.clearedLines = clearedLines;
            System.Array.Clear(generationFlowVisited, 0, generationFlowVisited.Length);

            int occupied = 0;
            int largestRegion = 0;
            int regionCount = 0;
            int isolatedHoles = 0;
            int corridorCells = 0;
            for (int index = 0; index < GameConstants.BoardSize * GameConstants.BoardSize; index++)
            {
                if (generationFlowBoards[boardDepth, index])
                {
                    occupied++;
                    continue;
                }

                int x = index % GameConstants.BoardSize;
                int y = index / GameConstants.BoardSize;
                int emptyNeighbours = CountGenerationFlowEmptyNeighbours(boardDepth, x, y);
                if (x > 0 && x < GameConstants.BoardSize - 1
                    && y > 0 && y < GameConstants.BoardSize - 1
                    && emptyNeighbours == 0)
                {
                    isolatedHoles++;
                }
                else if (emptyNeighbours <= 2)
                {
                    corridorCells++;
                }

                if (!generationFlowVisited[index])
                {
                    regionCount++;
                    int regionSize = CountGenerationFlowEmptyRegion(boardDepth, index);
                    largestRegion = Mathf.Max(largestRegion, regionSize);
                }
            }

            int largestRectangle = CountGenerationFlowLargestOpenRectangle(boardDepth);
            int futureOptions = CountGenerationFlowFutureOptions(boardDepth);
            result.finalOccupiedCells = occupied;
            result.largestEmptyRegion = largestRegion;
            result.largestOpenRectangle = largestRectangle;
            result.emptyRegionCount = regionCount;
            result.isolatedHoles = isolatedHoles;
            result.narrowCorridorCells = corridorCells;
            result.futurePlacementOptions = futureOptions;
            result.cleanlinessScore = -occupied * 95
                + largestRegion * 72
                + largestRectangle * 45
                + futureOptions * 230
                + clearedLines * 720
                - regionCount * 360
                - isolatedHoles * 500
                - corridorCells * 80;
            return result;
        }

        private void PopulateGenerationFlowLineFill(int boardDepth)
        {
            System.Array.Clear(generationRowFill, 0, generationRowFill.Length);
            System.Array.Clear(generationColumnFill, 0, generationColumnFill.Length);
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    if (!generationFlowBoards[boardDepth, GenerationFlowIndex(x, y)])
                    {
                        continue;
                    }

                    generationRowFill[y]++;
                    generationColumnFill[x]++;
                }
            }
        }

        private int CountGenerationFlowOrthogonalContacts(int boardDepth, int x, int y)
        {
            int contacts = 0;
            contacts += IsGenerationFlowOccupied(boardDepth, x - 1, y) ? 1 : 0;
            contacts += IsGenerationFlowOccupied(boardDepth, x + 1, y) ? 1 : 0;
            contacts += IsGenerationFlowOccupied(boardDepth, x, y - 1) ? 1 : 0;
            contacts += IsGenerationFlowOccupied(boardDepth, x, y + 1) ? 1 : 0;
            return contacts;
        }

        private int CountGenerationFlowEmptyNeighbours(int boardDepth, int x, int y)
        {
            int empty = 0;
            empty += IsGenerationFlowInsideAndEmpty(boardDepth, x - 1, y) ? 1 : 0;
            empty += IsGenerationFlowInsideAndEmpty(boardDepth, x + 1, y) ? 1 : 0;
            empty += IsGenerationFlowInsideAndEmpty(boardDepth, x, y - 1) ? 1 : 0;
            empty += IsGenerationFlowInsideAndEmpty(boardDepth, x, y + 1) ? 1 : 0;
            return empty;
        }

        private int CountGenerationFlowEmptyRegion(int boardDepth, int startIndex)
        {
            int head = 0;
            int tail = 0;
            int size = 0;
            generationFlowVisited[startIndex] = true;
            generationFlowQueue[tail++] = startIndex;
            while (head < tail)
            {
                int index = generationFlowQueue[head++];
                size++;
                int x = index % GameConstants.BoardSize;
                int y = index / GameConstants.BoardSize;
                TryQueueGenerationFlowEmptyNeighbour(boardDepth, x - 1, y, ref tail);
                TryQueueGenerationFlowEmptyNeighbour(boardDepth, x + 1, y, ref tail);
                TryQueueGenerationFlowEmptyNeighbour(boardDepth, x, y - 1, ref tail);
                TryQueueGenerationFlowEmptyNeighbour(boardDepth, x, y + 1, ref tail);
            }

            return size;
        }

        private void TryQueueGenerationFlowEmptyNeighbour(int boardDepth, int x, int y, ref int tail)
        {
            if (!IsGenerationFlowInsideAndEmpty(boardDepth, x, y))
            {
                return;
            }

            int index = GenerationFlowIndex(x, y);
            if (generationFlowVisited[index])
            {
                return;
            }

            generationFlowVisited[index] = true;
            generationFlowQueue[tail++] = index;
        }

        private int CountGenerationFlowLargestOpenRectangle(int boardDepth)
        {
            System.Array.Clear(generationFlowHistogram, 0, generationFlowHistogram.Length);
            int bestArea = 0;
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    generationFlowHistogram[x] = generationFlowBoards[boardDepth, GenerationFlowIndex(x, y)]
                        ? 0
                        : generationFlowHistogram[x] + 1;
                }

                for (int right = 0; right < GameConstants.BoardSize; right++)
                {
                    int minHeight = int.MaxValue;
                    for (int left = right; left >= 0; left--)
                    {
                        minHeight = Mathf.Min(minHeight, generationFlowHistogram[left]);
                        bestArea = Mathf.Max(bestArea, minHeight * (right - left + 1));
                    }
                }
            }

            return bestArea;
        }

        private int CountGenerationFlowFutureOptions(int boardDepth)
        {
            int options = 0;
            for (int i = 0; i < GenerationFlowFutureShapeIds.Length; i++)
            {
                PieceData data = PieceCatalog.Get(GenerationFlowFutureShapeIds[i]);
                bool fits = false;
                for (int y = 0; y <= GameConstants.BoardSize - data.height && !fits; y++)
                {
                    for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                    {
                        if (CanPlaceOnGenerationFlowBoard(boardDepth, data, x, y))
                        {
                            fits = true;
                            break;
                        }
                    }
                }

                options += fits ? 1 : 0;
            }

            return options;
        }

        private bool IsGenerationFlowOccupied(int boardDepth, int x, int y)
        {
            return x >= 0 && y >= 0 && x < GameConstants.BoardSize && y < GameConstants.BoardSize
                && generationFlowBoards[boardDepth, GenerationFlowIndex(x, y)];
        }

        private bool IsGenerationFlowInsideAndEmpty(int boardDepth, int x, int y)
        {
            return x >= 0 && y >= 0 && x < GameConstants.BoardSize && y < GameConstants.BoardSize
                && !generationFlowBoards[boardDepth, GenerationFlowIndex(x, y)];
        }

        private static int GenerationFlowIndex(int x, int y)
        {
            return y * GameConstants.BoardSize + x;
        }

        // Scores the best follow-up after the first piece's most useful non-clear
        // placement. It models an opportunity, not a forced solution.
        public int ScoreGenerationSetupPayoff(
            PieceInstance setupPiece,
            Vector2Int setupOrigin,
            PieceInstance payoffPiece)
        {
            if (setupPiece == null || payoffPiece == null || !CanPlace(setupPiece, setupOrigin))
            {
                return 0;
            }

            PopulateGenerationLineFill();
            PieceData payoffData = payoffPiece.Data;
            int bestScore = 0;
            for (int y = 0; y <= GameConstants.BoardSize - payoffData.height; y++)
            {
                for (int x = 0; x <= GameConstants.BoardSize - payoffData.width; x++)
                {
                    Vector2Int payoffOrigin = new Vector2Int(x, y);
                    if (!CanPlaceAfterVirtualPlacement(setupPiece, setupOrigin, payoffPiece, payoffOrigin))
                    {
                        continue;
                    }

                    System.Array.Clear(generationRowAdd, 0, generationRowAdd.Length);
                    System.Array.Clear(generationColumnAdd, 0, generationColumnAdd.Length);
                    AddPieceToGenerationLineFill(setupPiece, setupOrigin);
                    AddPieceToGenerationLineFill(payoffPiece, payoffOrigin);

                    int completedLines = 0;
                    int lineProgress = 0;
                    for (int line = 0; line < GameConstants.BoardSize; line++)
                    {
                        if (generationRowAdd[line] > 0)
                        {
                            int afterFill = generationRowFill[line] + generationRowAdd[line];
                            completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                            lineProgress += ScoreGenerationLineProgress(generationRowFill[line], afterFill);
                        }

                        if (generationColumnAdd[line] > 0)
                        {
                            int afterFill = generationColumnFill[line] + generationColumnAdd[line];
                            completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                            lineProgress += ScoreGenerationLineProgress(generationColumnFill[line], afterFill);
                        }
                    }

                    if (completedLines > 0)
                    {
                        bestScore = Mathf.Max(bestScore, completedLines * 180 + lineProgress * 3);
                    }
                }
            }

            return bestScore;
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
            Color activePieceColor = ChromaPalette.GetTileArtworkColor(piece.color);
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
                    ShowCompletionSpritePreview(x, y, piece.color);
                    ShowCompletionCellGlow(x, y, activePieceColor);
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
                    ShowCompletionSpritePreview(x, y, piece.color);
                    ShowCompletionCellGlow(x, y, activePieceColor);
                }
            }

            if ((completionPreviewBlocks.Count > 0 || activeCompletionGlows > 0)
                && completionPreviewPulseRoutine == null)
            {
                completionPreviewPulseRoutine = StartCoroutine(CompletionPreviewPulseRoutine());
            }
        }

        private void ShowCompletionSpritePreview(int x, int y, ChromaColor color)
        {
            if (!IsInside(x, y))
            {
                return;
            }

            BlockView block = blocks[x, y];
            if (block == null)
            {
                return;
            }

            if (block.BeginCompletionSpritePreview(color) && !completionPreviewBlocks.Contains(block))
            {
                completionPreviewBlocks.Add(block);
            }
        }

        private void ShowCompletionCellGlow(int x, int y, Color color)
        {
            if (!IsInside(x, y) || completionGlowLayer == null)
            {
                return;
            }

            Vector2Int coordinate = new Vector2Int(x, y);
            if (!completionGlowCoordinates.Add(coordinate))
            {
                return;
            }

            CompletionCellGlowVisual glow = GetCompletionCellGlow(activeCompletionGlows);
            activeCompletionGlows++;
            ConfigureBoardRect(glow.root, x, y);
            glow.baseColor = color;
            SetCompletionGlowAlpha(glow, 0f);
            glow.root.gameObject.SetActive(true);
            glow.root.SetAsLastSibling();
        }

        private CompletionCellGlowVisual GetCompletionCellGlow(int index)
        {
            while (completionGlowPool.Count <= index)
            {
                GameObject rootObject = new GameObject(
                    $"CompletionCellGlow_{completionGlowPool.Count}",
                    typeof(RectTransform));
                RectTransform root = (RectTransform)rootObject.transform;
                root.SetParent(completionGlowLayer, false);

                UnityEngine.UI.Image outerGlow = CreateCompletionGlowFrame(
                    root,
                    "OuterGlow",
                    new Vector2(-12f, -12f),
                    new Vector2(12f, 12f),
                    0.20f,
                    0.22f,
                    true);
                UnityEngine.UI.Image coreRim = CreateCompletionGlowFrame(
                    root,
                    "CoreRim",
                    Vector2.zero,
                    Vector2.zero,
                    0.20f,
                    0.055f,
                    false);

                CompletionCellGlowVisual glow = new CompletionCellGlowVisual
                {
                    root = root,
                    outerGlow = outerGlow,
                    coreRim = coreRim
                };
                rootObject.SetActive(false);
                completionGlowPool.Add(glow);
            }

            return completionGlowPool[index];
        }

        private static UnityEngine.UI.Image CreateCompletionGlowFrame(
            RectTransform parent,
            string objectName,
            Vector2 offsetMin,
            Vector2 offsetMax,
            float radius,
            float thicknessOrFeather,
            bool softGlow)
        {
            GameObject frameObject = new GameObject(objectName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            RectTransform frameRect = (RectTransform)frameObject.transform;
            frameRect.SetParent(parent, false);
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = offsetMin;
            frameRect.offsetMax = offsetMax;
            frameRect.pivot = new Vector2(0.5f, 0.5f);

            UnityEngine.UI.Image image = frameObject.GetComponent<UnityEngine.UI.Image>();
            if (softGlow)
            {
                UISpriteFactory.ApplySoftFrame(image, radius, thicknessOrFeather);
            }
            else
            {
                UISpriteFactory.ApplyFrame(image, radius, thicknessOrFeather);
            }
            image.preserveAspect = false;
            image.fillCenter = false;
            image.raycastTarget = false;
            image.material = null;
            return image;
        }

        private static void SetCompletionGlowAlpha(CompletionCellGlowVisual glow, float pulse)
        {
            if (glow == null)
            {
                return;
            }

            float easedPulse = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pulse));
            if (glow.outerGlow != null)
            {
                Color outerColor = glow.baseColor;
                outerColor.a = Mathf.Lerp(0.30f, 1f, easedPulse);
                glow.outerGlow.color = outerColor;
            }

            if (glow.coreRim != null)
            {
                Color rimColor = glow.baseColor;
                rimColor.a = Mathf.Lerp(0.55f, 1f, easedPulse);
                glow.coreRim.color = rimColor;
            }
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

        private void ClearCompletionLinePreview()
        {
            if (completionPreviewPulseRoutine != null)
            {
                StopCoroutine(completionPreviewPulseRoutine);
                completionPreviewPulseRoutine = null;
            }

            for (int i = 0; i < completionPreviewBlocks.Count; i++)
            {
                if (completionPreviewBlocks[i] != null)
                {
                    completionPreviewBlocks[i].EndCompletionSpritePreview();
                }
            }

            completionPreviewBlocks.Clear();

            for (int i = 0; i < activeCompletionGlows; i++)
            {
                if (completionGlowPool[i] != null && completionGlowPool[i].root != null)
                {
                    completionGlowPool[i].root.gameObject.SetActive(false);
                }
            }

            activeCompletionGlows = 0;
            completionGlowCoordinates.Clear();
        }

        private IEnumerator CompletionPreviewPulseRoutine()
        {
            const float pulsePeriod = 0.70f;

            while (completionPreviewBlocks.Count > 0 || activeCompletionGlows > 0)
            {
                float pulseRadians = Time.unscaledTime * (Mathf.PI * 2f / pulsePeriod);
                float glowPulse = 0.5f + Mathf.Sin(pulseRadians) * 0.5f;
                for (int i = 0; i < activeCompletionGlows; i++)
                {
                    SetCompletionGlowAlpha(completionGlowPool[i], glowPulse);
                }

                yield return null;
            }

            completionPreviewPulseRoutine = null;
        }

        private IEnumerator PrewarmBoardBlockPool()
        {
            while (boardBlockPool.Count < BoardBlockPrewarmCount)
            {
                int blocksThisFrame = Mathf.Min(
                    BoardBlockPrewarmPerFrame,
                    BoardBlockPrewarmCount - boardBlockPool.Count);
                for (int i = 0; i < blocksThisFrame; i++)
                {
                    CreatePooledBoardBlock(true);
                }

                yield return null;
            }

            boardBlockPoolPrewarmRoutine = null;
        }

        private BlockView CreateBoardBlock(int x, int y, ChromaColor color, string blockName)
        {
            BlockView block = AcquireBoardBlock();
            if (block == null)
            {
                Debug.LogError("Board block pool reached its bounded capacity unexpectedly.");
                return null;
            }

            block.name = blockName;
            ConfigureBoardRect((RectTransform)block.transform, x, y);
            block.Initialize(color, false);
            block.SetClearCompletionCallback(ReturnBoardBlock);
            return block;
        }

        private BlockView AcquireBoardBlock()
        {
            for (int i = 0; i < boardBlockPool.Count; i++)
            {
                BlockView block = boardBlockPool[i];
                if (block == null || activeBoardBlocks.Contains(block))
                {
                    continue;
                }

                activeBoardBlocks.Add(block);
                block.PrepareForPool();
                block.transform.SetParent(blockLayer, false);
                block.gameObject.SetActive(true);
                return block;
            }

            if (boardBlockPool.Count >= BoardBlockMaximumCount)
            {
                return null;
            }

            return CreatePooledBoardBlock(false);
        }

        private BlockView CreatePooledBoardBlock(bool prewarming)
        {
            if (blockPrefab == null || boardBlockPool.Count >= BoardBlockMaximumCount)
            {
                return null;
            }

            EnsureBoardBlockPoolRoot();
            BlockView block = Instantiate(blockPrefab, boardBlockPoolRoot);
            block.name = $"PooledBoardBlock_{boardBlockPool.Count}";
            block.PrepareForPool();
            block.gameObject.SetActive(false);
            boardBlockPool.Add(block);
            activeBoardBlocks.Remove(block);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (prewarming)
            {
                BoardBlockPoolPrewarmedCount++;
            }
            else
            {
                BoardBlockPoolRuntimeExpansions++;
                BoardBlockPoolRuntimeInstantiations++;
            }
#endif

            if (prewarming)
            {
                return null;
            }

            activeBoardBlocks.Add(block);
            block.transform.SetParent(blockLayer, false);
            block.gameObject.SetActive(true);
            return block;
        }

        private void ReturnBoardBlock(BlockView block)
        {
            if (block == null || !activeBoardBlocks.Remove(block))
            {
                return;
            }

            block.PrepareForPool();
            EnsureBoardBlockPoolRoot();
            block.transform.SetParent(boardBlockPoolRoot, false);
            block.name = $"PooledBoardBlock_{boardBlockPool.IndexOf(block)}";
            block.gameObject.SetActive(false);
        }

        private void EnsureBoardBlockPoolRoot()
        {
            if (boardBlockPoolRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("BoardBlockPool", typeof(RectTransform));
            boardBlockPoolRoot = root.GetComponent<RectTransform>();
            boardBlockPoolRoot.SetParent(transform, false);
        }

        public void PlacePiece(PieceInstance piece, Vector2Int origin)
        {
            Vector2Int[] shapeCells = piece.Data.cells;
            for (int i = 0; i < shapeCells.Length; i++)
            {
                int x = origin.x + shapeCells[i].x;
                int y = origin.y + shapeCells[i].y;
                float placementDelay = CalculatePlacementDelay(i, shapeCells[i], piece.Data.width, piece.Data.height);
                BlockView block = CreateBoardBlock(x, y, piece.color, $"Block_{x}_{y}_{piece.color}");
                if (block == null)
                {
                    continue;
                }

                cells[x, y]?.PlayFlash(ChromaPalette.GetColor(piece.color), placementDelay * 0.45f, 0.11f);
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
            return Mathf.Min(0.025f, orderIndex * 0.0045f + distanceFromCenter * 0.0018f);
        }

        public ClearResult ResolveClears()
        {
            return ResolveClears(ChromaColor.Cyan);
        }

        public ClearResult ResolveClears(ChromaColor completionColor)
        {
            ResolveClearCalculationMarker.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long resolveCalculationStarted = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
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
                ResolveClearCalculationMarker.End();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DebugLastResolveClearCalculationMilliseconds = ElapsedMilliseconds(resolveCalculationStarted);
#endif
                return result;
            }

            Color pieceColor = ChromaPalette.GetColor(completionColor);
            Color anticipationColor = Color.Lerp(pieceColor, Color.white, 0.34f);

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
            ResolveClearCalculationMarker.End();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugLastResolveClearCalculationMilliseconds = ElapsedMilliseconds(resolveCalculationStarted);
#endif

            ClearVisualDispatchMarker.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long visualDispatchStarted = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            FlashCompletedLines(rows, columns, anticipationColor);
            PlayCompletedLineGlow(rows, columns, pieceColor, result.pureLines);

            foreach (Vector2Int cell in toClear)
            {
                BlockView block = blocks[cell.x, cell.y];
                if (block == null)
                {
                    continue;
                }

                float clearDelay = CalculateLineClearDelay(cell, rows, columns);
                cells[cell.x, cell.y]?.PlayFlash(anticipationColor, clearDelay, 0.052f);
                result.AddClearedCell(block.Color);
                float clearStrength = Mathf.Clamp01(
                    Mathf.Max(0, result.linesCleared - 1) * 0.5f
                    + result.pureLines * 0.25f);
                block.PlayClear(clearDelay, clearStrength);
                blocks[cell.x, cell.y] = null;
            }

            ShakeForLineClear(result);
            UpdateOpportunityHints();
            ClearVisualDispatchMarker.End();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugLastClearVisualDispatchMilliseconds = ElapsedMilliseconds(visualDispatchStarted);
#endif
            return result;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static double ElapsedMilliseconds(long started)
        {
            return (System.Diagnostics.Stopwatch.GetTimestamp() - started)
                * 1000.0
                / System.Diagnostics.Stopwatch.Frequency;
        }
#endif

        private float CalculateLineClearDelay(Vector2Int cell, List<int> rows, List<int> columns)
        {
            if (!MobilePerformance.UseFullJuice())
            {
                return 0f;
            }

            const float sweepLead = 0.009f;
            const float cellStep = 0.003f;
            const float lineStep = 0.001f;
            const float jitter = 0.001f;
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
            bool strong = result.linesCleared >= 2 || result.pureLines > 0;
            float duration = strong ? 0.055f : 0.040f;
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

            Camera popParticleCamera = toPop.Count > 0 && MobilePerformance.UseFullJuice()
                ? Camera.main
                : null;
            int popParticleSequenceId = toPop.Count > 0
                ? BeginPopParticleSequence(toPop.Count)
                : 0;

            for (int i = 0; i < toPop.Count; i++)
            {
                Vector2Int cell = toPop[i];
                BlockView block = blocks[cell.x, cell.y];
                if (block == null)
                {
                    continue;
                }

                SchedulePopClearParticles(block, i * 0.006f, 1.22f, popParticleSequenceId, popParticleCamera);
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

            BlockView block = CreateBoardBlock(x, y, color, $"DailyBlock_{x}_{y}_{color}");
            if (block == null)
            {
                return false;
            }

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
                    blocks[x, y] = CreateBoardBlock(x, y, color, $"RestoredBlock_{x}_{y}_{color}");
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

        private void PopulateGenerationLineFill()
        {
            System.Array.Clear(generationRowFill, 0, generationRowFill.Length);
            System.Array.Clear(generationColumnFill, 0, generationColumnFill.Length);
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    if (blocks[x, y] == null)
                    {
                        continue;
                    }

                    generationRowFill[y]++;
                    generationColumnFill[x]++;
                }
            }
        }

        private void AddPieceToGenerationLineFill(PieceInstance piece, Vector2Int origin)
        {
            Vector2Int[] shapeCells = piece.Data.cells;
            for (int i = 0; i < shapeCells.Length; i++)
            {
                generationRowAdd[origin.y + shapeCells[i].y]++;
                generationColumnAdd[origin.x + shapeCells[i].x]++;
            }
        }

        private bool CanPlaceAfterVirtualPlacement(
            PieceInstance placedPiece,
            Vector2Int placedOrigin,
            PieceInstance candidatePiece,
            Vector2Int candidateOrigin)
        {
            Vector2Int[] candidateCells = candidatePiece.Data.cells;
            for (int i = 0; i < candidateCells.Length; i++)
            {
                int x = candidateOrigin.x + candidateCells[i].x;
                int y = candidateOrigin.y + candidateCells[i].y;
                if (!IsInside(x, y)
                    || blocks[x, y] != null
                    || PieceOccupiesCell(placedPiece, placedOrigin, x, y))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanPlaceAfterTwoVirtualPlacements(
            PieceInstance firstPiece,
            Vector2Int firstOrigin,
            PieceInstance secondPiece,
            Vector2Int secondOrigin,
            PieceInstance candidatePiece,
            Vector2Int candidateOrigin)
        {
            Vector2Int[] candidateCells = candidatePiece.Data.cells;
            for (int i = 0; i < candidateCells.Length; i++)
            {
                int x = candidateOrigin.x + candidateCells[i].x;
                int y = candidateOrigin.y + candidateCells[i].y;
                if (!IsInside(x, y)
                    || blocks[x, y] != null
                    || PieceOccupiesCell(firstPiece, firstOrigin, x, y)
                    || PieceOccupiesCell(secondPiece, secondOrigin, x, y))
                {
                    return false;
                }
            }

            return true;
        }

        private int CountExistingOrthogonalContacts(int x, int y)
        {
            int contacts = 0;
            contacts += HasExistingBlockAt(x - 1, y) ? 1 : 0;
            contacts += HasExistingBlockAt(x + 1, y) ? 1 : 0;
            contacts += HasExistingBlockAt(x, y - 1) ? 1 : 0;
            contacts += HasExistingBlockAt(x, y + 1) ? 1 : 0;
            return contacts;
        }

        private bool HasExistingBlockAt(int x, int y)
        {
            return IsInside(x, y) && blocks[x, y] != null;
        }

        private int CountGenerationIsolatedHolesAfterPlacement(PieceInstance piece, Vector2Int origin)
        {
            int isolatedHoles = 0;
            for (int y = 1; y < GameConstants.BoardSize - 1; y++)
            {
                for (int x = 1; x < GameConstants.BoardSize - 1; x++)
                {
                    if (blocks[x, y] != null || PieceOccupiesCell(piece, origin, x, y))
                    {
                        continue;
                    }

                    if (IsOccupiedAfterGenerationPlacement(piece, origin, x - 1, y)
                        && IsOccupiedAfterGenerationPlacement(piece, origin, x + 1, y)
                        && IsOccupiedAfterGenerationPlacement(piece, origin, x, y - 1)
                        && IsOccupiedAfterGenerationPlacement(piece, origin, x, y + 1))
                    {
                        isolatedHoles++;
                    }
                }
            }

            return isolatedHoles;
        }

        private bool IsOccupiedAfterGenerationPlacement(PieceInstance piece, Vector2Int origin, int x, int y)
        {
            return blocks[x, y] != null || PieceOccupiesCell(piece, origin, x, y);
        }

        private int ScoreGenerationLineProgress(int beforeFill, int afterFill)
        {
            int beforeProgress = Mathf.Max(0, beforeFill - 4);
            int afterProgress = Mathf.Max(0, afterFill - 4);
            return Mathf.Max(0, afterProgress * afterProgress - beforeProgress * beforeProgress);
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
                float lineDelay = lineOrder * 0.015f;
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    cells[x, y]?.PlaySweep(color, lineDelay + x * 0.007f, strong);
                }

                lineOrder++;
            }

            for (int i = 0; i < columns.Count; i++)
            {
                int x = columns[i];
                float lineDelay = lineOrder * 0.015f;
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    cells[x, y]?.PlaySweep(color, lineDelay + y * 0.007f, strong);
                }

                lineOrder++;
            }
        }

        private void PlayCompletedLineGlow(List<int> rows, List<int> columns, Color color, int pureLines)
        {
            if (!MobilePerformance.UseFullJuice() || lineClearEffectLayer == null)
            {
                return;
            }

            int totalLines = rows.Count + columns.Count;
            float intensity = Mathf.Clamp(
                1f + Mathf.Max(0, totalLines - 1) * 0.11f + Mathf.Max(0, pureLines) * 0.05f,
                1f,
                1.32f);
            int effectGeneration = lineClearEffectGeneration;
            int order = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                PlayLineGlow(rows[i], true, color, order++ * 0.011f, intensity, effectGeneration);
            }

            for (int i = 0; i < columns.Count; i++)
            {
                PlayLineGlow(columns[i], false, color, order++ * 0.011f, intensity, effectGeneration);
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
            const float duration = 0.075f;
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
                    0.032f + i * 0.004f,
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
            const float duration = 0.070f;
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

        private void SpawnClearParticles(BlockView block)
        {
            if (!MobilePerformance.UseFullJuice() || block == null || block.RectTransform == null)
            {
                return;
            }

            // Rewarded-revive clears are capped and intentionally retain their
            // existing presentation path. POP uses the pooled path below.
            if (clearParticlesByColor == null || clearParticlesByColor.Length <= (int)block.Color)
            {
                return;
            }

            ParticleSystem prefab = clearParticlesByColor[(int)block.Color];
            Camera camera = Camera.main;
            if (prefab == null || camera == null)
            {
                return;
            }

            Vector3 screen = RectTransformUtility.WorldToScreenPoint(null, block.RectTransform.position);
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(
                screen.x,
                screen.y,
                Mathf.Abs(camera.transform.position.z)));
            ParticleSystem particles = Instantiate(prefab, world, Quaternion.identity);
            particles.Play();
            Destroy(particles.gameObject, 2f);
        }

        private int BeginPopParticleSequence(int poppedTileCount)
        {
            nextPopParticleSequenceId++;
            if (nextPopParticleSequenceId == int.MaxValue)
            {
                nextPopParticleSequenceId = 1;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastPopPoppedTileCount = poppedTileCount;
            LastPopParticlePoolSize = GetTotalPooledParticleCount();
            LastPopPooledInstancesReused = 0;
            LastPopParticlePoolExpansions = 0;
            LastPopParticleInstantiations = 0;
#endif
            return nextPopParticleSequenceId;
        }

        private void SchedulePopClearParticles(
            BlockView block,
            float delay,
            float scaleMultiplier,
            int popSequenceId,
            Camera particleCamera)
        {
            if (!MobilePerformance.UseFullJuice() || block == null || block.RectTransform == null)
            {
                return;
            }

            Vector3 screen = RectTransformUtility.WorldToScreenPoint(null, block.RectTransform.position);
            if (!TryGetParticleWorldPosition(screen, particleCamera, out Vector3 world))
            {
                return;
            }

            scheduledPopParticleSpawns.Add(new ScheduledParticleSpawn
            {
                color = block.Color,
                worldPosition = world,
                scaleMultiplier = scaleMultiplier,
                spawnTime = Time.time + Mathf.Max(0f, delay),
                popSequenceId = popSequenceId
            });
        }

        private void ProcessScheduledPopParticleSpawns()
        {
            if (scheduledPopParticleSpawns.Count == 0)
            {
                return;
            }

            if (!MobilePerformance.UseFullJuice())
            {
                scheduledPopParticleSpawns.Clear();
                return;
            }

            float now = Time.time;
            for (int i = scheduledPopParticleSpawns.Count - 1; i >= 0; i--)
            {
                ScheduledParticleSpawn scheduled = scheduledPopParticleSpawns[i];
                if (scheduled.spawnTime > now)
                {
                    continue;
                }

                PlayPooledClearParticles(
                    scheduled.color,
                    scheduled.worldPosition,
                    scheduled.scaleMultiplier,
                    scheduled.popSequenceId);

                int lastIndex = scheduledPopParticleSpawns.Count - 1;
                scheduledPopParticleSpawns[i] = scheduledPopParticleSpawns[lastIndex];
                scheduledPopParticleSpawns.RemoveAt(lastIndex);
            }
        }

        private bool TryGetParticleWorldPosition(Vector3 screen, Camera camera, out Vector3 world)
        {
            if (camera == null)
            {
                world = Vector3.zero;
                return false;
            }

            world = camera.ScreenToWorldPoint(new Vector3(
                screen.x,
                screen.y,
                Mathf.Abs(camera.transform.position.z)));
            return true;
        }

        private void EnsureClearParticlePoolRoot()
        {
            if (clearParticlePoolRoot != null)
            {
                return;
            }

            GameObject poolObject = new GameObject("ClearParticlePool");
            clearParticlePoolRoot = poolObject.transform;
        }

        private void InitializeClearParticlePools()
        {
            for (int colorIndex = 0; colorIndex < GameConstants.ColorCount; colorIndex++)
            {
                if (clearParticlePools[colorIndex] == null)
                {
                    clearParticlePools[colorIndex] = new List<PooledClearParticle>(PopParticleMaximumPerColor);
                }
            }
        }

        private IEnumerator PrewarmClearParticlePool()
        {
            for (int instanceIndex = 0; instanceIndex < PopParticlePrewarmPerColor; instanceIndex++)
            {
                for (int colorIndex = 0; colorIndex < GameConstants.ColorCount; colorIndex++)
                {
                    CreatePooledClearParticle((ChromaColor)colorIndex);
                }

                // Spread the 32-instance prewarm across setup frames, never across a move.
                yield return null;
            }

            particlePoolPrewarmRoutine = null;
        }

        private List<PooledClearParticle> GetClearParticlePool(ChromaColor color)
        {
            int colorIndex = Mathf.Clamp((int)color, 0, GameConstants.ColorCount - 1);
            if (clearParticlePools[colorIndex] == null)
            {
                clearParticlePools[colorIndex] = new List<PooledClearParticle>(PopParticleMaximumPerColor);
            }

            return clearParticlePools[colorIndex];
        }

        private PooledClearParticle CreatePooledClearParticle(ChromaColor color)
        {
            if (clearParticlesByColor == null || clearParticlesByColor.Length <= (int)color)
            {
                return null;
            }

            ParticleSystem prefab = clearParticlesByColor[(int)color];
            if (prefab == null)
            {
                return null;
            }

            List<PooledClearParticle> pool = GetClearParticlePool(color);
            if (pool.Count >= PopParticleMaximumPerColor)
            {
                return null;
            }

            EnsureClearParticlePoolRoot();
            ParticleSystem particles = Instantiate(prefab, clearParticlePoolRoot);
            particles.name = $"Pooled_{prefab.name}_{pool.Count}";
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.gameObject.SetActive(false);

            PooledClearParticle pooled = new PooledClearParticle
            {
                particleSystem = particles,
                baseScale = particles.transform.localScale
            };
            pool.Add(pooled);
            return pooled;
        }

        private void PlayPooledClearParticles(ChromaColor color, Vector3 worldPosition, float scaleMultiplier, int popSequenceId)
        {
            if (!MobilePerformance.UseFullJuice())
            {
                return;
            }

            PooledClearParticle pooled = AcquirePooledClearParticle(color, popSequenceId);
            if (pooled == null || pooled.particleSystem == null)
            {
                return;
            }

            Transform particleTransform = pooled.particleSystem.transform;
            particleTransform.position = worldPosition;
            particleTransform.rotation = Quaternion.identity;
            particleTransform.localScale = pooled.baseScale * Mathf.Clamp(scaleMultiplier, 0.75f, 1.35f);

            pooled.particleSystem.gameObject.SetActive(true);
            pooled.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            pooled.particleSystem.Play(true);
            pooled.inUse = true;
            pooled.releaseTime = Time.time + PooledParticleReturnDelay;
            activeClearParticles.Add(pooled);
        }

        private PooledClearParticle AcquirePooledClearParticle(ChromaColor color, int popSequenceId)
        {
            List<PooledClearParticle> pool = GetClearParticlePool(color);
            for (int i = 0; i < pool.Count; i++)
            {
                PooledClearParticle pooled = pool[i];
                if (pooled != null && pooled.particleSystem != null && !pooled.inUse)
                {
                    RecordPooledParticleReuse(popSequenceId);
                    return pooled;
                }
            }

            if (pool.Count < PopParticleMaximumPerColor)
            {
                PooledClearParticle expanded = CreatePooledClearParticle(color);
                if (expanded != null)
                {
                    RecordPooledParticleExpansion(popSequenceId);
                    return expanded;
                }
            }

            PooledClearParticle oldestActive = null;
            for (int i = 0; i < pool.Count; i++)
            {
                PooledClearParticle pooled = pool[i];
                if (pooled != null
                    && pooled.particleSystem != null
                    && pooled.inUse
                    && (oldestActive == null || pooled.releaseTime < oldestActive.releaseTime))
                {
                    oldestActive = pooled;
                }
            }

            if (oldestActive == null)
            {
                return null;
            }

            ReturnPooledClearParticle(oldestActive);
            RecordPooledParticleReuse(popSequenceId);
            return oldestActive;
        }

        private void ReturnFinishedPooledParticles()
        {
            float now = Time.time;
            for (int i = activeClearParticles.Count - 1; i >= 0; i--)
            {
                PooledClearParticle pooled = activeClearParticles[i];
                if (pooled == null || pooled.particleSystem == null || now >= pooled.releaseTime)
                {
                    ReturnPooledClearParticle(pooled);
                }
            }
        }

        private void ReturnPooledClearParticle(PooledClearParticle pooled)
        {
            if (pooled == null)
            {
                return;
            }

            activeClearParticles.Remove(pooled);
            pooled.inUse = false;
            pooled.releaseTime = 0f;

            if (pooled.particleSystem == null)
            {
                return;
            }

            pooled.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Transform particleTransform = pooled.particleSystem.transform;
            particleTransform.position = Vector3.zero;
            particleTransform.rotation = Quaternion.identity;
            particleTransform.localScale = pooled.baseScale;
            pooled.particleSystem.gameObject.SetActive(false);
        }

        private int GetTotalPooledParticleCount()
        {
            int total = 0;
            for (int i = 0; i < clearParticlePools.Length; i++)
            {
                if (clearParticlePools[i] != null)
                {
                    total += clearParticlePools[i].Count;
                }
            }

            return total;
        }

        private void RecordPooledParticleReuse(int popSequenceId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (popSequenceId != 0 && popSequenceId == nextPopParticleSequenceId)
            {
                LastPopPooledInstancesReused++;
                LastPopParticlePoolSize = GetTotalPooledParticleCount();
            }
#endif
        }

        private void RecordPooledParticleExpansion(int popSequenceId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (popSequenceId != 0 && popSequenceId == nextPopParticleSequenceId)
            {
                LastPopParticlePoolExpansions++;
                LastPopParticleInstantiations++;
                LastPopParticlePoolSize = GetTotalPooledParticleCount();
            }
#endif
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

            if (completionGlowLayer == null && boardRoot != null)
            {
                Transform existing = boardRoot.Find("CompletionLineGlowLayer");
                completionGlowLayer = existing as RectTransform;
                if (completionGlowLayer == null)
                {
                    GameObject layer = new GameObject("CompletionLineGlowLayer", typeof(RectTransform));
                    completionGlowLayer = (RectTransform)layer.transform;
                    completionGlowLayer.SetParent(boardRoot, false);
                }

                completionGlowLayer.anchorMin = Vector2.zero;
                completionGlowLayer.anchorMax = Vector2.one;
                completionGlowLayer.offsetMin = Vector2.zero;
                completionGlowLayer.offsetMax = Vector2.zero;
                completionGlowLayer.localScale = Vector3.one;
                completionGlowLayer.SetAsLastSibling();
            }

        }

        private sealed class CompletionCellGlowVisual
        {
            public RectTransform root;
            public UnityEngine.UI.Image outerGlow;
            public UnityEngine.UI.Image coreRim;
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
