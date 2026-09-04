using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class PieceSpawner : MonoBehaviour
    {
        private static readonly ProfilerMarker GenerateTrayMarker = new ProfilerMarker("ChromaBlast.Generation.GenerateNextTray");
        private static readonly ProfilerMarker CandidateEvaluationMarker = new ProfilerMarker("ChromaBlast.Generation.Evaluate56Candidates");
        private static readonly ProfilerMarker DeepProjectionMarker = new ProfilerMarker("ChromaBlast.Generation.DeepTop4Projection");
        private static readonly ProfilerMarker ContinuationConstructionMarker = new ProfilerMarker("ChromaBlast.Generation.ConstructContinuation");
        public enum BoardOccupancyState
        {
            Open,
            Balanced,
            Pressured,
            Critical
        }

        // One presentation cell size for every piece in the active three-piece tray.
        // Board placement continues to use PieceView's board-scale transition.
        private const float SharedTrayCellSize = 54f;
        private static readonly Vector2 TraySize = new Vector2(960f, 280f);
        private static readonly Vector2 SlotSize = new Vector2(300f, 260f);
        private static readonly float[] SlotCentres = { -310f, 0f, 310f };

        private sealed class GenerationMetrics
        {
            public bool profileReady;
            public GenerationPlacementProfile profile;
            public bool placementOptionsReady;
            public int placementOptions;
            public bool clearOpportunitiesReady;
            public int clearOpportunities;
            public bool setupOpportunityReady;
            public int setupOpportunity;

            public void Reset()
            {
                profileReady = false;
                placementOptionsReady = false;
                clearOpportunitiesReady = false;
                setupOpportunityReady = false;
            }
        }

        private readonly struct SetupPayoffKey : IEquatable<SetupPayoffKey>
        {
            private readonly string setupShapeId;
            private readonly string payoffShapeId;

            public SetupPayoffKey(string setupShapeId, string payoffShapeId)
            {
                this.setupShapeId = setupShapeId;
                this.payoffShapeId = payoffShapeId;
            }

            public bool Equals(SetupPayoffKey other)
            {
                return string.Equals(setupShapeId, other.setupShapeId, StringComparison.Ordinal)
                    && string.Equals(payoffShapeId, other.payoffShapeId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is SetupPayoffKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((setupShapeId == null ? 0 : setupShapeId.GetHashCode()) * 397)
                        ^ (payoffShapeId == null ? 0 : payoffShapeId.GetHashCode());
                }
            }
        }

        private struct SetupPayoffAnalysis
        {
            public int legacyScore;
            public int pureScore;
        }

        // FlowState is intentionally run-temporary. It captures up to two useful
        // lines from the actual board after a tray is consumed; it is never saved
        // and never dictates an exact placement for the next tray.
        private struct FlowTarget
        {
            public bool row;
            public int lineIndex;
            public int filledCells;
        }

        private struct AssistDifficultyProfile
        {
            public int easeScore;
            public int totalPlacementOptions;
            public int minimumPlacementOptions;
            public int immediateClears;
            public int setup;
            public int cleanliness;
        }

        [SerializeField] private TraySlot[] traySlots;
        [SerializeField] private PieceView piecePrefab;
        [SerializeField] private BlockView pieceBlockPrefab;
        [SerializeField] private RectTransform dragLayer;

        private GameManager gameManager;
        private float sharedPreviewCellSize = SharedTrayCellSize;
        private readonly Dictionary<string, GenerationMetrics> generationMetricCache = new Dictionary<string, GenerationMetrics>(32);
        private readonly Dictionary<SetupPayoffKey, int> generationSetupPayoffCache = new Dictionary<SetupPayoffKey, int>(128);
        private BoardManager generationMetricBoard;
        private bool generationEmptyCellsReady;
        private int generationEmptyCells;
        private const int OpenPureSetupDiversityBonus = 2500;
        private const int RelaxFlowCandidateCapacity = 32;
        private const int RelaxFlowDeepEvaluationCount = 6;
        private const int LateChallengeCandidateCapacity = GameConstants.GuaranteedSetAttempts;
        private const float ClassicEarlyFlowStrength = 3.15f;
        private const float ClassicMidFlowStrength = 3.00f;
        private const float ClassicTransitionFlowStrength = 1.65f;
        private const float ClassicRecoveryFlowStrength = 0.75f;
        private const float ClassicEarlyMidFlowBoost = 2.60f;
        private const float ClassicTransitionFlowBoost = 2.00f;
        private const float ClassicRecoveryFlowBoost = 1.25f;
        private const float ClassicEarlyMidProjectionWeight = 1.18f;
        private const float ClassicTransitionProjectionWeight = 1.20f;
        private const float ClassicRecoveryProjectionWeight = 0.65f;
        private const float ClassicReliefLoopAssistScale = 0.68f;
        private const int ClassicFlowRecoveryTrayWindow = 3;
        private const int ClassicEarlyBuildFlexBonus = 3300;
        private const int ClassicEarlyMidOpenClearSaturationPenalty = 180;
        private static readonly string[] ContinuationShapeIds =
        {
            "single",
            "line2_h", "line2_v",
            "line3_h", "line3_v",
            "line4_h", "line4_v",
            "line5_h", "line5_v",
            "square2",
            "corner3", "corner3_m",
            "l4", "l4_m", "l4_r", "l4_rm"
        };
        private readonly FlowTarget[] flowTargets = new FlowTarget[2];
        private int flowTargetCount;
        private int classicFlowRecoveryTraysRemaining;
        private PieceInstance[] continuationShapeCandidates;
        private readonly PieceInstance[] constructedContinuationSet = new PieceInstance[GameConstants.TraySize];
        private readonly PieceInstance[][] relaxFlowCandidateSets =
            CreatePieceSetBuffers(RelaxFlowCandidateCapacity);
        private readonly int[] relaxFlowCandidateScores = new int[RelaxFlowCandidateCapacity];
        private readonly int[] relaxFlowCandidateFitCounts = new int[RelaxFlowCandidateCapacity];
        private int relaxFlowCandidateCount;
        private PieceInstance[][] lateChallengeCandidateSets;
        private readonly int[] lateChallengeCandidateScores = new int[LateChallengeCandidateCapacity];
        private readonly int[] lateChallengeCandidateEaseScores = new int[LateChallengeCandidateCapacity];
        private readonly int[] lateChallengeCandidateFitCounts = new int[LateChallengeCandidateCapacity];
        private int lateChallengeCandidateCount;

        private static PieceInstance[][] CreatePieceSetBuffers(int count)
        {
            PieceInstance[][] buffers = new PieceInstance[count][];
            for (int i = 0; i < count; i++)
            {
                buffers[i] = new PieceInstance[GameConstants.TraySize];
            }

            return buffers;
        }

        public bool LastGenerationReliefBiased { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int LastGenerationCandidateCount { get; private set; }
        public int LastGenerationMetricEvaluations { get; private set; }
        public int LastGenerationMetricCacheHits { get; private set; }
        public int LastGenerationChunks { get; private set; }
        public double LastGenerationElapsedMilliseconds { get; private set; }
        public double LastCandidateEvaluationMilliseconds { get; private set; }
        public double LastDeepProjectionMilliseconds { get; private set; }
        public double LastContinuationConstructionMilliseconds { get; private set; }
        public int LastGenerationSelectedScore { get; private set; }
        public int LastGenerationOccupiedCells { get; private set; }
        public BoardOccupancyState LastGenerationOccupancyState { get; private set; }
        public float LastGenerationRunPressure { get; private set; }
        public int LastGenerationImmediateClearOpportunities { get; private set; }
        public int LastGenerationSetupOpportunities { get; private set; }
        public int LastGenerationSetupPayoffOpportunities { get; private set; }
        public int LastGenerationLegacySetupPayoffOpportunities { get; private set; }
        public int LastGenerationPureSetupOpportunities { get; private set; }
        public int LastGenerationPureSetupWithoutImmediateClearOpportunities { get; private set; }
        public int LastGenerationImmediateClearSetupOverlap { get; private set; }
        public bool LastGenerationOpenDiversityBonusApplied { get; private set; }
        public int LastGenerationAdjacencyContacts { get; private set; }
        public int LastGenerationLineProgress { get; private set; }
        public int LastGenerationCleanlinessScore { get; private set; }
        public int LastGenerationRelaxFlowScore { get; private set; }
        public int LastGenerationProjectedOccupiedCells { get; private set; }
        public int LastGenerationProjectedLargestEmptyRegion { get; private set; }
        public int LastGenerationProjectedFragmentation { get; private set; }
        public int LastGenerationFlowTargetCount { get; private set; }
        public bool LastGenerationMatchedFlowTarget { get; private set; }
        public bool LastGenerationConstructedContinuation { get; private set; }
        public bool LastFlowTargetProducedClear { get; private set; }
        public int LastGenerationTrayEaseScore { get; private set; }
        public bool LastGenerationChallengeBandFallback { get; private set; }
        public bool LastGenerationCriticalChallengeBypass { get; private set; }
#endif

        public void Initialize(GameManager owner)
        {
            gameManager = owner;
            EnsureRuntimePrefabs();
            EnsureTraySlots();
            EnsureLateChallengeBuffers();
        }

        public void ResetFlowState()
        {
            ClearFlowTargets();
            classicFlowRecoveryTraysRemaining = 0;
        }

        private void ClearFlowTargets()
        {
            flowTargetCount = 0;
            for (int i = 0; i < flowTargets.Length; i++)
            {
                flowTargets[i] = default;
            }
        }

        // Called only after the player has consumed a real tray. The next tray
        // can softly continue what is actually left on board, without persisting
        // any of this transient assistance.
        public void CaptureFlowState(BoardManager board)
        {
            ClearFlowTargets();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastFlowTargetProducedClear = false;
#endif
            if (board == null)
            {
                return;
            }

            for (int orientation = 0; orientation < 2; orientation++)
            {
                bool row = orientation == 0;
                for (int line = 0; line < GameConstants.BoardSize; line++)
                {
                    int filled = board.GetGenerationLineFill(row, line);
                    // ContinuationIntent starts earlier than the former near-line
                    // FlowTarget. Two occupied cells are enough to identify a
                    // readable row/column objective; the actual A -> B verifier
                    // still rejects vague shape compatibility during selection.
                    if (filled < 2 || filled > GameConstants.BoardSize - 1)
                    {
                        continue;
                    }

                    int score = GetContinuationIntentPriority(filled);

                    InsertFlowTarget(row, line, filled, score);
                }
            }

            if (GameSession.SelectedMode == GameMode.Classic)
            {
                classicFlowRecoveryTraysRemaining = flowTargetCount > 0
                    ? ClassicFlowRecoveryTrayWindow
                    : Mathf.Max(0, classicFlowRecoveryTraysRemaining - 1);
            }
        }

        // Called after a real piece is placed but before the normal clear pass.
        // This is diagnostic-only: it records whether the player actually closed
        // a carried target and never influences the move, score, or clear itself.
        public void RecordFlowTargetClearIfCompleted(BoardManager board)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (board == null || flowTargetCount == 0 || LastFlowTargetProducedClear)
            {
                return;
            }

            for (int i = 0; i < flowTargetCount; i++)
            {
                FlowTarget target = flowTargets[i];
                if (board.IsGenerationFlowTargetComplete(target.row, target.lineIndex))
                {
                    LastFlowTargetProducedClear = true;
                    return;
                }
            }
#endif
        }

        private void InsertFlowTarget(bool row, int lineIndex, int filledCells, int score)
        {
            int insertAt = flowTargetCount;
            if (insertAt >= flowTargets.Length)
            {
                insertAt = flowTargets.Length - 1;
            }

            while (insertAt > 0 && score > GetFlowTargetPriority(flowTargets[insertAt - 1]))
            {
                if (insertAt < flowTargets.Length)
                {
                    flowTargets[insertAt] = flowTargets[insertAt - 1];
                }

                insertAt--;
            }

            if (insertAt < flowTargets.Length
                && (flowTargetCount < flowTargets.Length || score > GetFlowTargetPriority(flowTargets[insertAt])))
            {
                flowTargets[insertAt] = new FlowTarget
                {
                    row = row,
                    lineIndex = lineIndex,
                    filledCells = filledCells
                };
                if (flowTargetCount < flowTargets.Length)
                {
                    flowTargetCount++;
                }
            }
        }

        private static int GetFlowTargetPriority(FlowTarget target)
        {
            return GetContinuationIntentPriority(target.filledCells);
        }

        private static int GetContinuationIntentPriority(int filledCells)
        {
            switch (filledCells)
            {
                case 6:
                    return 1400;
                case 5:
                    return 1250;
                case 4:
                    return 1050;
                case 7:
                    return 900;
                case 3:
                    return 760;
                case 2:
                    return 520;
                default:
                    return 0;
            }
        }

        private void BeginRelaxFlowCandidateSelection()
        {
            relaxFlowCandidateCount = 0;
            for (int i = 0; i < relaxFlowCandidateScores.Length; i++)
            {
                relaxFlowCandidateScores[i] = int.MinValue;
                relaxFlowCandidateFitCounts[i] = 0;
            }
        }

        private void EnsureLateChallengeBuffers()
        {
            if (lateChallengeCandidateSets != null)
            {
                return;
            }

            lateChallengeCandidateSets = new PieceInstance[LateChallengeCandidateCapacity][];
            for (int i = 0; i < lateChallengeCandidateSets.Length; i++)
            {
                lateChallengeCandidateSets[i] = new PieceInstance[GameConstants.TraySize];
            }
        }

        private void BeginLateChallengeSelection()
        {
            EnsureLateChallengeBuffers();
            lateChallengeCandidateCount = 0;
        }

        private void ConsiderLateChallengeCandidate(
            BoardManager board,
            PieceInstance[] candidate,
            int normalScore,
            int fitCount)
        {
            if (candidate == null || fitCount < 2)
            {
                return;
            }

            int easeScore = CalculateTrayEaseScore(board, candidate, out int totalPlacementOptions);
            if (totalPlacementOptions < 5)
            {
                return;
            }

            int insertAt = lateChallengeCandidateCount;
            while (insertAt > 0
                && easeScore < lateChallengeCandidateEaseScores[insertAt - 1])
            {
                if (insertAt < LateChallengeCandidateCapacity)
                {
                    lateChallengeCandidateScores[insertAt] = lateChallengeCandidateScores[insertAt - 1];
                    lateChallengeCandidateEaseScores[insertAt] = lateChallengeCandidateEaseScores[insertAt - 1];
                    lateChallengeCandidateFitCounts[insertAt] = lateChallengeCandidateFitCounts[insertAt - 1];
                    Array.Copy(
                        lateChallengeCandidateSets[insertAt - 1],
                        lateChallengeCandidateSets[insertAt],
                        GameConstants.TraySize);
                }

                insertAt--;
            }

            if (insertAt >= LateChallengeCandidateCapacity)
            {
                return;
            }

            lateChallengeCandidateScores[insertAt] = normalScore;
            lateChallengeCandidateEaseScores[insertAt] = easeScore;
            lateChallengeCandidateFitCounts[insertAt] = fitCount;
            Array.Copy(candidate, lateChallengeCandidateSets[insertAt], GameConstants.TraySize);
            if (lateChallengeCandidateCount < LateChallengeCandidateCapacity)
            {
                lateChallengeCandidateCount++;
            }
        }

        private int CalculateTrayEaseScore(
            BoardManager board,
            PieceInstance[] candidate,
            out int totalPlacementOptions)
        {
            AssistDifficultyProfile profile = CalculateAssistDifficultyProfile(board, candidate);
            totalPlacementOptions = profile.totalPlacementOptions;
            return profile.easeScore;
        }

        private AssistDifficultyProfile CalculateAssistDifficultyProfile(
            BoardManager board,
            PieceInstance[] candidate)
        {
            AssistDifficultyProfile result = default;
            int minimumPlacementOptions = int.MaxValue;
            int setupValue = 0;
            for (int i = 0; i < candidate.Length; i++)
            {
                if (candidate[i] == null)
                {
                    minimumPlacementOptions = 0;
                    continue;
                }

                GenerationPlacementProfile profile = GetGenerationPlacementProfile(board, candidate[i]);
                result.totalPlacementOptions += profile.placementOptions;
                minimumPlacementOptions = Mathf.Min(minimumPlacementOptions, profile.placementOptions);
                result.immediateClears += profile.clearOpportunities;
                setupValue += profile.bestSetupScore;
                result.cleanliness += profile.bestCleanlinessScore;
            }

            if (minimumPlacementOptions == int.MaxValue)
            {
                minimumPlacementOptions = 0;
            }

            result.minimumPlacementOptions = minimumPlacementOptions;
            result.setup = setupValue;
            result.easeScore = result.totalPlacementOptions * 120
                + minimumPlacementOptions * 520
                + result.immediateClears * 820
                + setupValue * 4
                + result.cleanliness * 2;
            return result;
        }

        private PieceInstance[] SelectLateChallengeCandidate(
            int classicTrayNumber,
            out int selectedScore,
            out int selectedFitCount,
            out int selectedEaseScore,
            out bool usedFallback)
        {
            selectedScore = int.MinValue;
            selectedFitCount = 0;
            selectedEaseScore = 0;
            usedFallback = false;
            if (lateChallengeCandidateCount <= 0)
            {
                return null;
            }

            GetLateChallengeEaseBand(classicTrayNumber, out float lowerPercentile, out float upperPercentile);
            int lastIndex = lateChallengeCandidateCount - 1;
            int lowerIndex = Mathf.Clamp(
                Mathf.CeilToInt(lastIndex * lowerPercentile),
                0,
                lastIndex);
            int upperIndex = Mathf.Clamp(
                Mathf.FloorToInt(lastIndex * upperPercentile),
                lowerIndex,
                lastIndex);
            int selectedIndex = -1;
            for (int i = lowerIndex; i <= upperIndex; i++)
            {
                if (lateChallengeCandidateScores[i] <= selectedScore)
                {
                    continue;
                }

                selectedIndex = i;
                selectedScore = lateChallengeCandidateScores[i];
            }

            if (selectedIndex < 0)
            {
                usedFallback = true;
                selectedIndex = Mathf.Clamp((lowerIndex + upperIndex) / 2, 0, lastIndex);
                selectedScore = lateChallengeCandidateScores[selectedIndex];
            }

            selectedFitCount = lateChallengeCandidateFitCounts[selectedIndex];
            selectedEaseScore = lateChallengeCandidateEaseScores[selectedIndex];
            return lateChallengeCandidateSets[selectedIndex];
        }

        private static void GetLateChallengeEaseBand(
            int classicTrayNumber,
            out float lowerPercentile,
            out float upperPercentile)
        {
            if (classicTrayNumber <= 11)
            {
                lowerPercentile = 0.35f;
                upperPercentile = 0.55f;
            }
            else if (classicTrayNumber <= 15)
            {
                lowerPercentile = 0.20f;
                upperPercentile = 0.40f;
            }
            else if (classicTrayNumber <= 20)
            {
                lowerPercentile = 0.05f;
                upperPercentile = 0.25f;
            }
            else
            {
                lowerPercentile = 0f;
                upperPercentile = 0.15f;
            }
        }

        private void ConsiderRelaxFlowCandidate(
            PieceInstance[] candidateSet,
            int score,
            int fitCount)
        {
            int insertAt = relaxFlowCandidateCount;
            if (insertAt >= RelaxFlowCandidateCapacity)
            {
                insertAt = RelaxFlowCandidateCapacity - 1;
            }

            while (insertAt > 0 && score > relaxFlowCandidateScores[insertAt - 1])
            {
                if (insertAt < RelaxFlowCandidateCapacity)
                {
                    relaxFlowCandidateScores[insertAt] = relaxFlowCandidateScores[insertAt - 1];
                    relaxFlowCandidateFitCounts[insertAt] = relaxFlowCandidateFitCounts[insertAt - 1];
                    Array.Copy(
                        relaxFlowCandidateSets[insertAt - 1],
                        relaxFlowCandidateSets[insertAt],
                        GameConstants.TraySize);
                }

                insertAt--;
            }

            if (insertAt >= RelaxFlowCandidateCapacity
                || (relaxFlowCandidateCount >= RelaxFlowCandidateCapacity
                    && score <= relaxFlowCandidateScores[insertAt]))
            {
                return;
            }

            relaxFlowCandidateScores[insertAt] = score;
            relaxFlowCandidateFitCounts[insertAt] = fitCount;
            Array.Copy(candidateSet, relaxFlowCandidateSets[insertAt], GameConstants.TraySize);
            if (relaxFlowCandidateCount < RelaxFlowCandidateCapacity)
            {
                relaxFlowCandidateCount++;
            }
        }

        private PieceInstance[] SelectRelaxFlowCandidate(
            BoardManager board,
            PieceInstance[] baselineSet,
            int classicTrayNumber,
            out int selectedScore,
            out int selectedFitCount)
        {
            selectedScore = int.MinValue;
            selectedFitCount = 0;
            PieceInstance[] selected = null;
            float projectionWeight = GetRelaxFlowProjectionWeight(classicTrayNumber);
            float continuityStrength = GetFlowContinuityStrength(classicTrayNumber);
            GenerationFlowProjection selectedProjection = default;
            int selectedFlowScore = 0;
            bool selectedMatchedFlowTarget = false;
            AssistDifficultyProfile baselineProfile = CalculateAssistDifficultyProfile(board, baselineSet);

            int evaluationCount = Mathf.Min(relaxFlowCandidateCount, RelaxFlowDeepEvaluationCount);
            for (int i = 0; i < evaluationCount; i++)
            {
                PieceInstance[] candidate = relaxFlowCandidateSets[i];
                AssistDifficultyProfile candidateProfile = CalculateAssistDifficultyProfile(board, candidate);
                if (!IsComparableAssistDifficulty(baselineProfile, candidateProfile))
                {
                    continue;
                }

                GenerationFlowProjection projection = board.EvaluateGenerationFlowProjection(candidate);
                int flowScore = CalculateFlowContinuityScore(
                    board,
                    candidate,
                    continuityStrength,
                    classicTrayNumber,
                    out bool matchedFlowTarget);
                int score = relaxFlowCandidateScores[i]
                    + Mathf.RoundToInt(projection.cleanlinessScore * projectionWeight)
                    + flowScore;
                if (score <= selectedScore)
                {
                    continue;
                }

                selected = candidate;
                selectedScore = score;
                selectedFitCount = relaxFlowCandidateFitCounts[i];
                selectedProjection = projection;
                selectedFlowScore = flowScore;
                selectedMatchedFlowTarget = matchedFlowTarget;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (selected != null)
            {
                LastGenerationRelaxFlowScore = selectedFlowScore;
                LastGenerationProjectedOccupiedCells = selectedProjection.finalOccupiedCells;
                LastGenerationProjectedLargestEmptyRegion = selectedProjection.largestEmptyRegion;
                LastGenerationProjectedFragmentation = selectedProjection.emptyRegionCount;
                LastGenerationMatchedFlowTarget = selectedMatchedFlowTarget;
            }
#endif
            return selected;
        }

        private int CalculateFlowContinuityScore(
            BoardManager board,
            PieceInstance[] set,
            float strength,
            int classicTrayNumber,
            out bool matchedFlowTarget)
        {
            matchedFlowTarget = false;
            if (board == null || set == null || flowTargetCount == 0 || strength <= 0f)
            {
                return 0;
            }

            int bestTargetScore = 0;
            for (int targetIndex = 0; targetIndex < flowTargetCount; targetIndex++)
            {
                FlowTarget target = flowTargets[targetIndex];
                for (int continuationIndex = 0; continuationIndex < set.Length; continuationIndex++)
                {
                    PieceInstance continuationPiece = set[continuationIndex];
                    if (continuationPiece == null)
                    {
                        continue;
                    }

                    for (int payoffIndex = 0; payoffIndex < set.Length; payoffIndex++)
                    {
                        if (payoffIndex == continuationIndex || set[payoffIndex] == null)
                        {
                            continue;
                        }

                        // This is deliberately stricter than a geometric
                        // overlap test: A must land on the captured target and
                        // the bounded virtual board must prove that B can pay
                        // that setup off with a clear.
                        int payoffScore = board.ScoreGenerationFlowTargetPayoff(
                            continuationPiece,
                            set[payoffIndex],
                            target.row,
                            target.lineIndex,
                            out int continuationAdvance,
                            out bool completesTarget);
                        if (payoffScore <= 0 || continuationAdvance <= 0)
                        {
                            continue;
                        }

                        bool hasFlexibleThirdPiece = false;
                        for (int flexIndex = 0; flexIndex < set.Length; flexIndex++)
                        {
                            if (flexIndex != continuationIndex
                                && flexIndex != payoffIndex
                                && set[flexIndex] != null
                                && GetPlacementOptions(board, set[flexIndex]) >= 4)
                            {
                                hasFlexibleThirdPiece = true;
                                break;
                            }
                        }

                        // A continuation is only strong when the remaining third
                        // piece is still broadly playable. This keeps the early
                        // eligibility gate from selecting a flashy A -> B payoff
                        // that strands the player with an awkward third piece.
                        if (!hasFlexibleThirdPiece)
                        {
                            continue;
                        }

                        int relationshipScore = continuationAdvance * 1600
                            + payoffScore * 16
                            + 2300
                            + 1000
                            + (completesTarget ? 480 : 0);
                        bestTargetScore = Mathf.Max(bestTargetScore, relationshipScore);
                    }
                }
            }

            matchedFlowTarget = bestTargetScore > 0;
            return Mathf.RoundToInt(bestTargetScore * strength * GetFlowAssistBoost(classicTrayNumber));
        }

        private static float GetFlowContinuityStrength(int classicTrayNumber)
        {
            if (classicTrayNumber <= 4)
            {
                return ClassicEarlyFlowStrength;
            }

            if (classicTrayNumber <= 6) return ClassicMidFlowStrength;
            if (classicTrayNumber <= 8) return ClassicTransitionFlowStrength;
            return classicTrayNumber <= 10 ? ClassicRecoveryFlowStrength : 0f;
        }

        private static float GetFlowAssistBoost(int classicTrayNumber)
        {
            if (classicTrayNumber <= 6)
            {
                return ClassicEarlyMidFlowBoost;
            }

            if (classicTrayNumber <= 8) return ClassicTransitionFlowBoost;
            return classicTrayNumber <= 10 ? ClassicRecoveryFlowBoost : 1f;
        }

        private static float GetRelaxFlowProjectionWeight(int classicTrayNumber)
        {
            if (classicTrayNumber <= 6)
            {
                return ClassicEarlyMidProjectionWeight;
            }

            if (classicTrayNumber <= 8) return ClassicTransitionProjectionWeight;
            return classicTrayNumber <= 10 ? ClassicRecoveryProjectionWeight : 0f;
        }

        private static bool IsComparableAssistDifficulty(
            AssistDifficultyProfile baseline,
            AssistDifficultyProfile candidate)
        {
            int easeTolerance = Mathf.Max(250, Mathf.RoundToInt(Mathf.Abs(baseline.easeScore) * 0.02f));
            int placementTolerance = Mathf.Max(1, Mathf.CeilToInt(baseline.totalPlacementOptions * 0.02f));
            int setupTolerance = Mathf.Max(60, Mathf.RoundToInt(Mathf.Abs(baseline.setup) * 0.02f));
            int cleanlinessTolerance = Mathf.Max(60, Mathf.RoundToInt(Mathf.Abs(baseline.cleanliness) * 0.02f));
            return candidate.easeScore >= baseline.easeScore - easeTolerance
                && candidate.easeScore <= baseline.easeScore + easeTolerance
                && candidate.totalPlacementOptions <= baseline.totalPlacementOptions + placementTolerance
                && candidate.minimumPlacementOptions <= baseline.minimumPlacementOptions + 1
                && candidate.immediateClears <= baseline.immediateClears
                && candidate.setup <= baseline.setup + setupTolerance
                && candidate.cleanliness <= baseline.cleanliness + cleanlinessTolerance;
        }

        // If none of the ordinary 56 candidates services an early intent, build
        // one bounded tray from existing catalog shapes. A and B must pass the
        // same actual-board target/payoff proof used by normal selection, while
        // C is retained from the ordinary Phase 7H winner and must have at least
        // four legal placements. No shape, placement, or clear is manufactured.
        private bool TryConstructReadableContinuation(
            BoardManager board,
            PieceInstance[] normalWinner,
            System.Random random,
            out int fitCount)
        {
            fitCount = 0;
            if (board == null || normalWinner == null || random == null || flowTargetCount <= 0)
            {
                return false;
            }

            int flexibleIndex = -1;
            int bestFlexOptions = 3;
            for (int i = 0; i < normalWinner.Length; i++)
            {
                PieceInstance candidate = normalWinner[i];
                if (candidate == null)
                {
                    continue;
                }

                int options = GetPlacementOptions(board, candidate);
                if (options > bestFlexOptions)
                {
                    bestFlexOptions = options;
                    flexibleIndex = i;
                }
            }

            if (flexibleIndex < 0)
            {
                return false;
            }

            EnsureContinuationShapeCandidates();
            PieceInstance bestAdvancePiece = null;
            PieceInstance bestPayoffPiece = null;
            int bestRelationshipScore = 0;
            for (int targetIndex = 0; targetIndex < flowTargetCount; targetIndex++)
            {
                FlowTarget target = flowTargets[targetIndex];
                for (int advanceIndex = 0; advanceIndex < continuationShapeCandidates.Length; advanceIndex++)
                {
                    PieceInstance advancePiece = continuationShapeCandidates[advanceIndex];
                    int advanceOptions = GetPlacementOptions(board, advancePiece);
                    if (advanceOptions <= 0)
                    {
                        continue;
                    }

                    for (int payoffIndex = 0; payoffIndex < continuationShapeCandidates.Length; payoffIndex++)
                    {
                        PieceInstance payoffPiece = continuationShapeCandidates[payoffIndex];
                        int payoffScore = board.ScoreGenerationFlowTargetPayoff(
                            advancePiece,
                            payoffPiece,
                            target.row,
                            target.lineIndex,
                            out int continuationAdvance,
                            out bool completesTarget);
                        if (payoffScore <= 0 || continuationAdvance <= 0)
                        {
                            continue;
                        }

                        int relationshipScore = payoffScore * 16
                            + continuationAdvance * 1800
                            + Mathf.Min(advanceOptions, 12) * 80
                            + (completesTarget ? 700 : 0);
                        if (relationshipScore <= bestRelationshipScore)
                        {
                            continue;
                        }

                        bestRelationshipScore = relationshipScore;
                        bestAdvancePiece = advancePiece;
                        bestPayoffPiece = payoffPiece;
                    }
                }
            }

            if (bestAdvancePiece == null || bestPayoffPiece == null)
            {
                return false;
            }

            PieceInstance flexiblePiece = normalWinner[flexibleIndex];
            constructedContinuationSet[0] = new PieceInstance(
                bestAdvancePiece.shapeId,
                (ChromaColor)random.Next(GameConstants.ColorCount));
            constructedContinuationSet[1] = new PieceInstance(
                bestPayoffPiece.shapeId,
                (ChromaColor)random.Next(GameConstants.ColorCount));
            constructedContinuationSet[2] = new PieceInstance(
                flexiblePiece.shapeId,
                flexiblePiece.color);
            fitCount = CountFittingPieces(board, constructedContinuationSet);
            CalculateTrayEaseScore(board, constructedContinuationSet, out int totalPlacementOptions);
            return fitCount >= 2 && totalPlacementOptions >= 5;
        }

        private void EnsureContinuationShapeCandidates()
        {
            if (continuationShapeCandidates != null)
            {
                return;
            }

            continuationShapeCandidates = new PieceInstance[ContinuationShapeIds.Length];
            for (int i = 0; i < ContinuationShapeIds.Length; i++)
            {
                continuationShapeCandidates[i] = new PieceInstance(ContinuationShapeIds[i], ChromaColor.Cyan);
            }
        }

        public void SpawnGuaranteedSet(BoardManager board, System.Random random)
        {
            SpawnGuaranteedSet(board, random, 0.5f);
        }

        public void SpawnGuaranteedSet(BoardManager board, System.Random random, float difficulty01)
        {
            SpawnGuaranteedSet(board, random, difficulty01, 0f, 0f, 0, 0);
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

        public void SpawnGuaranteedSet(
            BoardManager board,
            System.Random random,
            float difficulty01,
            float assist01,
            float runPressure01 = 0f,
            int consecutiveReliefBiasedTrays = 0,
            int classicTrayNumber = 0)
        {
            EnsureTraySlots();
            if (board == null || random == null || traySlots == null || traySlots.Length == 0)
            {
                return;
            }

            difficulty01 = Mathf.Clamp01(difficulty01);
            assist01 = Mathf.Clamp01(assist01);
            runPressure01 = Mathf.Clamp01(runPressure01);
            consecutiveReliefBiasedTrays = Mathf.Max(0, consecutiveReliefBiasedTrays);
            classicTrayNumber = Mathf.Max(0, classicTrayNumber);
            // Two strong relief trays in a row are enough. The next selection stays
            // fair, but stops forcing another near-perfect bailout set.
            float loopAdjustedAssist = consecutiveReliefBiasedTrays >= 2
                ? assist01 * ClassicReliefLoopAssistScale
                : assist01;
            BeginGenerationMetricCache(board);
            GenerateTrayMarker.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            LastGenerationCandidateCount = 0;
            LastGenerationMetricEvaluations = 0;
            LastGenerationMetricCacheHits = 0;
            LastGenerationChunks = 1;
            LastGenerationSelectedScore = 0;
            LastGenerationOccupiedCells = GameConstants.BoardSize * GameConstants.BoardSize - GetEmptyCells(board);
            LastGenerationOccupancyState = GetOccupancyState(LastGenerationOccupiedCells);
            LastGenerationRunPressure = runPressure01;
            LastGenerationImmediateClearOpportunities = 0;
            LastGenerationSetupOpportunities = 0;
            LastGenerationSetupPayoffOpportunities = 0;
            LastGenerationLegacySetupPayoffOpportunities = 0;
            LastGenerationPureSetupOpportunities = 0;
            LastGenerationPureSetupWithoutImmediateClearOpportunities = 0;
            LastGenerationImmediateClearSetupOverlap = 0;
            LastGenerationOpenDiversityBonusApplied = false;
            LastGenerationAdjacencyContacts = 0;
            LastGenerationLineProgress = 0;
            LastGenerationCleanlinessScore = 0;
            LastGenerationRelaxFlowScore = 0;
            LastGenerationProjectedOccupiedCells = 0;
            LastGenerationProjectedLargestEmptyRegion = 0;
            LastGenerationProjectedFragmentation = 0;
            LastGenerationFlowTargetCount = flowTargetCount;
            LastGenerationMatchedFlowTarget = false;
            LastGenerationConstructedContinuation = false;
            LastGenerationTrayEaseScore = 0;
            LastGenerationChallengeBandFallback = false;
            LastGenerationCriticalChallengeBypass = false;
            LastCandidateEvaluationMilliseconds = 0d;
            LastDeepProjectionMilliseconds = 0d;
            LastContinuationConstructionMilliseconds = 0d;
#endif
            LastGenerationReliefBiased = false;
            try
            {
                PieceInstance[] candidateSet = new PieceInstance[GameConstants.TraySize];
                PieceInstance[] bestSet = null;
                int bestScore = int.MinValue;
                int bestFitCount = 0;
                bool selectedByStrongContinuationGate = false;
                bool selectedByLateChallengeBand = false;
                BoardOccupancyState generationOccupancyState = GetOccupancyState(
                    GameConstants.BoardSize * GameConstants.BoardSize - GetEmptyCells(board));
                bool useLateChallengeBand = GameSession.SelectedMode == GameMode.Classic
                    && classicTrayNumber >= 11
                    && generationOccupancyState != BoardOccupancyState.Critical;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastGenerationCriticalChallengeBypass = GameSession.SelectedMode == GameMode.Classic
                    && classicTrayNumber >= 11
                    && generationOccupancyState == BoardOccupancyState.Critical;
#endif
                bool allowLateClassicStair5 = CanConsiderStair5InRandomSet(runPressure01);
                bool boundedContinuationGate = GameSession.SelectedMode == GameMode.Classic
                    && classicTrayNumber >= 1
                    && classicTrayNumber <= 10
                    && flowTargetCount > 0;
                bool useRelaxFlow = GameSession.SelectedMode == GameMode.Classic
                    && classicTrayNumber <= 10
                    && !boundedContinuationGate
                    && (classicTrayNumber >= 7 || classicFlowRecoveryTraysRemaining > 0);
                BeginRelaxFlowCandidateSelection();
                BeginLateChallengeSelection();

                CandidateEvaluationMarker.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                long candidateTimingStart = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
                for (int attempt = 0; attempt < GameConstants.GuaranteedSetAttempts; attempt++)
                {
                    PieceCatalog.FillRandomSet(candidateSet, random, difficulty01, allowLateClassicStair5);
                    ReplaceIneligibleClassicStair5(board, candidateSet, random, difficulty01, runPressure01);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LastGenerationCandidateCount++;
#endif
                    int fitCount = CountFittingPieces(board, candidateSet);
                    int score = ScoreSetForBoard(
                        board,
                        candidateSet,
                        fitCount,
                        difficulty01,
                        loopAdjustedAssist,
                        runPressure01,
                        consecutiveReliefBiasedTrays,
                        classicTrayNumber);
                    if (useLateChallengeBand)
                    {
                        ConsiderLateChallengeCandidate(board, candidateSet, score, fitCount);
                    }
                    if (boundedContinuationGate)
                    {
                        // Inspect all existing 56 legal candidates. If a strict
                        // target-bound A -> B clear with a flexible third exists,
                        // choose the strongest proven relationship in that fair
                        // subset rather than merely the ordinary top board score.
                        int continuityScore = CalculateFlowContinuityScore(
                            board,
                            candidateSet,
                            GetFlowContinuityStrength(classicTrayNumber),
                            classicTrayNumber,
                            out bool hasStrongContinuation);
                        if (hasStrongContinuation)
                        {
                            AssistDifficultyProfile candidateProfile = CalculateAssistDifficultyProfile(
                                board,
                                candidateSet);
                            if (fitCount >= 2 && candidateProfile.totalPlacementOptions >= 5)
                            {
                                ConsiderRelaxFlowCandidate(
                                    candidateSet,
                                    score + continuityScore,
                                    fitCount);
                            }
                        }
                    }
                    else if (useRelaxFlow)
                    {
                        AssistDifficultyProfile candidateProfile = CalculateAssistDifficultyProfile(
                            board,
                            candidateSet);
                        if (fitCount >= 2 && candidateProfile.totalPlacementOptions >= 5)
                        {
                            ConsiderRelaxFlowCandidate(candidateSet, score, fitCount);
                        }
                    }
                    if (score > bestScore)
                    {
                        bestSet ??= new PieceInstance[GameConstants.TraySize];
                        Array.Copy(candidateSet, bestSet, GameConstants.TraySize);
                        bestScore = score;
                        bestFitCount = fitCount;
                    }
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastCandidateEvaluationMilliseconds = ElapsedMilliseconds(candidateTimingStart);
#endif
                CandidateEvaluationMarker.End();

                if (boundedContinuationGate && relaxFlowCandidateCount > 0)
                {
                    AssistDifficultyProfile baselineProfile = CalculateAssistDifficultyProfile(board, bestSet);
                    int selectedContinuationIndex = -1;
                    int continuationScanCount = classicTrayNumber <= 4
                        ? relaxFlowCandidateCount
                        : Mathf.Min(relaxFlowCandidateCount, 24);
                    for (int i = 0; i < continuationScanCount; i++)
                    {
                        AssistDifficultyProfile candidateProfile = CalculateAssistDifficultyProfile(
                            board,
                            relaxFlowCandidateSets[i]);
                        if (IsComparableAssistDifficulty(baselineProfile, candidateProfile))
                        {
                            selectedContinuationIndex = i;
                            break;
                        }
                    }

                    if (selectedContinuationIndex >= 0)
                    {
                        bestSet ??= new PieceInstance[GameConstants.TraySize];
                        Array.Copy(
                            relaxFlowCandidateSets[selectedContinuationIndex],
                            bestSet,
                            GameConstants.TraySize);
                        bestScore = relaxFlowCandidateScores[selectedContinuationIndex];
                        bestFitCount = relaxFlowCandidateFitCounts[selectedContinuationIndex];
                        selectedByStrongContinuationGate = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        LastGenerationMatchedFlowTarget = true;
#endif
                    }
                }

                if (boundedContinuationGate && !selectedByStrongContinuationGate && bestSet != null)
                {
                    AssistDifficultyProfile baselineProfile = CalculateAssistDifficultyProfile(board, bestSet);
                    ContinuationConstructionMarker.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    long constructionTimingStart = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
                    bool constructed = TryConstructReadableContinuation(
                        board,
                        bestSet,
                        random,
                        out int constructedFitCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LastContinuationConstructionMilliseconds = ElapsedMilliseconds(constructionTimingStart);
#endif
                    ContinuationConstructionMarker.End();
                    if (constructed)
                    {
                        AssistDifficultyProfile constructedProfile = CalculateAssistDifficultyProfile(
                            board,
                            constructedContinuationSet);
                        if (IsComparableAssistDifficulty(baselineProfile, constructedProfile))
                        {
                            Array.Copy(constructedContinuationSet, bestSet, GameConstants.TraySize);
                            bestFitCount = constructedFitCount;
                            bestScore = ScoreSetForBoard(
                                board,
                                bestSet,
                                bestFitCount,
                                difficulty01,
                                loopAdjustedAssist,
                                runPressure01,
                                consecutiveReliefBiasedTrays,
                                classicTrayNumber);
                            selectedByStrongContinuationGate = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            LastGenerationMatchedFlowTarget = true;
                            LastGenerationConstructedContinuation = true;
#endif
                        }
                    }
                }

                if (!selectedByStrongContinuationGate && useRelaxFlow && relaxFlowCandidateCount > 0)
                {
                    DeepProjectionMarker.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    long projectionTimingStart = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
                    PieceInstance[] relaxedSelection = SelectRelaxFlowCandidate(
                        board,
                        bestSet,
                        classicTrayNumber,
                        out int relaxedScore,
                        out int relaxedFitCount);
                    if (relaxedSelection != null)
                    {
                        bestSet ??= new PieceInstance[GameConstants.TraySize];
                        Array.Copy(relaxedSelection, bestSet, GameConstants.TraySize);
                        bestScore = relaxedScore;
                        bestFitCount = relaxedFitCount;
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LastDeepProjectionMilliseconds = ElapsedMilliseconds(projectionTimingStart);
#endif
                    DeepProjectionMarker.End();
                }

                if (useLateChallengeBand)
                {
                    PieceInstance[] challengeSelection = SelectLateChallengeCandidate(
                        classicTrayNumber,
                        out int challengeScore,
                        out int challengeFitCount,
                        out int challengeEaseScore,
                        out bool challengeFallback);
                    if (challengeSelection != null)
                    {
                        bestSet ??= new PieceInstance[GameConstants.TraySize];
                        Array.Copy(challengeSelection, bestSet, GameConstants.TraySize);
                        bestScore = challengeScore;
                        bestFitCount = challengeFitCount;
                        selectedByLateChallengeBand = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        LastGenerationTrayEaseScore = challengeEaseScore;
                        LastGenerationChallengeBandFallback = challengeFallback;
#endif
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    else
                    {
                        LastGenerationChallengeBandFallback = true;
                    }
#endif
                }

                if (bestSet != null && bestFitCount > 0)
                {
                    BoardOccupancyState selectionState = GetOccupancyState(
                        GameConstants.BoardSize * GameConstants.BoardSize - GetEmptyCells(board));
                    bool lateNonEssentialCuration = GameSession.SelectedMode == GameMode.Classic
                        && classicTrayNumber >= 11
                        && selectionState != BoardOccupancyState.Critical;
                    float postSelectionAssist = loopAdjustedAssist
                        * GetLateNonEssentialAssistScale(classicTrayNumber, selectionState);

                    // An early strict-continuation winner has already proven
                    // an actual A -> B target sequence plus a flexible third
                    // piece. Do not replace any member after selection or the
                    // hard eligibility guarantee would no longer be true.
                    // All other winners retain the established fit rescue and
                    // optional presentation curation.
                    if (!selectedByStrongContinuationGate && !selectedByLateChallengeBand)
                    {
                        ImproveSetWithRescuePieces(board, bestSet, random, difficulty01, loopAdjustedAssist);
                        if (!lateNonEssentialCuration)
                        {
                            EnsureSatisfyingPiece(board, bestSet, random, difficulty01, postSelectionAssist);
                            EnsureComebackPiece(board, bestSet, random, postSelectionAssist);
                            EnsureImmediateClearPiece(board, bestSet, random, postSelectionAssist);
                            EnsureJuicySetMass(board, bestSet, random, difficulty01);
                            EnsureSatisfyingSetShapeMix(board, bestSet, random, difficulty01, postSelectionAssist);
                        }
                    }
                    RecordSelectedGenerationMetrics(
                        board,
                        bestSet,
                        CountFittingPieces(board, bestSet),
                        difficulty01,
                        loopAdjustedAssist,
                        runPressure01,
                        consecutiveReliefBiasedTrays,
                        classicTrayNumber);
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
                RecordSelectedGenerationMetrics(
                    board,
                    fallback,
                    CountFittingPieces(board, fallback),
                    difficulty01,
                    loopAdjustedAssist,
                    runPressure01,
                    consecutiveReliefBiasedTrays,
                    classicTrayNumber);
                SpawnSet(fallback);
                RefreshFitHints(board);
            }
            finally
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                stopwatch.Stop();
                LastGenerationElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
#endif
                GenerateTrayMarker.End();
                EndGenerationMetricCache();
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static double ElapsedMilliseconds(long startTimestamp)
        {
            return (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp)
                * 1000.0
                / System.Diagnostics.Stopwatch.Frequency;
        }
#endif

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
            generationSetupPayoffCache.Clear();
            generationEmptyCellsReady = false;
            generationEmptyCells = 0;
        }

        // stair5 remains in the catalog and in the normal Blitz pool. Classic only
        // considers it once the established run-pressure curve is active, and then
        // only if the current board gives it more than one genuine placement.
        private static bool CanConsiderStair5InRandomSet(float runPressure01)
        {
            return GameSession.SelectedMode != GameMode.Classic || runPressure01 >= 0.22f;
        }

        private void ReplaceIneligibleClassicStair5(
            BoardManager board,
            PieceInstance[] set,
            System.Random random,
            float difficulty01,
            float runPressure01)
        {
            if (GameSession.SelectedMode != GameMode.Classic || set == null)
            {
                return;
            }

            bool latePressureActive = runPressure01 >= 0.22f;
            for (int i = 0; i < set.Length; i++)
            {
                PieceInstance piece = set[i];
                if (piece == null || piece.shapeId != "stair5")
                {
                    continue;
                }

                if (latePressureActive && GetPlacementOptions(board, piece) >= 2)
                {
                    continue;
                }

                set[i] = PieceCatalog.RandomPiece(random, difficulty01, allowStair5: false);
            }
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

        private GenerationPlacementProfile GetGenerationPlacementProfile(BoardManager board, PieceInstance piece)
        {
            GenerationMetrics metrics = GetGenerationMetrics(board, piece);
            if (metrics == null)
            {
                return board == null ? default : board.EvaluateGenerationPlacementProfile(piece);
            }

            if (!metrics.profileReady)
            {
                metrics.profile = board.EvaluateGenerationPlacementProfile(piece);
                metrics.profileReady = true;
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
            return metrics.profile;
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
                return board == null ? 0 : board.EvaluateGenerationPlacementProfile(piece).placementOptions;
            }

            if (!metrics.placementOptionsReady)
            {
                metrics.placementOptions = GetGenerationPlacementProfile(board, piece).placementOptions;
                metrics.placementOptionsReady = true;
            }
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
                return board == null ? 0 : board.EvaluateGenerationPlacementProfile(piece).clearOpportunities;
            }

            if (!metrics.clearOpportunitiesReady)
            {
                metrics.clearOpportunities = GetGenerationPlacementProfile(board, piece).clearOpportunities;
                metrics.clearOpportunitiesReady = true;
            }
            return metrics.clearOpportunities;
        }

        private int GetSetupOpportunity(BoardManager board, PieceInstance piece)
        {
            GenerationMetrics metrics = GetGenerationMetrics(board, piece);
            if (metrics == null)
            {
                return board == null ? 0 : board.EvaluateGenerationPlacementProfile(piece).bestSetupScore;
            }

            if (!metrics.setupOpportunityReady)
            {
                metrics.setupOpportunity = GetGenerationPlacementProfile(board, piece).bestSetupScore;
                metrics.setupOpportunityReady = true;
            }
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

        private int ScoreSetForBoard(
            BoardManager board,
            PieceInstance[] set,
            int fitCount,
            float difficulty01,
            float assist01,
            float runPressure01,
            int consecutiveReliefBiasedTrays,
            int classicTrayNumber)
        {
            int emptyCells = GetEmptyCells(board);
            int occupiedCells = GameConstants.BoardSize * GameConstants.BoardSize - emptyCells;
            BoardOccupancyState occupancyState = GetOccupancyState(occupiedCells);
            float occupancyPressure01 = GetOccupancyPressure(occupancyState);
            int placementOptions = 0;
            int clearOpportunities = 0;
            int setupOpportunities = 0;
            int adjacencyContacts = 0;
            int lineProgress = 0;
            int cleanlinessScore = 0;
            int totalCells = 0;
            int largestPiece = 0;
            int mediumPieceCount = 0;
            int largePieceCount = 0;
            int smallPieceCount = 0;
            int duplicatePenalty = 0;
            int restrictivePieceCount = 0;
            int severelyRestrictivePieceCount = 0;
            int poorEarlyCClassCount = 0;

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null)
                {
                    continue;
                }

                GenerationPlacementProfile profile = GetGenerationPlacementProfile(board, set[i]);
                placementOptions += profile.placementOptions;
                clearOpportunities += profile.clearOpportunities;
                setupOpportunities += profile.bestSetupScore;
                adjacencyContacts += profile.bestAdjacencyContacts;
                lineProgress += profile.bestLineProgress;
                cleanlinessScore += profile.bestCleanlinessScore;

                if (profile.placementOptions <= 4)
                {
                    restrictivePieceCount++;
                }

                if (profile.placementOptions <= 2)
                {
                    severelyRestrictivePieceCount++;
                }

                if (IsCClassShape(set[i].shapeId)
                    && profile.placementOptions <= 4
                    && profile.clearOpportunities == 0
                    && profile.bestSetupScore < 70)
                {
                    poorEarlyCClassCount++;
                }

                int cells = set[i].Data.cells.Length;
                totalCells += cells;
                largestPiece = Mathf.Max(largestPiece, cells);
                if (cells == 3 || cells == 4)
                {
                    mediumPieceCount++;
                }
                else if (cells <= 2)
                {
                    smallPieceCount++;
                }

                if (cells >= 5)
                {
                    largePieceCount++;
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

            SetupPayoffAnalysis setupPayoffAnalysis = AnalyzeSetupPayoff(board, set);
            int legacySetupPayoffOpportunities = setupPayoffAnalysis.legacyScore;
            bool usePureSetupScoring = occupancyState == BoardOccupancyState.Open;
            int setupPayoffOpportunities = usePureSetupScoring
                ? setupPayoffAnalysis.pureScore
                : legacySetupPayoffOpportunities;
            // Phase 7H keeps the broad current setup heuristic for non-OPEN survival
            // states. OPEN scoring instead rewards the proven non-clearing A -> B
            // relationship, so an already-clearing piece cannot collect the full
            // clever-setup reward as well.
            int setupOpportunitiesForScore = usePureSetupScoring
                ? setupPayoffAnalysis.pureScore
                : setupOpportunities;
            float openBoard01 = Mathf.Clamp01((emptyCells - 18f) / 28f);
            float targetCells = Mathf.Lerp(12.6f, 16.2f, difficulty01)
                - occupancyPressure01 * 1.4f
                + runPressure01 * 1.1f;
            int targetScore = Mathf.RoundToInt(920f - Mathf.Abs(totalCells - targetCells) * 76f);
            int mobilityWeight = Mathf.RoundToInt(Mathf.Lerp(52f, 30f, difficulty01) + occupancyPressure01 * 38f);
            int earlyLargePenalty = difficulty01 < 0.16f && largestPiece >= 6 ? 120 : 0;
            int tightLargePenalty = occupancyState == BoardOccupancyState.Critical && largestPiece >= 6
                ? 250
                : 0;
            int allFitBonus = fitCount == GameConstants.TraySize ? 1100 : 0;
            int multiFitBonus = fitCount >= 2 ? 900 : 0;
            int clearWeight = GetImmediateClearWeight(occupancyState, difficulty01);
            int setupWeight = GetSetupWeight(occupancyState);
            int adjacencyWeight = GetAdjacencyWeight(occupancyState);
            int lineProgressWeight = GetLineProgressWeight(occupancyState);
            int cleanlinessWeight = GetCleanlinessWeight(occupancyState);
            int clearBonus = clearOpportunities * clearWeight;
            float pureSetupScale = GetLatePureSetupScale(classicTrayNumber, occupancyState);
            int setupBonus = Mathf.RoundToInt(setupOpportunitiesForScore * setupWeight
                * (usePureSetupScoring ? pureSetupScale : 1f));
            int adjacencyBonus = adjacencyContacts * adjacencyWeight;
            int lineProgressBonus = lineProgress * lineProgressWeight;
            int setupPayoffBonus = Mathf.RoundToInt(setupPayoffOpportunities
                * GetSetupPayoffWeight(occupancyState)
                * (usePureSetupScoring ? pureSetupScale : 1f));
            int openDiversityBonus = usePureSetupScoring
                && setupPayoffAnalysis.pureScore > 0
                && clearOpportunities == 0
                ? GetOpenDiversityBonus(classicTrayNumber)
                : 0;
            float cleanBoardAssistScale = GetExtraCleanBoardAssistScale(classicTrayNumber);
            int earlyBuildFlexBonus = Mathf.RoundToInt(
                CalculateEarlyBuildFlexBonus(board, set, classicTrayNumber) * cleanBoardAssistScale);
            int cleanlinessBonus = Mathf.RoundToInt(
                cleanlinessScore * cleanlinessWeight * cleanBoardAssistScale);
            int satisfyingBonus = Mathf.RoundToInt(openBoard01 * (
                mediumPieceCount * 560f + largePieceCount * 680f) * cleanBoardAssistScale);
            float nonEssentialAssist = assist01 * GetLateNonEssentialAssistScale(classicTrayNumber, occupancyState);
            int miniPiecePenalty = CalculateMiniPiecePenalty(set, emptyCells, nonEssentialAssist);
            int shapeMixBonus = Mathf.RoundToInt(
                CalculateShapeMixBonus(set, emptyCells, openBoard01) * cleanBoardAssistScale);
            int comebackBonus = Mathf.RoundToInt(nonEssentialAssist * (
                clearOpportunities * 1550f
                + setupOpportunities * 58f
                + legacySetupPayoffOpportunities * 42f
                + fitCount * 460f));
            int comebackNoProgressPenalty = nonEssentialAssist > 0.25f
                && clearOpportunities == 0
                && setupOpportunities < 85
                && legacySetupPayoffOpportunities == 0
                ? Mathf.RoundToInt(nonEssentialAssist * 1400f)
                : 0;
            int tightSurvivalBonus = Mathf.RoundToInt(occupancyPressure01 * (
                clearOpportunities * 780f
                + setupOpportunities * 18f
                + legacySetupPayoffOpportunities * 18f
                + fitCount * 620f));
            int tightNoProgressPenalty = occupancyPressure01 > 0.52f
                && clearOpportunities == 0
                && setupOpportunities < 70
                && legacySetupPayoffOpportunities == 0
                ? Mathf.RoundToInt(occupancyPressure01 * 950f)
                : 0;

            // Open boards should not receive a continuous stream of instant clears.
            int openClearSaturationPenalty = occupancyState == BoardOccupancyState.Open
                ? Mathf.Max(0, clearOpportunities - 1)
                    * (GameSession.SelectedMode == GameMode.Classic && classicTrayNumber <= 10
                        ? ClassicEarlyMidOpenClearSaturationPenalty
                        : 320)
                : 0;
            // Pressure changes the decisions required, not whether a legal move exists.
            int pressureTinyPenalty = Mathf.RoundToInt(runPressure01 * smallPieceCount * 760f);
            int pressureMediumBonus = Mathf.RoundToInt(runPressure01 * mediumPieceCount * 280f);
            int pressurePerfectTrayPenalty = Mathf.RoundToInt(runPressure01 * Mathf.Max(0, clearOpportunities - 1) * 190f);
            int reliefLoopPenalty = consecutiveReliefBiasedTrays >= 2
                && occupancyState != BoardOccupancyState.Critical
                ? Mathf.RoundToInt((clearOpportunities * 260f + legacySetupPayoffOpportunities * 12f) * 0.55f)
                : 0;
            // This is a bad-feel filter, not a perfect-tray solver. Early and
            // mid Classic rejects trays where every option fights for the same
            // tiny space, while leaving genuinely fitting T/S/Z shapes available.
            int earlyMidRestrictionPenalty = 0;
            if (GameSession.SelectedMode == GameMode.Classic && runPressure01 < 0.58f)
            {
                if (severelyRestrictivePieceCount >= 2)
                {
                    earlyMidRestrictionPenalty += 3800;
                }

                if (restrictivePieceCount >= GameConstants.TraySize)
                {
                    earlyMidRestrictionPenalty += 2600;
                }

                earlyMidRestrictionPenalty += poorEarlyCClassCount * 620;
            }

            return fitCount * 12000
                + placementOptions * mobilityWeight
                + clearBonus
                + setupBonus
                + adjacencyBonus
                + lineProgressBonus
                + setupPayoffBonus
                + openDiversityBonus
                + earlyBuildFlexBonus
                + cleanlinessBonus
                + targetScore
                + allFitBonus
                + multiFitBonus
                + satisfyingBonus
                + shapeMixBonus
                + comebackBonus
                + tightSurvivalBonus
                + pressureMediumBonus
                - earlyLargePenalty
                - tightLargePenalty
                - miniPiecePenalty
                - comebackNoProgressPenalty
                - tightNoProgressPenalty
                - openClearSaturationPenalty
                - pressureTinyPenalty
                - pressurePerfectTrayPenalty
                - reliefLoopPenalty
                - earlyMidRestrictionPenalty
                - duplicatePenalty;
        }

        private static bool IsCClassShape(string shapeId)
        {
            return shapeId == "t4" || shapeId == "t4_v" || shapeId == "s4" || shapeId == "z4";
        }

        // Late Classic reduces optional relief without ever changing the legal
        // fit guarantee or the existing Critical rescue path.
        private static float GetLateNonEssentialAssistScale(
            int classicTrayNumber,
            BoardOccupancyState occupancyState)
        {
            if (GameSession.SelectedMode != GameMode.Classic)
            {
                return 1f;
            }

            if (classicTrayNumber >= 11) return 0f;
            if (occupancyState == BoardOccupancyState.Critical) return 1f;
            if (classicTrayNumber <= 6) return 1f;
            if (classicTrayNumber <= 8) return 0.70f;
            return 0.30f;
        }

        private static float GetLatePureSetupScale(
            int classicTrayNumber,
            BoardOccupancyState occupancyState)
        {
            if (GameSession.SelectedMode != GameMode.Classic)
            {
                return 1f;
            }

            if (classicTrayNumber >= 11) return 0f;
            if (occupancyState == BoardOccupancyState.Critical) return 1f;
            if (classicTrayNumber <= 6) return 1f;
            if (classicTrayNumber <= 8) return 0.70f;
            return 0.30f;
        }

        private static int GetOpenDiversityBonus(int classicTrayNumber)
        {
            if (GameSession.SelectedMode != GameMode.Classic || classicTrayNumber <= 4)
            {
                return OpenPureSetupDiversityBonus;
            }

            if (classicTrayNumber <= 6) return 2000;
            if (classicTrayNumber <= 8) return 1000;
            return classicTrayNumber <= 10 ? 300 : 0;
        }

        private static float GetExtraCleanBoardAssistScale(int classicTrayNumber)
        {
            if (GameSession.SelectedMode != GameMode.Classic || classicTrayNumber <= 4)
            {
                return 1f;
            }

            if (classicTrayNumber <= 6) return 0.75f;
            if (classicTrayNumber <= 8) return 0.45f;
            return classicTrayNumber <= 10 ? 0.15f : 0f;
        }

        private BoardOccupancyState GetOccupancyState(int occupiedCells)
        {
            if (occupiedCells >= 50)
            {
                return BoardOccupancyState.Critical;
            }

            if (occupiedCells >= 40)
            {
                return BoardOccupancyState.Pressured;
            }

            return occupiedCells >= 28
                ? BoardOccupancyState.Balanced
                : BoardOccupancyState.Open;
        }

        private float GetOccupancyPressure(BoardOccupancyState state)
        {
            switch (state)
            {
                case BoardOccupancyState.Balanced:
                    return 0.32f;
                case BoardOccupancyState.Pressured:
                    return 0.68f;
                case BoardOccupancyState.Critical:
                    return 1f;
                default:
                    return 0f;
            }
        }

        private int GetImmediateClearWeight(BoardOccupancyState state, float difficulty01)
        {
            int baseWeight = Mathf.RoundToInt(Mathf.Lerp(500f, 390f, difficulty01));
            switch (state)
            {
                case BoardOccupancyState.Balanced:
                    return baseWeight + 90;
                case BoardOccupancyState.Pressured:
                    return baseWeight + 220;
                case BoardOccupancyState.Critical:
                    return baseWeight + 330;
                default:
                    return baseWeight - 50;
            }
        }

        private int GetSetupWeight(BoardOccupancyState state)
        {
            switch (state)
            {
                case BoardOccupancyState.Balanced:
                    return 36;
                case BoardOccupancyState.Pressured:
                    return 48;
                case BoardOccupancyState.Critical:
                    return 54;
                default:
                    return 24;
            }
        }

        private int GetAdjacencyWeight(BoardOccupancyState state)
        {
            switch (state)
            {
                case BoardOccupancyState.Balanced:
                    return 72;
                case BoardOccupancyState.Pressured:
                    return 102;
                case BoardOccupancyState.Critical:
                    return 116;
                default:
                    return 34;
            }
        }

        private int GetLineProgressWeight(BoardOccupancyState state)
        {
            switch (state)
            {
                case BoardOccupancyState.Balanced:
                    return 46;
                case BoardOccupancyState.Pressured:
                    return 62;
                case BoardOccupancyState.Critical:
                    return 70;
                default:
                    return 30;
            }
        }

        private int GetCleanlinessWeight(BoardOccupancyState state)
        {
            switch (state)
            {
                case BoardOccupancyState.Balanced:
                    return 3;
                case BoardOccupancyState.Pressured:
                    return 4;
                case BoardOccupancyState.Critical:
                    return 5;
                default:
                    return 2;
            }
        }

        private int GetSetupPayoffWeight(BoardOccupancyState state)
        {
            switch (state)
            {
                case BoardOccupancyState.Balanced:
                    return 10;
                case BoardOccupancyState.Pressured:
                    return 13;
                case BoardOccupancyState.Critical:
                    return 15;
                default:
                    return 7;
            }
        }

        // When an early board has no carried FlowTarget, prefer an ordinary
        // readable build-and-flex tray: A creates meaningful line progress, B
        // can cash it in, and the remaining piece is not a dead end. This is a
        // preference only; it never manufactures pieces or replaces fairness.
        private int CalculateEarlyBuildFlexBonus(
            BoardManager board,
            PieceInstance[] set,
            int classicTrayNumber)
        {
            if (GameSession.SelectedMode != GameMode.Classic
                || classicTrayNumber < 1
                || classicTrayNumber > 10
                || flowTargetCount > 0
                || board == null
                || set == null)
            {
                return 0;
            }

            for (int setupIndex = 0; setupIndex < set.Length; setupIndex++)
            {
                PieceInstance setupPiece = set[setupIndex];
                if (setupPiece == null)
                {
                    continue;
                }

                GenerationPlacementProfile setupProfile = GetGenerationPlacementProfile(board, setupPiece);
                if (!setupProfile.hasSetupOrigin
                    || setupProfile.clearOpportunities > 0
                    || setupProfile.bestLineProgress < 4)
                {
                    continue;
                }

                for (int payoffIndex = 0; payoffIndex < set.Length; payoffIndex++)
                {
                    if (payoffIndex == setupIndex || set[payoffIndex] == null)
                    {
                        continue;
                    }

                    if (GetCachedSetupPayoffScore(board, setupPiece, set[payoffIndex]) <= 0)
                    {
                        continue;
                    }

                    for (int flexIndex = 0; flexIndex < set.Length; flexIndex++)
                    {
                        if (flexIndex != setupIndex
                            && flexIndex != payoffIndex
                            && set[flexIndex] != null
                            && GetPlacementOptions(board, set[flexIndex]) >= 4)
                        {
                            return ClassicEarlyBuildFlexBonus;
                        }
                    }
                }
            }

            return 0;
        }

        private int GetCachedSetupPayoffScore(
            BoardManager board,
            PieceInstance setupPiece,
            PieceInstance payoffPiece)
        {
            SetupPayoffKey key = new SetupPayoffKey(setupPiece.shapeId, payoffPiece.shapeId);
            if (generationSetupPayoffCache.TryGetValue(key, out int payoffScore))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastGenerationMetricCacheHits++;
#endif
                return payoffScore;
            }

            GenerationPlacementProfile setupProfile = GetGenerationPlacementProfile(board, setupPiece);
            payoffScore = !setupProfile.hasSetupOrigin
                ? 0
                : board.ScoreGenerationSetupPayoff(
                    setupPiece,
                    setupProfile.bestSetupOrigin,
                    payoffPiece);
            generationSetupPayoffCache.Add(key, payoffScore);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastGenerationMetricEvaluations++;
#endif
            return payoffScore;
        }

        private SetupPayoffAnalysis AnalyzeSetupPayoff(BoardManager board, PieceInstance[] set)
        {
            SetupPayoffAnalysis analysis = default;
            if (board == null || set == null)
            {
                return analysis;
            }

            for (int first = 0; first < set.Length; first++)
            {
                PieceInstance setupPiece = set[first];
                if (setupPiece == null)
                {
                    continue;
                }

                GenerationPlacementProfile setupProfile = GetGenerationPlacementProfile(board, setupPiece);
                if (!setupProfile.hasSetupOrigin || setupProfile.bestLineProgress <= 0)
                {
                    continue;
                }

                for (int second = 0; second < set.Length; second++)
                {
                    if (first == second || set[second] == null)
                    {
                        continue;
                    }

                    int payoffScore = GetCachedSetupPayoffScore(board, setupPiece, set[second]);
                    analysis.legacyScore = Mathf.Max(analysis.legacyScore, payoffScore);

                    // A strict Pure Setup is the deliberately non-clearing A -> B
                    // sequence approved for OPEN boards. Neither participating shape
                    // may have an independent clear on the current board, while the
                    // cached virtual pair simulation confirms B clears after A.
                    GenerationPlacementProfile payoffProfile = GetGenerationPlacementProfile(board, set[second]);
                    if (setupProfile.clearOpportunities == 0
                        && payoffProfile.clearOpportunities == 0)
                    {
                        analysis.pureScore = Mathf.Max(analysis.pureScore, payoffScore);
                    }
                }
            }

            return analysis;
        }

        private void RecordSelectedGenerationMetrics(
            BoardManager board,
            PieceInstance[] selectedSet,
            int fitCount,
            float difficulty01,
            float assist01,
            float runPressure01,
            int consecutiveReliefBiasedTrays,
            int classicTrayNumber)
        {
            int selectedScore = ScoreSetForBoard(
                board,
                selectedSet,
                fitCount,
                difficulty01,
                assist01,
                runPressure01,
                consecutiveReliefBiasedTrays,
                classicTrayNumber);
            int immediateClears = 0;
            int setupScore = 0;
            int adjacency = 0;
            int lineProgress = 0;
            int cleanliness = 0;
            for (int i = 0; i < selectedSet.Length; i++)
            {
                if (selectedSet[i] == null)
                {
                    continue;
                }

                GenerationPlacementProfile profile = GetGenerationPlacementProfile(board, selectedSet[i]);
                immediateClears += profile.clearOpportunities;
                setupScore += profile.bestSetupScore;
                adjacency += profile.bestAdjacencyContacts;
                lineProgress += profile.bestLineProgress;
                cleanliness += profile.bestCleanlinessScore;
            }

            SetupPayoffAnalysis setupPayoffAnalysis = AnalyzeSetupPayoff(board, selectedSet);
            int legacySetupPayoff = setupPayoffAnalysis.legacyScore;
            int pureSetup = setupPayoffAnalysis.pureScore;
            int occupied = GameConstants.BoardSize * GameConstants.BoardSize - GetEmptyCells(board);
            BoardOccupancyState state = GetOccupancyState(occupied);
            LastGenerationReliefBiased = (int)state >= (int)BoardOccupancyState.Pressured
                && (immediateClears > 0 || legacySetupPayoff > 0 || lineProgress >= 10);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastGenerationTrayEaseScore = CalculateTrayEaseScore(
                board,
                selectedSet,
                out _);
            LastGenerationSelectedScore = selectedScore;
            LastGenerationOccupiedCells = occupied;
            LastGenerationOccupancyState = state;
            LastGenerationRunPressure = runPressure01;
            LastGenerationImmediateClearOpportunities = immediateClears;
            LastGenerationSetupOpportunities = setupScore;
            LastGenerationSetupPayoffOpportunities = legacySetupPayoff;
            LastGenerationLegacySetupPayoffOpportunities = legacySetupPayoff;
            LastGenerationPureSetupOpportunities = pureSetup;
            LastGenerationPureSetupWithoutImmediateClearOpportunities = immediateClears == 0
                ? pureSetup
                : 0;
            LastGenerationImmediateClearSetupOverlap = immediateClears > 0 && legacySetupPayoff > 0
                ? 1
                : 0;
            LastGenerationOpenDiversityBonusApplied = state == BoardOccupancyState.Open
                && pureSetup > 0
                && immediateClears == 0
                && GetOpenDiversityBonus(classicTrayNumber) > 0;
            LastGenerationAdjacencyContacts = adjacency;
            LastGenerationLineProgress = lineProgress;
            LastGenerationCleanlinessScore = cleanliness;
#endif
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

            // Keep a little visual substance without turning this post-pass
            // into a second large-piece generator. This only prevents a tray
            // of three tiny pieces.
            int targetTotalCells = emptyCells >= 34 ? 13 : emptyCells >= 26 ? 12 : 10;
            if (difficulty01 < 0.18f)
            {
                targetTotalCells--;
            }

            int guard = 0;
            while (TotalPieceCells(set) < targetTotalCells && guard < 1)
            {
                guard++;
                int replaceIndex = FindSmallestNonClearingPieceIndex(board, set);
                if (replaceIndex < 0)
                {
                    return;
                }

                int currentCells = set[replaceIndex] == null ? 0 : set[replaceIndex].Data.cells.Length;
                PieceInstance largerPiece = FindBestFittingPieceInRange(board, random, 3, 5);
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
            int mediumPieces = CountPiecesBetween(set, 3, 4);
            if (smallPieces < 2 || mediumPieces > 0)
            {
                return;
            }

            int replaceIndex = FindSmallestNonClearingPieceIndex(board, set);
            if (replaceIndex < 0)
            {
                replaceIndex = FindSmallestFittingPieceIndex(board, set);
            }

            if (replaceIndex < 0)
            {
                return;
            }

            PieceInstance connector = FindBestFittingPieceInRange(board, random, 3, 4);
            if (connector != null)
            {
                set[replaceIndex] = connector;
            }
        }

        private int CalculateMiniPiecePenalty(PieceInstance[] set, int emptyCells, float assist01)
        {
            int smallPieces = CountPiecesAtMost(set, 2);
            if (smallPieces <= 1 || emptyCells < 15 || assist01 > 0.70f)
            {
                return 0;
            }

            int penalty = smallPieces == 2 ? 420 : 1080;
            if (emptyCells >= 30)
            {
                penalty += 320;
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

        private PieceInstance FindBestFittingPieceInRange(
            BoardManager board,
            System.Random random,
            int minimumCells,
            int maximumCells)
        {
            PieceInstance best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < PieceCatalog.All.Count; i++)
            {
                PieceData data = PieceCatalog.All[i];
                int cells = data.cells.Length;
                if (data.id == "plus5" || data.id == "stair5" || cells < minimumCells || cells > maximumCells)
                {
                    continue;
                }

                PieceInstance candidate = new PieceInstance(
                    data.id,
                    (ChromaColor)random.Next(GameConstants.ColorCount));
                int options = GetPlacementOptions(board, candidate);
                if (options <= 0)
                {
                    continue;
                }

                int score = options * 74
                    + GetClearOpportunities(board, candidate) * 920
                    + GetSetupOpportunity(board, candidate) * 38
                    + random.Next(16);

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
                if (data.id == "plus5" || data.id == "stair5")
                {
                    continue;
                }

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
                if (data.id == "plus5" || data.id == "stair5")
                {
                    continue;
                }

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
