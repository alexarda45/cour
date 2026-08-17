using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class PieceSpawner : MonoBehaviour
    {
        // One presentation cell size for every piece in the active three-piece tray.
        // Board placement continues to use PieceView's board-scale transition.
        private const float SharedTrayCellSize = 54f;
        private static readonly Vector2 TraySize = new Vector2(960f, 280f);
        private static readonly Vector2 SlotSize = new Vector2(300f, 260f);
        private static readonly float[] SlotCentres = { -310f, 0f, 310f };

        private sealed class GenerationMetrics
        {
            public bool placementOptionsReady;
            public int placementOptions;
            public bool clearOpportunitiesReady;
            public int clearOpportunities;
            public bool setupOpportunityReady;
            public int setupOpportunity;

            public void Reset()
            {
                placementOptionsReady = false;
                clearOpportunitiesReady = false;
                setupOpportunityReady = false;
            }
        }

        [SerializeField] private TraySlot[] traySlots;
        [SerializeField] private PieceView piecePrefab;
        [SerializeField] private BlockView pieceBlockPrefab;
        [SerializeField] private RectTransform dragLayer;

        private GameManager gameManager;
        private float sharedPreviewCellSize = SharedTrayCellSize;
        private readonly Dictionary<string, GenerationMetrics> generationMetricCache = new Dictionary<string, GenerationMetrics>(32);
        private BoardManager generationMetricBoard;
        private bool generationEmptyCellsReady;
        private int generationEmptyCells;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int LastGenerationCandidateCount { get; private set; }
        public int LastGenerationMetricEvaluations { get; private set; }
        public int LastGenerationMetricCacheHits { get; private set; }
        public int LastGenerationChunks { get; private set; }
        public double LastGenerationElapsedMilliseconds { get; private set; }
#endif

        public void Initialize(GameManager owner)
        {
            gameManager = owner;
            EnsureRuntimePrefabs();
            EnsureTraySlots();
        }

        public void SpawnGuaranteedSet(BoardManager board, System.Random random)
        {
            SpawnGuaranteedSet(board, random, 0.5f);
        }

        public void SpawnGuaranteedSet(BoardManager board, System.Random random, float difficulty01)
        {
            SpawnGuaranteedSet(board, random, difficulty01, 0f);
        }

        public void SpawnOpeningSatisfyingSet(BoardManager board, System.Random random, float difficulty01)
        {
            EnsureTraySlots();
            if (board == null || random == null || traySlots == null || traySlots.Length == 0)
            {
                return;
            }

            if (board.CountEmptyCells() != GameConstants.BoardSize * GameConstants.BoardSize)
            {
                SpawnGuaranteedSet(board, random, difficulty01, 0f);
                return;
            }

            string[] recipe = random.NextDouble() < 0.64
                ? new[] { "square3", "square3", "rect2x3" }
                : new[] { "rect3x2", "rect3x2", "square2" };
            PieceInstance[] openingSet = new PieceInstance[GameConstants.TraySize];
            for (int i = 0; i < openingSet.Length; i++)
            {
                openingSet[i] = new PieceInstance(
                    recipe[i],
                    (ChromaColor)random.Next(GameConstants.ColorCount));
            }

            for (int i = openingSet.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                PieceInstance swap = openingSet[i];
                openingSet[i] = openingSet[swapIndex];
                openingSet[swapIndex] = swap;
            }

            SpawnSet(openingSet);
            RefreshFitHints(board);
        }

        public void SpawnGuaranteedSet(BoardManager board, System.Random random, float difficulty01, float assist01)
        {
            EnsureTraySlots();
            if (board == null || random == null || traySlots == null || traySlots.Length == 0)
            {
                return;
            }

            difficulty01 = Mathf.Clamp01(difficulty01);
            assist01 = Mathf.Clamp01(assist01);
            BeginGenerationMetricCache(board);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            LastGenerationCandidateCount = 0;
            LastGenerationMetricEvaluations = 0;
            LastGenerationMetricCacheHits = 0;
            LastGenerationChunks = 1;
#endif
            try
            {
                PieceInstance[] candidateSet = new PieceInstance[GameConstants.TraySize];
                PieceInstance[] bestSet = null;
                int bestScore = int.MinValue;
                int bestFitCount = 0;

                for (int attempt = 0; attempt < GameConstants.GuaranteedSetAttempts; attempt++)
                {
                    PieceCatalog.FillRandomSet(candidateSet, random, difficulty01);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LastGenerationCandidateCount++;
#endif
                    int fitCount = CountFittingPieces(board, candidateSet);
                    int score = ScoreSetForBoard(board, candidateSet, fitCount, difficulty01, assist01);

                    if (score > bestScore)
                    {
                        bestSet ??= new PieceInstance[GameConstants.TraySize];
                        Array.Copy(candidateSet, bestSet, GameConstants.TraySize);
                        bestScore = score;
                        bestFitCount = fitCount;
                    }
                }

                if (bestSet != null && bestFitCount > 0)
                {
                    ImproveSetWithRescuePieces(board, bestSet, random, difficulty01, assist01);
                    EnsureSatisfyingPiece(board, bestSet, random, difficulty01, assist01);
                    EnsureComebackPiece(board, bestSet, random, assist01);
                    EnsureImmediateClearPiece(board, bestSet, random, assist01);
                    EnsureJuicySetMass(board, bestSet, random, difficulty01);
                    EnsureSatisfyingSetShapeMix(board, bestSet, random, difficulty01, assist01);
                    SpawnSet(bestSet);
                    RefreshFitHints(board);
                    return;
                }

                PieceInstance[] fallback = new[]
                {
                    new PieceInstance("single", (ChromaColor)random.Next(GameConstants.ColorCount)),
                    new PieceInstance("single", (ChromaColor)random.Next(GameConstants.ColorCount)),
                    new PieceInstance("single", (ChromaColor)random.Next(GameConstants.ColorCount))
                };
                SpawnSet(fallback);
                RefreshFitHints(board);
            }
            finally
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                stopwatch.Stop();
                LastGenerationElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
#endif
                EndGenerationMetricCache();
            }
        }

        public void SpawnSet(PieceInstance[] set)
        {
            EnsureTraySlots();
            ClearTray();
            sharedPreviewCellSize = SharedTrayCellSize;
            for (int i = 0; i < traySlots.Length && i < set.Length; i++)
            {
                if (set[i] != null && traySlots[i] != null)
                {
                    SpawnPieceInSlot(set[i], traySlots[i], i);
                }
            }
        }

        public bool HasActivePieces()
        {
            EnsureTraySlots();
            for (int i = 0; i < traySlots.Length; i++)
            {
                if (traySlots[i] != null && !traySlots[i].IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }

        public bool AllSlotsEmpty()
        {
            EnsureTraySlots();
            for (int i = 0; i < traySlots.Length; i++)
            {
                if (traySlots[i] != null && !traySlots[i].IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }

        public bool AnyActivePieceFits(BoardManager board)
        {
            if (!HasActivePieces())
            {
                return true;
            }

            return board.CanAnyOfPiecesFit(GetActivePieceSnapshots());
        }

        public void RefreshFitHints(BoardManager board)
        {
            EnsureTraySlots();
            if (traySlots == null)
            {
                return;
            }

            for (int i = 0; i < traySlots.Length; i++)
            {
                PieceView piece = traySlots[i] == null ? null : traySlots[i].CurrentPiece;
                if (piece == null || piece.Instance == null)
                {
                    continue;
                }

                bool canFit = board != null && CanAnyPieceFit(board, piece.Instance);
                int clearOpportunities = canFit ? GetClearOpportunities(board, piece.Instance) : 0;
                piece.SetCanFitNow(canFit, clearOpportunities);
            }
        }

        public bool ShowBestMoveHint(BoardManager board)
        {
            EnsureTraySlots();
            if (board == null || traySlots == null)
            {
                return false;
            }

            PieceView bestPiece = null;
            Vector2Int bestOrigin = new Vector2Int(int.MinValue, int.MinValue);
            int bestScore = int.MinValue;
            for (int i = 0; i < traySlots.Length; i++)
            {
                PieceView piece = traySlots[i] == null ? null : traySlots[i].CurrentPiece;
                if (piece == null || piece.Instance == null)
                {
                    continue;
                }

                if (!board.TryFindBestPlacement(piece.Instance, out Vector2Int origin, out int score))
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestOrigin = origin;
                    bestPiece = piece;
                }
            }

            if (bestPiece == null)
            {
                return false;
            }

            board.ClearPreview();
            int pureLines;
            int lineCount = board.GetPlacementClearPreview(bestPiece.Instance, bestOrigin, out pureLines);
            if (lineCount > 0)
            {
                gameManager?.ShowPlacementPreview(lineCount, pureLines);
            }
            else
            {
                gameManager?.ShowSmartMoveHint();
            }

            bestPiece.PlayHintPulse();
            return true;
        }

        public PieceInstance[] GetActivePieceSnapshots()
        {
            EnsureTraySlots();
            PieceInstance[] snapshots = new PieceInstance[traySlots.Length];
            for (int i = 0; i < traySlots.Length; i++)
            {
                snapshots[i] = traySlots[i] == null ? null : traySlots[i].GetPieceSnapshot();
            }

            return snapshots;
        }

        public void RestoreTray(PieceInstance[] snapshots)
        {
            EnsureTraySlots();
            ClearTray();
            if (snapshots == null)
            {
                return;
            }

            sharedPreviewCellSize = SharedTrayCellSize;
            for (int i = 0; i < traySlots.Length && i < snapshots.Length; i++)
            {
                if (snapshots[i] != null)
                {
                    SpawnPieceInSlot(snapshots[i].Clone(), traySlots[i], i);
                }
            }
        }

        public void ClearTray()
        {
            EnsureTraySlots();
            for (int i = 0; i < traySlots.Length; i++)
            {
                if (traySlots[i] != null)
                {
                    traySlots[i].ClearAndDestroy();
                }
            }
        }

        private void SpawnPieceInSlot(PieceInstance instance, TraySlot slot, int slotIndex)
        {
            if (slot == null)
            {
                return;
            }

            EnsureRuntimePrefabs();

            PieceView piece = piecePrefab == null
                ? CreateRuntimePieceInstance()
                : Instantiate(piecePrefab);

            piece.name = $"Piece_{instance.shapeId}_{instance.color}";
            piece.Initialize(instance.Clone(), slot, gameManager, pieceBlockPrefab, dragLayer, sharedPreviewCellSize);
            slot.SetPiece(piece);
            piece.PlaySpawnReveal(slotIndex * 0.055f);
        }

        private void EnsureRuntimePrefabs()
        {
            if (pieceBlockPrefab == null)
            {
                GameObject blockObject = new GameObject("RuntimePieceBlockTemplate", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(BlockView));
                blockObject.transform.SetParent(transform, false);
                blockObject.SetActive(false);
                pieceBlockPrefab = blockObject.GetComponent<BlockView>();
            }
        }

        private void EnsureTraySlots()
        {
            if (traySlots != null && traySlots.Length > 0)
            {
                ConfigureReferenceTrayLayout();
                return;
            }

            traySlots = FindObjectsByType<TraySlot>(FindObjectsInactive.Include);
            if (traySlots == null || traySlots.Length == 0)
            {
                TraySlot[] allSlots = Resources.FindObjectsOfTypeAll<TraySlot>();
                int sceneSlotCount = 0;
                for (int i = 0; i < allSlots.Length; i++)
                {
                    if (allSlots[i] != null && allSlots[i].gameObject.scene.IsValid())
                    {
                        sceneSlotCount++;
                    }
                }

                traySlots = new TraySlot[sceneSlotCount];
                int writeIndex = 0;
                for (int i = 0; i < allSlots.Length; i++)
                {
                    if (allSlots[i] != null && allSlots[i].gameObject.scene.IsValid())
                    {
                        traySlots[writeIndex] = allSlots[i];
                        writeIndex++;
                    }
                }
            }

            Array.Sort(traySlots, (a, b) => string.CompareOrdinal(a.name, b.name));
            ConfigureReferenceTrayLayout();
        }

        private void ConfigureReferenceTrayLayout()
        {
            if (traySlots == null || traySlots.Length == 0)
            {
                return;
            }

            RectTransform trayRect = traySlots[0] == null ? null : traySlots[0].transform.parent as RectTransform;
            if (trayRect != null)
            {
                trayRect.anchorMin = new Vector2(0.5f, 0.15f);
                trayRect.anchorMax = trayRect.anchorMin;
                trayRect.pivot = new Vector2(0.5f, 0.5f);
                trayRect.anchoredPosition = Vector2.zero;
                trayRect.sizeDelta = TraySize;
                trayRect.localScale = Vector3.one;

                Image trayImage = trayRect.GetComponent<Image>();
                if (trayImage != null)
                {
                    trayImage.color = Color.clear;
                    trayImage.enabled = false;
                    trayImage.raycastTarget = false;
                }

                Outline trayOutline = trayRect.GetComponent<Outline>();
                if (trayOutline != null)
                {
                    trayOutline.enabled = false;
                }

                Shadow[] trayShadows = trayRect.GetComponents<Shadow>();
                for (int i = 0; i < trayShadows.Length; i++)
                {
                    if (trayShadows[i] != null)
                    {
                        trayShadows[i].enabled = false;
                    }
                }
            }

            for (int i = 0; i < traySlots.Length && i < SlotCentres.Length; i++)
            {
                RectTransform slotRect = traySlots[i] == null ? null : traySlots[i].transform as RectTransform;
                if (slotRect == null)
                {
                    continue;
                }

                slotRect.anchorMin = new Vector2(0.5f, 0.5f);
                slotRect.anchorMax = slotRect.anchorMin;
                slotRect.pivot = new Vector2(0.5f, 0.5f);
                slotRect.anchoredPosition = new Vector2(SlotCentres[i], 0f);
                slotRect.sizeDelta = SlotSize;
                slotRect.localScale = Vector3.one;
            }
        }

        private PieceView CreateRuntimePieceInstance()
        {
            GameObject pieceObject = new GameObject("RuntimePiece", typeof(RectTransform), typeof(CanvasGroup), typeof(PieceView));
            return pieceObject.GetComponent<PieceView>();
        }

        private void BeginGenerationMetricCache(BoardManager board)
        {
            foreach (GenerationMetrics metrics in generationMetricCache.Values)
            {
                metrics.Reset();
            }

            generationMetricBoard = board;
            generationEmptyCellsReady = false;
            generationEmptyCells = 0;
        }

        private void EndGenerationMetricCache()
        {
            generationMetricBoard = null;
            generationEmptyCellsReady = false;
        }

        private GenerationMetrics GetGenerationMetrics(BoardManager board, PieceInstance piece)
        {
            if (board == null || piece == null || generationMetricBoard != board)
            {
                return null;
            }

            string shapeId = piece.shapeId;
            if (!generationMetricCache.TryGetValue(shapeId, out GenerationMetrics metrics))
            {
                metrics = new GenerationMetrics();
                generationMetricCache.Add(shapeId, metrics);
            }
            return metrics;
        }

        private int GetEmptyCells(BoardManager board)
        {
            if (board == null)
            {
                return 0;
            }

            if (generationMetricBoard != board)
            {
                return board.CountEmptyCells();
            }

            if (!generationEmptyCellsReady)
            {
                generationEmptyCells = board.CountEmptyCells();
                generationEmptyCellsReady = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastGenerationMetricEvaluations++;
#endif
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                LastGenerationMetricCacheHits++;
            }
#endif
            return generationEmptyCells;
        }

        private int GetPlacementOptions(BoardManager board, PieceInstance piece)
        {
            GenerationMetrics metrics = GetGenerationMetrics(board, piece);
            if (metrics == null)
            {
                return board == null ? 0 : board.CountPlacementOptions(piece);
            }

            if (!metrics.placementOptionsReady)
            {
                metrics.placementOptions = board.CountPlacementOptions(piece);
                metrics.placementOptionsReady = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastGenerationMetricEvaluations++;
#endif
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                LastGenerationMetricCacheHits++;
            }
#endif
            return metrics.placementOptions;
        }

        private bool CanAnyPieceFit(BoardManager board, PieceInstance piece)
        {
            return GetPlacementOptions(board, piece) > 0;
        }

        private int GetClearOpportunities(BoardManager board, PieceInstance piece)
        {
            GenerationMetrics metrics = GetGenerationMetrics(board, piece);
            if (metrics == null)
            {
                return board == null ? 0 : board.CountClearOpportunities(piece);
            }

            if (!metrics.clearOpportunitiesReady)
            {
                metrics.clearOpportunities = board.CountClearOpportunities(piece);
                metrics.clearOpportunitiesReady = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastGenerationMetricEvaluations++;
#endif
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                LastGenerationMetricCacheHits++;
            }
#endif
            return metrics.clearOpportunities;
        }

        private int GetSetupOpportunity(BoardManager board, PieceInstance piece)
        {
            GenerationMetrics metrics = GetGenerationMetrics(board, piece);
            if (metrics == null)
            {
                return board == null ? 0 : board.ScoreBestSetupOpportunity(piece);
            }

            if (!metrics.setupOpportunityReady)
            {
                metrics.setupOpportunity = board.ScoreBestSetupOpportunity(piece);
                metrics.setupOpportunityReady = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastGenerationMetricEvaluations++;
#endif
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                LastGenerationMetricCacheHits++;
            }
#endif
            return metrics.setupOpportunity;
        }

        private int CountFittingPieces(BoardManager board, PieceInstance[] set)
        {
            int count = 0;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null && CanAnyPieceFit(board, set[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private int ScoreSetForBoard(BoardManager board, PieceInstance[] set, int fitCount, float difficulty01, float assist01)
        {
            int emptyCells = GetEmptyCells(board);
            float tightBoard01 = Mathf.Clamp01((28f - emptyCells) / 20f);
            int placementOptions = 0;
            int clearOpportunities = 0;
            int setupOpportunities = 0;
            int totalCells = 0;
            int largestPiece = 0;
            int mediumPieceCount = 0;
            int largePieceCount = 0;
            int duplicatePenalty = 0;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null)
                {
                    placementOptions += GetPlacementOptions(board, set[i]);
                    clearOpportunities += GetClearOpportunities(board, set[i]);
                    setupOpportunities += GetSetupOpportunity(board, set[i]);
                    int cells = set[i].Data.cells.Length;
                    totalCells += cells;
                    largestPiece = Mathf.Max(largestPiece, cells);
                    if (cells >= 4)
                    {
                        mediumPieceCount++;
                    }

                    if (cells >= 5)
                    {
                        largePieceCount++;
                    }
                }
            }

            for (int a = 0; a < set.Length; a++)
            {
                for (int b = a + 1; b < set.Length; b++)
                {
                    if (set[a] != null && set[b] != null && set[a].shapeId == set[b].shapeId)
                    {
                        duplicatePenalty += 280;
                    }
                }
            }

            float openBoard01 = Mathf.Clamp01((emptyCells - 18f) / 28f);
            float targetCells = Mathf.Lerp(12.6f, 16.4f, difficulty01) - tightBoard01 * 2.1f;
            int targetScore = Mathf.RoundToInt(920f - Mathf.Abs(totalCells - targetCells) * 76f);
            int mobilityWeight = Mathf.RoundToInt(Mathf.Lerp(52f, 30f, difficulty01) + tightBoard01 * 38f);
            int earlyLargePenalty = difficulty01 < 0.16f && largestPiece >= 6 ? 120 : 0;
            int tightLargePenalty = tightBoard01 > 0.62f && largestPiece >= 6 ? Mathf.RoundToInt(tightBoard01 * 340f) : 0;
            int allFitBonus = fitCount == GameConstants.TraySize ? 1100 : 0;
            int multiFitBonus = fitCount >= 2 ? 900 : 0;
            int clearBonus = clearOpportunities * Mathf.RoundToInt(Mathf.Lerp(700f, 420f, difficulty01));
            int setupBonus = setupOpportunities * Mathf.RoundToInt(Mathf.Lerp(42f, 30f, difficulty01));
            int satisfyingBonus = Mathf.RoundToInt(openBoard01 * (mediumPieceCount * 560f + largePieceCount * 680f));
            int miniPiecePenalty = CalculateMiniPiecePenalty(set, emptyCells, assist01);
            int shapeMixBonus = CalculateShapeMixBonus(set, emptyCells, openBoard01);
            int comebackBonus = Mathf.RoundToInt(assist01 * (clearOpportunities * 1550f + setupOpportunities * 58f + fitCount * 460f));
            int comebackNoProgressPenalty = assist01 > 0.25f && clearOpportunities == 0 && setupOpportunities < 85
                ? Mathf.RoundToInt(assist01 * 1400f)
                : 0;
            int tightSurvivalBonus = Mathf.RoundToInt(tightBoard01 * (clearOpportunities * 780f + setupOpportunities * 18f + fitCount * 620f));
            int tightNoProgressPenalty = tightBoard01 > 0.52f && clearOpportunities == 0 && setupOpportunities < 70
                ? Mathf.RoundToInt(tightBoard01 * 950f)
                : 0;

            return fitCount * 12000
                + placementOptions * mobilityWeight
                + clearBonus
                + setupBonus
                + targetScore
                + allFitBonus
                + multiFitBonus
                + satisfyingBonus
                + shapeMixBonus
                + comebackBonus
                + tightSurvivalBonus
                - earlyLargePenalty
                - tightLargePenalty
                - miniPiecePenalty
                - comebackNoProgressPenalty
                - tightNoProgressPenalty
                - duplicatePenalty;
        }

        private void ImproveSetWithRescuePieces(BoardManager board, PieceInstance[] set, System.Random random, float difficulty01, float assist01)
        {
            int emptyCells = GetEmptyCells(board);
            int targetFitCount = emptyCells >= 14 && difficulty01 < 0.80f ? 3 : emptyCells >= 6 ? 2 : 1;
            if (assist01 > 0.35f && emptyCells >= 10)
            {
                targetFitCount = 3;
            }

            int guard = 0;
            while (CountFittingPieces(board, set) < targetFitCount && guard < GameConstants.TraySize)
            {
                guard++;
                int replaceIndex = FindWeakestPieceIndex(board, set);
                if (replaceIndex < 0)
                {
                    return;
                }

                PieceInstance rescue = FindBestRescuePiece(board, random);
                if (rescue == null)
                {
                    return;
                }

                set[replaceIndex] = rescue;
            }
        }

        private void EnsureSatisfyingPiece(BoardManager board, PieceInstance[] set, System.Random random, float difficulty01, float assist01)
        {
            int emptyCells = GetEmptyCells(board);
            if (emptyCells < 18 || (difficulty01 > 0.92f && assist01 < 0.45f))
            {
                return;
            }

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null && set[i].Data.cells.Length >= 4 && CanAnyPieceFit(board, set[i]))
                {
                    return;
                }
            }

            PieceInstance largePiece = FindBestLargeFittingPiece(board, random);
            if (largePiece == null)
            {
                return;
            }

            int replaceIndex = FindSmallestFittingPieceIndex(board, set);
            if (replaceIndex < 0)
            {
                replaceIndex = FindWeakestPieceIndex(board, set);
            }

            if (replaceIndex >= 0)
            {
                set[replaceIndex] = largePiece;
            }
        }

        private void EnsureComebackPiece(BoardManager board, PieceInstance[] set, System.Random random, float assist01)
        {
            if (assist01 < 0.22f)
            {
                return;
            }

            int bestCurrentProgress = 0;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null || !CanAnyPieceFit(board, set[i]))
                {
                    continue;
                }

                bestCurrentProgress = Mathf.Max(
                    bestCurrentProgress,
                    GetClearOpportunities(board, set[i]) * 1900 + GetSetupOpportunity(board, set[i]));
            }

            int threshold = assist01 > 0.70f ? 450 : 1050;
            if (bestCurrentProgress >= threshold)
            {
                return;
            }

            PieceInstance comeback = FindBestComebackPiece(board, random, assist01);
            if (comeback == null)
            {
                return;
            }

            int replaceIndex = FindWeakestPieceIndex(board, set);
            if (replaceIndex >= 0)
            {
                set[replaceIndex] = comeback;
            }
        }

        private void EnsureImmediateClearPiece(BoardManager board, PieceInstance[] set, System.Random random, float assist01)
        {
            if (assist01 < 0.46f)
            {
                return;
            }

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null && GetClearOpportunities(board, set[i]) > 0)
                {
                    return;
                }
            }

            PieceInstance clearingPiece = FindBestImmediateClearPiece(board, random, assist01);
            if (clearingPiece == null)
            {
                return;
            }

            int replaceIndex = FindWeakestPieceIndex(board, set);
            if (replaceIndex >= 0)
            {
                set[replaceIndex] = clearingPiece;
            }
        }

        private void EnsureJuicySetMass(BoardManager board, PieceInstance[] set, System.Random random, float difficulty01)
        {
            int emptyCells = GetEmptyCells(board);
            if (emptyCells < 18 || set == null)
            {
                return;
            }

            int targetTotalCells = emptyCells >= 34 ? 15 : emptyCells >= 26 ? 13 : 11;
            if (difficulty01 < 0.18f)
            {
                targetTotalCells--;
            }

            int guard = 0;
            while (TotalPieceCells(set) < targetTotalCells && guard < 2)
            {
                guard++;
                int replaceIndex = FindSmallestNonClearingPieceIndex(board, set);
                if (replaceIndex < 0)
                {
                    return;
                }

                int currentCells = set[replaceIndex] == null ? 0 : set[replaceIndex].Data.cells.Length;
                PieceInstance largerPiece = FindBestLargeFittingPiece(board, random);
                if (largerPiece == null || largerPiece.Data.cells.Length <= currentCells)
                {
                    return;
                }

                set[replaceIndex] = largerPiece;
            }
        }

        private void EnsureSatisfyingSetShapeMix(BoardManager board, PieceInstance[] set, System.Random random, float difficulty01, float assist01)
        {
            if (set == null || board == null)
            {
                return;
            }

            int emptyCells = GetEmptyCells(board);
            if (emptyCells < 16)
            {
                return;
            }

            int smallPieces = CountPiecesAtMost(set, 3);
            int largePieces = CountPiecesAtLeast(set, 5);
            int targetLargePieces = emptyCells >= 34 ? 2 : 1;
            if (assist01 > 0.60f)
            {
                targetLargePieces = 1;
            }

            int guard = 0;
            while ((smallPieces >= 2 || largePieces < targetLargePieces) && guard < 2)
            {
                guard++;
                int replaceIndex = FindSmallestNonClearingPieceIndex(board, set);
                if (replaceIndex < 0)
                {
                    replaceIndex = FindSmallestFittingPieceIndex(board, set);
                }

                if (replaceIndex < 0)
                {
                    return;
                }

                int currentCells = set[replaceIndex] == null ? 0 : set[replaceIndex].Data.cells.Length;
                PieceInstance largerPiece = FindBestLargeFittingPiece(board, random);
                if (largerPiece == null || largerPiece.Data.cells.Length <= currentCells)
                {
                    return;
                }

                if (difficulty01 < 0.18f && largerPiece.Data.cells.Length >= 6 && emptyCells < 38)
                {
                    return;
                }

                set[replaceIndex] = largerPiece;
                smallPieces = CountPiecesAtMost(set, 3);
                largePieces = CountPiecesAtLeast(set, 5);
            }
        }

        private int CalculateMiniPiecePenalty(PieceInstance[] set, int emptyCells, float assist01)
        {
            int smallPieces = CountPiecesAtMost(set, 3);
            if (smallPieces <= 1 || emptyCells < 15 || assist01 > 0.70f)
            {
                return 0;
            }

            int penalty = smallPieces == 2 ? 850 : 1900;
            if (emptyCells >= 30)
            {
                penalty += 650;
            }

            return penalty;
        }

        private int CalculateShapeMixBonus(PieceInstance[] set, int emptyCells, float openBoard01)
        {
            if (set == null || emptyCells < 16)
            {
                return 0;
            }

            int largePieces = CountPiecesAtLeast(set, 5);
            int mediumPieces = CountPiecesBetween(set, 4, 5);
            int smallPieces = CountPiecesAtMost(set, 2);
            int bonus = Mathf.RoundToInt(openBoard01 * (largePieces * 420f + mediumPieces * 260f));
            if (largePieces >= 1 && smallPieces <= 1)
            {
                bonus += 520;
            }

            if (emptyCells >= 34 && largePieces >= 2)
            {
                bonus += 780;
            }

            return bonus;
        }

        private int CountPiecesAtMost(PieceInstance[] set, int maxCells)
        {
            int count = 0;
            if (set == null)
            {
                return count;
            }

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null && set[i].Data.cells.Length <= maxCells)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountPiecesAtLeast(PieceInstance[] set, int minCells)
        {
            int count = 0;
            if (set == null)
            {
                return count;
            }

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null && set[i].Data.cells.Length >= minCells)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountPiecesBetween(PieceInstance[] set, int minCells, int maxCells)
        {
            int count = 0;
            if (set == null)
            {
                return count;
            }

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null)
                {
                    continue;
                }

                int cells = set[i].Data.cells.Length;
                if (cells >= minCells && cells <= maxCells)
                {
                    count++;
                }
            }

            return count;
        }

        private int TotalPieceCells(PieceInstance[] set)
        {
            int total = 0;
            if (set == null)
            {
                return total;
            }

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null)
                {
                    total += set[i].Data.cells.Length;
                }
            }

            return total;
        }

        private int FindSmallestNonClearingPieceIndex(BoardManager board, PieceInstance[] set)
        {
            int bestIndex = -1;
            int bestCells = int.MaxValue;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null)
                {
                    return i;
                }

                if (GetClearOpportunities(board, set[i]) > 0)
                {
                    continue;
                }

                int cells = set[i].Data.cells.Length;
                if (cells < bestCells)
                {
                    bestCells = cells;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private int FindSmallestFittingPieceIndex(BoardManager board, PieceInstance[] set)
        {
            int bestIndex = -1;
            int bestCells = int.MaxValue;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null)
                {
                    return i;
                }

                int cells = set[i].Data.cells.Length;
                if (cells < bestCells && CanAnyPieceFit(board, set[i]))
                {
                    bestCells = cells;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private int FindWeakestPieceIndex(BoardManager board, PieceInstance[] set)
        {
            int weakestIndex = -1;
            int weakestOptions = int.MaxValue;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null)
                {
                    return i;
                }

                int options = GetPlacementOptions(board, set[i]);
                if (options < weakestOptions)
                {
                    weakestOptions = options;
                    weakestIndex = i;
                }
            }

            return weakestIndex;
        }

        private PieceInstance FindBestRescuePiece(BoardManager board, System.Random random)
        {
            int emptyCells = GetEmptyCells(board);
            string[] rescueIds = emptyCells >= 18
                ? new[]
                {
                    "line5_h",
                    "line5_v",
                    "square3",
                    "rect2x3",
                    "rect3x2",
                    "line4_h",
                    "line4_v",
                    "square2",
                    "l4",
                    "l4_m",
                    "l4_r",
                    "l4_rm",
                    "t4",
                    "t4_v",
                    "s4",
                    "z4",
                    "line3_h",
                    "line3_v"
                }
                : emptyCells >= 10
                ? new[]
                {
                    "line4_h",
                    "line4_v",
                    "square2",
                    "l4",
                    "l4_m",
                    "t4",
                    "line3_h",
                    "line3_v",
                    "corner3",
                    "corner3_m"
                }
                : new[]
            {
                "single",
                "line2_h",
                "line2_v",
                "corner3",
                "corner3_m",
                "line3_h",
                "line3_v",
                "square2"
            };

            PieceInstance best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < rescueIds.Length; i++)
            {
                PieceInstance candidate = new PieceInstance(rescueIds[i], (ChromaColor)random.Next(GameConstants.ColorCount));
                int options = GetPlacementOptions(board, candidate);
                if (options <= 0)
                {
                    continue;
                }

                int cells = candidate.Data.cells.Length;
                int score = options * (emptyCells >= 18 ? 70 : 100)
                    + GetClearOpportunities(board, candidate) * 950
                    + GetSetupOpportunity(board, candidate) * 42
                    + cells * (emptyCells >= 18 ? 160 : 40);
                if (emptyCells >= 18 && cells >= 4)
                {
                    score += 280;
                }

                if (emptyCells >= 18 && cells <= 2)
                {
                    score -= 500;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private PieceInstance FindBestLargeFittingPiece(BoardManager board, System.Random random)
        {
            string[] candidateIds =
            {
                "line5_h",
                "line5_v",
                "square3",
                "rect2x3",
                "rect3x2",
                "plus5",
                "stair5",
                "line4_h",
                "line4_v",
                "square2",
                "l4",
                "l4_m",
                "l4_r",
                "l4_rm",
                "t4",
                "t4_v",
                "s4",
                "z4"
            };

            int emptyCells = GetEmptyCells(board);
            PieceInstance best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < candidateIds.Length; i++)
            {
                PieceInstance candidate = new PieceInstance(candidateIds[i], (ChromaColor)random.Next(GameConstants.ColorCount));
                int options = GetPlacementOptions(board, candidate);
                if (options <= 0)
                {
                    continue;
                }

                int cells = candidate.Data.cells.Length;
                int score = options * 72
                    + GetClearOpportunities(board, candidate) * 1000
                    + GetSetupOpportunity(board, candidate) * 45
                    + cells * 140
                    + random.Next(18);
                if (candidate.shapeId.StartsWith("line4", StringComparison.Ordinal)
                    || candidate.shapeId.StartsWith("line5", StringComparison.Ordinal)
                    || candidate.shapeId.StartsWith("rect", StringComparison.Ordinal)
                    || candidate.shapeId == "square2"
                    || candidate.shapeId == "square3")
                {
                    score += 260;
                }

                if (emptyCells < 24 && cells >= 6)
                {
                    score -= 220;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private PieceInstance FindBestComebackPiece(BoardManager board, System.Random random, float assist01)
        {
            PieceInstance best = null;
            int bestScore = int.MinValue;
            int emptyCells = GetEmptyCells(board);
            for (int i = 0; i < PieceCatalog.All.Count; i++)
            {
                PieceData data = PieceCatalog.All[i];
                PieceInstance candidate = new PieceInstance(data.id, (ChromaColor)random.Next(GameConstants.ColorCount));
                int options = GetPlacementOptions(board, candidate);
                if (options <= 0)
                {
                    continue;
                }

                int clearOpportunities = GetClearOpportunities(board, candidate);
                int setupOpportunities = GetSetupOpportunity(board, candidate);
                int cells = data.cells.Length;
                int score = clearOpportunities * Mathf.RoundToInt(Mathf.Lerp(1900f, 3600f, assist01))
                    + setupOpportunities * Mathf.RoundToInt(Mathf.Lerp(38f, 72f, assist01))
                    + options * (emptyCells < 16 ? 105 : 68)
                    + cells * (emptyCells >= 18 ? 125 : 44)
                    + random.Next(22);

                if (clearOpportunities == 0 && setupOpportunities < 50)
                {
                    score -= Mathf.RoundToInt(assist01 * 900f);
                }

                if (assist01 > 0.65f && clearOpportunities > 0)
                {
                    score += 900;
                }

                if (emptyCells >= 20 && cells >= 4)
                {
                    score += 220;
                }

                if (emptyCells < 14 && cells >= 6)
                {
                    score -= 360;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private PieceInstance FindBestImmediateClearPiece(BoardManager board, System.Random random, float assist01)
        {
            PieceInstance best = null;
            int bestScore = int.MinValue;
            int emptyCells = GetEmptyCells(board);
            for (int i = 0; i < PieceCatalog.All.Count; i++)
            {
                PieceData data = PieceCatalog.All[i];
                PieceInstance candidate = new PieceInstance(data.id, (ChromaColor)random.Next(GameConstants.ColorCount));
                int clearOpportunities = GetClearOpportunities(board, candidate);
                if (clearOpportunities <= 0)
                {
                    continue;
                }

                int options = GetPlacementOptions(board, candidate);
                int cells = data.cells.Length;
                int score = clearOpportunities * Mathf.RoundToInt(Mathf.Lerp(5200f, 7600f, assist01))
                    + options * (emptyCells < 16 ? 75 : 48)
                    + GetSetupOpportunity(board, candidate) * 16
                    + cells * (emptyCells >= 20 ? 120 : 42)
                    + random.Next(24);

                if (clearOpportunities >= 2)
                {
                    score += 1500;
                }

                if (emptyCells < 13 && cells >= 6)
                {
                    score -= 420;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
