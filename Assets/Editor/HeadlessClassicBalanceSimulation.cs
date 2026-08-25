using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ChromaBlast.EditorTools
{
    // Analysis-only, pure-data Classic simulator. It deliberately owns no GameObjects,
    // MonoBehaviours, UI, audio, haptics, or persistence. The generator is a literal
    // editor-side port of PieceSpawner's current scoring and post-selection guarantees,
    // because those runtime methods are intentionally private and scene-bound.
    public static class HeadlessClassicBalanceSimulation
    {
        private const int DefaultRunsPerConfiguration = 150;
        private const int RootCauseRunsPerConfiguration = 300;
        private const int PlacementSafetyCeiling = 500;
        private const int SeedStart = 730_000;
        private const int BoardCellCount = GameConstants.BoardSize * GameConstants.BoardSize;
        // Retained for editor-only historical comparison helpers. The active
        // production validation below does not use any trio lookahead.
        private const int GenerationTrioSecondPlacementLimit = 3;

        private static readonly string[] LargeCandidateIds =
        {
            "line5_h", "line5_v", "square3", "rect2x3", "rect3x2",
            "line4_h", "line4_v", "square2", "l4", "l4_m", "l4_r", "l4_rm", "t4",
            "t4_v", "s4", "z4"
        };

        private struct SimFlowTarget
        {
            public bool row;
            public int lineIndex;
            public int filledCells;
        }

        private struct SimFlowProjection
        {
            public bool valid;
            public int finalOccupiedCells;
            public int largestOpenArea;
            public int largestOpenRectangle;
            public int emptyRegionCount;
            public int isolatedHoles;
            public int narrowCorridorCells;
            public int futureOptions;
            public int clearedLines;
            public int cleanlinessScore;
        }

        private struct SimFlowPlacement
        {
            public int x;
            public int y;
            public int score;
        }

        private struct SimAssistDifficultyProfile
        {
            public int easeScore;
            public int totalPlacementOptions;
            public int minimumPlacementOptions;
            public int immediateClears;
            public int setup;
            public int cleanliness;
        }

        private static readonly int[,] SimFlowOrders =
        {
            { 0, 1, 2 }, { 0, 2, 1 }, { 1, 0, 2 },
            { 1, 2, 0 }, { 2, 0, 1 }, { 2, 1, 0 }
        };
        private static readonly string[] SimFlowFutureShapeIds =
        {
            "single", "line2_h", "line2_v", "line3_h", "line3_v", "square2", "corner3", "corner3_m"
        };
        private static readonly string[] SimContinuationShapeIds =
        {
            "single", "line2_h", "line2_v", "line3_h", "line3_v",
            "line4_h", "line4_v", "line5_h", "line5_v", "square2",
            "corner3", "corner3_m", "l4", "l4_m", "l4_r", "l4_rm"
        };

        [MenuItem("Chroma Blast/Balance/Run Phase 9 Relax Flow Sanity (200)", false, 100)]
        public static void RunFromMenu()
        {
            UnityEngine.Debug.Log(RunDefaultStudy());
        }

        // Suitable for Unity -batchmode -executeMethod. It does not call EditorApplication.Exit,
        // so invoking it from the interactive editor is also safe.
        public static void RunFromCommandLine()
        {
            UnityEngine.Debug.Log(RunDefaultStudy());
        }

        public static string RunDefaultStudy()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Aggregate production = RunConfiguration(
                SimulationConfiguration.CreateCurrent("PHASE 9 RELAX FLOW"),
                DefaultRunsPerConfiguration,
                SeedStart);
            stopwatch.Stop();
            return BuildProductionPureSetupReport(production, stopwatch.Elapsed);
        }

        private static Aggregate RunConfiguration(
            SimulationConfiguration configuration,
            int runCount,
            int seedStart)
        {
            Aggregate aggregate = new Aggregate(configuration, runCount);
            for (int runIndex = 0; runIndex < runCount; runIndex++)
            {
                int seed = seedStart + runIndex;
                aggregate.Add(RunSingle(seed, configuration));
            }

            aggregate.FinalizeMetrics();
            return aggregate;
        }

        private static RunMetrics RunSingle(int seed, SimulationConfiguration configuration)
        {
            System.Random random = new System.Random(seed);
            System.Random playerRandom = new System.Random(unchecked(seed * 1_103_515_245 + 12_345));
            HeadlessBoard board = new HeadlessBoard();
            RunMetrics metrics = new RunMetrics();
            PieceInstance[] tray = CreateOpeningTray(random);
            int traysGenerated = 1;
            int consecutiveReliefTrays = 0;
            int consecutivePerfectCurationTrays = 0;
            int movesSinceClear = 0;
            int chain = 0;
            int score = 0;
            int normalScore = 0;
            int popScore = 0;
            int popUses = 0;
            int[] chroma = new int[GameConstants.ColorCount];
            int lastPopPlacement = -1;
            SimFlowTarget[] flowTargets = new SimFlowTarget[2];
            int flowTargetCount = 0;

            TrayChoiceTracker trayChoices = new TrayChoiceTracker();
            SetupPayoffExecutionTracker setupPayoffExecutions = new SetupPayoffExecutionTracker();
            RecordTray(metrics, board, null, consecutiveReliefTrays);
            RecordTrayChoiceOptions(metrics, board, tray);

            while (metrics.placements < PlacementSafetyCeiling)
            {
                if (!HasAnyFit(board, tray))
                {
                    break;
                }

                if (TryUsePop(
                        board,
                        tray,
                        chroma,
                        popUses,
                        configuration.popFatigueEnabled,
                        playerRandom,
                        out ChromaColor popColor,
                        out int popped))
                {
                    int gained = popped * 90 + (popped >= 8 ? 300 : 0);
                    score += gained;
                    popScore += gained;
                    chroma[(int)popColor] = 0;
                    popUses++;
                    movesSinceClear = 0;
                    metrics.popUses++;
                    if (lastPopPlacement >= 0)
                    {
                        metrics.popPlacementIntervals.Add(metrics.placements - lastPopPlacement);
                    }

                    lastPopPlacement = metrics.placements;
                    metrics.maxOccupiedCells = Math.Max(metrics.maxOccupiedCells, board.OccupiedCount);
                    continue;
                }

                if (!TryChoosePlacement(board, tray, playerRandom, configuration.playerPolicy, out PlacementDecision decision))
                {
                    break;
                }

                int setupScore = board.ScorePlacementSetup(decision.piece, decision.x, decision.y);
                ClearOutcome clear = board.PlaceAndResolve(decision.piece, decision.x, decision.y);
                RemovePiece(tray, decision.trayIndex);
                trayChoices.Record(clear.lines, setupScore, decision.adjacencyContacts);
                setupPayoffExecutions.RecordMove(setupScore, clear.lines, traysGenerated);
                metrics.placements++;
                metrics.occupancyAfterPlacementTotal += board.OccupiedCount;
                metrics.occupancyAfterPlacementSamples++;
                metrics.maxOccupiedCells = Math.Max(metrics.maxOccupiedCells, board.OccupiedCount);

                int placedCells = decision.piece.Data.cells.Length;
                int moveScore = placedCells * 5;
                if (clear.lines > 0)
                {
                    chain++;
                    float chainMultiplier = GetChainScoreMultiplier(chain);
                    moveScore += Mathf.RoundToInt(clear.lines * 150f * chainMultiplier);
                    moveScore += Mathf.RoundToInt(clear.pureLines * 650f * chainMultiplier);
                    moveScore += clear.cellsCleared * 12;
                    metrics.clears++;
                    if (clear.lines == 1)
                    {
                        metrics.oneLineClearMoves++;
                    }
                    if (clear.lines > 1)
                    {
                        metrics.multiLineClears++;
                    }

                    for (int color = 0; color < GameConstants.ColorCount; color++)
                    {
                        if (clear.clearedByColor[color] > 0)
                        {
                            AddChroma(chroma, color, clear.clearedByColor[color], popUses, configuration.popFatigueEnabled);
                        }

                        if (clear.pureLinesByColor[color] > 0)
                        {
                            AddChroma(chroma, color, clear.pureLinesByColor[color] * 6, popUses, configuration.popFatigueEnabled);
                        }
                    }

                    moveScore += CalculateSatisfyingClearBonus(clear, chain);
                    moveScore += CalculateBoardSweepBonus(clear, board.OccupiedCount);
                    movesSinceClear = 0;
                }
                else
                {
                    chain = 0;
                    movesSinceClear++;
                }

                bool trayCompleted = IsTrayEmpty(tray);
                if (trayCompleted)
                {
                    moveScore += CalculateTrayCompleteBonus(clear, chain, board.OccupiedCount);
                }

                moveScore += CalculateLargePieceBonus(decision.piece, clear);
                moveScore += CalculateSetupMoveBonus(decision.piece, clear, setupScore);
                score += moveScore;
                normalScore += moveScore;

                if (trayCompleted)
                {
                    trayChoices.AddTo(metrics);
                    setupPayoffExecutions.CompleteTray(traysGenerated);
                    trayChoices = new TrayChoiceTracker();
                    traysGenerated++;
                    flowTargetCount = CaptureSimFlowTargets(board, flowTargets);
                    metrics.flowTargetsCreated += flowTargetCount;
                    metrics.flowContinuationEligibleTrays += flowTargetCount > 0 ? 1 : 0;
                    float difficulty = GetClassicDifficulty(score);
                    float assist = GetPieceAssist(movesSinceClear, board.EmptyCount);
                    float runPressure = configuration.pressureEnabled
                        ? GetClassicRunPressure(traysGenerated, popUses)
                        : 0f;
                    int reliefStreakForGenerator = configuration.reliefLoopEnabled
                        ? consecutiveReliefTrays
                        : 0;
                    if (reliefStreakForGenerator >= 2)
                    {
                        metrics.antiReliefLoopActivations++;
                    }
                    bool usePerfectCurationStreakBreaker = configuration.perfectCurationStreakBreakerEnabled
                        && consecutivePerfectCurationTrays >= 2;
                    if (usePerfectCurationStreakBreaker)
                    {
                        metrics.perfectCurationStreakBreakerActivations++;
                    }
                    TrayGenerationResult result = GenerateGuaranteedTray(
                        board,
                        random,
                        difficulty,
                        assist,
                        runPressure,
                        reliefStreakForGenerator,
                        usePerfectCurationStreakBreaker,
                        traysGenerated,
                        flowTargets,
                        flowTargetCount,
                        configuration,
                        metrics);
                    tray = result.pieces;
                    if (result.matchedFlowTarget)
                    {
                        metrics.flowTargetContinuationTrays++;
                    }
                    if (configuration.reliefLoopEnabled && result.reliefBiased)
                    {
                        consecutiveReliefTrays++;
                    }
                    else
                    {
                        consecutiveReliefTrays = 0;
                    }

                    bool perfectlyCurated = IsPerfectlyCuratedTray(result);
                    if (perfectlyCurated)
                    {
                        metrics.perfectlyCuratedTrays++;
                    }

                    if (usePerfectCurationStreakBreaker)
                    {
                        consecutivePerfectCurationTrays = 0;
                    }
                    else
                    {
                        consecutivePerfectCurationTrays = perfectlyCurated
                            ? consecutivePerfectCurationTrays + 1
                            : 0;
                    }
                    metrics.maxConsecutivePerfectCurationTrays = Math.Max(
                        metrics.maxConsecutivePerfectCurationTrays,
                        consecutivePerfectCurationTrays);

                    RecordTray(metrics, board, result, consecutiveReliefTrays);
                    RecordTrayChoiceOptions(metrics, board, tray);
                }
            }

            metrics.censored = metrics.placements >= PlacementSafetyCeiling;
            metrics.trays = traysGenerated;
            metrics.finalOccupiedCells = board.OccupiedCount;
            metrics.finalScore = score;
            metrics.normalScore = normalScore;
            metrics.popScore = popScore;
            metrics.pressureAtEnd = configuration.pressureEnabled
                ? GetClassicRunPressure(traysGenerated, popUses)
                : 0f;
            metrics.popRequirementAtEnd = GetPopRequirement(popUses, configuration.popFatigueEnabled);
            setupPayoffExecutions.AddTo(metrics);
            return metrics;
        }

        private static PieceInstance[] CreateOpeningTray(System.Random random)
        {
            string[] recipe = random.NextDouble() < 0.64d
                ? new[] { "square3", "square3", "rect2x3" }
                : new[] { "rect3x2", "rect3x2", "square2" };
            PieceInstance[] tray = new PieceInstance[GameConstants.TraySize];
            for (int i = 0; i < tray.Length; i++)
            {
                tray[i] = new PieceInstance(recipe[i], (ChromaColor)random.Next(GameConstants.ColorCount));
            }

            for (int i = tray.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                PieceInstance swap = tray[i];
                tray[i] = tray[swapIndex];
                tray[swapIndex] = swap;
            }

            return tray;
        }

        private static TrayGenerationResult GenerateGuaranteedTray(
            HeadlessBoard board,
            System.Random random,
            float difficulty01,
            float assist01,
            float runPressure01,
            int consecutiveReliefBiasedTrays,
            bool usePerfectCurationStreakBreaker,
            int classicTrayNumber,
            SimFlowTarget[] flowTargets,
            int flowTargetCount,
            SimulationConfiguration configuration,
            RunMetrics metrics)
        {
            difficulty01 = Mathf.Clamp01(difficulty01);
            assist01 = Mathf.Clamp01(assist01);
            runPressure01 = Mathf.Clamp01(runPressure01);
            consecutiveReliefBiasedTrays = Math.Max(0, consecutiveReliefBiasedTrays);
            float loopAdjustedAssist = consecutiveReliefBiasedTrays >= 2
                ? assist01 * 0.52f
                : assist01;
            float phase8CurationMultiplier = GetPhase8CurationMultiplier(
                configuration,
                runPressure01,
                usePerfectCurationStreakBreaker);

            GenerationContext context = new GenerationContext(board);
            PieceInstance[] candidateSet = new PieceInstance[GameConstants.TraySize];
            PieceInstance[] bestSet = null;
            int bestScore = int.MinValue;
            int bestFitCount = 0;
            int bestPhase7HScore = int.MinValue;
            int selectedPhase7HScoreBeforePostSelection = int.MinValue;
            bool selectedMatchedFlowTarget = false;
            bool selectedByStrongContinuationGate = false;
            PieceInstance[][] relaxFlowCandidates = new PieceInstance[32][];
            for (int i = 0; i < relaxFlowCandidates.Length; i++)
            {
                relaxFlowCandidates[i] = new PieceInstance[GameConstants.TraySize];
            }
            int[] relaxFlowCandidateScores = new int[relaxFlowCandidates.Length];
            Array.Fill(relaxFlowCandidateScores, int.MinValue);
            int[] relaxFlowCandidateFitCounts = new int[relaxFlowCandidates.Length];
            int relaxFlowCandidateCount = 0;
            bool selectedByLateChallengeBand = false;
            bool useLateChallengeBand = classicTrayNumber >= 9
                && GetOccupancyState(board.OccupiedCount) != OccupancyState.Critical;
            PieceInstance[][] lateChallengeCandidates = useLateChallengeBand
                ? new PieceInstance[GameConstants.GuaranteedSetAttempts][]
                : null;
            int[] lateChallengeScores = useLateChallengeBand
                ? new int[GameConstants.GuaranteedSetAttempts]
                : null;
            int[] lateChallengeEaseScores = useLateChallengeBand
                ? new int[GameConstants.GuaranteedSetAttempts]
                : null;
            int[] lateChallengeFitCounts = useLateChallengeBand
                ? new int[GameConstants.GuaranteedSetAttempts]
                : null;
            int lateChallengeCount = 0;
            PieceInstance[][] trioCandidates = configuration.satisfactionCurationEnabled
                ? new[]
                {
                    new PieceInstance[GameConstants.TraySize],
                    new PieceInstance[GameConstants.TraySize]
                }
                : null;
            int[] trioCandidateScores = configuration.satisfactionCurationEnabled
                ? new[] { int.MinValue, int.MinValue }
                : null;
            int[] trioCandidateFitCounts = configuration.satisfactionCurationEnabled
                ? new int[2]
                : null;
            CandidateTermBaseline candidateTermBaseline = default;
            List<PieceInstance[]> validCandidates = configuration.candidateSelectionMode == CandidateSelectionMode.RandomValid
                ? new List<PieceInstance[]>(GameConstants.GuaranteedSetAttempts)
                : null;
            bool allowLateClassicStair5 = runPressure01 >= 0.22f;
            bool maximumContinuationGate = classicTrayNumber >= 1
                && classicTrayNumber <= 6
                && flowTargetCount > 0;
            bool useRelaxFlow = classicTrayNumber >= 7 && classicTrayNumber <= 8;
            for (int attempt = 0; attempt < GameConstants.GuaranteedSetAttempts; attempt++)
            {
                PieceCatalog.FillRandomSet(candidateSet, random, difficulty01, allowLateClassicStair5);
                ReplaceIneligibleClassicStair5(context, candidateSet, random, difficulty01, runPressure01);
                int fitCount = CountFittingPieces(context, candidateSet);
                RecordRawCandidate(metrics, context, candidateSet, fitCount);
                if (fitCount > 0 && validCandidates != null)
                {
                    PieceInstance[] copy = new PieceInstance[GameConstants.TraySize];
                    Array.Copy(candidateSet, copy, GameConstants.TraySize);
                    validCandidates.Add(copy);
                }
                if (validCandidates != null)
                {
                    continue;
                }
                ScoreTerms candidateTerms = CalculateScoreTerms(
                    context,
                    candidateSet,
                    fitCount,
                    difficulty01,
                    loopAdjustedAssist,
                    runPressure01,
                    consecutiveReliefBiasedTrays,
                    classicTrayNumber,
                    flowTargetCount,
                    configuration,
                    phase8CurationMultiplier);
                candidateTermBaseline.Add(candidateTerms);
                int score = candidateTerms.Total;
                if (useLateChallengeBand)
                {
                    ConsiderSimLateChallengeCandidate(
                        context,
                        candidateSet,
                        score,
                        fitCount,
                        lateChallengeCandidates,
                        lateChallengeScores,
                        lateChallengeEaseScores,
                        lateChallengeFitCounts,
                        ref lateChallengeCount);
                }
                if (maximumContinuationGate)
                {
                    int continuityScore = CalculateSimFlowContinuityScore(
                        context,
                        candidateSet,
                        flowTargets,
                        flowTargetCount,
                        GetSimFlowContinuityStrength(classicTrayNumber),
                        classicTrayNumber,
                        out bool hasStrongContinuation);
                    if (hasStrongContinuation)
                    {
                        CalculateSimTrayEaseScore(
                            context,
                            candidateSet,
                            out int totalPlacementOptions);
                        if (fitCount >= 2 && totalPlacementOptions >= 5)
                        {
                            ConsiderSimRelaxFlowCandidate(
                                candidateSet,
                                score + continuityScore,
                                fitCount,
                                relaxFlowCandidates,
                                relaxFlowCandidateScores,
                                relaxFlowCandidateFitCounts,
                                ref relaxFlowCandidateCount);
                        }
                    }
                }
                else if (useRelaxFlow)
                {
                    CalculateSimTrayEaseScore(
                        context,
                        candidateSet,
                        out int totalPlacementOptions);
                    if (fitCount >= 2 && totalPlacementOptions >= 5)
                    {
                        ConsiderSimRelaxFlowCandidate(
                            candidateSet,
                            score,
                            fitCount,
                            relaxFlowCandidates,
                            relaxFlowCandidateScores,
                            relaxFlowCandidateFitCounts,
                            ref relaxFlowCandidateCount);
                    }
                }
                int phase7HScore = score - candidateTerms.phase8CurationAfterGate;
                if (phase7HScore > bestPhase7HScore)
                {
                    bestPhase7HScore = phase7HScore;
                }
                if (trioCandidates != null)
                {
                    ConsiderTrioCurationCandidate(
                        candidateSet,
                        score,
                        fitCount,
                        trioCandidates,
                        trioCandidateScores,
                        trioCandidateFitCounts);
                }
                if (score > bestScore)
                {
                    bestSet ??= new PieceInstance[GameConstants.TraySize];
                    Array.Copy(candidateSet, bestSet, GameConstants.TraySize);
                    bestScore = score;
                    bestFitCount = fitCount;
                }
            }

            if (trioCandidates != null && !maximumContinuationGate)
            {
                SelectBestTrioCuratedCandidate(
                    context,
                    trioCandidates,
                    trioCandidateScores,
                    trioCandidateFitCounts,
                    ref bestSet,
                    ref bestScore,
                    ref bestFitCount,
                    configuration,
                    runPressure01,
                    phase8CurationMultiplier);
            }

            if (validCandidates == null
                && maximumContinuationGate
                && relaxFlowCandidateCount > 0)
            {
                SimAssistDifficultyProfile baselineProfile = CalculateSimAssistDifficultyProfile(
                    context,
                    bestSet);
                int selectedContinuationIndex = -1;
                int continuationScanCount = classicTrayNumber <= 4
                    ? relaxFlowCandidateCount
                    : Math.Min(relaxFlowCandidateCount, 24);
                for (int i = 0; i < continuationScanCount; i++)
                {
                    SimAssistDifficultyProfile candidateProfile = CalculateSimAssistDifficultyProfile(
                        context,
                        relaxFlowCandidates[i]);
                    if (IsSimComparableAssistDifficulty(baselineProfile, candidateProfile))
                    {
                        selectedContinuationIndex = i;
                        break;
                    }
                }

                if (selectedContinuationIndex >= 0)
                {
                    bestSet ??= new PieceInstance[GameConstants.TraySize];
                    Array.Copy(
                        relaxFlowCandidates[selectedContinuationIndex],
                        bestSet,
                        GameConstants.TraySize);
                    bestScore = relaxFlowCandidateScores[selectedContinuationIndex];
                    bestFitCount = relaxFlowCandidateFitCounts[selectedContinuationIndex];
                    metrics.generatedFlowMatchTrays++;
                    selectedMatchedFlowTarget = true;
                    selectedByStrongContinuationGate = true;
                }
            }

            if (validCandidates == null
                && maximumContinuationGate
                && !selectedByStrongContinuationGate
                && TryConstructSimReadableContinuation(
                    context,
                    bestSet,
                    random,
                    flowTargets,
                    flowTargetCount,
                    out PieceInstance[] constructedSet,
                    out int constructedFitCount))
            {
                SimAssistDifficultyProfile baselineProfile = CalculateSimAssistDifficultyProfile(
                    context,
                    bestSet);
                SimAssistDifficultyProfile constructedProfile = CalculateSimAssistDifficultyProfile(
                    context,
                    constructedSet);
                if (IsSimComparableAssistDifficulty(baselineProfile, constructedProfile))
                {
                    bestSet ??= new PieceInstance[GameConstants.TraySize];
                    Array.Copy(constructedSet, bestSet, GameConstants.TraySize);
                    bestFitCount = constructedFitCount;
                    bestScore = CalculateScoreTerms(
                        context, bestSet, bestFitCount, difficulty01, loopAdjustedAssist,
                        runPressure01, consecutiveReliefBiasedTrays, classicTrayNumber,
                        flowTargetCount, configuration, phase8CurationMultiplier).Total;
                    selectedMatchedFlowTarget = true;
                    selectedByStrongContinuationGate = true;
                    metrics.generatedFlowMatchTrays++;
                    metrics.constructedContinuationTrays[GetContinuationStage(classicTrayNumber)]++;
                }
            }

            if (validCandidates == null
                && !selectedByStrongContinuationGate
                && useRelaxFlow
                && relaxFlowCandidateCount > 0)
            {
                PieceInstance[] relaxed = SelectSimRelaxFlowCandidate(
                    board,
                    context,
                    bestSet,
                    classicTrayNumber,
                    flowTargets,
                    flowTargetCount,
                    relaxFlowCandidates,
                    relaxFlowCandidateScores,
                    relaxFlowCandidateFitCounts,
                    relaxFlowCandidateCount,
                    out int relaxedScore,
                    out int relaxedFitCount,
                    out SimFlowProjection relaxedProjection,
                    out int relaxedFlowScore,
                    out bool matchedFlowTarget);
                if (relaxed != null)
                {
                    bestSet ??= new PieceInstance[GameConstants.TraySize];
                    Array.Copy(relaxed, bestSet, GameConstants.TraySize);
                    bestScore = relaxedScore;
                    bestFitCount = relaxedFitCount;
                    metrics.projectedOccupiedCells += relaxedProjection.finalOccupiedCells;
                    metrics.projectedLargestOpenArea += relaxedProjection.largestOpenArea;
                    metrics.projectedFragmentation += relaxedProjection.emptyRegionCount;
                    metrics.projectedTraySamples++;
                    metrics.flowScoreTotal += relaxedFlowScore;
                    metrics.generatedFlowMatchTrays += matchedFlowTarget ? 1 : 0;
                    selectedMatchedFlowTarget = matchedFlowTarget;
                }
            }

            bool challengeFallback = false;
            int selectedTrayEase = 0;
            if (validCandidates == null && useLateChallengeBand)
            {
                PieceInstance[] challengeSelection = SelectSimLateChallengeCandidate(
                    classicTrayNumber,
                    lateChallengeCandidates,
                    lateChallengeScores,
                    lateChallengeEaseScores,
                    lateChallengeFitCounts,
                    lateChallengeCount,
                    out int challengeScore,
                    out int challengeFitCount,
                    out selectedTrayEase,
                    out challengeFallback);
                if (challengeSelection != null)
                {
                    bestSet ??= new PieceInstance[GameConstants.TraySize];
                    Array.Copy(challengeSelection, bestSet, GameConstants.TraySize);
                    bestScore = challengeScore;
                    bestFitCount = challengeFitCount;
                    selectedByLateChallengeBand = true;
                }
                else
                {
                    challengeFallback = true;
                }
            }

            if (validCandidates != null && validCandidates.Count > 0)
            {
                bestSet = validCandidates[random.Next(validCandidates.Count)];
                bestFitCount = CountFittingPieces(context, bestSet);
            }

            PieceInstance[] selected;
            if (bestSet != null && bestFitCount > 0)
            {
                ScoreTerms selectedBeforePostSelection = CalculateScoreTerms(
                    context,
                    bestSet,
                    bestFitCount,
                    difficulty01,
                    loopAdjustedAssist,
                    runPressure01,
                    consecutiveReliefBiasedTrays,
                    classicTrayNumber,
                    flowTargetCount,
                    configuration,
                    phase8CurationMultiplier);
                selectedPhase7HScoreBeforePostSelection = selectedBeforePostSelection.Total
                    - selectedBeforePostSelection.phase8CurationAfterGate;
                int prePostSelectionClearOpportunities = CountImmediateClearOpportunities(context, bestSet);
                OccupancyState selectionState = GetOccupancyState(context.board.OccupiedCount);
                bool lateNonEssentialCuration = classicTrayNumber >= 9
                    && selectionState != OccupancyState.Critical;
                float postSelectionAssist = loopAdjustedAssist
                    * GetLateNonEssentialAssistScale(classicTrayNumber, selectionState);
                if (!selectedByStrongContinuationGate && !selectedByLateChallengeBand)
                {
                    ImproveSetWithRescuePieces(context, bestSet, random, difficulty01, loopAdjustedAssist);
                    if (!lateNonEssentialCuration)
                    {
                        EnsureSatisfyingPiece(context, bestSet, random, difficulty01, postSelectionAssist);
                        EnsureComebackPiece(context, bestSet, random, postSelectionAssist);
                        EnsureImmediateClearPiece(context, bestSet, random, postSelectionAssist);
                    }
                    if (!lateNonEssentialCuration
                        && configuration.satisfactionCurationEnabled
                        && configuration.satisfactionPostSelectionGuardsEnabled)
                    {
                        EnsureCuratedJuicySetMass(context, bestSet, random, difficulty01);
                        EnsureCuratedSatisfyingSetShapeMix(context, bestSet, random);
                    }
                    else if (!lateNonEssentialCuration)
                    {
                        EnsureJuicySetMass(context, bestSet, random, difficulty01);
                        EnsureSatisfyingSetShapeMix(context, bestSet, random, difficulty01, postSelectionAssist);
                    }
                }
                RecordPostSelectionEffect(metrics, prePostSelectionClearOpportunities, CountImmediateClearOpportunities(context, bestSet));
                selected = bestSet;
            }
            else
            {
                selected = new[]
                {
                    new PieceInstance("single", (ChromaColor)random.Next(GameConstants.ColorCount)),
                    new PieceInstance("single", (ChromaColor)random.Next(GameConstants.ColorCount)),
                    new PieceInstance("single", (ChromaColor)random.Next(GameConstants.ColorCount))
                };
            }

            TrayGenerationResult result = CreateTrayGenerationResult(
                context,
                selected,
                difficulty01,
                loopAdjustedAssist,
                runPressure01,
                consecutiveReliefBiasedTrays,
                classicTrayNumber,
                flowTargetCount,
                configuration,
                phase8CurationMultiplier);
            result.matchedFlowTarget = selectedMatchedFlowTarget;
            result.trayEaseScore = selectedByLateChallengeBand
                ? selectedTrayEase
                : CalculateSimTrayEaseScore(context, selected, out _);
            result.challengeBandFallback = challengeFallback;
            result.criticalChallengeBypass = classicTrayNumber >= 9
                && GetOccupancyState(board.OccupiedCount) == OccupancyState.Critical;
            int easeStage = GetTrayEaseStage(classicTrayNumber);
            metrics.trayEaseScores[easeStage] += result.trayEaseScore;
            metrics.trayEaseSamples[easeStage]++;
            if (classicTrayNumber >= 9)
            {
                metrics.lateChallengeTraySamples++;
                metrics.challengeBandFallbacks += result.challengeBandFallback ? 1 : 0;
                metrics.criticalChallengeBypasses += result.criticalChallengeBypass ? 1 : 0;
            }
            metrics.continuationTraySamples[GetContinuationStage(classicTrayNumber)]++;
            metrics.readableContinuationTrays[GetContinuationStage(classicTrayNumber)] += selectedMatchedFlowTarget ? 1 : 0;
            result.selectionReason = candidateTermBaseline.GetDominantAdvantage(result.scoreTerms);
            result.curationChangedRanking = configuration.satisfactionCurationEnabled
                && selectedPhase7HScoreBeforePostSelection < bestPhase7HScore;
            return result;
        }

        private static int CaptureSimFlowTargets(HeadlessBoard board, SimFlowTarget[] targets)
        {
            int count = 0;
            for (int orientation = 0; orientation < 2; orientation++)
            {
                bool row = orientation == 0;
                for (int line = 0; line < GameConstants.BoardSize; line++)
                {
                    int filled = board.GetLineFillForFlow(row, line);
                    if (filled < 2 || filled > GameConstants.BoardSize - 1)
                    {
                        continue;
                    }

                    int score = GetSimContinuationIntentPriority(filled);
                    int insertAt = count < targets.Length ? count : targets.Length - 1;
                    while (insertAt > 0 && score > GetSimFlowTargetPriority(targets[insertAt - 1]))
                    {
                        if (insertAt < targets.Length)
                        {
                            targets[insertAt] = targets[insertAt - 1];
                        }
                        insertAt--;
                    }

                    if (insertAt < targets.Length
                        && (count < targets.Length || score > GetSimFlowTargetPriority(targets[insertAt])))
                    {
                        targets[insertAt] = new SimFlowTarget
                        {
                            row = row,
                            lineIndex = line,
                            filledCells = filled
                        };
                        if (count < targets.Length)
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }

        private static int GetSimFlowTargetPriority(SimFlowTarget target)
        {
            return GetSimContinuationIntentPriority(target.filledCells);
        }

        private static int GetSimContinuationIntentPriority(int filledCells)
        {
            switch (filledCells)
            {
                case 6: return 1400;
                case 5: return 1250;
                case 4: return 1050;
                case 7: return 900;
                case 3: return 760;
                case 2: return 520;
                default: return 0;
            }
        }

        private static int GetContinuationStage(int classicTrayNumber)
        {
            if (classicTrayNumber <= 4) return 0;
            if (classicTrayNumber <= 6) return 1;
            if (classicTrayNumber <= 8) return 2;
            return 3;
        }

        private static int GetTrayEaseStage(int classicTrayNumber)
        {
            if (classicTrayNumber <= 8) return 0;
            if (classicTrayNumber <= 11) return 1;
            if (classicTrayNumber <= 15) return 2;
            if (classicTrayNumber <= 20) return 3;
            return 4;
        }

        private static bool TryConstructSimReadableContinuation(
            GenerationContext context,
            PieceInstance[] normalWinner,
            System.Random random,
            SimFlowTarget[] targets,
            int targetCount,
            out PieceInstance[] constructed,
            out int fitCount)
        {
            constructed = null;
            fitCount = 0;
            if (context == null || normalWinner == null || targetCount <= 0)
            {
                return false;
            }

            PieceInstance flexiblePiece = null;
            int bestFlexOptions = 3;
            for (int i = 0; i < normalWinner.Length; i++)
            {
                PieceInstance candidate = normalWinner[i];
                if (candidate == null) continue;
                int options = context.GetProfile(candidate).placementOptions;
                if (options > bestFlexOptions)
                {
                    bestFlexOptions = options;
                    flexiblePiece = candidate;
                }
            }

            if (flexiblePiece == null) return false;
            string bestAdvance = null;
            string bestPayoff = null;
            int bestRelationshipScore = 0;
            for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
            {
                SimFlowTarget target = targets[targetIndex];
                for (int advanceIndex = 0; advanceIndex < SimContinuationShapeIds.Length; advanceIndex++)
                {
                    PieceInstance advance = new PieceInstance(SimContinuationShapeIds[advanceIndex], ChromaColor.Cyan);
                    int advanceOptions = context.GetProfile(advance).placementOptions;
                    if (advanceOptions <= 0) continue;
                    for (int payoffIndex = 0; payoffIndex < SimContinuationShapeIds.Length; payoffIndex++)
                    {
                        PieceInstance payoff = new PieceInstance(SimContinuationShapeIds[payoffIndex], ChromaColor.Cyan);
                        int payoffScore = context.board.ScoreFlowTargetPayoff(
                            advance, payoff, target.row, target.lineIndex,
                            out int continuationAdvance, out bool completesTarget);
                        if (payoffScore <= 0 || continuationAdvance <= 0) continue;
                        int relationshipScore = payoffScore * 16
                            + continuationAdvance * 1800
                            + Math.Min(advanceOptions, 12) * 80
                            + (completesTarget ? 700 : 0);
                        if (relationshipScore <= bestRelationshipScore) continue;
                        bestRelationshipScore = relationshipScore;
                        bestAdvance = advance.shapeId;
                        bestPayoff = payoff.shapeId;
                    }
                }
            }

            if (bestAdvance == null || bestPayoff == null) return false;
            constructed = new[]
            {
                new PieceInstance(bestAdvance, (ChromaColor)random.Next(GameConstants.ColorCount)),
                new PieceInstance(bestPayoff, (ChromaColor)random.Next(GameConstants.ColorCount)),
                new PieceInstance(flexiblePiece.shapeId, flexiblePiece.color)
            };
            fitCount = CountFittingPieces(context, constructed);
            CalculateSimTrayEaseScore(context, constructed, out int totalPlacementOptions);
            return fitCount >= 2 && totalPlacementOptions >= 5;
        }

        private static void ConsiderSimRelaxFlowCandidate(
            PieceInstance[] candidate,
            int score,
            int fitCount,
            PieceInstance[][] candidates,
            int[] scores,
            int[] fitCounts,
            ref int count)
        {
            int insertAt = count < candidates.Length ? count : candidates.Length - 1;
            while (insertAt > 0 && score > scores[insertAt - 1])
            {
                if (insertAt < candidates.Length)
                {
                    scores[insertAt] = scores[insertAt - 1];
                    fitCounts[insertAt] = fitCounts[insertAt - 1];
                    Array.Copy(candidates[insertAt - 1], candidates[insertAt], GameConstants.TraySize);
                }
                insertAt--;
            }

            if (count >= candidates.Length && score <= scores[insertAt])
            {
                return;
            }

            scores[insertAt] = score;
            fitCounts[insertAt] = fitCount;
            Array.Copy(candidate, candidates[insertAt], GameConstants.TraySize);
            if (count < candidates.Length)
            {
                count++;
            }
        }

        private static void ConsiderSimLateChallengeCandidate(
            GenerationContext context,
            PieceInstance[] candidate,
            int normalScore,
            int fitCount,
            PieceInstance[][] candidates,
            int[] scores,
            int[] easeScores,
            int[] fitCounts,
            ref int count)
        {
            if (candidate == null || fitCount < 2)
            {
                return;
            }

            int easeScore = CalculateSimTrayEaseScore(context, candidate, out int totalOptions);
            if (totalOptions < 5)
            {
                return;
            }

            int insertAt = count;
            while (insertAt > 0 && easeScore < easeScores[insertAt - 1])
            {
                if (insertAt < candidates.Length)
                {
                    candidates[insertAt] = candidates[insertAt - 1];
                    scores[insertAt] = scores[insertAt - 1];
                    easeScores[insertAt] = easeScores[insertAt - 1];
                    fitCounts[insertAt] = fitCounts[insertAt - 1];
                }

                insertAt--;
            }

            if (insertAt >= candidates.Length)
            {
                return;
            }

            PieceInstance[] copy = new PieceInstance[GameConstants.TraySize];
            Array.Copy(candidate, copy, GameConstants.TraySize);
            candidates[insertAt] = copy;
            scores[insertAt] = normalScore;
            easeScores[insertAt] = easeScore;
            fitCounts[insertAt] = fitCount;
            if (count < candidates.Length)
            {
                count++;
            }
        }

        private static int CalculateSimTrayEaseScore(
            GenerationContext context,
            PieceInstance[] candidate,
            out int totalPlacementOptions)
        {
            SimAssistDifficultyProfile profile = CalculateSimAssistDifficultyProfile(context, candidate);
            totalPlacementOptions = profile.totalPlacementOptions;
            return profile.easeScore;
        }

        private static SimAssistDifficultyProfile CalculateSimAssistDifficultyProfile(
            GenerationContext context,
            PieceInstance[] candidate)
        {
            SimAssistDifficultyProfile result = default;
            int minimumPlacementOptions = int.MaxValue;
            int setupValue = 0;
            for (int i = 0; i < candidate.Length; i++)
            {
                if (candidate[i] == null)
                {
                    minimumPlacementOptions = 0;
                    continue;
                }

                PlacementProfile profile = context.GetProfile(candidate[i]);
                result.totalPlacementOptions += profile.placementOptions;
                minimumPlacementOptions = Math.Min(minimumPlacementOptions, profile.placementOptions);
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

        private static bool IsSimComparableAssistDifficulty(
            SimAssistDifficultyProfile baseline,
            SimAssistDifficultyProfile candidate)
        {
            int easeTolerance = Math.Max(250, Mathf.RoundToInt(Mathf.Abs(baseline.easeScore) * 0.02f));
            int placementTolerance = Math.Max(1, Mathf.CeilToInt(baseline.totalPlacementOptions * 0.02f));
            int setupTolerance = Math.Max(60, Mathf.RoundToInt(Mathf.Abs(baseline.setup) * 0.02f));
            int cleanlinessTolerance = Math.Max(60, Mathf.RoundToInt(Mathf.Abs(baseline.cleanliness) * 0.02f));
            return candidate.easeScore >= baseline.easeScore - easeTolerance
                && candidate.easeScore <= baseline.easeScore + easeTolerance
                && candidate.totalPlacementOptions <= baseline.totalPlacementOptions + placementTolerance
                && candidate.minimumPlacementOptions <= baseline.minimumPlacementOptions + 1
                && candidate.immediateClears <= baseline.immediateClears
                && candidate.setup <= baseline.setup + setupTolerance
                && candidate.cleanliness <= baseline.cleanliness + cleanlinessTolerance;
        }

        private static PieceInstance[] SelectSimLateChallengeCandidate(
            int classicTrayNumber,
            PieceInstance[][] candidates,
            int[] scores,
            int[] easeScores,
            int[] fitCounts,
            int count,
            out int selectedScore,
            out int selectedFitCount,
            out int selectedEaseScore,
            out bool usedFallback)
        {
            selectedScore = int.MinValue;
            selectedFitCount = 0;
            selectedEaseScore = 0;
            usedFallback = false;
            if (count <= 0)
            {
                return null;
            }

            GetSimLateChallengeEaseBand(classicTrayNumber, out float lower, out float upper);
            int last = count - 1;
            int lowerIndex = Mathf.Clamp(Mathf.CeilToInt(last * lower), 0, last);
            int upperIndex = Mathf.Clamp(Mathf.FloorToInt(last * upper), lowerIndex, last);
            int selectedIndex = -1;
            for (int i = lowerIndex; i <= upperIndex; i++)
            {
                if (scores[i] <= selectedScore) continue;
                selectedIndex = i;
                selectedScore = scores[i];
            }

            if (selectedIndex < 0)
            {
                usedFallback = true;
                selectedIndex = Mathf.Clamp((lowerIndex + upperIndex) / 2, 0, last);
                selectedScore = scores[selectedIndex];
            }

            selectedFitCount = fitCounts[selectedIndex];
            selectedEaseScore = easeScores[selectedIndex];
            return candidates[selectedIndex];
        }

        private static void GetSimLateChallengeEaseBand(
            int classicTrayNumber,
            out float lower,
            out float upper)
        {
            if (classicTrayNumber <= 11)
            {
                lower = 0.35f;
                upper = 0.55f;
            }
            else if (classicTrayNumber <= 15)
            {
                lower = 0.20f;
                upper = 0.40f;
            }
            else if (classicTrayNumber <= 20)
            {
                lower = 0.05f;
                upper = 0.25f;
            }
            else
            {
                lower = 0f;
                upper = 0.15f;
            }
        }

        private static PieceInstance[] SelectSimRelaxFlowCandidate(
            HeadlessBoard board,
            GenerationContext context,
            PieceInstance[] baselineSet,
            int classicTrayNumber,
            SimFlowTarget[] flowTargets,
            int flowTargetCount,
            PieceInstance[][] candidates,
            int[] scores,
            int[] fitCounts,
            int count,
            out int selectedScore,
            out int selectedFitCount,
            out SimFlowProjection selectedProjection,
            out int selectedFlowScore,
            out bool matchedFlowTarget)
        {
            selectedScore = int.MinValue;
            selectedFitCount = 0;
            selectedProjection = default;
            selectedFlowScore = 0;
            matchedFlowTarget = false;
            PieceInstance[] selected = null;
            SimAssistDifficultyProfile baselineProfile = CalculateSimAssistDifficultyProfile(
                context,
                baselineSet);
            float projectionWeight = GetSimRelaxFlowProjectionWeight(classicTrayNumber);
            float continuityStrength = GetSimFlowContinuityStrength(classicTrayNumber);
            int evaluationCount = Math.Min(count, 4);
            for (int i = 0; i < evaluationCount; i++)
            {
                SimAssistDifficultyProfile candidateProfile = CalculateSimAssistDifficultyProfile(
                    context,
                    candidates[i]);
                if (!IsSimComparableAssistDifficulty(baselineProfile, candidateProfile))
                {
                    continue;
                }

                SimFlowProjection projection = EvaluateSimFlowProjection(board, candidates[i]);
                int flowScore = CalculateSimFlowContinuityScore(
                    context,
                    candidates[i],
                    flowTargets,
                    flowTargetCount,
                    continuityStrength,
                    classicTrayNumber,
                    out bool matched);
                int total = scores[i] + Mathf.RoundToInt(projection.cleanlinessScore * projectionWeight) + flowScore;
                if (total <= selectedScore)
                {
                    continue;
                }

                selected = candidates[i];
                selectedScore = total;
                selectedFitCount = fitCounts[i];
                selectedProjection = projection;
                selectedFlowScore = flowScore;
                matchedFlowTarget = matched;
            }

            return selected;
        }

        private static int CalculateSimFlowContinuityScore(
            GenerationContext context,
            PieceInstance[] set,
            SimFlowTarget[] targets,
            int targetCount,
            float strength,
            int classicTrayNumber,
            out bool matchedFlowTarget)
        {
            matchedFlowTarget = false;
            if (targetCount <= 0 || strength <= 0f)
            {
                return 0;
            }

            int best = 0;
            for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
            {
                SimFlowTarget target = targets[targetIndex];
                for (int continuationIndex = 0; continuationIndex < set.Length; continuationIndex++)
                {
                    PieceInstance continuation = set[continuationIndex];
                    if (continuation == null)
                    {
                        continue;
                    }

                    for (int payoffIndex = 0; payoffIndex < set.Length; payoffIndex++)
                    {
                        if (payoffIndex == continuationIndex || set[payoffIndex] == null)
                        {
                            continue;
                        }

                        int payoffScore = context.board.ScoreFlowTargetPayoff(
                            continuation,
                            set[payoffIndex],
                            target.row,
                            target.lineIndex,
                            out int advance,
                            out bool completesTarget);
                        if (payoffScore <= 0 || advance <= 0)
                        {
                            continue;
                        }

                        bool hasFlexibleThird = false;
                        for (int flexIndex = 0; flexIndex < set.Length; flexIndex++)
                        {
                            if (flexIndex != continuationIndex
                                && flexIndex != payoffIndex
                                && set[flexIndex] != null
                                && context.GetProfile(set[flexIndex]).placementOptions >= 4)
                            {
                                hasFlexibleThird = true;
                                break;
                            }
                        }

                        if (!hasFlexibleThird)
                        {
                            continue;
                        }

                        int score = advance * 1600
                            + payoffScore * 16
                            + 2300
                            + 1000
                            + (completesTarget ? 480 : 0);
                        best = Mathf.Max(best, score);
                    }
                }
            }

            matchedFlowTarget = best > 0;
            return Mathf.RoundToInt(best * strength * GetSimFlowAssistBoost(classicTrayNumber));
        }

        private static int CalculateSimEarlyBuildFlexBonus(
            GenerationContext context,
            PieceInstance[] set,
            int classicTrayNumber,
            int flowTargetCount)
        {
            if (classicTrayNumber < 1
                || classicTrayNumber > 8
                || flowTargetCount > 0
                || context == null
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

                PlacementProfile setupProfile = context.GetProfile(setupPiece);
                if (!setupProfile.hasSetupOrigin
                    || setupProfile.clearOpportunities > 0
                    || setupProfile.bestLineProgress < 4)
                {
                    continue;
                }

                for (int payoffIndex = 0; payoffIndex < set.Length; payoffIndex++)
                {
                    if (payoffIndex == setupIndex || set[payoffIndex] == null
                        || context.GetPairPayoffScore(setupPiece, set[payoffIndex]) <= 0)
                    {
                        continue;
                    }

                    for (int flexIndex = 0; flexIndex < set.Length; flexIndex++)
                    {
                        if (flexIndex != setupIndex
                            && flexIndex != payoffIndex
                            && set[flexIndex] != null
                            && context.GetProfile(set[flexIndex]).placementOptions >= 4)
                        {
                            return 2200;
                        }
                    }
                }
            }

            return 0;
        }

        private static SimFlowProjection EvaluateSimFlowProjection(HeadlessBoard board, PieceInstance[] set)
        {
            SimFlowProjection best = default;
            for (int order = 0; order < SimFlowOrders.GetLength(0); order++)
            {
                EvaluateSimFlowOrder(board, set, order, 0, 0, ref best);
            }
            return best;
        }

        private static void EvaluateSimFlowOrder(
            HeadlessBoard board,
            PieceInstance[] set,
            int orderIndex,
            int depth,
            int clearedLines,
            ref SimFlowProjection best)
        {
            if (depth >= GameConstants.TraySize)
            {
                SimFlowProjection result = new SimFlowProjection
                {
                    valid = true,
                    finalOccupiedCells = board.OccupiedCount,
                    largestOpenArea = board.CountLargestOpenAreaForFlow(),
                    largestOpenRectangle = board.CountLargestOpenRectangleForFlow(),
                    emptyRegionCount = board.CountEmptyRegionsForFlow(),
                    isolatedHoles = board.CountIsolatedHolesForFlow(),
                    narrowCorridorCells = board.CountNarrowCorridorCellsForFlow(),
                    futureOptions = board.CountFutureFlowOptions(),
                    clearedLines = clearedLines
                };
                result.cleanlinessScore = -result.finalOccupiedCells * 95
                    + result.largestOpenArea * 72
                    + result.largestOpenRectangle * 45
                    + result.futureOptions * 230
                    + result.clearedLines * 720
                    - result.emptyRegionCount * 360
                    - result.isolatedHoles * 500
                    - result.narrowCorridorCells * 80;
                if (!best.valid || result.cleanlinessScore > best.cleanlinessScore)
                {
                    best = result;
                }
                return;
            }

            PieceInstance piece = set[SimFlowOrders[orderIndex, depth]];
            SimFlowPlacement[] placements = new SimFlowPlacement[3];
            int placementCount = CollectSimFlowPlacements(board, piece, placements);
            for (int i = 0; i < placementCount; i++)
            {
                int lines = board.CountLinesAfterPlacementForFlow(piece, placements[i].x, placements[i].y);
                HeadlessBoard next = board.CloneAfterPlacement(piece, placements[i].x, placements[i].y);
                EvaluateSimFlowOrder(next, set, orderIndex, depth + 1, clearedLines + lines, ref best);
            }
        }

        private static int CollectSimFlowPlacements(HeadlessBoard board, PieceInstance piece, SimFlowPlacement[] destination)
        {
            int count = 0;
            PieceData data = piece.Data;
            for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
            {
                for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                {
                    if (!board.CanPlace(piece, x, y))
                    {
                        continue;
                    }

                    int score = board.CountLinesAfterPlacementForFlow(piece, x, y) * 4500
                        + board.ScorePlacementSetup(piece, x, y);
                    int insertAt = count < destination.Length ? count : destination.Length - 1;
                    while (insertAt > 0 && score > destination[insertAt - 1].score)
                    {
                        if (insertAt < destination.Length)
                        {
                            destination[insertAt] = destination[insertAt - 1];
                        }
                        insertAt--;
                    }

                    if (count >= destination.Length && score <= destination[insertAt].score)
                    {
                        continue;
                    }

                    destination[insertAt] = new SimFlowPlacement { x = x, y = y, score = score };
                    if (count < destination.Length)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static float GetSimFlowContinuityStrength(int trayNumber)
        {
            if (trayNumber <= 4) return 2.70f;
            if (trayNumber <= 6) return 2.58f;
            return trayNumber <= 8 ? 1.28f : 0f;
        }

        private static float GetSimFlowAssistBoost(int trayNumber)
        {
            if (trayNumber <= 6) return 2.30f;
            return trayNumber <= 8 ? 1.70f : 1f;
        }

        private static float GetSimRelaxFlowProjectionWeight(int trayNumber)
        {
            if (trayNumber <= 6) return 1f;
            return trayNumber <= 8 ? 0.95f : 0f;
        }

        // Mirrors PieceSpawner's only final shape-pool exception: stair5 is not
        // generated in normal Classic trays and, under late established pressure,
        // survives only with at least two legal placements on the current board.
        private static void ReplaceIneligibleClassicStair5(
            GenerationContext context,
            PieceInstance[] set,
            System.Random random,
            float difficulty01,
            float runPressure01)
        {
            bool latePressureActive = runPressure01 >= 0.22f;
            for (int i = 0; i < set.Length; i++)
            {
                PieceInstance piece = set[i];
                if (piece == null || piece.shapeId != "stair5")
                {
                    continue;
                }

                if (latePressureActive && context.GetProfile(piece).placementOptions >= 2)
                {
                    continue;
                }

                set[i] = PieceCatalog.RandomPiece(random, difficulty01, allowStair5: false);
            }
        }

        private static int CountImmediateClearOpportunities(GenerationContext context, PieceInstance[] set)
        {
            int total = 0;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null)
                {
                    total += context.GetProfile(set[i]).clearOpportunities;
                }
            }

            return total;
        }

        private static void ConsiderTrioCurationCandidate(
            PieceInstance[] set,
            int score,
            int fitCount,
            PieceInstance[][] candidates,
            int[] candidateScores,
            int[] candidateFitCounts)
        {
            int lastIndex = candidateScores.Length - 1;
            if (score <= candidateScores[lastIndex])
            {
                return;
            }

            int insertIndex = lastIndex;
            while (insertIndex > 0 && score > candidateScores[insertIndex - 1])
            {
                candidateScores[insertIndex] = candidateScores[insertIndex - 1];
                candidateFitCounts[insertIndex] = candidateFitCounts[insertIndex - 1];
                Array.Copy(candidates[insertIndex - 1], candidates[insertIndex], GameConstants.TraySize);
                insertIndex--;
            }

            candidateScores[insertIndex] = score;
            candidateFitCounts[insertIndex] = fitCount;
            Array.Copy(set, candidates[insertIndex], GameConstants.TraySize);
        }

        private static void SelectBestTrioCuratedCandidate(
            GenerationContext context,
            PieceInstance[][] candidates,
            int[] candidateScores,
            int[] candidateFitCounts,
            ref PieceInstance[] bestSet,
            ref int bestScore,
            ref int bestFitCount,
            SimulationConfiguration configuration,
            float runPressure01,
            float phase8CurationMultiplier)
        {
            int curatedScore = int.MinValue;
            int selectedIndex = -1;
            for (int i = 0; i < candidateScores.Length; i++)
            {
                if (candidateScores[i] == int.MinValue)
                {
                    continue;
                }

                TrayCurationAnalysis curation = AnalyzeTrayCuration(
                    context,
                    candidates[i],
                    true,
                    configuration,
                    runPressure01,
                    phase8CurationMultiplier);
                int gatedSequenceScore = ApplyImmediateClearCurationGate(
                    curation.sequenceScore,
                    CountImmediateClearOpportunities(context, candidates[i]),
                    GetOccupancyState(context.board.OccupiedCount),
                    configuration);
                int score = candidateScores[i] + gatedSequenceScore;
                if (score > curatedScore)
                {
                    curatedScore = score;
                    selectedIndex = i;
                }
            }

            if (selectedIndex >= 0)
            {
                bestSet ??= new PieceInstance[GameConstants.TraySize];
                Array.Copy(candidates[selectedIndex], bestSet, GameConstants.TraySize);
                bestScore = curatedScore;
                bestFitCount = candidateFitCounts[selectedIndex];
            }
        }

        private static void RecordPostSelectionEffect(RunMetrics metrics, int before, int after)
        {
            metrics.postSelectionTrays++;
            metrics.prePostSelectionImmediateOpportunities += before;
            metrics.postPostSelectionImmediateOpportunities += after;
            if (before == 0 && after > 0)
            {
                metrics.postSelectionInjectedImmediateClearTrays++;
            }
        }

        private static TrayGenerationResult CreateTrayGenerationResult(
            GenerationContext context,
            PieceInstance[] pieces,
            float difficulty01,
            float assist01,
            float runPressure01,
            int consecutiveReliefBiasedTrays,
            int classicTrayNumber,
            int flowTargetCount,
            SimulationConfiguration configuration,
            float phase8CurationMultiplier)
        {
            ScoreTerms selectedTerms = CalculateScoreTerms(
                context,
                pieces,
                CountFittingPieces(context, pieces),
                difficulty01,
                assist01,
                runPressure01,
                consecutiveReliefBiasedTrays,
                classicTrayNumber,
                flowTargetCount,
                configuration,
                phase8CurationMultiplier);
            TrayGenerationResult result = new TrayGenerationResult
            {
                pieces = pieces,
                selectedScore = selectedTerms.Total,
                scoreTerms = selectedTerms,
                occupiedCells = context.board.OccupiedCount,
                occupancyState = GetOccupancyState(context.board.OccupiedCount),
                runPressure = runPressure01,
                selectionReason = GetDominantSelectionReason(selectedTerms),
                piecePlacementOptions = new int[pieces.Length]
            };

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null)
                {
                    continue;
                }

                PlacementProfile profile = context.GetProfile(pieces[i]);
                result.piecePlacementOptions[i] = profile.placementOptions;
                result.immediateClearOpportunities += profile.clearOpportunities;
                result.setupOpportunities += profile.bestSetupScore;
                result.adjacencyContacts += profile.bestAdjacencyContacts;
                result.lineProgress += profile.bestLineProgress;
                result.cleanlinessScore += profile.bestCleanlinessScore;
            }

            SetupPayoffAnalysis setupPayoffAnalysis = context.AnalyzeSetupPayoff(pieces);
            result.curation = AnalyzeTrayCuration(
                context,
                pieces,
                true,
                configuration,
                runPressure01,
                phase8CurationMultiplier);
            if (configuration.satisfactionCurationEnabled)
            {
                int gatedSequenceScore = ApplyImmediateClearCurationGate(
                    result.curation.sequenceScore,
                    result.immediateClearOpportunities,
                    result.occupancyState,
                    configuration);
                result.lightCurationScoreBeforeGate = selectedTerms.phase8CurationBeforeGate
                    + result.curation.sequenceScore;
                result.lightCurationScoreAfterGate = selectedTerms.phase8CurationAfterGate
                    + gatedSequenceScore;
            }
            result.setupPayoffOpportunities = setupPayoffAnalysis.currentScore;
            result.pureSetupOpportunities = setupPayoffAnalysis.pureScore;
            result.immediateClearSetupOverlap = result.immediateClearOpportunities > 0
                && result.setupPayoffOpportunities > 0;
            result.usesPureSetupScoring = result.occupancyState == OccupancyState.Open
                && configuration.pureSetupScoringOnOpen;
            result.openDiversityBonusApplied = result.usesPureSetupScoring
                && setupPayoffAnalysis.pureScore > 0
                && result.immediateClearOpportunities == 0
                && configuration.openPureSetupDiversityBonus == 2500;
            result.pureSetupDescription = setupPayoffAnalysis.Description;
            result.setupClassification = GetSetupClassification(result);
            result.reliefBiased = result.occupancyState >= OccupancyState.Pressured
                && (result.immediateClearOpportunities > 0
                    || result.setupPayoffOpportunities > 0
                    || result.lineProgress >= 10);
            return result;
        }

        private static SetupClassification GetSetupClassification(TrayGenerationResult result)
        {
            if (result.pureSetupOpportunities > 0)
            {
                return SetupClassification.PureTwoStep;
            }

            if (result.setupPayoffOpportunities > 0 && result.immediateClearOpportunities > 0)
            {
                return SetupClassification.ImmediateClearAndPayoff;
            }

            return result.setupPayoffOpportunities > 0
                ? SetupClassification.DirectPayoff
                : SetupClassification.GeneralFutureSetup;
        }

        private static SelectionReason GetDominantSelectionReason(ScoreTerms terms)
        {
            int best = terms.fitFairness;
            SelectionReason reason = SelectionReason.FitFairness;
            if (terms.setupPayoff > best) { best = terms.setupPayoff; reason = SelectionReason.SetupPayoff; }
            if (terms.immediateClear > best) { best = terms.immediateClear; reason = SelectionReason.ImmediateClear; }
            if (terms.connectivity > best) { best = terms.connectivity; reason = SelectionReason.Connectivity; }
            if (terms.lineProgress > best) { best = terms.lineProgress; reason = SelectionReason.LineProgress; }
            if (terms.rescueRelief > best) { best = terms.rescueRelief; reason = SelectionReason.Relief; }
            if (terms.pieceSize > best) { best = terms.pieceSize; reason = SelectionReason.PieceSize; }
            if (terms.other > best) { reason = SelectionReason.Other; }
            return reason;
        }

        private static ScoreTerms CalculateScoreTerms(
            GenerationContext context,
            PieceInstance[] set,
            int fitCount,
            float difficulty01,
            float assist01,
            float runPressure01,
            int consecutiveReliefBiasedTrays,
            int classicTrayNumber,
            int flowTargetCount,
            SimulationConfiguration configuration,
            float phase8CurationMultiplier)
        {
            HeadlessBoard board = context.board;
            int emptyCells = board.EmptyCount;
            int occupiedCells = board.OccupiedCount;
            OccupancyState occupancyState = GetOccupancyState(occupiedCells);
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

                PlacementProfile profile = context.GetProfile(set[i]);
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

                if ((set[i].shapeId == "t4" || set[i].shapeId == "t4_v" || set[i].shapeId == "s4" || set[i].shapeId == "z4")
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

            SetupPayoffAnalysis setupPayoffAnalysis = context.AnalyzeSetupPayoff(set);
            int currentSetupPayoffOpportunities = setupPayoffAnalysis.currentScore;
            bool usePureSetupScoring = occupancyState == OccupancyState.Open
                && configuration.pureSetupScoringOnOpen;
            int setupPayoffOpportunities = usePureSetupScoring
                ? setupPayoffAnalysis.pureScore
                : currentSetupPayoffOpportunities;
            // The current broad per-piece setup sum is retained for all non-OPEN states
            // and Variant A. Variants B/C replace only its OPEN scoring contribution with
            // a proven non-clearing A -> newly-clearing B sequence.
            int setupOpportunitiesForScore = usePureSetupScoring
                ? setupPayoffAnalysis.pureScore
                : setupOpportunities;
            float openBoard01 = Mathf.Clamp01((emptyCells - 18f) / 28f);
            float targetCells = configuration.satisfactionCurationEnabled && configuration.curatedTargetMassEnabled
                ? Mathf.Lerp(12.0f, 14.8f, difficulty01) - occupancyPressure01 * 1.0f + runPressure01 * 0.8f
                : Mathf.Lerp(12.6f, 16.2f, difficulty01) - occupancyPressure01 * 1.4f + runPressure01 * 1.1f;
            int targetScore = Mathf.RoundToInt(920f - Mathf.Abs(totalCells - targetCells) * 76f);
            int mobilityWeight = Mathf.RoundToInt(Mathf.Lerp(52f, 30f, difficulty01) + occupancyPressure01 * 38f);
            int earlyLargePenalty = difficulty01 < 0.16f && largestPiece >= 6 ? 120 : 0;
            int tightLargePenalty = occupancyState == OccupancyState.Critical && largestPiece >= 6 ? 250 : 0;
            int allFitBonus = fitCount == GameConstants.TraySize ? 1100 : 0;
            int multiFitBonus = fitCount >= 2 ? 900 : 0;
            int clearWeight = occupancyState == OccupancyState.Open && !configuration.openImmediateClearScoreEnabled
                ? 0
                : GetImmediateClearWeight(occupancyState, difficulty01, configuration);
            int clearBonus = clearOpportunities * clearWeight;
            float pureSetupScale = GetLatePureSetupScale(classicTrayNumber, occupancyState);
            int setupBonus = Mathf.RoundToInt(setupOpportunitiesForScore * GetSetupWeight(occupancyState)
                * (usePureSetupScoring ? pureSetupScale : 1f));
            int adjacencyBonus = adjacencyContacts * GetAdjacencyWeight(occupancyState);
            int lineProgressBonus = lineProgress * GetLineProgressWeight(occupancyState);
            float setupPayoffMultiplier = occupancyState == OccupancyState.Open
                ? configuration.openSetupPayoffMultiplier
                : 1f;
            int setupPayoffBonus = Mathf.RoundToInt(setupPayoffOpportunities
                * GetSetupPayoffWeight(occupancyState)
                * setupPayoffMultiplier
                * (usePureSetupScoring ? pureSetupScale : 1f));
            int openPureSetupDiversityBonus = usePureSetupScoring
                && configuration.openPureSetupDiversityBonus > 0
                && setupPayoffAnalysis.pureScore > 0
                && clearOpportunities == 0
                ? Mathf.RoundToInt(GetSimOpenDiversityBonus(
                    classicTrayNumber,
                    configuration.openPureSetupDiversityBonus) * pureSetupScale)
                : 0;
            float cleanBoardAssistScale = GetSimExtraCleanBoardAssistScale(classicTrayNumber);
            int earlyBuildFlexBonus = Mathf.RoundToInt(CalculateSimEarlyBuildFlexBonus(
                context,
                set,
                classicTrayNumber,
                flowTargetCount) * cleanBoardAssistScale);
            int cleanlinessBonus = Mathf.RoundToInt(
                cleanlinessScore * GetCleanlinessWeight(occupancyState) * cleanBoardAssistScale);
            int satisfyingBonus = Mathf.RoundToInt(openBoard01
                * (mediumPieceCount * 560f + largePieceCount * 680f)
                * cleanBoardAssistScale);
            float nonEssentialAssist = assist01 * GetLateNonEssentialAssistScale(classicTrayNumber, occupancyState);
            int miniPiecePenalty = CalculateMiniPiecePenalty(set, emptyCells, nonEssentialAssist);
            int shapeMixBonus = Mathf.RoundToInt(
                CalculateShapeMixBonus(set, emptyCells, openBoard01) * cleanBoardAssistScale);
            TrayCurationAnalysis curation = configuration.satisfactionCurationEnabled
                ? AnalyzeTrayCuration(
                    context,
                    set,
                    false,
                    configuration,
                    runPressure01,
                    phase8CurationMultiplier)
                : default;
            int phase8CurationBeforeGate = configuration.satisfactionCurationEnabled
                ? curation.BaseScore
                : 0;
            int phase8CurationAfterGate = ApplyImmediateClearCurationGate(
                phase8CurationBeforeGate,
                clearOpportunities,
                occupancyState,
                configuration);
            float comebackClearOpportunityWeight = occupancyState == OccupancyState.Open
                ? configuration.openComebackClearOpportunityWeight
                : 1550f;
            int comebackBonus = Mathf.RoundToInt(nonEssentialAssist * (
                clearOpportunities * comebackClearOpportunityWeight
                + setupOpportunities * 58f
                + currentSetupPayoffOpportunities * 42f
                + fitCount * 460f));
            int comebackNoProgressPenalty = nonEssentialAssist > 0.25f
                && clearOpportunities == 0
                && setupOpportunities < 85
                && currentSetupPayoffOpportunities == 0
                ? Mathf.RoundToInt(nonEssentialAssist * 1400f)
                : 0;
            int tightSurvivalBonus = Mathf.RoundToInt(occupancyPressure01 * (
                clearOpportunities * 780f
                + setupOpportunities * 18f
                + currentSetupPayoffOpportunities * 18f
                + fitCount * 620f));
            int tightNoProgressPenalty = occupancyPressure01 > 0.52f
                && clearOpportunities == 0
                && setupOpportunities < 70
                && currentSetupPayoffOpportunities == 0
                ? Mathf.RoundToInt(occupancyPressure01 * 950f)
                : 0;
            int openClearSaturationPenalty = occupancyState == OccupancyState.Open
                && configuration.openImmediateClearScoreEnabled
                ? Mathf.Max(0, clearOpportunities - 1) * configuration.openClearSaturationPenalty
                : 0;
            int pressureTinyPenalty = Mathf.RoundToInt(runPressure01 * smallPieceCount * 760f);
            int pressureMediumBonus = Mathf.RoundToInt(runPressure01 * mediumPieceCount * 280f);
            int pressurePerfectTrayPenalty = Mathf.RoundToInt(runPressure01 * Mathf.Max(0, clearOpportunities - 1) * 190f);
            int reliefLoopPenalty = consecutiveReliefBiasedTrays >= 2 && occupancyState != OccupancyState.Critical
                ? Mathf.RoundToInt((clearOpportunities * 260f + currentSetupPayoffOpportunities * 12f) * 0.55f)
                : 0;
            int earlyMidRestrictionPenalty = 0;
            if (runPressure01 < 0.58f)
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

            return new ScoreTerms
            {
                fitFairness = fitCount * 12000
                    + placementOptions * mobilityWeight
                    + allFitBonus
                    + multiFitBonus,
                connectivity = adjacencyBonus,
                lineProgress = lineProgressBonus,
                immediateClear = clearBonus - openClearSaturationPenalty,
                setupPayoff = setupBonus + setupPayoffBonus + openPureSetupDiversityBonus + earlyBuildFlexBonus,
                cleanliness = cleanlinessBonus,
                rescueRelief = comebackBonus
                    + tightSurvivalBonus
                    - comebackNoProgressPenalty
                    - tightNoProgressPenalty
                    - reliefLoopPenalty,
                pressure = pressureMediumBonus - pressureTinyPenalty - pressurePerfectTrayPenalty,
                pieceSize = targetScore
                    + (configuration.satisfactionCurationEnabled ? phase8CurationAfterGate : satisfyingBonus + shapeMixBonus)
                    - earlyLargePenalty
                    - tightLargePenalty
                    - miniPiecePenalty,
                other = -duplicatePenalty - earlyMidRestrictionPenalty,
                phase8CurationBeforeGate = phase8CurationBeforeGate,
                phase8CurationAfterGate = phase8CurationAfterGate
            };
        }

        private static int ApplyImmediateClearCurationGate(
            int phase8CurationScore,
            int clearOpportunities,
            OccupancyState occupancyState,
            SimulationConfiguration configuration)
        {
            if (!configuration.immediateClearCurationGateEnabled
                || clearOpportunities <= 0
                || phase8CurationScore <= 0)
            {
                return phase8CurationScore;
            }

            // This is deliberately not a clear penalty. It only prevents optional
            // Light Curation from amplifying an already-rewarded immediate clear.
            if (occupancyState == OccupancyState.Open)
            {
                return Mathf.RoundToInt(phase8CurationScore * 0.20f);
            }

            return occupancyState == OccupancyState.Balanced
                ? Mathf.RoundToInt(phase8CurationScore * 0.60f)
                : phase8CurationScore;
        }

        // Editor-side literal scoring mirror of PieceSpawner's Phase 8 curation
        // layer. The live generator uses reusable fixed buffers; this analysis
        // version only measures its selection consequences.
        private static TrayCurationAnalysis AnalyzeTrayCuration(
            GenerationContext context,
            PieceInstance[] set,
            bool includeFullSequence,
            SimulationConfiguration configuration,
            float runPressure01,
            float phase8CurationMultiplier)
        {
            TrayCurationAnalysis analysis = default;
            if (context == null || set == null)
            {
                return analysis;
            }

            OccupancyState state = GetOccupancyState(context.board.OccupiedCount);
            int healthyPlacementTarget = GetHealthyPlacementTarget(state);
            int threeCellPieces = 0;
            int fourCellPieces = 0;
            int tinyPieces = 0;
            int fittingPieces = 0;
            int lowestPlacementOptions = int.MaxValue;
            float pressure01 = GetOccupancyPressure(state);
            float flexibilityMultiplier = configuration.flexibilityBonusMultiplier * phase8CurationMultiplier;
            float satisfactionMultiplier = phase8CurationMultiplier;

            for (int i = 0; i < set.Length; i++)
            {
                PieceInstance piece = set[i];
                if (piece == null)
                {
                    continue;
                }

                PlacementProfile placementProfile = context.GetProfile(piece);
                int placementOptions = placementProfile.placementOptions;
                PieceErgonomicProfile ergonomic = PieceCatalog.GetErgonomicProfile(piece.Data);
                int classBias;
                switch (ergonomic.satisfactionClass)
                {
                    case PieceSatisfactionClass.A:
                        classBias = Mathf.RoundToInt(260f * configuration.shapeSatisfactionMultiplier);
                        analysis.highSatisfactionPieces++;
                        break;
                    case PieceSatisfactionClass.B:
                        classBias = Mathf.RoundToInt(120f * configuration.shapeSatisfactionMultiplier);
                        analysis.highSatisfactionPieces++;
                        break;
                    case PieceSatisfactionClass.C:
                        classBias = configuration.pressureAwareCuration
                            ? -Mathf.RoundToInt(Mathf.Lerp(80f, 0f, GetCClassAllowance01(runPressure01)))
                            : -80;
                        classBias = Mathf.RoundToInt(classBias * configuration.cClassPenaltyMultiplier);
                        break;
                    default:
                        bool hasMeaningfulFits = placementOptions >= GetDClassMeaningfulFitTarget(runPressure01);
                        float dPenalty = configuration.pressureAwareCuration
                            ? GetDClassPenalty(runPressure01, hasMeaningfulFits)
                            : Mathf.Lerp(760f, 250f, pressure01);
                        classBias = -Mathf.RoundToInt(dPenalty * configuration.dClassPenaltyMultiplier);
                        analysis.awkwardPieces++;
                        break;
                }
                analysis.satisfactionScore += Mathf.RoundToInt(
                    ergonomic.satisfactionScore * 6f
                    * configuration.shapeSatisfactionMultiplier
                    * satisfactionMultiplier);
                analysis.satisfactionScore += Mathf.RoundToInt(classBias * satisfactionMultiplier);

                int cells = piece.Data.cells.Length;
                if (cells <= 2)
                {
                    tinyPieces++;
                }
                else if (cells == 3)
                {
                    threeCellPieces++;
                }
                else if (cells == 4)
                {
                    fourCellPieces++;
                }
                else
                {
                    analysis.largePieces++;
                }

                if (placementOptions > 0)
                {
                    fittingPieces++;
                    lowestPlacementOptions = Mathf.Min(lowestPlacementOptions, placementOptions);
                }

                if (placementOptions >= healthyPlacementTarget)
                {
                    analysis.healthyFlexibilityPieces++;
                    analysis.flexibilityScore += Mathf.RoundToInt(220f * flexibilityMultiplier);
                }
                else if (placementOptions >= Mathf.Max(1, healthyPlacementTarget / 2))
                {
                    analysis.flexibilityScore += Mathf.RoundToInt(40f * flexibilityMultiplier);
                }
                else if (state != OccupancyState.Critical)
                {
                    analysis.flexibilityScore -= Mathf.RoundToInt(Mathf.Lerp(360f, 120f, pressure01));
                }
            }

            if (analysis.healthyFlexibilityPieces >= 2)
            {
                analysis.flexibilityScore += Mathf.RoundToInt(520f * flexibilityMultiplier);
            }
            else if (fittingPieces == GameConstants.TraySize && lowestPlacementOptions <= 2
                && state != OccupancyState.Critical)
            {
                analysis.flexibilityScore -= Mathf.RoundToInt(Mathf.Lerp(920f, 320f, pressure01));
            }

                analysis.compositionScore += GetSoftSizeCompositionScore(
                    state,
                    tinyPieces,
                    threeCellPieces,
                    fourCellPieces,
                    analysis.largePieces,
                    configuration,
                    phase8CurationMultiplier);
            if (analysis.awkwardPieces >= 2)
            {
                analysis.compositionScore -= Mathf.RoundToInt(Mathf.Lerp(1450f, 450f, pressure01));
            }
            if (analysis.largePieces >= 3)
            {
                float threeLargeMultiplier = GetThreeLargePenaltyMultiplier(configuration, runPressure01);
                analysis.compositionScore -= Mathf.RoundToInt(
                    Mathf.Lerp(2700f, 800f, pressure01) * threeLargeMultiplier);
            }
            else if (analysis.largePieces >= 2 && state == OccupancyState.Open)
            {
                analysis.compositionScore -= Mathf.RoundToInt(1200f * configuration.satisfyingLargePenaltyMultiplier);
            }

            SetupPayoffAnalysis setupPayoff = context.AnalyzeSetupPayoff(set);
            if (setupPayoff.pureScore > 0)
            {
                analysis.hasCoherentTwoPieceSequence = true;
                analysis.pairCoherenceScore += Mathf.RoundToInt(
                    620f * configuration.pureSetupCurationMultiplier * phase8CurationMultiplier);
            }
            else if (setupPayoff.currentScore > 0)
            {
                analysis.hasCoherentTwoPieceSequence = true;
                analysis.pairCoherenceScore += Mathf.RoundToInt(
                    (state == OccupancyState.Open ? 120f : 280f)
                    * configuration.trioCoherenceMultiplier
                    * phase8CurationMultiplier);
            }
            if (analysis.highSatisfactionPieces >= 2)
            {
                analysis.pairCoherenceScore += Mathf.RoundToInt(
                    440f * configuration.trioCoherenceMultiplier * phase8CurationMultiplier);
            }
            if (threeCellPieces + fourCellPieces >= 2)
            {
                analysis.pairCoherenceScore += Mathf.RoundToInt(
                    300f * configuration.trioCoherenceMultiplier * phase8CurationMultiplier);
            }

            if (includeFullSequence)
            {
                AnalyzeBestFullTraySequence(context, set, ref analysis);
                analysis.sequenceScore = Mathf.RoundToInt(
                    analysis.sequenceScore
                    * configuration.fullSequenceBonusMultiplier
                    * phase8CurationMultiplier);
            }

            return analysis;
        }

        // Phase 8B is intentionally simulator-only. These multipliers affect only
        // the optional curation score, never Phase 7H playability, Pure Setup,
        // the +2500 diversity reward, rescue, or the pressure curve itself.
        private static float GetPhase8CurationMultiplier(
            SimulationConfiguration configuration,
            float runPressure01,
            bool usePerfectCurationStreakBreaker)
        {
            if (!configuration.satisfactionCurationEnabled)
            {
                return 1f;
            }

            float multiplier = 1f;
            if (configuration.pressureAwareCuration)
            {
                if (runPressure01 > 0.55f)
                {
                    multiplier = configuration.pressureCurationLateMultiplier;
                }
                else if (runPressure01 > 0.35f)
                {
                    multiplier = configuration.pressureCurationMidHighMultiplier;
                }
                else if (runPressure01 > 0.15f)
                {
                    multiplier = configuration.pressureCurationMidLowMultiplier;
                }
            }

            return usePerfectCurationStreakBreaker
                ? multiplier * configuration.perfectCurationStreakBreakerMultiplier
                : multiplier;
        }

        private static float GetCClassAllowance01(float runPressure01)
        {
            if (runPressure01 <= 0.15f) return 0f;
            if (runPressure01 <= 0.35f) return 0.35f;
            if (runPressure01 <= 0.55f) return 0.70f;
            return 1f;
        }

        private static int GetDClassMeaningfulFitTarget(float runPressure01)
        {
            return runPressure01 > 0.55f ? 2 : runPressure01 > 0.35f ? 3 : 4;
        }

        private static float GetDClassPenalty(float runPressure01, bool hasMeaningfulFits)
        {
            // Keep stair5 rare: it becomes merely possible late when it has a
            // real choice set, not an automatic pressure-relief answer.
            float basePenalty = runPressure01 > 0.55f ? 360f : runPressure01 > 0.35f ? 560f : 760f;
            return hasMeaningfulFits ? basePenalty : basePenalty + 480f;
        }

        private static float GetThreeLargePenaltyMultiplier(
            SimulationConfiguration configuration,
            float runPressure01)
        {
            float multiplier = configuration.threeLargePenaltyMultiplier;
            if (!configuration.pressureAwareCuration)
            {
                return multiplier;
            }

            // Early remains strongly guarded; later the penalty eases without
            // making three large shapes a default outcome.
            return multiplier * (runPressure01 > 0.55f ? 0.65f : runPressure01 > 0.35f ? 0.85f : 1.15f);
        }

        private static bool IsPerfectlyCuratedTray(TrayGenerationResult result)
        {
            return result.pureSetupOpportunities > 0
                && result.curation.healthyFlexibilityPieces >= 2
                && result.curation.hasCoherentFullSequence
                && result.curation.awkwardPieces == 0;
        }

        private static int GetHealthyPlacementTarget(OccupancyState state)
        {
            switch (state)
            {
                case OccupancyState.Balanced:
                    return 6;
                case OccupancyState.Pressured:
                    return 3;
                case OccupancyState.Critical:
                    return 1;
                default:
                    return 12;
            }
        }

        private static int GetSoftSizeCompositionScore(
            OccupancyState state,
            int tinyPieces,
            int threeCellPieces,
            int fourCellPieces,
            int largePieces,
            SimulationConfiguration configuration,
            float phase8CurationMultiplier)
        {
            int score = 0;
            switch (state)
            {
                case OccupancyState.Open:
                    score += Mathf.RoundToInt(threeCellPieces * 560f * phase8CurationMultiplier);
                    score += Mathf.RoundToInt(fourCellPieces * 700f * configuration.fourCellPreferenceMultiplier * phase8CurationMultiplier);
                    score += largePieces * 160;
                    score -= Mathf.RoundToInt(Mathf.Max(0, largePieces - 1) * 1280f * configuration.satisfyingLargePenaltyMultiplier);
                    if (threeCellPieces + fourCellPieces == 0) score -= 700;
                    if (tinyPieces >= 2) score -= 520;
                    break;
                case OccupancyState.Balanced:
                    score += Mathf.RoundToInt(threeCellPieces * 440f * phase8CurationMultiplier);
                    score += Mathf.RoundToInt(fourCellPieces * 540f * configuration.fourCellPreferenceMultiplier * phase8CurationMultiplier);
                    score += largePieces * 80;
                    score -= Mathf.RoundToInt(Mathf.Max(0, largePieces - 1) * 620f * configuration.satisfyingLargePenaltyMultiplier);
                    if (tinyPieces >= 2) score -= 260;
                    break;
                case OccupancyState.Pressured:
                    score += Mathf.RoundToInt(threeCellPieces * 360f * phase8CurationMultiplier);
                    score += Mathf.RoundToInt(fourCellPieces * 430f * configuration.fourCellPreferenceMultiplier * phase8CurationMultiplier);
                    score += largePieces * 20;
                    score -= Mathf.RoundToInt(Mathf.Max(0, largePieces - 1) * 250f * configuration.satisfyingLargePenaltyMultiplier);
                    break;
                default:
                    score += Mathf.RoundToInt(threeCellPieces * 100f * phase8CurationMultiplier)
                        + Mathf.RoundToInt(fourCellPieces * 120f * configuration.fourCellPreferenceMultiplier * phase8CurationMultiplier);
                    break;
            }

            return score;
        }

        private static void AnalyzeBestFullTraySequence(
            GenerationContext context,
            PieceInstance[] set,
            ref TrayCurationAnalysis analysis)
        {
            int bestScore = 0;
            int bestThirdOptions = 0;
            int bestLines = 0;
            for (int first = 0; first < set.Length; first++)
            {
                PieceInstance firstPiece = set[first];
                if (firstPiece == null)
                {
                    continue;
                }

                PlacementProfile firstProfile = context.GetProfile(firstPiece);
                if (!firstProfile.hasSetupOrigin)
                {
                    continue;
                }

                // Mirror PieceSpawner: three cyclic orders catch a real
                // play-through while avoiding exhaustive six-order solving.
                int second = (first + 1) % set.Length;
                int third = (first + 2) % set.Length;
                if (set[second] == null || set[third] == null)
                {
                    continue;
                }

                TrioSequenceProfile sequence = context.GetTrioSequence(
                    firstPiece,
                    firstProfile.bestSetupX,
                    firstProfile.bestSetupY,
                    set[second],
                    set[third]);
                if (!sequence.hasCoherentSequence)
                {
                    continue;
                }

                int sequenceScore = sequence.score + sequence.thirdPlacementOptions * 18 + sequence.completedLines * 240;
                if (sequenceScore > bestScore)
                {
                    bestScore = sequenceScore;
                    bestThirdOptions = sequence.thirdPlacementOptions;
                    bestLines = sequence.completedLines;
                }
            }

            if (bestScore <= 0)
            {
                return;
            }

            analysis.hasCoherentFullSequence = bestThirdOptions >= 2;
            analysis.sequenceScore += Mathf.Min(1500, bestScore);
            if (bestThirdOptions >= GetHealthyPlacementTarget(GetOccupancyState(context.board.OccupiedCount)))
            {
                analysis.sequenceScore += 360;
            }
            if (bestLines > 0)
            {
                analysis.sequenceScore += 280;
            }
        }

        private static void ImproveSetWithRescuePieces(GenerationContext context, PieceInstance[] set, System.Random random, float difficulty01, float assist01)
        {
            int emptyCells = context.board.EmptyCount;
            int targetFitCount = emptyCells >= 14 && difficulty01 < 0.80f ? 3 : emptyCells >= 6 ? 2 : 1;
            if (assist01 > 0.35f && emptyCells >= 10)
            {
                targetFitCount = 3;
            }

            int guard = 0;
            while (CountFittingPieces(context, set) < targetFitCount && guard < GameConstants.TraySize)
            {
                guard++;
                int replaceIndex = FindWeakestPieceIndex(context, set);
                if (replaceIndex < 0)
                {
                    return;
                }

                PieceInstance rescue = FindBestRescuePiece(context, random);
                if (rescue == null)
                {
                    return;
                }

                set[replaceIndex] = rescue;
            }
        }

        private static void EnsureSatisfyingPiece(GenerationContext context, PieceInstance[] set, System.Random random, float difficulty01, float assist01)
        {
            int emptyCells = context.board.EmptyCount;
            if (emptyCells < 18 || (difficulty01 > 0.92f && assist01 < 0.45f))
            {
                return;
            }

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null && set[i].Data.cells.Length >= 4 && CanAnyPieceFit(context, set[i]))
                {
                    return;
                }
            }

            PieceInstance largePiece = FindBestLargeFittingPiece(context, random);
            if (largePiece == null)
            {
                return;
            }

            int replaceIndex = FindSmallestFittingPieceIndex(context, set);
            if (replaceIndex < 0)
            {
                replaceIndex = FindWeakestPieceIndex(context, set);
            }

            if (replaceIndex >= 0)
            {
                set[replaceIndex] = largePiece;
            }
        }

        private static void EnsureComebackPiece(GenerationContext context, PieceInstance[] set, System.Random random, float assist01)
        {
            if (assist01 < 0.22f)
            {
                return;
            }

            int bestCurrentProgress = 0;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null || !CanAnyPieceFit(context, set[i]))
                {
                    continue;
                }

                PlacementProfile profile = context.GetProfile(set[i]);
                bestCurrentProgress = Mathf.Max(bestCurrentProgress, profile.clearOpportunities * 1900 + profile.bestSetupScore);
            }

            int threshold = assist01 > 0.70f ? 450 : 1050;
            if (bestCurrentProgress >= threshold)
            {
                return;
            }

            PieceInstance comeback = FindBestComebackPiece(context, random, assist01);
            int replaceIndex = FindWeakestPieceIndex(context, set);
            if (comeback != null && replaceIndex >= 0)
            {
                set[replaceIndex] = comeback;
            }
        }

        private static void EnsureImmediateClearPiece(GenerationContext context, PieceInstance[] set, System.Random random, float assist01)
        {
            if (assist01 < 0.46f)
            {
                return;
            }

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null && context.GetProfile(set[i]).clearOpportunities > 0)
                {
                    return;
                }
            }

            PieceInstance clearingPiece = FindBestImmediateClearPiece(context, random, assist01);
            int replaceIndex = FindWeakestPieceIndex(context, set);
            if (clearingPiece != null && replaceIndex >= 0)
            {
                set[replaceIndex] = clearingPiece;
            }
        }

        private static void EnsureJuicySetMass(GenerationContext context, PieceInstance[] set, System.Random random, float difficulty01)
        {
            int emptyCells = context.board.EmptyCount;
            if (emptyCells < 18 || set == null)
            {
                return;
            }

            int targetTotalCells = emptyCells >= 34 ? 13 : emptyCells >= 26 ? 12 : 10;
            if (difficulty01 < 0.18f)
            {
                targetTotalCells--;
            }

            int guard = 0;
            while (TotalPieceCells(set) < targetTotalCells && guard < 1)
            {
                guard++;
                int replaceIndex = FindSmallestNonClearingPieceIndex(context, set);
                if (replaceIndex < 0)
                {
                    return;
                }

                int currentCells = set[replaceIndex] == null ? 0 : set[replaceIndex].Data.cells.Length;
                PieceInstance largerPiece = FindBestFittingPieceInRange(context, random, 3, 5);
                if (largerPiece == null || largerPiece.Data.cells.Length <= currentCells)
                {
                    return;
                }

                set[replaceIndex] = largerPiece;
            }
        }

        private static void EnsureSatisfyingSetShapeMix(GenerationContext context, PieceInstance[] set, System.Random random, float difficulty01, float assist01)
        {
            int emptyCells = context.board.EmptyCount;
            if (set == null || emptyCells < 16)
            {
                return;
            }

            int smallPieces = CountPiecesAtMost(set, 3);
            int mediumPieces = CountPiecesBetween(set, 3, 4);
            if (smallPieces < 2 || mediumPieces > 0)
            {
                return;
            }

            int replaceIndex = FindSmallestNonClearingPieceIndex(context, set);
            if (replaceIndex < 0)
            {
                replaceIndex = FindSmallestFittingPieceIndex(context, set);
            }

            if (replaceIndex < 0)
            {
                return;
            }

            PieceInstance connector = FindBestFittingPieceInRange(context, random, 3, 4);
            if (connector != null)
            {
                set[replaceIndex] = connector;
            }
        }

        // Phase 8 mirror of PieceSpawner's lightweight post-selection guard. It
        // preserves the existing fairness passes while avoiding their old second
        // large-piece push on open boards.
        private static void EnsureCuratedJuicySetMass(GenerationContext context, PieceInstance[] set, System.Random random, float difficulty01)
        {
            int emptyCells = context.board.EmptyCount;
            if (emptyCells < 18 || set == null)
            {
                return;
            }

            int targetTotalCells = emptyCells >= 34 ? 13 : emptyCells >= 26 ? 12 : 10;
            if (difficulty01 < 0.18f)
            {
                targetTotalCells--;
            }

            if (TotalPieceCells(set) >= targetTotalCells)
            {
                return;
            }

            int replaceIndex = FindSmallestNonClearingPieceIndex(context, set);
            if (replaceIndex < 0)
            {
                return;
            }

            int currentCells = set[replaceIndex] == null ? 0 : set[replaceIndex].Data.cells.Length;
            PieceInstance replacement = FindBestFittingPieceInRange(context, random, 3, 5);
            if (replacement != null && replacement.Data.cells.Length > currentCells)
            {
                set[replaceIndex] = replacement;
            }
        }

        private static void EnsureCuratedSatisfyingSetShapeMix(GenerationContext context, PieceInstance[] set, System.Random random)
        {
            if (set == null || context.board.EmptyCount < 16)
            {
                return;
            }

            int smallPieces = CountPiecesAtMost(set, 3);
            int mediumPieces = CountPiecesBetween(set, 3, 4);
            if (smallPieces < 2 || mediumPieces > 0)
            {
                return;
            }

            int replaceIndex = FindSmallestNonClearingPieceIndex(context, set);
            if (replaceIndex < 0)
            {
                replaceIndex = FindSmallestFittingPieceIndex(context, set);
            }

            PieceInstance connector = replaceIndex < 0
                ? null
                : FindBestFittingPieceInRange(context, random, 3, 4);
            if (connector != null)
            {
                set[replaceIndex] = connector;
            }
        }

        private static PieceInstance FindBestRescuePiece(GenerationContext context, System.Random random)
        {
            int emptyCells = context.board.EmptyCount;
            string[] rescueIds = emptyCells >= 18
                ? new[] { "line5_h", "line5_v", "square3", "rect2x3", "rect3x2", "line4_h", "line4_v", "square2", "l4", "l4_m", "l4_r", "l4_rm", "t4", "t4_v", "s4", "z4", "line3_h", "line3_v" }
                : emptyCells >= 10
                    ? new[] { "line4_h", "line4_v", "square2", "l4", "l4_m", "t4", "line3_h", "line3_v", "corner3", "corner3_m" }
                    : new[] { "single", "line2_h", "line2_v", "corner3", "corner3_m", "line3_h", "line3_v", "square2" };
            PieceInstance best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < rescueIds.Length; i++)
            {
                PieceInstance candidate = new PieceInstance(rescueIds[i], (ChromaColor)random.Next(GameConstants.ColorCount));
                PlacementProfile profile = context.GetProfile(candidate);
                if (profile.placementOptions <= 0)
                {
                    continue;
                }

                int cells = candidate.Data.cells.Length;
                int score = profile.placementOptions * (emptyCells >= 18 ? 70 : 100)
                    + profile.clearOpportunities * 950
                    + profile.bestSetupScore * 42
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

        private static PieceInstance FindBestLargeFittingPiece(GenerationContext context, System.Random random)
        {
            int emptyCells = context.board.EmptyCount;
            PieceInstance best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < LargeCandidateIds.Length; i++)
            {
                PieceInstance candidate = new PieceInstance(LargeCandidateIds[i], (ChromaColor)random.Next(GameConstants.ColorCount));
                PlacementProfile profile = context.GetProfile(candidate);
                if (profile.placementOptions <= 0)
                {
                    continue;
                }

                int cells = candidate.Data.cells.Length;
                int score = profile.placementOptions * 72
                    + profile.clearOpportunities * 1000
                    + profile.bestSetupScore * 45
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

        private static PieceInstance FindBestFittingPieceInRange(
            GenerationContext context,
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

                PieceInstance candidate = new PieceInstance(data.id, (ChromaColor)random.Next(GameConstants.ColorCount));
                PlacementProfile profile = context.GetProfile(candidate);
                if (profile.placementOptions <= 0)
                {
                    continue;
                }

                int score = profile.placementOptions * 74
                    + profile.clearOpportunities * 920
                    + profile.bestSetupScore * 38
                    + random.Next(16);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private static PieceInstance FindBestComebackPiece(GenerationContext context, System.Random random, float assist01)
        {
            int emptyCells = context.board.EmptyCount;
            PieceInstance best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < PieceCatalog.All.Count; i++)
            {
                PieceData data = PieceCatalog.All[i];
                if (data.id == "plus5" || data.id == "stair5")
                {
                    continue;
                }

                PieceInstance candidate = new PieceInstance(data.id, (ChromaColor)random.Next(GameConstants.ColorCount));
                PlacementProfile profile = context.GetProfile(candidate);
                if (profile.placementOptions <= 0)
                {
                    continue;
                }

                int cells = data.cells.Length;
                int score = profile.clearOpportunities * Mathf.RoundToInt(Mathf.Lerp(1900f, 3600f, assist01))
                    + profile.bestSetupScore * Mathf.RoundToInt(Mathf.Lerp(38f, 72f, assist01))
                    + profile.placementOptions * (emptyCells < 16 ? 105 : 68)
                    + cells * (emptyCells >= 18 ? 125 : 44)
                    + random.Next(22);
                if (profile.clearOpportunities == 0 && profile.bestSetupScore < 50)
                {
                    score -= Mathf.RoundToInt(assist01 * 900f);
                }

                if (assist01 > 0.65f && profile.clearOpportunities > 0)
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

        private static PieceInstance FindBestImmediateClearPiece(GenerationContext context, System.Random random, float assist01)
        {
            int emptyCells = context.board.EmptyCount;
            PieceInstance best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < PieceCatalog.All.Count; i++)
            {
                PieceData data = PieceCatalog.All[i];
                if (data.id == "plus5" || data.id == "stair5")
                {
                    continue;
                }

                PieceInstance candidate = new PieceInstance(data.id, (ChromaColor)random.Next(GameConstants.ColorCount));
                PlacementProfile profile = context.GetProfile(candidate);
                if (profile.clearOpportunities <= 0)
                {
                    continue;
                }

                int cells = data.cells.Length;
                int score = profile.clearOpportunities * Mathf.RoundToInt(Mathf.Lerp(5200f, 7600f, assist01))
                    + profile.placementOptions * (emptyCells < 16 ? 75 : 48)
                    + profile.bestSetupScore * 16
                    + cells * (emptyCells >= 20 ? 120 : 42)
                    + random.Next(24);
                if (profile.clearOpportunities >= 2)
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

        private static bool TryChoosePlacement(
            HeadlessBoard board,
            PieceInstance[] tray,
            System.Random random,
            SimulatedPlayerPolicy playerPolicy,
            out PlacementDecision bestDecision)
        {
            bestDecision = default;
            int bestScore = int.MinValue;
            for (int trayIndex = 0; trayIndex < tray.Length; trayIndex++)
            {
                PieceInstance piece = tray[trayIndex];
                if (piece == null)
                {
                    continue;
                }

                PieceData data = piece.Data;
                for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
                {
                    for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                    {
                        if (!board.CanPlace(piece, x, y))
                        {
                            continue;
                        }

                        PlacementEvaluation evaluation = board.EvaluatePlacement(piece, x, y);
                        int setupScore = playerPolicy == SimulatedPlayerPolicy.BalancedHuman
                            ? board.ScorePlacementSetup(piece, x, y)
                            : 0;
                        int score = GetPlayerPlacementScore(evaluation, setupScore, playerPolicy);

                        // One bounded, non-solving lookahead keeps order decisions human-like.
                        if (evaluation.lines == 0 && tray.Length > 1)
                        {
                            HeadlessBoard projected = board.CloneAfterPlacement(piece, x, y);
                            int followUp = FindBestFollowUpScore(projected, tray, trayIndex);
                            score += Mathf.RoundToInt(followUp * 0.30f);
                        }

                        score += random.Next(0, 1200);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestDecision = new PlacementDecision
                            {
                                trayIndex = trayIndex,
                                piece = piece,
                                x = x,
                                y = y,
                                adjacencyContacts = evaluation.adjacencyContacts
                            };
                        }
                    }
                }
            }

            return bestScore != int.MinValue;
        }

        private static int GetPlayerPlacementScore(
            PlacementEvaluation evaluation,
            int setupScore,
            SimulatedPlayerPolicy playerPolicy)
        {
            if (playerPolicy != SimulatedPlayerPolicy.BalancedHuman)
            {
                return GetHumanChoiceScore(evaluation);
            }

            // Competent but not clear-obsessed: multi-lines first, then strong planned
            // payoffs and cleanliness, with a single-line clear intentionally below them.
            return Mathf.Max(0, evaluation.lines - 1) * 16000
                + setupScore * 120
                + evaluation.largestOpenArea * 30
                + evaluation.lineProgress * 85
                + (evaluation.lines == 1 ? 4200 : 0)
                + evaluation.adjacencyContacts * 35
                - evaluation.isolatedHoles * 290
                - evaluation.occupiedAfterClear * 16;
        }

        private static int FindBestFollowUpScore(HeadlessBoard board, PieceInstance[] tray, int usedTrayIndex)
        {
            int best = 0;
            for (int trayIndex = 0; trayIndex < tray.Length; trayIndex++)
            {
                if (trayIndex == usedTrayIndex || tray[trayIndex] == null)
                {
                    continue;
                }

                PieceInstance piece = tray[trayIndex];
                PieceData data = piece.Data;
                for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
                {
                    for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                    {
                        if (!board.CanPlace(piece, x, y))
                        {
                            continue;
                        }

                        PlacementEvaluation evaluation = board.EvaluatePlacement(piece, x, y);
                        int score = evaluation.lines * 5000
                            + evaluation.lineProgress * 48
                            + evaluation.adjacencyContacts * 20
                            - evaluation.isolatedHoles * 120
                            - evaluation.occupiedAfterClear * 6;
                        best = Mathf.Max(best, score);
                    }
                }
            }

            return best;
        }

        private static bool TryUsePop(
            HeadlessBoard board,
            PieceInstance[] tray,
            int[] chroma,
            int popUses,
            bool fatigueEnabled,
            System.Random random,
            out ChromaColor selectedColor,
            out int popped)
        {
            selectedColor = ChromaColor.Cyan;
            popped = 0;
            int requirement = GetPopRequirement(popUses, fatigueEnabled);
            int occupied = board.OccupiedCount;
            bool strongNonPopRecovery = HasStrongNonPopRecovery(board, tray);
            bool allowPop = occupied >= 50
                || (occupied >= 40 && (!strongNonPopRecovery || random.NextDouble() < 0.32d))
                || (occupied >= 34 && !strongNonPopRecovery);
            if (!allowPop)
            {
                return false;
            }

            int bestCount = 0;
            for (int color = 0; color < GameConstants.ColorCount; color++)
            {
                if (chroma[color] < requirement)
                {
                    continue;
                }

                int count = board.CountColor((ChromaColor)color);
                if (count > bestCount)
                {
                    bestCount = count;
                    selectedColor = (ChromaColor)color;
                }
            }

            if (bestCount <= 0)
            {
                return false;
            }

            popped = board.PopColor(selectedColor);
            return popped > 0;
        }

        private static bool HasStrongNonPopRecovery(HeadlessBoard board, PieceInstance[] tray)
        {
            int bestLines = 0;
            for (int i = 0; i < tray.Length; i++)
            {
                PieceInstance piece = tray[i];
                if (piece == null)
                {
                    continue;
                }

                PieceData data = piece.Data;
                for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
                {
                    for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                    {
                        if (board.CanPlace(piece, x, y))
                        {
                            bestLines = Mathf.Max(bestLines, board.EvaluatePlacement(piece, x, y).lines);
                        }
                    }
                }
            }

            return bestLines >= 2;
        }

        private static bool HasAnyFit(HeadlessBoard board, PieceInstance[] tray)
        {
            for (int i = 0; i < tray.Length; i++)
            {
                if (tray[i] != null && board.CountPlacementOptions(tray[i]) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemovePiece(PieceInstance[] tray, int index)
        {
            if (index >= 0 && index < tray.Length)
            {
                tray[index] = null;
            }
        }

        private static bool IsTrayEmpty(PieceInstance[] tray)
        {
            for (int i = 0; i < tray.Length; i++)
            {
                if (tray[i] != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void RecordTray(RunMetrics metrics, HeadlessBoard board, TrayGenerationResult? result, int consecutiveReliefTrays)
        {
            metrics.traySamples++;
            switch (GetOccupancyState(board.OccupiedCount))
            {
                case OccupancyState.Open:
                    metrics.openTrays++;
                    break;
                case OccupancyState.Balanced:
                    metrics.balancedTrays++;
                    break;
                case OccupancyState.Pressured:
                    metrics.pressuredTrays++;
                    break;
                default:
                    metrics.criticalTrays++;
                    break;
            }

            metrics.maxConsecutiveReliefTrays = Math.Max(metrics.maxConsecutiveReliefTrays, consecutiveReliefTrays);
            if (!result.HasValue)
            {
                return;
            }

            TrayGenerationResult generation = result.Value;
            if (generation.immediateClearOpportunities > 0)
            {
                metrics.immediateClearTrays++;
            }

            metrics.totalImmediateClearOpportunities += generation.immediateClearOpportunities;

            if (generation.setupPayoffOpportunities > 0)
            {
                metrics.setupPayoffTrays++;
                if (generation.immediateClearOpportunities == 0)
                {
                    metrics.setupWithoutImmediateClearTrays++;
                }
            }

            if (generation.pureSetupOpportunities > 0)
            {
                metrics.pureSetupTrays++;
                if (generation.immediateClearOpportunities == 0)
                {
                    metrics.pureSetupWithoutImmediateClearTrays++;
                }
            }

            if (generation.openDiversityBonusApplied)
            {
                metrics.openDiversityBonusTrays++;
            }

            if (generation.immediateClearSetupOverlap)
            {
                metrics.immediateClearSetupOverlapTrays++;
                metrics.setupScoreWithImmediateTotal += generation.setupPayoffOpportunities;
                metrics.setupScoreWithImmediateTrays++;
                metrics.immediateOpportunitiesOnSetupTrays += generation.immediateClearOpportunities;
            }
            else if (generation.setupPayoffOpportunities > 0)
            {
                metrics.setupScoreWithoutImmediateTotal += generation.setupPayoffOpportunities;
                metrics.setupScoreWithoutImmediateTrays++;
            }

            metrics.setupClassifications[(int)generation.setupClassification]++;
            metrics.selectionReasons[(int)generation.selectionReason]++;
            if (generation.occupancyState == OccupancyState.Open && metrics.openExamples.Count < 5)
            {
                metrics.openExamples.Add(BuildOpenTrayExample(generation));
            }

            if (generation.occupancyState == OccupancyState.Open
                && (generation.lightCurationScoreBeforeGate != 0 || generation.lightCurationScoreAfterGate != 0))
            {
                List<string> lightExamples = generation.immediateClearOpportunities > 0
                    ? metrics.openImmediateLightCurationExamples
                    : metrics.openNoImmediateLightCurationExamples;
                if (lightExamples.Count < 5)
                {
                    lightExamples.Add(BuildOpenTrayExample(generation));
                }
            }

            if ((generation.occupancyState == OccupancyState.Open || generation.occupancyState == OccupancyState.Balanced)
                && metrics.curatedExamples.Count < 10)
            {
                metrics.curatedExamples.Add(BuildOpenTrayExample(generation));
            }

            if (generation.occupancyState == OccupancyState.Open
                && generation.pureSetupOpportunities > 0
                && metrics.pureOpenExamples.Count < 5)
            {
                metrics.pureOpenExamples.Add(BuildOpenTrayExample(generation));
            }

            if (generation.reliefBiased)
            {
                metrics.reliefBiasedTrays++;
            }

            if (generation.lightCurationScoreBeforeGate != 0 || generation.lightCurationScoreAfterGate != 0)
            {
                if (generation.curationChangedRanking)
                {
                    metrics.curationChangedRankingTrays++;
                    if (generation.immediateClearOpportunities > 0)
                    {
                        metrics.curationChangedImmediateClearTrays++;
                    }
                    else
                    {
                        metrics.curationChangedNoImmediateClearTrays++;
                    }
                }
                else
                {
                    metrics.phase7HPrimaryWinnerTrays++;
                }
            }

            int curatedTrayNumber = ++metrics.generatedCurationTraySamples;
            int curationStage = GetCurationStage(curatedTrayNumber);
            metrics.curationStages[curationStage].Add(generation);
            if (metrics.curationStageExamples[curationStage].Count < 5)
            {
                metrics.curationStageExamples[curationStage].Add(BuildOpenTrayExample(generation));
            }

            RecordSelectedPieceStats(metrics.selectedPieces, board, generation.pieces, curatedTrayNumber);
            metrics.trayQuality.Add(generation.curation, generation.pieces);
            metrics.selectedScoreTerms.Add(generation.scoreTerms);
        }

        private static int GetCurationStage(int generatedTrayNumber)
        {
            return generatedTrayNumber <= 5 ? 0 : generatedTrayNumber <= 15 ? 1 : 2;
        }

        private static string BuildOpenTrayExample(TrayGenerationResult generation)
        {
            StringBuilder pieces = new StringBuilder();
            int fittingPieces = 0;
            int largePieces = 0;
            bool hasAwkwardWithoutPurpose = false;
            for (int i = 0; i < generation.pieces.Length; i++)
            {
                if (i > 0)
                {
                    pieces.Append(", ");
                }

                PieceInstance piece = generation.pieces[i];
                pieces.Append(piece.shapeId);
                pieces.Append('[');
                pieces.Append(piece.Data.cells.Length);
                pieces.Append('/');
                pieces.Append(PieceCatalog.GetErgonomicProfile(piece.Data).satisfactionClass);
                pieces.Append('/');
                pieces.Append(generation.piecePlacementOptions[i]);
                pieces.Append(']');
                if (generation.piecePlacementOptions[i] > 0)
                {
                    fittingPieces++;
                }
                if (piece.Data.cells.Length >= 5)
                {
                    largePieces++;
                }
                if (PieceCatalog.GetErgonomicProfile(piece.Data).satisfactionClass == PieceSatisfactionClass.D
                    && generation.piecePlacementOptions[i] < 2
                    && generation.pureSetupOpportunities == 0
                    && generation.immediateClearOpportunities == 0)
                {
                    hasAwkwardWithoutPurpose = true;
                }
            }

            string immediate = generation.immediateClearOpportunities > 0 ? "YES" : "NO";
            string pure = generation.pureSetupOpportunities > 0 ? "YES" : "NO";
            string diversity = generation.openDiversityBonusApplied ? "YES" : "NO";
            string concept = generation.pureSetupOpportunities > 0
                ? generation.pureSetupDescription
                : generation.immediateClearOpportunities > 0
                    ? "an immediate-clear placement is already available"
                    : "general future line progress only";
            string coherence = generation.curation.hasCoherentFullSequence
                ? "coherent full A->B->C sequence"
                : generation.curation.hasCoherentTwoPieceSequence
                    ? "coherent two-piece setup"
                    : "multiple independent fallback placements";
            string risk = fittingPieces < GameConstants.TraySize
                ? "risk flag: a piece has no valid placement"
                : largePieces >= 3 && generation.curation.healthyFlexibilityPieces < 2
                    ? "risk flag: three restrictive large pieces"
                    : hasAwkwardWithoutPurpose
                        ? "risk flag: awkward shape lacks a clear purpose"
                        : "risk flag: none (no 1-useful-plus-2-garbage pattern)";
            string curation = generation.lightCurationScoreBeforeGate != 0
                || generation.lightCurationScoreAfterGate != 0
                ? $"Light Curation {generation.lightCurationScoreBeforeGate}->{generation.lightCurationScoreAfterGate}"
                : "no Light Curation";
            return $"occ {generation.occupiedCells}; pieces {pieces}; immediate {immediate}; pure {pure}; diversity +2500 {diversity}; {curation}; {coherence}; won by {SelectionReasonLabel(generation.selectionReason)}; {concept}; {risk}.";
        }

        private static string SelectionReasonLabel(SelectionReason reason)
        {
            switch (reason)
            {
                case SelectionReason.SetupPayoff: return "setup/payoff";
                case SelectionReason.ImmediateClear: return "immediate clear";
                case SelectionReason.Connectivity: return "connectivity";
                case SelectionReason.LineProgress: return "line progress";
                case SelectionReason.Relief: return "relief";
                case SelectionReason.PieceSize: return "piece size";
                case SelectionReason.Other: return "other";
                default: return "fit/fairness";
            }
        }

        private static void RecordSelectedPieceStats(PieceStats stats, HeadlessBoard board, PieceInstance[] pieces, int trayNumber)
        {
            int stage = trayNumber <= 5 ? 0 : trayNumber <= 10 ? 1 : trayNumber <= 18 ? 2 : 3;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null)
                {
                    continue;
                }

                int sizeBucket = GetPieceSizeBucket(pieces[i].Data.cells.Length);
                bool hasImmediateClear = board.EvaluateGenerationProfile(pieces[i]).clearOpportunities > 0;
                stats.Add(stage, sizeBucket, hasImmediateClear, pieces[i].shapeId == "stair5");
            }
        }

        private static int GetPieceSizeBucket(int cells)
        {
            if (cells <= 1) return 0;
            if (cells == 2) return 1;
            if (cells == 3) return 2;
            if (cells == 4) return 3;
            return 4;
        }

        // Analysis-only estimate of how often a fresh tray offers more than one plausible
        // human choice. It deliberately uses the deterministic, pre-noise portion of the
        // casual-player placement score and does not alter the player's actual choice.
        private static void RecordTrayChoiceOptions(RunMetrics metrics, HeadlessBoard board, PieceInstance[] tray)
        {
            int bestScore = int.MinValue;
            for (int trayIndex = 0; trayIndex < tray.Length; trayIndex++)
            {
                PieceInstance piece = tray[trayIndex];
                if (piece == null)
                {
                    continue;
                }

                PieceData data = piece.Data;
                for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
                {
                    for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                    {
                        if (board.CanPlace(piece, x, y))
                        {
                            bestScore = Mathf.Max(bestScore, GetHumanChoiceScore(board.EvaluatePlacement(piece, x, y)));
                        }
                    }
                }
            }

            int reasonableOptions = 0;
            if (bestScore != int.MinValue)
            {
                const int reasonableScoreWindow = 1600;
                for (int trayIndex = 0; trayIndex < tray.Length; trayIndex++)
                {
                    PieceInstance piece = tray[trayIndex];
                    if (piece == null)
                    {
                        continue;
                    }

                    PieceData data = piece.Data;
                    for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
                    {
                        for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                        {
                            if (board.CanPlace(piece, x, y)
                                && GetHumanChoiceScore(board.EvaluatePlacement(piece, x, y)) >= bestScore - reasonableScoreWindow)
                            {
                                reasonableOptions++;
                            }
                        }
                    }
                }
            }

            metrics.choiceTraySamples++;
            metrics.totalReasonablePlacementOptions += reasonableOptions;
            if (reasonableOptions >= 2)
            {
                metrics.multipleReasonablePlacementTrays++;
            }
        }

        private static void RecordRawCandidate(
            RunMetrics metrics,
            GenerationContext context,
            PieceInstance[] set,
            int fitCount)
        {
            CandidatePoolStats stats = metrics.rawCandidateStats[(int)GetOccupancyState(context.board.OccupiedCount)];
            stats.candidateCount++;
            if (fitCount <= 0)
            {
                stats.noImmediateClearCandidates++;
                return;
            }

            int immediatePlacements = 0;
            int piecesWithImmediateClear = 0;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null)
                {
                    continue;
                }

                int opportunities = context.GetProfile(set[i]).clearOpportunities;
                immediatePlacements += opportunities;
                if (opportunities > 0)
                {
                    piecesWithImmediateClear++;
                }
            }

            if (immediatePlacements > 0)
            {
                stats.anyImmediateClearCandidates++;
            }
            else
            {
                stats.noImmediateClearCandidates++;
                if (context.GetSetupPayoffOpportunity(set) > 0)
                {
                    stats.setupWithoutImmediateClearCandidates++;
                }
            }

            if (immediatePlacements >= 2)
            {
                stats.multipleImmediatePlacementCandidates++;
            }

            if (piecesWithImmediateClear >= 2)
            {
                stats.multipleImmediatePieceCandidates++;
            }
        }

        private static int GetHumanChoiceScore(PlacementEvaluation evaluation)
        {
            return evaluation.lines * 10000
                + Mathf.Max(0, evaluation.lines - 1) * 3500
                + evaluation.lineProgress * 90
                + evaluation.adjacencyContacts * 45
                + evaluation.largestOpenArea * 12
                - evaluation.isolatedHoles * 230
                - evaluation.occupiedAfterClear * 12;
        }

        private static void AddChroma(int[] chroma, int color, int amount, int popUses, bool fatigueEnabled)
        {
            int requirement = GetPopRequirement(popUses, fatigueEnabled);
            chroma[color] = Mathf.Min(requirement, chroma[color] + amount);
        }

        private static int GetPopRequirement(int popUses, bool fatigueEnabled)
        {
            if (!fatigueEnabled)
            {
                return GameConstants.ChromaThreshold;
            }

            float multiplier = popUses switch
            {
                0 => 1f,
                1 => 1.35f,
                2 => 1.70f,
                3 => 2.10f,
                _ => 2.50f
            };
            return Mathf.CeilToInt(GameConstants.ChromaThreshold * multiplier);
        }

        private static float GetClassicDifficulty(int score)
        {
            return Mathf.Clamp01(0.10f + Mathf.Clamp01(score / 14000f) * 0.62f);
        }

        private static float GetPieceAssist(int movesSinceClear, int emptyCells)
        {
            float noClearAssist = movesSinceClear <= 1 ? 0f : Mathf.Clamp01((movesSinceClear - 1) / 3.5f);
            float tightBoardAssist = Mathf.Clamp01((24f - emptyCells) / 18f);
            return Mathf.Clamp01(noClearAssist * 0.80f + tightBoardAssist * 0.24f);
        }

        private static float GetClassicRunPressure(int trayNumber, int popUses)
        {
            float trayPressure;
            if (trayNumber <= 5)
            {
                trayPressure = 0f;
            }
            else if (trayNumber <= 10)
            {
                trayPressure = Mathf.Lerp(0.03f, 0.18f, (trayNumber - 6) / 4f);
            }
            else if (trayNumber <= 18)
            {
                trayPressure = Mathf.Lerp(0.22f, 0.58f, (trayNumber - 11) / 7f);
            }
            else
            {
                trayPressure = Mathf.Min(0.75f, 0.62f + (trayNumber - 19) * 0.025f);
            }

            return Mathf.Clamp01(trayPressure + Mathf.Min(0.12f, popUses * 0.03f));
        }

        private static float GetChainScoreMultiplier(int chain)
        {
            switch (Mathf.Max(1, chain))
            {
                case 1: return 1f;
                case 2: return 1.6f;
                case 3: return 2.4f;
                case 4: return 3.4f;
                case 5: return 4.6f;
                default: return 4.6f + (chain - 5) * 1.4f;
            }
        }

        private static int CalculateTrayCompleteBonus(ClearOutcome clear, int chain, int occupiedAfterClear)
        {
            int lineBonus = clear.lines * 24;
            int pureBonus = clear.pureLines * 32;
            int chainBonus = Mathf.Max(0, chain - 1) * 12;
            int emptySpaceBonus = Mathf.Clamp((BoardCellCount - occupiedAfterClear) / 5, 0, 12);
            return Mathf.Clamp(42 + lineBonus + pureBonus + chainBonus + emptySpaceBonus, 42, 180);
        }

        private static int CalculateLargePieceBonus(PieceInstance piece, ClearOutcome clear)
        {
            int cells = piece.Data.cells.Length;
            if (cells < 5)
            {
                return 0;
            }

            int bonus = (cells - 4) * 16;
            if (clear.lines <= 0)
            {
                bonus += 12;
            }

            return Mathf.Clamp(bonus, 0, 64);
        }

        private static int CalculateSetupMoveBonus(PieceInstance piece, ClearOutcome clear, int setupScore)
        {
            if (clear.lines > 0 || setupScore < 54)
            {
                return 0;
            }

            return Mathf.Clamp(18 + setupScore / 3 + piece.Data.cells.Length * 2, 24, 110);
        }

        private static int CalculateBoardSweepBonus(ClearOutcome clear, int occupiedAfterClear)
        {
            if (clear.lines <= 0)
            {
                return 0;
            }

            int emptyCells = BoardCellCount - occupiedAfterClear;
            if (emptyCells >= BoardCellCount)
            {
                return 420;
            }

            if (emptyCells >= 58 && clear.cellsCleared >= 12)
            {
                return 240;
            }

            return emptyCells >= 54 && clear.lines >= 3 ? 170 : 0;
        }

        private static int CalculateSatisfyingClearBonus(ClearOutcome clear, int chain)
        {
            if (clear.lines <= 0)
            {
                return 0;
            }

            int bonus = 0;
            if (clear.lines >= 2)
            {
                bonus += (clear.lines - 1) * 90;
            }

            if (clear.lines >= 4)
            {
                bonus += 180;
            }

            if (clear.pureLines > 0)
            {
                bonus += clear.pureLines * 130;
            }

            if (chain >= 2)
            {
                bonus += Mathf.RoundToInt(GetChainScoreMultiplier(chain) * 100f);
            }

            if (clear.cellsCleared >= 16)
            {
                bonus += 120;
            }

            return Mathf.Max(0, bonus);
        }

        private static int CountFittingPieces(GenerationContext context, PieceInstance[] set)
        {
            int count = 0;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null && CanAnyPieceFit(context, set[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool CanAnyPieceFit(GenerationContext context, PieceInstance piece)
        {
            return context.GetProfile(piece).placementOptions > 0;
        }

        private static int FindSmallestNonClearingPieceIndex(GenerationContext context, PieceInstance[] set)
        {
            int bestIndex = -1;
            int bestCells = int.MaxValue;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null)
                {
                    return i;
                }

                if (context.GetProfile(set[i]).clearOpportunities > 0)
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

        private static int FindSmallestFittingPieceIndex(GenerationContext context, PieceInstance[] set)
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
                if (cells < bestCells && CanAnyPieceFit(context, set[i]))
                {
                    bestCells = cells;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static int FindWeakestPieceIndex(GenerationContext context, PieceInstance[] set)
        {
            int weakestIndex = -1;
            int weakestOptions = int.MaxValue;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null)
                {
                    return i;
                }

                int options = context.GetProfile(set[i]).placementOptions;
                if (options < weakestOptions)
                {
                    weakestOptions = options;
                    weakestIndex = i;
                }
            }

            return weakestIndex;
        }

        private static int CalculateMiniPiecePenalty(PieceInstance[] set, int emptyCells, float assist01)
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

        private static int CalculateShapeMixBonus(PieceInstance[] set, int emptyCells, float openBoard01)
        {
            if (emptyCells < 16)
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

        private static int CountPiecesAtMost(PieceInstance[] set, int maxCells)
        {
            int count = 0;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null && set[i].Data.cells.Length <= maxCells)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountPiecesAtLeast(PieceInstance[] set, int minCells)
        {
            int count = 0;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null && set[i].Data.cells.Length >= minCells)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountPiecesBetween(PieceInstance[] set, int minCells, int maxCells)
        {
            int count = 0;
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

        private static int TotalPieceCells(PieceInstance[] set)
        {
            int total = 0;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null)
                {
                    total += set[i].Data.cells.Length;
                }
            }

            return total;
        }

        private static OccupancyState GetOccupancyState(int occupiedCells)
        {
            if (occupiedCells >= 50)
            {
                return OccupancyState.Critical;
            }

            if (occupiedCells >= 40)
            {
                return OccupancyState.Pressured;
            }

            return occupiedCells >= 28 ? OccupancyState.Balanced : OccupancyState.Open;
        }

        // Literal editor-side mirror of PieceSpawner's late Classic reduction:
        // it applies to optional assist weighting only, never to fit guarantees
        // or the existing Critical rescue behavior.
        private static float GetLateNonEssentialAssistScale(int classicTrayNumber, OccupancyState occupancyState)
        {
            if (classicTrayNumber >= 9) return 0f;
            if (occupancyState == OccupancyState.Critical) return 1f;
            if (classicTrayNumber <= 6) return 1f;
            return 0.60f;
        }

        private static float GetLatePureSetupScale(int classicTrayNumber, OccupancyState occupancyState)
        {
            if (classicTrayNumber >= 9) return 0f;
            if (occupancyState == OccupancyState.Critical) return 1f;
            if (classicTrayNumber <= 6) return 1f;
            return 0.60f;
        }

        private static int GetSimOpenDiversityBonus(int classicTrayNumber, int configuredBonus)
        {
            if (configuredBonus <= 0) return 0;
            if (classicTrayNumber <= 4) return configuredBonus;
            if (classicTrayNumber <= 6) return Math.Min(configuredBonus, 2000);
            return classicTrayNumber <= 8 ? Math.Min(configuredBonus, 750) : 0;
        }

        private static float GetSimExtraCleanBoardAssistScale(int classicTrayNumber)
        {
            if (classicTrayNumber <= 4) return 1f;
            if (classicTrayNumber <= 6) return 0.75f;
            return classicTrayNumber <= 8 ? 0.30f : 0f;
        }

        private static float GetOccupancyPressure(OccupancyState state)
        {
            switch (state)
            {
                case OccupancyState.Balanced: return 0.32f;
                case OccupancyState.Pressured: return 0.68f;
                case OccupancyState.Critical: return 1f;
                default: return 0f;
            }
        }

        private static int GetImmediateClearWeight(
            OccupancyState state,
            float difficulty01,
            SimulationConfiguration configuration)
        {
            int baseWeight = Mathf.RoundToInt(Mathf.Lerp(500f, 390f, difficulty01));
            switch (state)
            {
                case OccupancyState.Balanced: return baseWeight + 90;
                case OccupancyState.Pressured: return baseWeight + 220;
                case OccupancyState.Critical: return baseWeight + 330;
                default: return baseWeight + configuration.openImmediateClearOffset;
            }
        }

        private static int GetSetupWeight(OccupancyState state)
        {
            switch (state)
            {
                case OccupancyState.Balanced: return 36;
                case OccupancyState.Pressured: return 48;
                case OccupancyState.Critical: return 54;
                default: return 24;
            }
        }

        private static int GetAdjacencyWeight(OccupancyState state)
        {
            switch (state)
            {
                case OccupancyState.Balanced: return 72;
                case OccupancyState.Pressured: return 102;
                case OccupancyState.Critical: return 116;
                default: return 34;
            }
        }

        private static int GetLineProgressWeight(OccupancyState state)
        {
            switch (state)
            {
                case OccupancyState.Balanced: return 46;
                case OccupancyState.Pressured: return 62;
                case OccupancyState.Critical: return 70;
                default: return 30;
            }
        }

        private static int GetCleanlinessWeight(OccupancyState state)
        {
            switch (state)
            {
                case OccupancyState.Balanced: return 3;
                case OccupancyState.Pressured: return 4;
                case OccupancyState.Critical: return 5;
                default: return 2;
            }
        }

        private static int GetSetupPayoffWeight(OccupancyState state)
        {
            switch (state)
            {
                case OccupancyState.Balanced: return 10;
                case OccupancyState.Pressured: return 13;
                case OccupancyState.Critical: return 15;
                default: return 7;
            }
        }

        private static string BuildRootCauseReport(
            Aggregate current,
            Aggregate balancedPlayer,
            Aggregate randomCandidate,
            Aggregate zeroImmediate,
            Aggregate setupFirst,
            TimeSpan elapsed)
        {
            StringBuilder report = new StringBuilder(6200);
            report.AppendLine("CHROMABLAST HEADLESS CLASSIC FREE-CLEAR ROOT-CAUSE STUDY");
            report.AppendLine("Pure-data editor analysis. No scenes, GameObjects, saves, audio, UI, particles, haptics, or persistence were used.");
            report.AppendLine($"Completed: five matched {current.runCount}-run configurations in {elapsed.TotalSeconds:F1}s (seeds {SeedStart}-{SeedStart + current.runCount - 1}).");
            report.AppendLine($"Actual rules retained: {GameConstants.BoardSize}x{GameConstants.BoardSize}, PieceCatalog, {GameConstants.GuaranteedSetAttempts} candidates/tray, current Classic pressure, POP fatigue, guaranteed-playability, and post-selection rescue rules.");
            report.AppendLine("All controls use the same deterministic competent-casual seed range; only their editor-only selection or player-policy branch differs.");
            report.AppendLine();

            CandidatePoolStats rawAll = CombineCandidateStats(current);
            report.AppendLine("RAW CANDIDATE POOL (CURRENT run; all raw 56-candidate sets before winner selection):");
            AppendCandidateStats(report, "- all occupancies", rawAll);
            for (int state = 0; state < 4; state++)
            {
                AppendCandidateStats(report, $"- {((OccupancyState)state).ToString().ToUpperInvariant()}", current.GetRawCandidateStats((OccupancyState)state));
            }
            report.AppendLine();

            report.AppendLine("SELECTED PIECE SIZES (CURRENT selected trays after existing post-selection guarantees):");
            AppendPieceDistribution(report, current.SelectedPieces, "- overall");
            AppendPieceStageDistribution(report, current.SelectedPieces, "- first 5 trays", 0);
            AppendPieceStageDistribution(report, current.SelectedPieces, "- trays 6-10", 1);
            AppendPieceStageDistribution(report, current.SelectedPieces, "- trays 11-18", 2);
            AppendPieceStageDistribution(report, current.SelectedPieces, "- trays 19+", 3);
            report.AppendLine("CLEAR OPPORTUNITY BY PIECE SIZE (selected pieces; at least one immediate-clear placement available on generation board):");
            report.AppendLine($"- 1 / 2 / 3 / 4 / 5+ cells: {current.SelectedPieces.ImmediateClearPercent(0):F1}% / {current.SelectedPieces.ImmediateClearPercent(1):F1}% / {current.SelectedPieces.ImmediateClearPercent(2):F1}% / {current.SelectedPieces.ImmediateClearPercent(3):F1}% / {current.SelectedPieces.ImmediateClearPercent(4):F1}%");
            report.AppendLine();

            report.AppendLine("SCORE TERM DECOMPOSITION (CURRENT selected trays; mean [min..max]):");
            AppendScoreTerm(report, "fit/fairness", current.SelectedScoreTerms.fitFairness);
            AppendScoreTerm(report, "connectivity", current.SelectedScoreTerms.connectivity);
            AppendScoreTerm(report, "line progress", current.SelectedScoreTerms.lineProgress);
            AppendScoreTerm(report, "immediate clear (including OPEN saturation penalty)", current.SelectedScoreTerms.immediateClear);
            AppendScoreTerm(report, "setup/payoff", current.SelectedScoreTerms.setupPayoff);
            AppendScoreTerm(report, "cleanliness", current.SelectedScoreTerms.cleanliness);
            AppendScoreTerm(report, "rescue/relief", current.SelectedScoreTerms.rescueRelief);
            AppendScoreTerm(report, "pressure", current.SelectedScoreTerms.pressure);
            AppendScoreTerm(report, "piece-size / target mass", current.SelectedScoreTerms.pieceSize);
            AppendScoreTerm(report, "other (duplicate penalty)", current.SelectedScoreTerms.other);
            report.AppendLine($"- post-selection guarantee effect: immediate opportunities {current.PrePostSelectionImmediateOpportunitiesPerTray:F2} -> {current.PostPostSelectionImmediateOpportunitiesPerTray:F2} per tray; injection from no-immediate selected set: {current.PostSelectionInjectedImmediateClearTrayPercent:F1}% of trays.");
            report.AppendLine();

            report.AppendLine("PLAYER POLICY CONTROL (same current generator):");
            AppendPolicyComparison(report, current, balancedPlayer);
            report.AppendLine();

            report.AppendLine("RANDOM VALID CANDIDATE CONTROL (same candidate production; candidate selected randomly before existing production post-selection guarantees):");
            report.AppendLine($"- immediate-clear trays: {randomCandidate.ImmediateClearTrayPercent:F1}%; setup/payoff trays: {randomCandidate.SetupPayoffTrayPercent:F1}%; average occupancy: {randomCandidate.MeanOccupancy:F2}; mean run length: {randomCandidate.MeanPlacements:F1}; POP uses: {randomCandidate.MeanPopUses:F2}");
            report.AppendLine();

            report.AppendLine("VARIANT C — OPEN immediate-clear score = 0 (analysis only):");
            AppendGeneratorControl(report, zeroImmediate, false);
            report.AppendLine("VARIANT D — OPEN immediate-clear score = 0; OPEN setup/payoff multiplier = 3x (analysis only):");
            AppendGeneratorControl(report, setupFirst, true);
            report.AppendLine();

            report.AppendLine($"ROOT CAUSE classification: {GetRootCauseClassification(current, balancedPlayer, randomCandidate, zeroImmediate, setupFirst, rawAll)}");
            report.AppendLine($"Why the Phase 7D -100 / +200 test barely moved selection: {GetWeightSensitivityExplanation(current, zeroImmediate, rawAll)}");
            report.AppendLine($"RECOMMENDED NEXT LEVER (do not apply): {GetRecommendedNextLever(current, randomCandidate, zeroImmediate, setupFirst, rawAll)}");
            report.AppendLine("No runtime values, PieceCatalog data, SaveData, gameplay logic, or production scripts were modified.");
            return report.ToString();
        }

        private static string BuildComebackBonusReport(
            Aggregate current,
            Aggregate halfStrength,
            Aggregate removedOpenBonus,
            TimeSpan elapsed)
        {
            StringBuilder report = new StringBuilder(5800);
            report.AppendLine("CHROMABLAST HEADLESS CLASSIC OPEN COMEBACK CLEAR-OPPORTUNITY BONUS STUDY");
            report.AppendLine("Pure-data editor analysis. No scenes, GameObjects, saves, audio, UI, particles, haptics, or persistence were used.");
            report.AppendLine($"Completed: three matched {current.runCount}-run configurations in {elapsed.TotalSeconds:F1}s (seeds {SeedStart}-{SeedStart + current.runCount - 1}).");
            report.AppendLine($"Unchanged rules: {GameConstants.BoardSize}x{GameConstants.BoardSize}, PieceCatalog, {GameConstants.GuaranteedSetAttempts} candidates/tray, smart scoring, clear-first competent-casual player, pressure, POP fatigue, guaranteed playability, rescue fallback, and post-selection guarantees.");
            report.AppendLine("Only the editor simulator's OPEN-state `clearOpportunities` multiplier inside the assist-scaled comeback bonus differs. Balanced, Pressured, and Critical retain 1550 exactly.");
            report.AppendLine();

            report.AppendLine("TRAY SELECTION | A CURRENT 1550 | B HALF 775 | C OPEN 0");
            AppendThreeWayComparison(report, "immediate-clear trays", Percent(current.ImmediateClearTrayPercent), Percent(halfStrength.ImmediateClearTrayPercent), Percent(removedOpenBonus.ImmediateClearTrayPercent));
            AppendThreeWayComparison(report, "setup/payoff trays", Percent(current.SetupPayoffTrayPercent), Percent(halfStrength.SetupPayoffTrayPercent), Percent(removedOpenBonus.SetupPayoffTrayPercent));
            AppendThreeWayComparison(report, "setup/payoff without immediate clear", Percent(current.SetupWithoutImmediateClearTrayPercent), Percent(halfStrength.SetupWithoutImmediateClearTrayPercent), Percent(removedOpenBonus.SetupWithoutImmediateClearTrayPercent));
            AppendThreeWayComparison(report, "completed trays mainly played for immediate clear", Percent(current.SelectedImmediateClearTrayPercent), Percent(halfStrength.SelectedImmediateClearTrayPercent), Percent(removedOpenBonus.SelectedImmediateClearTrayPercent));
            AppendThreeWayComparison(report, "completed trays mainly played for setup/payoff", Percent(current.SelectedSetupPayoffTrayPercent), Percent(halfStrength.SelectedSetupPayoffTrayPercent), Percent(removedOpenBonus.SelectedSetupPayoffTrayPercent));
            AppendThreeWayComparison(report, "completed trays mainly played for connectivity", Percent(current.SelectedConnectivityTrayPercent), Percent(halfStrength.SelectedConnectivityTrayPercent), Percent(removedOpenBonus.SelectedConnectivityTrayPercent));
            AppendThreeWayComparison(report, "multiple-reasonable-choice trays", $"{current.MultipleReasonablePlacementTrayPercent:F1}% (avg {current.MeanReasonablePlacementOptions:F1})", $"{halfStrength.MultipleReasonablePlacementTrayPercent:F1}% (avg {halfStrength.MeanReasonablePlacementOptions:F1})", $"{removedOpenBonus.MultipleReasonablePlacementTrayPercent:F1}% (avg {removedOpenBonus.MeanReasonablePlacementOptions:F1})");
            report.AppendLine();

            report.AppendLine("SETUP → PAYOFF OBSERVATION | A CURRENT 1550 | B HALF 775 | C OPEN 0");
            report.AppendLine("A setup carrier is a completed tray with at least one non-clearing setup move (the existing >=54 threshold). Same-tray payoff is a later clear in that tray; next-tray payoff is the first later clear after an unresolved setup. This observes play only; it does not change player choices.");
            AppendThreeWayComparison(report, "generator pair opportunity (two tray pieces can work together)", Percent(current.SetupPayoffTrayPercent), Percent(halfStrength.SetupPayoffTrayPercent), Percent(removedOpenBonus.SetupPayoffTrayPercent));
            AppendThreeWayComparison(report, "executed same-tray setup → payoff", Percent(current.SameTraySetupPayoffPercent), Percent(halfStrength.SameTraySetupPayoffPercent), Percent(removedOpenBonus.SameTraySetupPayoffPercent));
            AppendThreeWayComparison(report, "executed next-tray setup → payoff", Percent(current.NextTraySetupPayoffPercent), Percent(halfStrength.NextTraySetupPayoffPercent), Percent(removedOpenBonus.NextTraySetupPayoffPercent));
            AppendThreeWayComparison(report, "mean later trays until setup pays off", $"{current.MeanTraysToSetupPayoff:F2}", $"{halfStrength.MeanTraysToSetupPayoff:F2}", $"{removedOpenBonus.MeanTraysToSetupPayoff:F2}");
            report.AppendLine();

            report.AppendLine("BOARD FLOW | A CURRENT 1550 | B HALF 775 | C OPEN 0");
            AppendThreeWayComparison(report, "average occupied cells", $"{current.MeanOccupancy:F2}", $"{halfStrength.MeanOccupancy:F2}", $"{removedOpenBonus.MeanOccupancy:F2}");
            AppendThreeWayComparison(report, "OPEN", Percent(current.OpenPercent), Percent(halfStrength.OpenPercent), Percent(removedOpenBonus.OpenPercent));
            AppendThreeWayComparison(report, "BALANCED", Percent(current.BalancedPercent), Percent(halfStrength.BalancedPercent), Percent(removedOpenBonus.BalancedPercent));
            AppendThreeWayComparison(report, "PRESSURED", Percent(current.PressuredPercent), Percent(halfStrength.PressuredPercent), Percent(removedOpenBonus.PressuredPercent));
            AppendThreeWayComparison(report, "CRITICAL", Percent(current.CriticalPercent), Percent(halfStrength.CriticalPercent), Percent(removedOpenBonus.CriticalPercent));
            report.AppendLine();

            report.AppendLine("CLEAR RHYTHM | A CURRENT 1550 | B HALF 775 | C OPEN 0");
            AppendThreeWayComparison(report, "placements between clears", $"{current.PlacementsPerClear:F2}", $"{halfStrength.PlacementsPerClear:F2}", $"{removedOpenBonus.PlacementsPerClear:F2}");
            AppendThreeWayComparison(report, "actual one-line clear moves", Percent(current.OneLineClearMovePercent), Percent(halfStrength.OneLineClearMovePercent), Percent(removedOpenBonus.OneLineClearMovePercent));
            AppendThreeWayComparison(report, "average clears/run", $"{current.MeanClears:F2}", $"{halfStrength.MeanClears:F2}", $"{removedOpenBonus.MeanClears:F2}");
            AppendThreeWayComparison(report, "multi-clear frequency", Percent(current.MultiClearPercent), Percent(halfStrength.MultiClearPercent), Percent(removedOpenBonus.MultiClearPercent));
            report.AppendLine();

            report.AppendLine("RUN LENGTH | A CURRENT 1550 | B HALF 775 | C OPEN 0");
            AppendThreeWayComparison(report, "mean placements", $"{current.MeanPlacements:F1}", $"{halfStrength.MeanPlacements:F1}", $"{removedOpenBonus.MeanPlacements:F1}");
            AppendThreeWayComparison(report, "median / P75", $"{current.MedianPlacements:F0} / {current.P75Placements:F0}", $"{halfStrength.MedianPlacements:F0} / {halfStrength.P75Placements:F0}", $"{removedOpenBonus.MedianPlacements:F0} / {removedOpenBonus.P75Placements:F0}");
            AppendThreeWayComparison(report, "P90 / P95", $"{current.P90Placements:F0} / {current.P95Placements:F0}", $"{halfStrength.P90Placements:F0} / {halfStrength.P95Placements:F0}", $"{removedOpenBonus.P90Placements:F0} / {removedOpenBonus.P95Placements:F0}");
            AppendThreeWayComparison(report, "longest completed / censored at 500", $"{current.LongestCompletedRun} / {current.ceilingHits} ({current.CeilingPercent:F1}%)", $"{halfStrength.LongestCompletedRun} / {halfStrength.ceilingHits} ({halfStrength.CeilingPercent:F1}%)", $"{removedOpenBonus.LongestCompletedRun} / {removedOpenBonus.ceilingHits} ({removedOpenBonus.CeilingPercent:F1}%)");
            report.AppendLine();

            report.AppendLine("POP + RELIEF | A CURRENT 1550 | B HALF 775 | C OPEN 0");
            AppendThreeWayComparison(report, "average POP uses/run", $"{current.MeanPopUses:F2}", $"{halfStrength.MeanPopUses:F2}", $"{removedOpenBonus.MeanPopUses:F2}");
            AppendThreeWayComparison(report, "median / P90 / maximum POP uses", $"{current.MedianPopUses:F0} / {current.P90PopUses:F0} / {current.MaxPopUses}", $"{halfStrength.MedianPopUses:F0} / {halfStrength.P90PopUses:F0} / {halfStrength.MaxPopUses}", $"{removedOpenBonus.MedianPopUses:F0} / {removedOpenBonus.P90PopUses:F0} / {removedOpenBonus.MaxPopUses}");
            AppendThreeWayComparison(report, "placements between POPs", $"{current.MeanPopInterval:F2}", $"{halfStrength.MeanPopInterval:F2}", $"{removedOpenBonus.MeanPopInterval:F2}");
            AppendThreeWayComparison(report, "relief-biased trays", Percent(current.ReliefTrayPercent), Percent(halfStrength.ReliefTrayPercent), Percent(removedOpenBonus.ReliefTrayPercent));
            AppendThreeWayComparison(report, "maximum consecutive relief trays", current.MaxConsecutiveRelief.ToString(), halfStrength.MaxConsecutiveRelief.ToString(), removedOpenBonus.MaxConsecutiveRelief.ToString());
            AppendThreeWayComparison(report, "anti-relief-loop activations", current.AntiReliefLoopActivations.ToString(), halfStrength.AntiReliefLoopActivations.ToString(), removedOpenBonus.AntiReliefLoopActivations.ToString());
            report.AppendLine("An activation is a generation occurring after two consecutive relief-biased trays. Balanced/Pressured/Critical comeback behavior is unchanged by construction.");
            report.AppendLine();

            report.AppendLine($"PLAYER-FEEL PROXY: A {GetComebackBonusAssessment(current)} | B {GetComebackBonusAssessment(halfStrength)} | C {GetComebackBonusAssessment(removedOpenBonus)}");
            report.AppendLine("PERFORMANCE: 56 candidates, per-tray simulator caches, deterministic seeds, and all runtime allocations remain unchanged. Only the editor-only multiplier field and reporting instrumentation changed.");
            report.AppendLine("No runtime values, PieceCatalog data, gameplay scripts, POP, saves, or production code were modified.");
            return report.ToString();
        }

        private static string BuildPureSetupReport(
            Aggregate current,
            Aggregate pureSetup,
            Aggregate pureSetupDiversity,
            TimeSpan elapsed)
        {
            StringBuilder report = new StringBuilder(8500);
            report.AppendLine("CHROMABLAST HEADLESS CLASSIC PURE SETUP / TWO-PIECE SYNERGY STUDY");
            report.AppendLine("Pure-data editor analysis. No scenes, GameObjects, saves, audio, UI, particles, haptics, or persistence were used.");
            report.AppendLine($"Completed: three matched {current.runCount}-run configurations in {elapsed.TotalSeconds:F1}s (seeds {SeedStart}-{SeedStart + current.runCount - 1}).");
            report.AppendLine($"Unchanged: {GameConstants.BoardSize}x{GameConstants.BoardSize}, PieceCatalog, {GameConstants.GuaranteedSetAttempts} candidates/tray, direct immediate-clear scoring, OPEN comeback 1550, pressure, POP fatigue, relief, guarantees, caches, and player policy.");
            report.AppendLine("B/C are editor-only: on OPEN boards, the broad per-piece setup/payoff score is replaced by a strict non-clearing A -> newly-clearing B relation. Both involved pieces must have zero independent immediate-clear opportunities. C additionally grants +2500 only when such a relation exists and the whole tray has no immediate clear.");
            report.AppendLine();

            report.AppendLine("CURRENT SETUP/PAYOFF DECOMPOSITION (A; selected generated trays)");
            report.AppendLine("- Current scoring uses each piece's best non-clearing setup origin plus any second-piece clear after it. It does not require either involved shape to lack an independent immediate clear.");
            report.AppendLine($"- A Pure two-step: {current.SetupClassificationPercent(SetupClassification.PureTwoStep):F1}%.");
            report.AppendLine($"- B Immediate-clear + payoff overlap: {current.SetupClassificationPercent(SetupClassification.ImmediateClearAndPayoff):F1}%.");
            report.AppendLine($"- C Direct/same-pair payoff without strict purity: {current.SetupClassificationPercent(SetupClassification.DirectPayoff):F1}%.");
            report.AppendLine($"- D General future setup only: {current.SetupClassificationPercent(SetupClassification.GeneralFutureSetup):F1}%.");
            report.AppendLine();

            report.AppendLine("CORRELATION (A CURRENT)");
            report.AppendLine($"- setup/payoff + any immediate clear overlap: {current.ImmediateClearSetupOverlapPercent:F1}% of fresh trays; mean immediate opportunities on those overlap trays: {current.MeanImmediateClearOpportunitiesPerSetupTray:F2}.");
            report.AppendLine($"- mean legacy pair-payoff score when immediate clear exists: {current.MeanSetupScoreWithImmediate:F1}; without immediate clear: {current.MeanSetupScoreWithoutImmediate:F1}.");
            report.AppendLine($"- mean immediate-clear opportunities per fresh tray: {current.MeanImmediateClearOpportunitiesPerTray:F2}. A material score gap between the overlap and non-overlap cohorts is the direct correlation signal; overlap alone is not treated as causation.");
            report.AppendLine();

            report.AppendLine("MAIN METRICS | A CURRENT | B PURE SETUP | C PURE + 2500 DIVERSITY");
            AppendThreeWayComparison(report, "immediate-clear trays", Percent(current.ImmediateClearTrayPercent), Percent(pureSetup.ImmediateClearTrayPercent), Percent(pureSetupDiversity.ImmediateClearTrayPercent));
            AppendThreeWayComparison(report, "Pure Setup trays", Percent(current.PureSetupTrayPercent), Percent(pureSetup.PureSetupTrayPercent), Percent(pureSetupDiversity.PureSetupTrayPercent));
            AppendThreeWayComparison(report, "setup/payoff without immediate clear", Percent(current.SetupWithoutImmediateClearTrayPercent), Percent(pureSetup.SetupWithoutImmediateClearTrayPercent), Percent(pureSetupDiversity.SetupWithoutImmediateClearTrayPercent));
            AppendThreeWayComparison(report, "immediate-clear + setup overlap", Percent(current.ImmediateClearSetupOverlapPercent), Percent(pureSetup.ImmediateClearSetupOverlapPercent), Percent(pureSetupDiversity.ImmediateClearSetupOverlapPercent));
            AppendThreeWayComparison(report, "multiple-reasonable-choice trays", $"{current.MultipleReasonablePlacementTrayPercent:F1}% (avg {current.MeanReasonablePlacementOptions:F1})", $"{pureSetup.MultipleReasonablePlacementTrayPercent:F1}% (avg {pureSetup.MeanReasonablePlacementOptions:F1})", $"{pureSetupDiversity.MultipleReasonablePlacementTrayPercent:F1}% (avg {pureSetupDiversity.MeanReasonablePlacementOptions:F1})");
            AppendThreeWayComparison(report, "two pieces meaningfully work together", Percent(current.PureSetupTrayPercent), Percent(pureSetup.PureSetupTrayPercent), Percent(pureSetupDiversity.PureSetupTrayPercent));
            AppendThreeWayComparison(report, "executed same-tray setup -> payoff", Percent(current.SameTraySetupPayoffPercent), Percent(pureSetup.SameTraySetupPayoffPercent), Percent(pureSetupDiversity.SameTraySetupPayoffPercent));
            report.AppendLine();

            report.AppendLine("CLEAR RHYTHM | A CURRENT | B PURE SETUP | C PURE + 2500 DIVERSITY");
            AppendThreeWayComparison(report, "placements between clears", $"{current.PlacementsPerClear:F2}", $"{pureSetup.PlacementsPerClear:F2}", $"{pureSetupDiversity.PlacementsPerClear:F2}");
            AppendThreeWayComparison(report, "average clears/run", $"{current.MeanClears:F2}", $"{pureSetup.MeanClears:F2}", $"{pureSetupDiversity.MeanClears:F2}");
            AppendThreeWayComparison(report, "multi-clear frequency", Percent(current.MultiClearPercent), Percent(pureSetup.MultiClearPercent), Percent(pureSetupDiversity.MultiClearPercent));
            report.AppendLine();

            report.AppendLine("BOARD FLOW | A CURRENT | B PURE SETUP | C PURE + 2500 DIVERSITY");
            AppendThreeWayComparison(report, "average occupied cells", $"{current.MeanOccupancy:F2}", $"{pureSetup.MeanOccupancy:F2}", $"{pureSetupDiversity.MeanOccupancy:F2}");
            AppendThreeWayComparison(report, "OPEN / BALANCED", $"{current.OpenPercent:F1}% / {current.BalancedPercent:F1}%", $"{pureSetup.OpenPercent:F1}% / {pureSetup.BalancedPercent:F1}%", $"{pureSetupDiversity.OpenPercent:F1}% / {pureSetupDiversity.BalancedPercent:F1}%");
            AppendThreeWayComparison(report, "PRESSURED / CRITICAL", $"{current.PressuredPercent:F1}% / {current.CriticalPercent:F1}%", $"{pureSetup.PressuredPercent:F1}% / {pureSetup.CriticalPercent:F1}%", $"{pureSetupDiversity.PressuredPercent:F1}% / {pureSetupDiversity.CriticalPercent:F1}%");
            report.AppendLine();

            report.AppendLine("RUN LENGTH | A CURRENT | B PURE SETUP | C PURE + 2500 DIVERSITY");
            AppendThreeWayComparison(report, "mean / median", $"{current.MeanPlacements:F1} / {current.MedianPlacements:F0}", $"{pureSetup.MeanPlacements:F1} / {pureSetup.MedianPlacements:F0}", $"{pureSetupDiversity.MeanPlacements:F1} / {pureSetupDiversity.MedianPlacements:F0}");
            AppendThreeWayComparison(report, "P75 / P90 / P95", $"{current.P75Placements:F0} / {current.P90Placements:F0} / {current.P95Placements:F0}", $"{pureSetup.P75Placements:F0} / {pureSetup.P90Placements:F0} / {pureSetup.P95Placements:F0}", $"{pureSetupDiversity.P75Placements:F0} / {pureSetupDiversity.P90Placements:F0} / {pureSetupDiversity.P95Placements:F0}");
            AppendThreeWayComparison(report, "longest completed / censored at 500", $"{current.LongestCompletedRun} / {current.CeilingPercent:F1}%", $"{pureSetup.LongestCompletedRun} / {pureSetup.CeilingPercent:F1}%", $"{pureSetupDiversity.LongestCompletedRun} / {pureSetupDiversity.CeilingPercent:F1}%");
            report.AppendLine();

            report.AppendLine("POP | A CURRENT | B PURE SETUP | C PURE + 2500 DIVERSITY");
            AppendThreeWayComparison(report, "average POP uses", $"{current.MeanPopUses:F2}", $"{pureSetup.MeanPopUses:F2}", $"{pureSetupDiversity.MeanPopUses:F2}");
            AppendThreeWayComparison(report, "median / P90 / maximum", $"{current.MedianPopUses:F0} / {current.P90PopUses:F0} / {current.MaxPopUses}", $"{pureSetup.MedianPopUses:F0} / {pureSetup.P90PopUses:F0} / {pureSetup.MaxPopUses}", $"{pureSetupDiversity.MedianPopUses:F0} / {pureSetupDiversity.P90PopUses:F0} / {pureSetupDiversity.MaxPopUses}");
            AppendThreeWayComparison(report, "placements between POPs", $"{current.MeanPopInterval:F2}", $"{pureSetup.MeanPopInterval:F2}", $"{pureSetupDiversity.MeanPopInterval:F2}");
            report.AppendLine();

            AppendSelectionReasonReport(report, current, pureSetup, pureSetupDiversity);
            AppendOpenExamples(report, "A CURRENT", current.OpenExamples);
            AppendOpenExamples(report, "B PURE SETUP", pureSetup.OpenExamples);
            AppendOpenExamples(report, "C PURE + 2500 DIVERSITY", pureSetupDiversity.OpenExamples);
            report.AppendLine("PERFORMANCE: candidate count remained 56; the existing bounded per-tray profile/payoff caches remain in use. Only editor-side scoring instrumentation and the requested analysis overrides changed. No runtime allocations or gameplay files changed.");
            report.AppendLine("No runtime values, PieceCatalog data, or production scripts were modified.");
            return report.ToString();
        }

        private static string BuildProductionPureSetupReport(Aggregate production, TimeSpan elapsed)
        {
            StringBuilder report = new StringBuilder(4200);
            report.AppendLine("CHROMABLAST HEADLESS CLASSIC — FINAL WOW/FINITE FLOW SANITY");
            report.AppendLine("One production-parity pass: maximum readable construction through tray 6, strong-moderate continuation on trays 7–8, and fair ease-percentile challenge selection from tray 9 onward.");
            report.AppendLine($"Completed: {production.runCount} Classic simulations in {elapsed.TotalSeconds:F1}s (seeds {SeedStart}-{SeedStart + production.runCount - 1}).");
            report.AppendLine($"Unchanged: {GameConstants.BoardSize}x{GameConstants.BoardSize}, {GameConstants.GuaranteedSetAttempts} candidates/tray, direct immediate-clear scoring, OPEN saturation penalty, comeback clear weight 1550, pressure, POP fatigue, relief-loop prevention, guarantees, and player policy. No Phase 8 curation is active.");
            report.AppendLine();
            report.AppendLine("RUN LENGTH");
            report.AppendLine($"- mean / median: {production.MeanPlacements:F1} / {production.MedianPlacements:F0}");
            report.AppendLine($"- P75 / P90 / P95: {production.P75Placements:F0} / {production.P90Placements:F0} / {production.P95Placements:F0}");
            report.AppendLine($"- longest completed / 500-placement censored: {production.LongestCompletedRun} / {production.ceilingHits} ({production.CeilingPercent:F1}%)");
            report.AppendLine();
            report.AppendLine("BOARD FLOW");
            report.AppendLine($"- average occupancy: {production.MeanOccupancy:F2}");
            report.AppendLine($"- early / mid / late tray occupancy: {production.GetCurationStage(0).AverageOccupancy:F2} / {production.GetCurationStage(1).AverageOccupancy:F2} / {production.GetCurationStage(2).AverageOccupancy:F2}");
            report.AppendLine($"- OPEN / BALANCED / PRESSURED / CRITICAL: {production.OpenPercent:F1}% / {production.BalancedPercent:F1}% / {production.PressuredPercent:F1}% / {production.CriticalPercent:F1}%");
            report.AppendLine($"- placements per clear / average clears per run: {production.PlacementsPerClear:F2} / {production.MeanClears:F2}");
            report.AppendLine();
            report.AppendLine("PURE SETUP TELEMETRY (selected generated trays)");
            report.AppendLine($"- immediate-clear trays: {production.ImmediateClearTrayPercent:F1}%");
            report.AppendLine($"- Pure Setup trays: {production.PureSetupTrayPercent:F1}%");
            report.AppendLine($"- setup without immediate clear: {production.SetupWithoutImmediateClearTrayPercent:F1}%");
            report.AppendLine($"- immediate-clear / legacy setup-payoff overlap: {production.ImmediateClearSetupOverlapPercent:F1}%");
            report.AppendLine($"- OPEN diversity +2500 applied: {production.OpenDiversityBonusTrayPercent:F1}%");
            report.AppendLine($"- multiple reasonable choices: {production.MultipleReasonablePlacementTrayPercent:F1}% (avg {production.MeanReasonablePlacementOptions:F1})");
            report.AppendLine();
            report.AppendLine("RELAX FLOW");
            report.AppendLine($"- FlowTarget next-tray continuation: {production.FlowTargetContinuationPercent:F1}%");
            report.AppendLine($"- readable A→B trays 1–4 / 5–6 / 7–8 / 9+: {production.ReadableContinuationPercent(0):F1}% / {production.ReadableContinuationPercent(1):F1}% / {production.ReadableContinuationPercent(2):F1}% / {production.ReadableContinuationPercent(3):F1}%");
            report.AppendLine($"- constructed trays 1–4 / 5–6 / 7–8 / 9+: {production.ConstructedContinuationPercent(0):F1}% / {production.ConstructedContinuationPercent(1):F1}% / {production.ConstructedContinuationPercent(2):F1}% / {production.ConstructedContinuationPercent(3):F1}%");
            report.AppendLine($"- projected final occupied / largest open region / fragmentation proxy: {production.MeanProjectedOccupiedCells:F2} / {production.MeanProjectedLargestOpenArea:F2} / {production.MeanProjectedFragmentation:F2}");
            report.AppendLine();
            report.AppendLine("LATE FAIR-CHALLENGE SELECTION");
            report.AppendLine($"- average TrayEaseScore 1–8 / 9–11 / 12–15 / 16–20 / 21+: {production.MeanTrayEaseScore(0):F1} / {production.MeanTrayEaseScore(1):F1} / {production.MeanTrayEaseScore(2):F1} / {production.MeanTrayEaseScore(3):F1} / {production.MeanTrayEaseScore(4):F1}");
            report.AppendLine($"- nearest-fair fallback / CRITICAL bypass (late trays): {production.ChallengeBandFallbackPercent:F2}% / {production.CriticalChallengeBypassPercent:F2}%");
            report.AppendLine("- ease percentile bands: trays 9–11 = 35–55, 12–15 = 20–40, 16–20 = 5–25, 21+ = 0–15 (0 hardest, 100 easiest).");
            report.AppendLine();
            report.AppendLine("POP");
            report.AppendLine($"- average / median / P90 / maximum uses: {production.MeanPopUses:F2} / {production.MedianPopUses:F0} / {production.P90PopUses:F0} / {production.MaxPopUses}");
            report.AppendLine($"- average placements between POPs: {production.MeanPopInterval:F2}");
            report.AppendLine();
            report.AppendLine("PLUS5 / STAIR5 SELECTED-PIECE FREQUENCY");
            report.AppendLine("- plus5: excluded from all random and post-selection gameplay pools (0.00%)");
            report.AppendLine($"- early generated trays 1–5: {production.SelectedPieces.Stair5Frequency(0):F2}%");
            report.AppendLine($"- mid generated trays 6–10: {production.SelectedPieces.Stair5Frequency(1):F2}%");
            report.AppendLine($"- late generated trays 11+: {production.SelectedPieces.Stair5Frequency(2):F2}%");
            report.AppendLine();
            AppendOpenExamples(report, "PRODUCTION PURE SETUP + OPEN DIVERSITY", production.PureOpenExamples);
            report.AppendLine("Performance: 56 candidates, top-four deep tray shortlist, three placements per projection step, per-tray profile/payoff caches, and deterministic seeds. Production PieceSpawner uses reusable buffers with no LINQ, GameObjects, coroutines, or Phase 8 curation.");
            report.AppendLine("This is deterministic heuristic-player evidence only; it does not replace Unity Play Mode or device validation.");
            return report.ToString();
        }

        private static string BuildSatisfyingTrayCurationReport(
            Aggregate current,
            Aggregate curated,
            TimeSpan elapsed)
        {
            StringBuilder report = new StringBuilder(6200);
            report.AppendLine("CHROMABLAST HEADLESS CLASSIC SATISFYING TRAY CURATION A/B");
            report.AppendLine("Matched deterministic 500-run simulation. A mirrors Phase 7H; B adds the Phase 8 ergonomic, flexibility, soft-composition, and bounded three-piece sequence selector.");
            report.AppendLine($"Completed: {current.runCount} A + {curated.runCount} B Classic runs in {elapsed.TotalSeconds:F1}s (matched seeds {SeedStart}-{SeedStart + current.runCount - 1}).");
            report.AppendLine($"Unchanged in both: {GameConstants.BoardSize}x{GameConstants.BoardSize}, {GameConstants.GuaranteedSetAttempts} candidates per tray, Pure Setup, OPEN +2500 diversity, playability/rescue guarantees, Classic pressure, POP fatigue, and player policy.");
            report.AppendLine();
            report.AppendLine("SELECTED PIECE SIZE DISTRIBUTION | A CURRENT | B CURATED");
            AppendComparison(report, "1–2 cells", Percent(current.SelectedPieces.SizePercent(0) + current.SelectedPieces.SizePercent(1)), Percent(curated.SelectedPieces.SizePercent(0) + curated.SelectedPieces.SizePercent(1)));
            AppendComparison(report, "3 cells", Percent(current.SelectedPieces.SizePercent(2)), Percent(curated.SelectedPieces.SizePercent(2)));
            AppendComparison(report, "4 cells", Percent(current.SelectedPieces.SizePercent(3)), Percent(curated.SelectedPieces.SizePercent(3)));
            AppendComparison(report, "5+ cells", Percent(current.SelectedPieces.SizePercent(4)), Percent(curated.SelectedPieces.SizePercent(4)));
            report.AppendLine();
            report.AppendLine("ERGONOMIC CLASS DISTRIBUTION | A CURRENT | B CURATED");
            AppendComparison(report, "A high satisfaction", Percent(current.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.A)), Percent(curated.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.A)));
            AppendComparison(report, "B good connector", Percent(current.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.B)), Percent(curated.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.B)));
            AppendComparison(report, "C situational", Percent(current.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.C)), Percent(curated.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.C)));
            AppendComparison(report, "D awkward/friction", Percent(current.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.D)), Percent(curated.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.D)));
            report.AppendLine();
            report.AppendLine("TRAY QUALITY | A CURRENT | B CURATED");
            AppendComparison(report, "2+ high-satisfaction pieces", Percent(current.TrayQuality.TwoOrMoreHighSatisfactionPercent), Percent(curated.TrayQuality.TwoOrMoreHighSatisfactionPercent));
            AppendComparison(report, "2+ awkward pieces", Percent(current.TrayQuality.TwoOrMoreAwkwardPercent), Percent(curated.TrayQuality.TwoOrMoreAwkwardPercent));
            AppendComparison(report, "three-large-piece trays", Percent(current.TrayQuality.ThreeLargePiecesPercent), Percent(curated.TrayQuality.ThreeLargePiecesPercent));
            AppendComparison(report, "healthy-flexibility trays", Percent(current.TrayQuality.HealthyFlexibilityPercent), Percent(curated.TrayQuality.HealthyFlexibilityPercent));
            AppendComparison(report, "multiple reasonable choices", $"{current.MultipleReasonablePlacementTrayPercent:F1}% (avg {current.MeanReasonablePlacementOptions:F1})", $"{curated.MultipleReasonablePlacementTrayPercent:F1}% (avg {curated.MeanReasonablePlacementOptions:F1})");
            AppendComparison(report, "coherent two-piece tray", Percent(current.TrayQuality.CoherentTwoPiecePercent), Percent(curated.TrayQuality.CoherentTwoPiecePercent));
            AppendComparison(report, "coherent full A→B→C sequence", Percent(current.TrayQuality.CoherentFullSequencePercent), Percent(curated.TrayQuality.CoherentFullSequencePercent));
            report.AppendLine();
            report.AppendLine("PURE SETUP + CLEAR RHYTHM | A CURRENT | B CURATED");
            AppendComparison(report, "immediate-clear trays", Percent(current.ImmediateClearTrayPercent), Percent(curated.ImmediateClearTrayPercent));
            AppendComparison(report, "Pure Setup trays", Percent(current.PureSetupTrayPercent), Percent(curated.PureSetupTrayPercent));
            AppendComparison(report, "OPEN diversity +2500 applied", Percent(current.OpenDiversityBonusTrayPercent), Percent(curated.OpenDiversityBonusTrayPercent));
            AppendComparison(report, "placements per clear", $"{current.PlacementsPerClear:F2}", $"{curated.PlacementsPerClear:F2}");
            AppendComparison(report, "average clears/run", $"{current.MeanClears:F1}", $"{curated.MeanClears:F1}");
            report.AppendLine();
            report.AppendLine("BOARD FLOW | A CURRENT | B CURATED");
            AppendComparison(report, "average occupied cells", $"{current.MeanOccupancy:F2}", $"{curated.MeanOccupancy:F2}");
            AppendComparison(report, "OPEN / BALANCED", $"{current.OpenPercent:F1}% / {current.BalancedPercent:F1}%", $"{curated.OpenPercent:F1}% / {curated.BalancedPercent:F1}%");
            AppendComparison(report, "PRESSURED / CRITICAL", $"{current.PressuredPercent:F1}% / {current.CriticalPercent:F1}%", $"{curated.PressuredPercent:F1}% / {curated.CriticalPercent:F1}%");
            report.AppendLine();
            report.AppendLine("RUN LENGTH | A CURRENT | B CURATED");
            AppendComparison(report, "mean / median", $"{current.MeanPlacements:F1} / {current.MedianPlacements:F0}", $"{curated.MeanPlacements:F1} / {curated.MedianPlacements:F0}");
            AppendComparison(report, "P75 / P90 / P95", $"{current.P75Placements:F0} / {current.P90Placements:F0} / {current.P95Placements:F0}", $"{curated.P75Placements:F0} / {curated.P90Placements:F0} / {curated.P95Placements:F0}");
            AppendComparison(report, "longest completed / 500-placement censored", $"{current.LongestCompletedRun} / {current.ceilingHits}", $"{curated.LongestCompletedRun} / {curated.ceilingHits}");
            report.AppendLine();
            report.AppendLine("REPRESENTATIVE CURATED OPEN/BALANCED TRAYS — shape[cells/class/current valid placements]");
            for (int i = 0; i < curated.CuratedExamples.Count; i++)
            {
                report.AppendLine($"- {i + 1}. {curated.CuratedExamples[i]}");
            }
            report.AppendLine();
            report.AppendLine("Performance parity: runtime candidate count remains 56. Ergonomic profiles are catalog-cached; full sequence review is limited to two shortlisted candidates, three cyclic orders, and three second placements per order, with shape-order caching. No per-candidate arrays, LINQ, or runtime generation GameObjects were added.");
            report.AppendLine("This is a deterministic heuristic-player comparison only. Unity Play Mode and device validation remain required before approval.");
            return report.ToString();
        }

        private static string BuildPhase8BReport(
            Aggregate baseline,
            Aggregate moderated,
            Aggregate pressureAware,
            TimeSpan elapsed)
        {
            StringBuilder report = new StringBuilder(10500);
            report.AppendLine("CHROMABLAST HEADLESS CLASSIC — PHASE 8B SATISFYING BUT FINITE TRAY CURATION");
            report.AppendLine("Matched deterministic simulation: A = exact Phase 7H baseline; B = moderated Phase 8; C = B plus pressure-aware Phase 8 curation and the one-tray perfect-streak breaker.");
            report.AppendLine($"Completed: {baseline.runCount} A + {moderated.runCount} B + {pressureAware.runCount} C Classic runs in {elapsed.TotalSeconds:F1}s (matched seeds {SeedStart}-{SeedStart + baseline.runCount - 1}).");
            report.AppendLine($"Controls held constant: {GameConstants.BoardSize}x{GameConstants.BoardSize}, {GameConstants.GuaranteedSetAttempts} candidates/tray, Pure Setup, OPEN +2500 diversity, playability/rescue, Classic pressure, POP fatigue, and clear-first competent-casual player policy.");
            report.AppendLine("B: 4-cell x0.55; satisfying 5+ penalties x0.50; Phase 8 Pure Setup x0.65; trio x0.55; full sequence x0.60; flexibility x0.75. C applies the requested 1.00/0.85/0.70/0.55 pressure layer only to Phase 8 satisfaction, trio, flexibility, and full-sequence terms.");
            report.AppendLine();
            report.AppendLine("SELECTED PIECE SIZE DISTRIBUTION | A PHASE 7H | B MODERATED | C PRESSURE-AWARE");
            AppendThreeWayComparison(report, "1–2 cells", Percent(baseline.SelectedPieces.SizePercent(0) + baseline.SelectedPieces.SizePercent(1)), Percent(moderated.SelectedPieces.SizePercent(0) + moderated.SelectedPieces.SizePercent(1)), Percent(pressureAware.SelectedPieces.SizePercent(0) + pressureAware.SelectedPieces.SizePercent(1)));
            AppendThreeWayComparison(report, "3 cells", Percent(baseline.SelectedPieces.SizePercent(2)), Percent(moderated.SelectedPieces.SizePercent(2)), Percent(pressureAware.SelectedPieces.SizePercent(2)));
            AppendThreeWayComparison(report, "4 cells", Percent(baseline.SelectedPieces.SizePercent(3)), Percent(moderated.SelectedPieces.SizePercent(3)), Percent(pressureAware.SelectedPieces.SizePercent(3)));
            AppendThreeWayComparison(report, "5+ cells", Percent(baseline.SelectedPieces.SizePercent(4)), Percent(moderated.SelectedPieces.SizePercent(4)), Percent(pressureAware.SelectedPieces.SizePercent(4)));
            report.AppendLine();
            report.AppendLine("SATISFACTION CLASS DISTRIBUTION | A | B | C");
            AppendThreeWayComparison(report, "A high satisfaction", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.A)), Percent(moderated.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.A)), Percent(pressureAware.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.A)));
            AppendThreeWayComparison(report, "B connector", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.B)), Percent(moderated.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.B)), Percent(pressureAware.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.B)));
            AppendThreeWayComparison(report, "C situational", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.C)), Percent(moderated.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.C)), Percent(pressureAware.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.C)));
            AppendThreeWayComparison(report, "D stair5", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.D)), Percent(moderated.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.D)), Percent(pressureAware.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.D)));
            report.AppendLine();
            report.AppendLine("TRAY FEEL | A | B | C");
            AppendThreeWayComparison(report, "immediate-clear trays", Percent(baseline.ImmediateClearTrayPercent), Percent(moderated.ImmediateClearTrayPercent), Percent(pressureAware.ImmediateClearTrayPercent));
            AppendThreeWayComparison(report, "Pure Setup trays", Percent(baseline.PureSetupTrayPercent), Percent(moderated.PureSetupTrayPercent), Percent(pressureAware.PureSetupTrayPercent));
            AppendThreeWayComparison(report, "setup without immediate clear", Percent(baseline.SetupWithoutImmediateClearTrayPercent), Percent(moderated.SetupWithoutImmediateClearTrayPercent), Percent(pressureAware.SetupWithoutImmediateClearTrayPercent));
            AppendThreeWayComparison(report, "healthy-flexibility trays", Percent(baseline.TrayQuality.HealthyFlexibilityPercent), Percent(moderated.TrayQuality.HealthyFlexibilityPercent), Percent(pressureAware.TrayQuality.HealthyFlexibilityPercent));
            AppendThreeWayComparison(report, "coherent two-piece trays", Percent(baseline.TrayQuality.CoherentTwoPiecePercent), Percent(moderated.TrayQuality.CoherentTwoPiecePercent), Percent(pressureAware.TrayQuality.CoherentTwoPiecePercent));
            AppendThreeWayComparison(report, "coherent full A→B→C sequences", Percent(baseline.TrayQuality.CoherentFullSequencePercent), Percent(moderated.TrayQuality.CoherentFullSequencePercent), Percent(pressureAware.TrayQuality.CoherentFullSequencePercent));
            AppendThreeWayComparison(report, "multiple reasonable choices", $"{baseline.MultipleReasonablePlacementTrayPercent:F1}% (avg {baseline.MeanReasonablePlacementOptions:F1})", $"{moderated.MultipleReasonablePlacementTrayPercent:F1}% (avg {moderated.MeanReasonablePlacementOptions:F1})", $"{pressureAware.MultipleReasonablePlacementTrayPercent:F1}% (avg {pressureAware.MeanReasonablePlacementOptions:F1})");
            AppendThreeWayComparison(report, "three-large-piece trays", Percent(baseline.TrayQuality.ThreeLargePiecesPercent), Percent(moderated.TrayQuality.ThreeLargePiecesPercent), Percent(pressureAware.TrayQuality.ThreeLargePiecesPercent));
            AppendThreeWayComparison(report, "2+ awkward-piece trays", Percent(baseline.TrayQuality.TwoOrMoreAwkwardPercent), Percent(moderated.TrayQuality.TwoOrMoreAwkwardPercent), Percent(pressureAware.TrayQuality.TwoOrMoreAwkwardPercent));
            AppendThreeWayComparison(report, "perfectly curated trays", Percent(baseline.PerfectlyCuratedTrayPercent), Percent(moderated.PerfectlyCuratedTrayPercent), Percent(pressureAware.PerfectlyCuratedTrayPercent));
            AppendThreeWayComparison(report, "perfect-tray breaker activations", "n/a", "n/a", $"{pressureAware.PerfectCurationStreakBreakerFrequency:F2}% (max streak {pressureAware.MaxConsecutivePerfectCuration})");
            report.AppendLine();
            report.AppendLine("BOARD FLOW | A | B | C");
            AppendThreeWayComparison(report, "average occupied cells", $"{baseline.MeanOccupancy:F2}", $"{moderated.MeanOccupancy:F2}", $"{pressureAware.MeanOccupancy:F2}");
            AppendThreeWayComparison(report, "OPEN", Percent(baseline.OpenPercent), Percent(moderated.OpenPercent), Percent(pressureAware.OpenPercent));
            AppendThreeWayComparison(report, "BALANCED", Percent(baseline.BalancedPercent), Percent(moderated.BalancedPercent), Percent(pressureAware.BalancedPercent));
            AppendThreeWayComparison(report, "PRESSURED", Percent(baseline.PressuredPercent), Percent(moderated.PressuredPercent), Percent(pressureAware.PressuredPercent));
            AppendThreeWayComparison(report, "CRITICAL", Percent(baseline.CriticalPercent), Percent(moderated.CriticalPercent), Percent(pressureAware.CriticalPercent));
            report.AppendLine();
            report.AppendLine("RUN LENGTH | A | B | C");
            AppendThreeWayComparison(report, "mean / median", $"{baseline.MeanPlacements:F1} / {baseline.MedianPlacements:F0}", $"{moderated.MeanPlacements:F1} / {moderated.MedianPlacements:F0}", $"{pressureAware.MeanPlacements:F1} / {pressureAware.MedianPlacements:F0}");
            AppendThreeWayComparison(report, "P75 / P90 / P95", $"{baseline.P75Placements:F0} / {baseline.P90Placements:F0} / {baseline.P95Placements:F0}", $"{moderated.P75Placements:F0} / {moderated.P90Placements:F0} / {moderated.P95Placements:F0}", $"{pressureAware.P75Placements:F0} / {pressureAware.P90Placements:F0} / {pressureAware.P95Placements:F0}");
            AppendThreeWayComparison(report, "longest completed", $"{baseline.LongestCompletedRun}", $"{moderated.LongestCompletedRun}", $"{pressureAware.LongestCompletedRun}");
            AppendThreeWayComparison(report, "500-placement censored", $"{baseline.ceilingHits} ({baseline.CeilingPercent:F1}%)", $"{moderated.ceilingHits} ({moderated.CeilingPercent:F1}%)", $"{pressureAware.ceilingHits} ({pressureAware.CeilingPercent:F1}%)");
            report.AppendLine();
            report.AppendLine("CLEAR / POP RHYTHM | A | B | C");
            AppendThreeWayComparison(report, "placements per clear", $"{baseline.PlacementsPerClear:F2}", $"{moderated.PlacementsPerClear:F2}", $"{pressureAware.PlacementsPerClear:F2}");
            AppendThreeWayComparison(report, "clears/run", $"{baseline.MeanClears:F1}", $"{moderated.MeanClears:F1}", $"{pressureAware.MeanClears:F1}");
            AppendThreeWayComparison(report, "multi-clear frequency", Percent(baseline.MultiClearPercent), Percent(moderated.MultiClearPercent), Percent(pressureAware.MultiClearPercent));
            AppendThreeWayComparison(report, "average / P90 POP uses", $"{baseline.MeanPopUses:F2} / {baseline.P90PopUses:F0}", $"{moderated.MeanPopUses:F2} / {moderated.P90PopUses:F0}", $"{pressureAware.MeanPopUses:F2} / {pressureAware.P90PopUses:F0}");
            AppendThreeWayComparison(report, "placements between POPs", $"{baseline.MeanPopInterval:F2}", $"{moderated.MeanPopInterval:F2}", $"{pressureAware.MeanPopInterval:F2}");
            report.AppendLine();
            AppendPhase8BStageReport(report, pressureAware, 0, "EARLY — generated trays 1–5");
            AppendPhase8BStageReport(report, pressureAware, 1, "MID — generated trays 6–15");
            AppendPhase8BStageReport(report, pressureAware, 2, "LATE — generated trays 16+");
            report.AppendLine("REPRESENTATIVE VARIANT C TRAYS — shape[cells/class/valid placements]");
            AppendPhase8BExamples(report, pressureAware, 0, "EARLY");
            AppendPhase8BExamples(report, pressureAware, 1, "MID");
            AppendPhase8BExamples(report, pressureAware, 2, "LATE");
            report.AppendLine("Performance: 56 candidates/tray is unchanged. This analysis-only mirror uses its existing deterministic per-tray structures; it adds no runtime candidate arrays, LINQ, GameObjects, rescans, or allocations to PieceSpawner. Runtime cache behavior is unchanged because Phase 8B does not touch production files.");
            report.AppendLine("This is deterministic heuristic-player evidence only. It is not Play Mode/device validation and does not approve either uncommitted Phase 8 runtime behavior or a production balance change.");
            return report.ToString();
        }

        private static string BuildPhase8CReport(
            Aggregate baseline,
            Aggregate lightCuration,
            TimeSpan elapsed)
        {
            StringBuilder report = new StringBuilder(8500);
            report.AppendLine("CHROMABLAST HEADLESS CLASSIC — PHASE 8C LIGHT CURATION INTERMEDIATE");
            report.AppendLine("Matched deterministic simulation: A is exact Phase 7H; B adds only a light tie-breaker curation layer. Phase 7H playability, Pure Setup, OPEN +2500 diversity, pressure, rescue, and post-selection guarantees remain intact.");
            report.AppendLine($"Completed: {baseline.runCount} A + {lightCuration.runCount} B Classic runs in {elapsed.TotalSeconds:F1}s (matched seeds {SeedStart}-{SeedStart + baseline.runCount - 1}).");
            report.AppendLine($"B controls: satisfaction x0.30; 4-cell preference x0.25; satisfying 5+ penalty x0.20; C/D penalties x0.45/x0.75; extra Pure Setup x0.20; trio/full sequence x0.18/x0.20; flexibility x0.35; three-large penalty x0.30; pressure decay 1.00/0.75/0.50/0.25; perfect-streak tray x0.50.");
            report.AppendLine("The Phase 8 post-selection shape/mass guards are disabled for B, so it stays a preference layer rather than a second generation system.");
            report.AppendLine();
            report.AppendLine("PIECE DISTRIBUTION | A PHASE 7H | B LIGHT CURATION");
            AppendComparison(report, "1–2 cells", Percent(baseline.SelectedPieces.SizePercent(0) + baseline.SelectedPieces.SizePercent(1)), Percent(lightCuration.SelectedPieces.SizePercent(0) + lightCuration.SelectedPieces.SizePercent(1)));
            AppendComparison(report, "3 cells", Percent(baseline.SelectedPieces.SizePercent(2)), Percent(lightCuration.SelectedPieces.SizePercent(2)));
            AppendComparison(report, "4 cells", Percent(baseline.SelectedPieces.SizePercent(3)), Percent(lightCuration.SelectedPieces.SizePercent(3)));
            AppendComparison(report, "5+ cells", Percent(baseline.SelectedPieces.SizePercent(4)), Percent(lightCuration.SelectedPieces.SizePercent(4)));
            report.AppendLine();
            report.AppendLine("SATISFACTION CLASSES | A | B");
            AppendComparison(report, "A", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.A)), Percent(lightCuration.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.A)));
            AppendComparison(report, "B", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.B)), Percent(lightCuration.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.B)));
            AppendComparison(report, "C", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.C)), Percent(lightCuration.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.C)));
            AppendComparison(report, "D", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.D)), Percent(lightCuration.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.D)));
            report.AppendLine();
            report.AppendLine("TRAY FEEL | A | B");
            AppendComparison(report, "immediate-clear trays", Percent(baseline.ImmediateClearTrayPercent), Percent(lightCuration.ImmediateClearTrayPercent));
            AppendComparison(report, "Pure Setup trays", Percent(baseline.PureSetupTrayPercent), Percent(lightCuration.PureSetupTrayPercent));
            AppendComparison(report, "setup without immediate clear", Percent(baseline.SetupWithoutImmediateClearTrayPercent), Percent(lightCuration.SetupWithoutImmediateClearTrayPercent));
            AppendComparison(report, "healthy flexibility", Percent(baseline.TrayQuality.HealthyFlexibilityPercent), Percent(lightCuration.TrayQuality.HealthyFlexibilityPercent));
            AppendComparison(report, "full coherence", Percent(baseline.TrayQuality.CoherentFullSequencePercent), Percent(lightCuration.TrayQuality.CoherentFullSequencePercent));
            AppendComparison(report, "multiple reasonable choices", $"{baseline.MultipleReasonablePlacementTrayPercent:F1}% (avg {baseline.MeanReasonablePlacementOptions:F1})", $"{lightCuration.MultipleReasonablePlacementTrayPercent:F1}% (avg {lightCuration.MeanReasonablePlacementOptions:F1})");
            AppendComparison(report, "three-large trays", Percent(baseline.TrayQuality.ThreeLargePiecesPercent), Percent(lightCuration.TrayQuality.ThreeLargePiecesPercent));
            AppendComparison(report, "2+ awkward pieces", Percent(baseline.TrayQuality.TwoOrMoreAwkwardPercent), Percent(lightCuration.TrayQuality.TwoOrMoreAwkwardPercent));
            AppendComparison(report, "perfectly curated trays", Percent(baseline.PerfectlyCuratedTrayPercent), Percent(lightCuration.PerfectlyCuratedTrayPercent));
            AppendComparison(report, "perfect-streak breaker", "n/a", $"{lightCuration.PerfectCurationStreakBreakerFrequency:F2}% (max streak {lightCuration.MaxConsecutivePerfectCuration})");
            report.AppendLine();
            report.AppendLine("BOARD FLOW | A | B");
            AppendComparison(report, "average occupancy", $"{baseline.MeanOccupancy:F2}", $"{lightCuration.MeanOccupancy:F2}");
            AppendComparison(report, "OPEN / BALANCED", $"{baseline.OpenPercent:F1}% / {baseline.BalancedPercent:F1}%", $"{lightCuration.OpenPercent:F1}% / {lightCuration.BalancedPercent:F1}%");
            AppendComparison(report, "PRESSURED / CRITICAL", $"{baseline.PressuredPercent:F1}% / {baseline.CriticalPercent:F1}%", $"{lightCuration.PressuredPercent:F1}% / {lightCuration.CriticalPercent:F1}%");
            report.AppendLine();
            report.AppendLine("RUN LENGTH | A | B");
            AppendComparison(report, "mean / median", $"{baseline.MeanPlacements:F1} / {baseline.MedianPlacements:F0}", $"{lightCuration.MeanPlacements:F1} / {lightCuration.MedianPlacements:F0}");
            AppendComparison(report, "P75 / P90 / P95", $"{baseline.P75Placements:F0} / {baseline.P90Placements:F0} / {baseline.P95Placements:F0}", $"{lightCuration.P75Placements:F0} / {lightCuration.P90Placements:F0} / {lightCuration.P95Placements:F0}");
            AppendComparison(report, "longest completed", $"{baseline.LongestCompletedRun}", $"{lightCuration.LongestCompletedRun}");
            AppendComparison(report, "500-placement censored", $"{baseline.ceilingHits} ({baseline.CeilingPercent:F1}%)", $"{lightCuration.ceilingHits} ({lightCuration.CeilingPercent:F1}%)");
            report.AppendLine();
            report.AppendLine("CLEAR / POP | A | B");
            AppendComparison(report, "placements per clear", $"{baseline.PlacementsPerClear:F2}", $"{lightCuration.PlacementsPerClear:F2}");
            AppendComparison(report, "clears/run", $"{baseline.MeanClears:F1}", $"{lightCuration.MeanClears:F1}");
            AppendComparison(report, "average / P90 POP uses", $"{baseline.MeanPopUses:F2} / {baseline.P90PopUses:F0}", $"{lightCuration.MeanPopUses:F2} / {lightCuration.P90PopUses:F0}");
            report.AppendLine();
            AppendLightCurationStageReport(report, lightCuration, 0, "EARLY — generated trays 1–5");
            AppendLightCurationStageReport(report, lightCuration, 1, "MID — generated trays 6–15");
            AppendLightCurationStageReport(report, lightCuration, 2, "LATE — generated trays 16+");
            report.AppendLine("REPRESENTATIVE LIGHT-CURATION TRAYS — shape[cells/class/valid placements]");
            AppendPhase8BExamples(report, lightCuration, 0, "EARLY");
            AppendPhase8BExamples(report, lightCuration, 1, "MID");
            AppendPhase8BExamples(report, lightCuration, 2, "LATE");
            report.AppendLine("Performance: candidate count remains 56. The simulator reuses its existing profile/payoff caching and bounded lookahead. This Phase 8C work changes only the editor mirror—no runtime allocation, board rescan, GameObject, or production PieceSpawner change was made.");
            report.AppendLine("This is deterministic heuristic-player evidence only. Play Mode/device validation is not applicable until a future approved runtime implementation exists.");
            return report.ToString();
        }

        private static string BuildPhase8DReport(
            Aggregate baseline,
            Aggregate lightCuration,
            Aggregate clearGate,
            TimeSpan elapsed)
        {
            StringBuilder report = new StringBuilder(12500);
            report.AppendLine("CHROMABLAST HEADLESS CLASSIC — PHASE 8D LIGHT CURATION IMMEDIATE-CLEAR GATE");
            report.AppendLine("Matched deterministic 500-run A/B/C analysis only: A = exact Phase 7H; B = Phase 8C Light Curation; C = the same Light Curation with only its positive bonus contribution gated when a tray already has an immediate clear (OPEN x0.20; BALANCED x0.60; PRESSURED/CRITICAL x1.00). Phase 7H direct clear, Pure Setup, OPEN +2500 diversity, comeback, fair-fit, pressure, POP fatigue, guarantees, and player policy are unchanged.");
            report.AppendLine($"Completed: {baseline.runCount} A + {lightCuration.runCount} B + {clearGate.runCount} C Classic runs in {elapsed.TotalSeconds:F1}s (matched seeds {SeedStart}-{SeedStart + baseline.runCount - 1}).");
            report.AppendLine("C applies no negative clear penalty and never bans an immediate-clear tray; it merely withholds the extra positive Light Curation preference where an immediate reward already exists. Candidate count remains 56.");
            report.AppendLine();

            report.AppendLine("PIECES | A PHASE 7H | B LIGHT CURATION | C LIGHT + CLEAR GATE");
            AppendThreeWayComparison(report, "1–2 cells", Percent(baseline.SelectedPieces.SizePercent(0) + baseline.SelectedPieces.SizePercent(1)), Percent(lightCuration.SelectedPieces.SizePercent(0) + lightCuration.SelectedPieces.SizePercent(1)), Percent(clearGate.SelectedPieces.SizePercent(0) + clearGate.SelectedPieces.SizePercent(1)));
            AppendThreeWayComparison(report, "3 cells", Percent(baseline.SelectedPieces.SizePercent(2)), Percent(lightCuration.SelectedPieces.SizePercent(2)), Percent(clearGate.SelectedPieces.SizePercent(2)));
            AppendThreeWayComparison(report, "4 cells", Percent(baseline.SelectedPieces.SizePercent(3)), Percent(lightCuration.SelectedPieces.SizePercent(3)), Percent(clearGate.SelectedPieces.SizePercent(3)));
            AppendThreeWayComparison(report, "5+ cells", Percent(baseline.SelectedPieces.SizePercent(4)), Percent(lightCuration.SelectedPieces.SizePercent(4)), Percent(clearGate.SelectedPieces.SizePercent(4)));
            report.AppendLine();

            report.AppendLine("CLASSES | A | B | C");
            AppendThreeWayComparison(report, "A high satisfaction", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.A)), Percent(lightCuration.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.A)), Percent(clearGate.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.A)));
            AppendThreeWayComparison(report, "B connector", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.B)), Percent(lightCuration.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.B)), Percent(clearGate.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.B)));
            AppendThreeWayComparison(report, "C situational", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.C)), Percent(lightCuration.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.C)), Percent(clearGate.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.C)));
            AppendThreeWayComparison(report, "D awkward", Percent(baseline.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.D)), Percent(lightCuration.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.D)), Percent(clearGate.TrayQuality.SatisfactionClassPercent(PieceSatisfactionClass.D)));
            report.AppendLine();

            report.AppendLine("TRAY FEEL | A | B | C");
            AppendThreeWayComparison(report, "immediate-clear trays", Percent(baseline.ImmediateClearTrayPercent), Percent(lightCuration.ImmediateClearTrayPercent), Percent(clearGate.ImmediateClearTrayPercent));
            AppendThreeWayComparison(report, "Pure Setup trays", Percent(baseline.PureSetupTrayPercent), Percent(lightCuration.PureSetupTrayPercent), Percent(clearGate.PureSetupTrayPercent));
            AppendThreeWayComparison(report, "setup without immediate clear", Percent(baseline.SetupWithoutImmediateClearTrayPercent), Percent(lightCuration.SetupWithoutImmediateClearTrayPercent), Percent(clearGate.SetupWithoutImmediateClearTrayPercent));
            AppendThreeWayComparison(report, "Pure Setup without immediate clear", Percent(baseline.PureSetupWithoutImmediateClearTrayPercent), Percent(lightCuration.PureSetupWithoutImmediateClearTrayPercent), Percent(clearGate.PureSetupWithoutImmediateClearTrayPercent));
            AppendThreeWayComparison(report, "healthy flexibility", Percent(baseline.TrayQuality.HealthyFlexibilityPercent), Percent(lightCuration.TrayQuality.HealthyFlexibilityPercent), Percent(clearGate.TrayQuality.HealthyFlexibilityPercent));
            AppendThreeWayComparison(report, "full coherence", Percent(baseline.TrayQuality.CoherentFullSequencePercent), Percent(lightCuration.TrayQuality.CoherentFullSequencePercent), Percent(clearGate.TrayQuality.CoherentFullSequencePercent));
            AppendThreeWayComparison(report, "multiple reasonable choices", $"{baseline.MultipleReasonablePlacementTrayPercent:F1}% (avg {baseline.MeanReasonablePlacementOptions:F1})", $"{lightCuration.MultipleReasonablePlacementTrayPercent:F1}% (avg {lightCuration.MeanReasonablePlacementOptions:F1})", $"{clearGate.MultipleReasonablePlacementTrayPercent:F1}% (avg {clearGate.MeanReasonablePlacementOptions:F1})");
            AppendThreeWayComparison(report, "three-large trays", Percent(baseline.TrayQuality.ThreeLargePiecesPercent), Percent(lightCuration.TrayQuality.ThreeLargePiecesPercent), Percent(clearGate.TrayQuality.ThreeLargePiecesPercent));
            AppendThreeWayComparison(report, "2+ awkward pieces", Percent(baseline.TrayQuality.TwoOrMoreAwkwardPercent), Percent(lightCuration.TrayQuality.TwoOrMoreAwkwardPercent), Percent(clearGate.TrayQuality.TwoOrMoreAwkwardPercent));
            report.AppendLine();

            report.AppendLine("BOARD | A | B | C");
            AppendThreeWayComparison(report, "average occupancy", $"{baseline.MeanOccupancy:F2}", $"{lightCuration.MeanOccupancy:F2}", $"{clearGate.MeanOccupancy:F2}");
            AppendThreeWayComparison(report, "OPEN", Percent(baseline.OpenPercent), Percent(lightCuration.OpenPercent), Percent(clearGate.OpenPercent));
            AppendThreeWayComparison(report, "BALANCED", Percent(baseline.BalancedPercent), Percent(lightCuration.BalancedPercent), Percent(clearGate.BalancedPercent));
            AppendThreeWayComparison(report, "PRESSURED", Percent(baseline.PressuredPercent), Percent(lightCuration.PressuredPercent), Percent(clearGate.PressuredPercent));
            AppendThreeWayComparison(report, "CRITICAL", Percent(baseline.CriticalPercent), Percent(lightCuration.CriticalPercent), Percent(clearGate.CriticalPercent));
            report.AppendLine();

            report.AppendLine("RUN | A | B | C");
            AppendThreeWayComparison(report, "mean / median", $"{baseline.MeanPlacements:F1} / {baseline.MedianPlacements:F0}", $"{lightCuration.MeanPlacements:F1} / {lightCuration.MedianPlacements:F0}", $"{clearGate.MeanPlacements:F1} / {clearGate.MedianPlacements:F0}");
            AppendThreeWayComparison(report, "P75 / P90 / P95", $"{baseline.P75Placements:F0} / {baseline.P90Placements:F0} / {baseline.P95Placements:F0}", $"{lightCuration.P75Placements:F0} / {lightCuration.P90Placements:F0} / {lightCuration.P95Placements:F0}", $"{clearGate.P75Placements:F0} / {clearGate.P90Placements:F0} / {clearGate.P95Placements:F0}");
            AppendThreeWayComparison(report, "longest completed", $"{baseline.LongestCompletedRun}", $"{lightCuration.LongestCompletedRun}", $"{clearGate.LongestCompletedRun}");
            AppendThreeWayComparison(report, "censored at 500", $"{baseline.ceilingHits} ({baseline.CeilingPercent:F1}%)", $"{lightCuration.ceilingHits} ({lightCuration.CeilingPercent:F1}%)", $"{clearGate.ceilingHits} ({clearGate.CeilingPercent:F1}%)");
            report.AppendLine();

            report.AppendLine("CLEAR / POP | A | B | C");
            AppendThreeWayComparison(report, "placements per clear", $"{baseline.PlacementsPerClear:F2}", $"{lightCuration.PlacementsPerClear:F2}", $"{clearGate.PlacementsPerClear:F2}");
            AppendThreeWayComparison(report, "clears/run", $"{baseline.MeanClears:F1}", $"{lightCuration.MeanClears:F1}", $"{clearGate.MeanClears:F1}");
            AppendThreeWayComparison(report, "average / P90 POP uses", $"{baseline.MeanPopUses:F2} / {baseline.P90PopUses:F0}", $"{lightCuration.MeanPopUses:F2} / {lightCuration.P90PopUses:F0}", $"{clearGate.MeanPopUses:F2} / {clearGate.P90PopUses:F0}");
            report.AppendLine();

            report.AppendLine("VARIANT C EARLY / MID / LATE");
            AppendPhase8DStageReport(report, clearGate, 0, "EARLY — generated trays 1–5");
            AppendPhase8DStageReport(report, clearGate, 1, "MID — generated trays 6–15");
            AppendPhase8DStageReport(report, clearGate, 2, "LATE — generated trays 16+");

            report.AppendLine("VARIANT C SELECTION ANALYSIS (generated C trays, before standard post-selection guarantees mutate the winner)");
            report.AppendLine($"- winning tray had an immediate clear: {clearGate.ImmediateClearTrayPercent:F1}%.");
            report.AppendLine($"- winning tray had Pure Setup without immediate clear: {clearGate.PureSetupWithoutImmediateClearTrayPercent:F1}%.");
            report.AppendLine($"- winner matched the best Phase 7H-score candidate: {clearGate.Phase7HPrimaryWinnerPercent:F1}%.");
            report.AppendLine($"- Light Curation changed the pre-guarantee ranking: {clearGate.CurationChangedRankingPercent:F1}%.");
            report.AppendLine($"- among ranking-changed winners: no immediate clear {clearGate.CurationChangedNoImmediateClearPercent:F1}% | immediate clear {clearGate.CurationChangedImmediateClearPercent:F1}%.");
            report.AppendLine();

            AppendOpenExamples(report, "VARIANT C — OPEN WINNERS WITH NO IMMEDIATE CLEAR", clearGate.OpenNoImmediateLightCurationExamples);
            AppendOpenExamples(report, "VARIANT C — OPEN WINNERS WITH AN IMMEDIATE CLEAR", clearGate.OpenImmediateLightCurationExamples);
            report.AppendLine("Performance: this analysis-only gate uses the already-computed immediate-clear count and occupancy state. It adds no board scan, candidate collection, LINQ, runtime allocation, GameObject, or production generator work.");
            report.AppendLine("This is deterministic heuristic-player evidence only. It does not apply any Phase 8 behavior to production and cannot replace future Unity Play Mode/device validation.");
            return report.ToString();
        }

        private static void AppendPhase8DStageReport(StringBuilder report, Aggregate aggregate, int stageIndex, string label)
        {
            StageTrayStats stage = aggregate.GetCurationStage(stageIndex);
            report.AppendLine($"C {label} (n={stage.Trays})");
            report.AppendLine($"- immediate clear: {stage.ImmediateClearPercent:F1}%; Pure Setup: {stage.PureSetupPercent:F1}%; healthy flexibility: {stage.Quality.HealthyFlexibilityPercent:F1}%; full coherence: {stage.Quality.CoherentFullSequencePercent:F1}%; avg occupancy: {stage.AverageOccupancy:F2}; avg piece size: {stage.AveragePieceSize:F2}.");
            report.AppendLine();
        }

        private static void AppendLightCurationStageReport(StringBuilder report, Aggregate aggregate, int stageIndex, string label)
        {
            StageTrayStats stage = aggregate.GetCurationStage(stageIndex);
            report.AppendLine($"VARIANT B {label} (n={stage.Trays})");
            report.AppendLine($"- avg piece size: {stage.AveragePieceSize:F2}; A / B / C / D: {stage.Quality.SatisfactionClassPercent(PieceSatisfactionClass.A):F1}% / {stage.Quality.SatisfactionClassPercent(PieceSatisfactionClass.B):F1}% / {stage.Quality.SatisfactionClassPercent(PieceSatisfactionClass.C):F1}% / {stage.Quality.SatisfactionClassPercent(PieceSatisfactionClass.D):F1}%.");
            report.AppendLine($"- healthy flexibility: {stage.Quality.HealthyFlexibilityPercent:F1}%; full coherence: {stage.Quality.CoherentFullSequencePercent:F1}%; Pure Setup: {stage.PureSetupPercent:F1}%; immediate clear: {stage.ImmediateClearPercent:F1}%.");
            report.AppendLine();
        }

        private static void AppendPhase8BStageReport(StringBuilder report, Aggregate aggregate, int stageIndex, string label)
        {
            StageTrayStats stage = aggregate.GetCurationStage(stageIndex);
            report.AppendLine($"VARIANT C {label} (n={stage.Trays})");
            report.AppendLine($"- A / B / C / D: {stage.Quality.SatisfactionClassPercent(PieceSatisfactionClass.A):F1}% / {stage.Quality.SatisfactionClassPercent(PieceSatisfactionClass.B):F1}% / {stage.Quality.SatisfactionClassPercent(PieceSatisfactionClass.C):F1}% / {stage.Quality.SatisfactionClassPercent(PieceSatisfactionClass.D):F1}%");
            report.AppendLine($"- avg piece size: {stage.AveragePieceSize:F2}; healthy flexibility: {stage.Quality.HealthyFlexibilityPercent:F1}%; two-piece / full coherence: {stage.Quality.CoherentTwoPiecePercent:F1}% / {stage.Quality.CoherentFullSequencePercent:F1}%; immediate clear: {stage.ImmediateClearPercent:F1}%; Pure Setup: {stage.PureSetupPercent:F1}%.");
            report.AppendLine();
        }

        private static void AppendPhase8BExamples(StringBuilder report, Aggregate aggregate, int stageIndex, string label)
        {
            report.AppendLine($"{label} (first {aggregate.GetCurationStageExamples(stageIndex).Count} selected generated trays)");
            IReadOnlyList<string> examples = aggregate.GetCurationStageExamples(stageIndex);
            for (int i = 0; i < examples.Count; i++)
            {
                report.AppendLine($"- {i + 1}. {examples[i]}");
            }
            report.AppendLine();
        }

        private static void AppendSelectionReasonReport(StringBuilder report, Aggregate a, Aggregate b, Aggregate c)
        {
            report.AppendLine("DOMINANT WINNING-TRAY SELECTION ADVANTAGE | A CURRENT | B PURE SETUP | C PURE + 2500 DIVERSITY");
            report.AppendLine("This is the largest score-term advantage of the final winner over the mean of its original 56 candidates; the setup column is legacy setup/payoff in A and pure setup/payoff in B/C.");
            AppendThreeWayComparison(report, "fit/fairness", Percent(a.SelectionReasonPercent(SelectionReason.FitFairness)), Percent(b.SelectionReasonPercent(SelectionReason.FitFairness)), Percent(c.SelectionReasonPercent(SelectionReason.FitFairness)));
            AppendThreeWayComparison(report, "Pure Setup / legacy setup-payoff", Percent(a.SelectionReasonPercent(SelectionReason.SetupPayoff)), Percent(b.SelectionReasonPercent(SelectionReason.SetupPayoff)), Percent(c.SelectionReasonPercent(SelectionReason.SetupPayoff)));
            AppendThreeWayComparison(report, "immediate clear", Percent(a.SelectionReasonPercent(SelectionReason.ImmediateClear)), Percent(b.SelectionReasonPercent(SelectionReason.ImmediateClear)), Percent(c.SelectionReasonPercent(SelectionReason.ImmediateClear)));
            AppendThreeWayComparison(report, "connectivity", Percent(a.SelectionReasonPercent(SelectionReason.Connectivity)), Percent(b.SelectionReasonPercent(SelectionReason.Connectivity)), Percent(c.SelectionReasonPercent(SelectionReason.Connectivity)));
            AppendThreeWayComparison(report, "line progress", Percent(a.SelectionReasonPercent(SelectionReason.LineProgress)), Percent(b.SelectionReasonPercent(SelectionReason.LineProgress)), Percent(c.SelectionReasonPercent(SelectionReason.LineProgress)));
            AppendThreeWayComparison(report, "relief", Percent(a.SelectionReasonPercent(SelectionReason.Relief)), Percent(b.SelectionReasonPercent(SelectionReason.Relief)), Percent(c.SelectionReasonPercent(SelectionReason.Relief)));
            AppendThreeWayComparison(report, "piece size", Percent(a.SelectionReasonPercent(SelectionReason.PieceSize)), Percent(b.SelectionReasonPercent(SelectionReason.PieceSize)), Percent(c.SelectionReasonPercent(SelectionReason.PieceSize)));
            AppendThreeWayComparison(report, "other", Percent(a.SelectionReasonPercent(SelectionReason.Other)), Percent(b.SelectionReasonPercent(SelectionReason.Other)), Percent(c.SelectionReasonPercent(SelectionReason.Other)));
            report.AppendLine();
        }

        private static void AppendOpenExamples(StringBuilder report, string label, IReadOnlyList<string> examples)
        {
            report.AppendLine($"REPRESENTATIVE OPEN-BOARD EXAMPLES — {label}");
            for (int i = 0; i < examples.Count; i++)
            {
                report.AppendLine($"- {i + 1}. {examples[i]}");
            }

            report.AppendLine();
        }

        private static void AppendThreeWayComparison(StringBuilder report, string label, string a, string b, string c)
        {
            report.AppendLine($"- {label}: {a} | {b} | {c}");
        }

        private static string Percent(float value)
        {
            return $"{value:F1}%";
        }

        private static string GetComebackBonusAssessment(Aggregate metrics)
        {
            if (metrics.MeanOccupancy > 27f
                || metrics.PressuredPercent + metrics.CriticalPercent > 20f
                || metrics.PlacementsPerClear > 3.2f
                || metrics.MedianPlacements < 55f
                || metrics.MeanPopUses > 2.5f)
            {
                return "TOO HARSH";
            }

            if (metrics.ImmediateClearTrayPercent > 70f
                && metrics.SetupWithoutImmediateClearTrayPercent < 30f
                && metrics.OpenPercent > 75f)
            {
                return "TOO GENEROUS";
            }

            return "PROMISING";
        }

        private static CandidatePoolStats CombineCandidateStats(Aggregate aggregate)
        {
            CandidatePoolStats combined = new CandidatePoolStats();
            for (int state = 0; state < 4; state++)
            {
                combined.Add(aggregate.GetRawCandidateStats((OccupancyState)state));
            }

            return combined;
        }

        private static void AppendCandidateStats(StringBuilder report, string label, CandidatePoolStats stats)
        {
            report.AppendLine($"{label} (n={stats.candidateCount}): any immediate {stats.AnyImmediateClearPercent:F1}%; 2+ immediate placements {stats.MultipleImmediatePlacementPercent:F1}%; immediate on 2+ pieces {stats.MultipleImmediatePiecePercent:F1}%; no immediate {stats.NoImmediateClearPercent:F1}%; setup/no-immediate {stats.SetupWithoutImmediateClearPercent:F1}%.");
        }

        private static void AppendPieceDistribution(StringBuilder report, PieceStats stats, string label)
        {
            report.AppendLine($"{label}: 1 / 2 / 3 / 4 / 5+ cells = {stats.SizePercent(0):F1}% / {stats.SizePercent(1):F1}% / {stats.SizePercent(2):F1}% / {stats.SizePercent(3):F1}% / {stats.SizePercent(4):F1}%.");
        }

        private static void AppendPieceStageDistribution(StringBuilder report, PieceStats stats, string label, int stage)
        {
            report.AppendLine($"{label}: 1 / 2 / 3 / 4 / 5+ cells = {stats.StageSizePercent(stage, 0):F1}% / {stats.StageSizePercent(stage, 1):F1}% / {stats.StageSizePercent(stage, 2):F1}% / {stats.StageSizePercent(stage, 3):F1}% / {stats.StageSizePercent(stage, 4):F1}%.");
        }

        private static void AppendScoreTerm(StringBuilder report, string label, TermRange term)
        {
            report.AppendLine($"- {label}: {term.Mean:F0} [{term.Min}..{term.Max}]");
        }

        private static void AppendPolicyComparison(StringBuilder report, Aggregate clearFirst, Aggregate balanced)
        {
            report.AppendLine("- metric: CURRENT clear-first | BALANCED human-like");
            report.AppendLine($"- one-line clear moves actually taken: {clearFirst.OneLineClearMovePercent:F1}% | {balanced.OneLineClearMovePercent:F1}%");
            report.AppendLine($"- average occupancy: {clearFirst.MeanOccupancy:F2} | {balanced.MeanOccupancy:F2}");
            report.AppendLine($"- placements between clears: {clearFirst.PlacementsPerClear:F2} | {balanced.PlacementsPerClear:F2}");
            report.AppendLine($"- mean run length: {clearFirst.MeanPlacements:F1} | {balanced.MeanPlacements:F1}");
            report.AppendLine($"- average POP uses: {clearFirst.MeanPopUses:F2} | {balanced.MeanPopUses:F2}");
        }

        private static void AppendGeneratorControl(StringBuilder report, Aggregate metrics, bool includeSetupWithoutImmediate)
        {
            report.AppendLine($"- immediate-clear trays: {metrics.ImmediateClearTrayPercent:F1}%; setup/payoff: {metrics.SetupPayoffTrayPercent:F1}%; average occupancy: {metrics.MeanOccupancy:F2}; placements between clears: {metrics.PlacementsPerClear:F2}; mean run: {metrics.MeanPlacements:F1}; censored: {metrics.CeilingPercent:F1}%.");
            if (includeSetupWithoutImmediate)
            {
                report.AppendLine($"- setup-without-immediate-clear trays: {metrics.SetupWithoutImmediateClearTrayPercent:F1}%; POP uses: {metrics.MeanPopUses:F2}.");
            }
        }

        private static string GetRootCauseClassification(
            Aggregate current,
            Aggregate balancedPlayer,
            Aggregate randomCandidate,
            Aggregate zeroImmediate,
            Aggregate setupFirst,
            CandidatePoolStats rawAll)
        {
            List<string> causes = new List<string>();
            float directClearScoreEffect = current.ImmediateClearTrayPercent - zeroImmediate.ImmediateClearTrayPercent;
            float smartSelectionEffect = current.ImmediateClearTrayPercent - randomCandidate.ImmediateClearTrayPercent;
            if (rawAll.AnyImmediateClearPercent >= 70f)
            {
                causes.Add("D raw 56-candidate pool has ubiquitous clear availability");
            }

            if (current.PostSelectionInjectedImmediateClearTrayPercent >= 8f
                || current.PostPostSelectionImmediateOpportunitiesPerTray - current.PrePostSelectionImmediateOpportunitiesPerTray >= 0.35f)
            {
                causes.Add("E post-selection guaranteed/rescue logic adds immediate-clear opportunities");
            }

            if (directClearScoreEffect >= 12f)
            {
                causes.Add("A immediate-clear score materially biases selection");
            }

            if (directClearScoreEffect < 5f && smartSelectionEffect >= 5f)
            {
                causes.Add("B setup/payoff and other non-immediate selection terms co-select immediate-clear trays");
            }

            if (directClearScoreEffect < 5f
                && current.SelectedScoreTerms.rescueRelief.Mean > current.SelectedScoreTerms.immediateClear.Mean * 2f)
            {
                causes.Add("E rescue/relief scoring (especially comeback clear opportunity) is the dominant indirect clear reward; post-selection injection itself is minor");
            }

            if (current.OneLineClearMovePercent - balancedPlayer.OneLineClearMovePercent >= 7f)
            {
                causes.Add("F clear-first simulated player materially amplifies clear consumption");
            }

            if (current.SelectedPieces.SizePercent(0) + current.SelectedPieces.SizePercent(1) >= 35f
                && (current.SelectedPieces.ImmediateClearPercent(0) + current.SelectedPieces.ImmediateClearPercent(1)) * 0.5f
                    >= current.SelectedPieces.ImmediateClearPercent(4) + 15f)
            {
                causes.Add("C small fillers disproportionately create immediate clear options");
            }

            if (setupFirst.SetupWithoutImmediateClearTrayPercent - zeroImmediate.SetupWithoutImmediateClearTrayPercent >= 8f)
            {
                causes.Add("B setup/payoff scoring can redirect selection when given enough separation");
            }

            return causes.Count == 0 ? "G mixed / no single dominant source in this sample" : string.Join("; ", causes);
        }

        private static string GetWeightSensitivityExplanation(Aggregate current, Aggregate zeroImmediate, CandidatePoolStats rawAll)
        {
            float selectedDelta = current.ImmediateClearTrayPercent - zeroImmediate.ImmediateClearTrayPercent;
            return $"The direct test is the stronger signal than a small weight delta: removing OPEN immediate-clear score changed selected immediate-clear trays by {selectedDelta:F1} points. Raw candidates contain any immediate clear {rawAll.AnyImmediateClearPercent:F1}% of the time, while fit/fairness and piece-mass terms have much larger score ranges; a -100 offset / +200 saturation adjustment only changes a small relative portion of the selection ranking when candidate availability and post-selection rules remain intact.";
        }

        private static string GetRecommendedNextLever(
            Aggregate current,
            Aggregate randomCandidate,
            Aggregate zeroImmediate,
            Aggregate setupFirst,
            CandidatePoolStats rawAll)
        {
            float directClearScoreEffect = current.ImmediateClearTrayPercent - zeroImmediate.ImmediateClearTrayPercent;
            float smartSelectionEffect = current.ImmediateClearTrayPercent - randomCandidate.ImmediateClearTrayPercent;
            if (directClearScoreEffect < 5f
                && smartSelectionEffect >= 5f
                && current.SelectedScoreTerms.rescueRelief.Mean > current.SelectedScoreTerms.immediateClear.Mean * 2f)
            {
                return "Run one focused analysis-only Variant E: for OPEN occupancy only, remove the `clearOpportunities * 1550f` component from the comeback bonus while retaining its setup, setup/payoff, fit, fairness, pressure, legal-move, and post-selection guarantees. Compare 300 matched runs. Do not alter PieceCatalog or global clear weights.";
            }

            if (rawAll.AnyImmediateClearPercent >= 70f && zeroImmediate.ImmediateClearTrayPercent >= current.ImmediateClearTrayPercent - 8f)
            {
                return "Run a new analysis-only OPEN selection constraint: when at least one fully fitting candidate has zero immediate clears and a setup/payoff opportunity, score only that subset; otherwise use the current selector. This directly tests converting free clears into setups without worsening legal-piece availability.";
            }

            if (zeroImmediate.ImmediateClearTrayPercent <= current.ImmediateClearTrayPercent - 12f)
            {
                return "Test a calibrated OPEN immediate-clear score reduction first; the zero-score control shows score selection, not raw availability, is the primary lever. Start the next analysis at 35% of the current OPEN clear weight, not a blanket penalty.";
            }

            if (randomCandidate.ImmediateClearTrayPercent < current.ImmediateClearTrayPercent - 12f)
            {
                return "Test a selection-only cap on OPEN immediate-clear opportunities per tray before touching PieceCatalog: require no more than one immediate-clear piece when a fully fitting setup tray exists.";
            }

            if (setupFirst.SetupWithoutImmediateClearTrayPercent > zeroImmediate.SetupWithoutImmediateClearTrayPercent + 8f)
            {
                return "Test a bounded OPEN setup-without-immediate-clear preference (candidate subset first, then current score), not larger global setup weights.";
            }

            return "Keep runtime values unchanged and collect device telemetry; this deterministic sample does not isolate a safe single runtime lever yet.";
        }

        private static string BuildVariantReport(Aggregate variantA, Aggregate variantB, TimeSpan elapsed)
        {
            StringBuilder report = new StringBuilder(3400);
            report.AppendLine("CHROMABLAST HEADLESS CLASSIC OPEN-CLEAR A/B STUDY");
            report.AppendLine("Pure-data editor analysis. No scenes, GameObjects, saves, audio, UI, particles, or haptics were used.");
            report.AppendLine($"Completed: {variantA.runCount} Variant A + {variantB.runCount} Variant B simulations in {elapsed.TotalSeconds:F1}s (matched seeds {SeedStart}-{SeedStart + variantA.runCount - 1}).");
            report.AppendLine($"Actual board/piece rules: {GameConstants.BoardSize}x{GameConstants.BoardSize}, PieceCatalog, {GameConstants.GuaranteedSetAttempts} candidate trays, current Classic pressure, current POP fatigue, current guaranteed-playability and rescue rules.");
            report.AppendLine("Player policy: identical competent-casual policy for both variants; clear-first, useful progress/open area/adjacency next, isolated holes discouraged, one bounded follow-up lookahead, deterministic placement noise, pressure-aware non-spam POP use.");
            report.AppendLine($"Variant A: runtime-equivalent OPEN immediate-clear offset {variantA.configuration.openImmediateClearOffset}, saturation penalty {variantA.configuration.openClearSaturationPenalty}.");
            report.AppendLine($"Variant B: analysis-only OPEN immediate-clear offset {variantB.configuration.openImmediateClearOffset}, saturation penalty {variantB.configuration.openClearSaturationPenalty}.");
            report.AppendLine();
            report.AppendLine("METRIC | VARIANT A CURRENT | VARIANT B REDUCED OPEN CLEAR");
            AppendComparison(report, "mean placements", variantA.MeanPlacements, variantB.MeanPlacements, "F1");
            AppendComparison(report, "median placements", variantA.MedianPlacements, variantB.MedianPlacements, "F1");
            AppendComparison(report, "P75 / P90 / P95 placements", $"{variantA.P75Placements:F1} / {variantA.P90Placements:F1} / {variantA.P95Placements:F1}", $"{variantB.P75Placements:F1} / {variantB.P90Placements:F1} / {variantB.P95Placements:F1}");
            AppendComparison(report, "longest completed", variantA.LongestCompletedRun, variantB.LongestCompletedRun);
            AppendComparison(report, "500-placement censored runs", $"{variantA.ceilingHits} ({variantA.CeilingPercent:F1}%)", $"{variantB.ceilingHits} ({variantB.CeilingPercent:F1}%)");
            report.AppendLine();
            AppendComparison(report, "average occupied cells", variantA.MeanOccupancy, variantB.MeanOccupancy, "F2");
            AppendComparison(report, "OPEN / BALANCED / PRESSURED / CRITICAL", $"{variantA.OpenPercent:F1}% / {variantA.BalancedPercent:F1}% / {variantA.PressuredPercent:F1}% / {variantA.CriticalPercent:F1}%", $"{variantB.OpenPercent:F1}% / {variantB.BalancedPercent:F1}% / {variantB.PressuredPercent:F1}% / {variantB.CriticalPercent:F1}%");
            report.AppendLine();
            AppendComparison(report, "placements between clears", variantA.PlacementsPerClear, variantB.PlacementsPerClear, "F2");
            AppendComparison(report, "immediate-clear tray frequency", $"{variantA.ImmediateClearTrayPercent:F1}%", $"{variantB.ImmediateClearTrayPercent:F1}%");
            AppendComparison(report, "setup/payoff tray frequency", $"{variantA.SetupPayoffTrayPercent:F1}%", $"{variantB.SetupPayoffTrayPercent:F1}%");
            AppendComparison(report, "multi-clear frequency", $"{variantA.MultiClearPercent:F1}%", $"{variantB.MultiClearPercent:F1}%");
            AppendComparison(report, "average clears/run", variantA.MeanClears, variantB.MeanClears, "F2");
            report.AppendLine();
            AppendComparison(report, "trays with multiple reasonable placements", $"{variantA.MultipleReasonablePlacementTrayPercent:F1}% (avg {variantA.MeanReasonablePlacementOptions:F1})", $"{variantB.MultipleReasonablePlacementTrayPercent:F1}% (avg {variantB.MeanReasonablePlacementOptions:F1})");
            AppendComparison(report, "completed trays mainly selected for immediate clear", $"{variantA.SelectedImmediateClearTrayPercent:F1}%", $"{variantB.SelectedImmediateClearTrayPercent:F1}%");
            AppendComparison(report, "completed trays mainly selected for setup/payoff", $"{variantA.SelectedSetupPayoffTrayPercent:F1}%", $"{variantB.SelectedSetupPayoffTrayPercent:F1}%");
            AppendComparison(report, "completed trays mainly selected for connectivity", $"{variantA.SelectedConnectivityTrayPercent:F1}%", $"{variantB.SelectedConnectivityTrayPercent:F1}%");
            AppendComparison(report, "relief-biased trays", $"{variantA.ReliefTrayPercent:F1}%", $"{variantB.ReliefTrayPercent:F1}%");
            report.AppendLine();
            AppendComparison(report, "average POP uses/run", variantA.MeanPopUses, variantB.MeanPopUses, "F2");
            AppendComparison(report, "median / P90 / max POP uses", $"{variantA.MedianPopUses:F1} / {variantA.P90PopUses:F1} / {variantA.MaxPopUses}", $"{variantB.MedianPopUses:F1} / {variantB.P90PopUses:F1} / {variantB.MaxPopUses}");
            AppendComparison(report, "placements between POPs", variantA.MeanPopInterval, variantB.MeanPopInterval, "F2");
            report.AppendLine();
            report.AppendLine($"Assessment A: {GetAssessment(variantA)}");
            report.AppendLine($"Assessment B: {GetAssessment(variantB)}");
            report.AppendLine($"Conclusion: {GetVariantConclusion(variantA, variantB)}");
            report.AppendLine("Performance: candidate count remained 56; editor-side generation caches remained per-tray and active. No runtime allocations or gameplay files were touched.");
            report.AppendLine("No runtime values were modified. Treat this as deterministic heuristic-player evidence, not a replacement for device playtesting.");
            return report.ToString();
        }

        private static void AppendComparison(StringBuilder report, string label, float valueA, float valueB, string format)
        {
            AppendComparison(report, label, valueA.ToString(format), valueB.ToString(format));
        }

        private static void AppendComparison(StringBuilder report, string label, int valueA, int valueB)
        {
            AppendComparison(report, label, valueA.ToString(), valueB.ToString());
        }

        private static void AppendComparison(StringBuilder report, string label, string valueA, string valueB)
        {
            report.AppendLine($"- {label}: {valueA} | {valueB}");
        }

        private static string GetAssessment(Aggregate metrics)
        {
            if (metrics.CeilingPercent > 10f
                || metrics.P95Placements >= 350f
                || (metrics.OpenPercent > 80f && metrics.ImmediateClearTrayPercent > 70f))
            {
                return "TOO EASY";
            }

            if (metrics.MedianPlacements < 45f
                || metrics.PressuredPercent + metrics.CriticalPercent > 60f
                || metrics.SetupPayoffTrayPercent < 5f)
            {
                return "TOO HARD";
            }

            return "PROMISING";
        }

        private static string GetVariantConclusion(Aggregate current, Aggregate reducedOpenClears)
        {
            bool tooHarsh = reducedOpenClears.MedianPlacements < 55f
                || reducedOpenClears.OpenPercent < 50f
                || reducedOpenClears.ImmediateClearTrayPercent < 30f
                || reducedOpenClears.SetupPayoffTrayPercent < 55f
                || reducedOpenClears.CeilingPercent > current.CeilingPercent + 2f;
            if (tooHarsh)
            {
                return "TEST INTERMEDIATE VALUE: OPEN offset -100 and saturation penalty 420. Variant B reduced the correct pressure, but crossed the clean-board safety threshold.";
            }

            bool targetBand = reducedOpenClears.OpenPercent >= 55f
                && reducedOpenClears.OpenPercent <= 70f
                && reducedOpenClears.BalancedPercent >= 25f
                && reducedOpenClears.BalancedPercent <= 40f
                && reducedOpenClears.ImmediateClearTrayPercent >= 35f
                && reducedOpenClears.ImmediateClearTrayPercent <= 55f
                && reducedOpenClears.SetupPayoffTrayPercent >= 60f
                && reducedOpenClears.SetupPayoffTrayPercent <= 85f
                && reducedOpenClears.CriticalPercent <= 3f
                && reducedOpenClears.CeilingPercent <= 1f;
            if (targetBand)
            {
                return "APPLY VARIANT B: OPEN offset -150 and saturation penalty 520.";
            }

            bool materiallyReducesOversupply = current.OpenPercent - reducedOpenClears.OpenPercent >= 5f
                || current.ImmediateClearTrayPercent - reducedOpenClears.ImmediateClearTrayPercent >= 8f
                || reducedOpenClears.PlacementsPerClear - current.PlacementsPerClear >= 0.35f;
            if (!materiallyReducesOversupply)
            {
                return "KEEP CURRENT: Variant B is safe but does not materially reduce OPEN-board clear oversupply; a weaker intermediate would not be justified.";
            }

            if (reducedOpenClears.CeilingPercent <= 1f)
            {
                return "TEST INTERMEDIATE VALUE: OPEN offset -175 and saturation penalty 620. Variant B moves in the intended direction but needs a measured additional reduction before any runtime change.";
            }

            return "KEEP CURRENT: Variant B did not improve open-board clear oversupply without a material tradeoff.";
        }

        private sealed class GenerationContext
        {
            private readonly Dictionary<string, PlacementProfile> profiles = new Dictionary<string, PlacementProfile>(32);
            private readonly Dictionary<SetupPayoffKey, int> payoffScores = new Dictionary<SetupPayoffKey, int>(128);
            private readonly Dictionary<TrioSequenceKey, TrioSequenceProfile> trioScores = new Dictionary<TrioSequenceKey, TrioSequenceProfile>(128);
            public readonly HeadlessBoard board;

            public GenerationContext(HeadlessBoard board)
            {
                this.board = board;
            }

            public PlacementProfile GetProfile(PieceInstance piece)
            {
                if (!profiles.TryGetValue(piece.shapeId, out PlacementProfile profile))
                {
                    profile = board.EvaluateGenerationProfile(piece);
                    profiles.Add(piece.shapeId, profile);
                }

                return profile;
            }

            public SetupPayoffAnalysis AnalyzeSetupPayoff(PieceInstance[] set)
            {
                SetupPayoffAnalysis analysis = default;
                for (int first = 0; first < set.Length; first++)
                {
                    PieceInstance setupPiece = set[first];
                    if (setupPiece == null)
                    {
                        continue;
                    }

                    PlacementProfile setupProfile = GetProfile(setupPiece);
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

                        PieceInstance payoffPiece = set[second];
                        int payoffScore = GetPairPayoffScore(setupPiece, setupProfile, payoffPiece);
                        analysis.currentScore = Mathf.Max(analysis.currentScore, payoffScore);

                        // A strict pure sequence requires both participating pieces to
                        // lack an independent immediate clear on the current board. The
                        // chosen setup origin is already non-clearing by construction.
                        PlacementProfile payoffProfile = GetProfile(payoffPiece);
                        if (setupProfile.clearOpportunities == 0
                            && payoffProfile.clearOpportunities == 0
                            && payoffScore > analysis.pureScore)
                        {
                            analysis.pureScore = payoffScore;
                            analysis.setupShapeId = setupPiece.shapeId;
                            analysis.payoffShapeId = payoffPiece.shapeId;
                            analysis.setupCells = setupPiece.Data.cells.Length;
                            analysis.payoffCells = payoffPiece.Data.cells.Length;
                        }
                    }
                }

                return analysis;
            }

            public int GetSetupPayoffOpportunity(PieceInstance[] set)
            {
                return AnalyzeSetupPayoff(set).currentScore;
            }

            public int GetPairPayoffScore(PieceInstance setupPiece, PieceInstance payoffPiece)
            {
                if (setupPiece == null || payoffPiece == null)
                {
                    return 0;
                }

                PlacementProfile setupProfile = GetProfile(setupPiece);
                return !setupProfile.hasSetupOrigin
                    ? 0
                    : GetPairPayoffScore(setupPiece, setupProfile, payoffPiece);
            }

            public TrioSequenceProfile GetTrioSequence(
                PieceInstance firstPiece,
                int firstX,
                int firstY,
                PieceInstance secondPiece,
                PieceInstance thirdPiece)
            {
                TrioSequenceKey key = new TrioSequenceKey(
                    firstPiece.shapeId,
                    secondPiece.shapeId,
                    thirdPiece.shapeId);
                if (!trioScores.TryGetValue(key, out TrioSequenceProfile profile))
                {
                    profile = board.EvaluateGenerationTrioSequence(
                        firstPiece,
                        firstX,
                        firstY,
                        secondPiece,
                        thirdPiece);
                    trioScores.Add(key, profile);
                }

                return profile;
            }

            private int GetPairPayoffScore(PieceInstance setupPiece, PlacementProfile setupProfile, PieceInstance payoffPiece)
            {
                SetupPayoffKey key = new SetupPayoffKey(setupPiece.shapeId, payoffPiece.shapeId);
                if (!payoffScores.TryGetValue(key, out int payoffScore))
                {
                    payoffScore = board.ScoreGenerationSetupPayoff(
                        setupPiece,
                        setupProfile.bestSetupX,
                        setupProfile.bestSetupY,
                        payoffPiece);
                    payoffScores.Add(key, payoffScore);
                }

                return payoffScore;
            }
        }

        private sealed class HeadlessBoard
        {
            private readonly int[] colors = new int[BoardCellCount];
            private readonly int[] rowFill = new int[GameConstants.BoardSize];
            private readonly int[] columnFill = new int[GameConstants.BoardSize];
            private readonly int[] generationTrioSecondX = new int[GenerationTrioSecondPlacementLimit];
            private readonly int[] generationTrioSecondY = new int[GenerationTrioSecondPlacementLimit];
            private readonly int[] generationTrioSecondScores = new int[GenerationTrioSecondPlacementLimit];
            private readonly int[] generationTrioSecondCompletedLines = new int[GenerationTrioSecondPlacementLimit];
            private int occupiedCount;

            public HeadlessBoard()
            {
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = -1;
                }
            }

            private HeadlessBoard(HeadlessBoard source)
            {
                Array.Copy(source.colors, colors, colors.Length);
                Array.Copy(source.rowFill, rowFill, rowFill.Length);
                Array.Copy(source.columnFill, columnFill, columnFill.Length);
                occupiedCount = source.occupiedCount;
            }

            public int OccupiedCount => occupiedCount;
            public int EmptyCount => BoardCellCount - occupiedCount;

            public int GetLineFillForFlow(bool row, int lineIndex)
            {
                if (lineIndex < 0 || lineIndex >= GameConstants.BoardSize)
                {
                    return 0;
                }

                return row ? rowFill[lineIndex] : columnFill[lineIndex];
            }

            public int GetBestFlowTargetAdvance(
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

                int existingFill = GetLineFillForFlow(row, lineIndex);
                int bestAdvance = 0;
                PieceData data = piece.Data;
                for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
                {
                    for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                    {
                        if (!CanPlace(piece, x, y))
                        {
                            continue;
                        }

                        int advance = row
                            ? CountPieceCellsInRow(piece, x, y, lineIndex)
                            : CountPieceCellsInColumn(piece, x, y, lineIndex);
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

            // Editor mirror of BoardManager.ScoreGenerationFlowTargetPayoff.
            // It confirms that a target-bound continuation can produce a real
            // follow-up clear, instead of counting loose shape compatibility.
            public int ScoreFlowTargetPayoff(
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

                SimFlowPlacement[] candidates = new SimFlowPlacement[3];
                for (int i = 0; i < candidates.Length; i++)
                {
                    candidates[i].score = int.MinValue;
                }

                int candidateCount = 0;
                PieceData continuationData = continuationPiece.Data;
                for (int y = 0; y <= GameConstants.BoardSize - continuationData.height; y++)
                {
                    for (int x = 0; x <= GameConstants.BoardSize - continuationData.width; x++)
                    {
                        if (!CanPlace(continuationPiece, x, y))
                        {
                            continue;
                        }

                        int advance = row
                            ? CountPieceCellsInRow(continuationPiece, x, y, lineIndex)
                            : CountPieceCellsInColumn(continuationPiece, x, y, lineIndex);
                        if (advance <= 0)
                        {
                            continue;
                        }

                        int score = advance * 10000 + ScorePlacementSetup(continuationPiece, x, y);
                        int insertAt = candidateCount < candidates.Length ? candidateCount : candidates.Length - 1;
                        while (insertAt > 0 && score > candidates[insertAt - 1].score)
                        {
                            if (insertAt < candidates.Length)
                            {
                                candidates[insertAt] = candidates[insertAt - 1];
                            }
                            insertAt--;
                        }

                        if (candidateCount >= candidates.Length && score <= candidates[insertAt].score)
                        {
                            continue;
                        }

                        candidates[insertAt] = new SimFlowPlacement { x = x, y = y, score = score };
                        if (candidateCount < candidates.Length)
                        {
                            candidateCount++;
                        }
                    }
                }

                int bestPayoff = 0;
                int existingFill = GetLineFillForFlow(row, lineIndex);
                for (int i = 0; i < candidateCount; i++)
                {
                    SimFlowPlacement candidate = candidates[i];
                    int payoff = ScoreGenerationSetupPayoff(
                        continuationPiece,
                        candidate.x,
                        candidate.y,
                        payoffPiece);
                    if (payoff <= bestPayoff)
                    {
                        continue;
                    }

                    bestPayoff = payoff;
                    continuationAdvance = row
                        ? CountPieceCellsInRow(continuationPiece, candidate.x, candidate.y, lineIndex)
                        : CountPieceCellsInColumn(continuationPiece, candidate.x, candidate.y, lineIndex);
                    continuationCompletesTarget = existingFill + continuationAdvance >= GameConstants.BoardSize;
                }

                return bestPayoff;
            }

            public int CountLinesAfterPlacementForFlow(PieceInstance piece, int originX, int originY)
            {
                return CountLinesAfterPlacement(piece, originX, originY, out _);
            }

            public int CountLargestOpenAreaForFlow()
            {
                return CountLargestOpenArea();
            }

            public int CountEmptyRegionsForFlow()
            {
                bool[] visited = new bool[BoardCellCount];
                int[] queue = new int[BoardCellCount];
                int regions = 0;
                for (int start = 0; start < BoardCellCount; start++)
                {
                    if (visited[start] || colors[start] >= 0)
                    {
                        continue;
                    }

                    regions++;
                    int read = 0;
                    int write = 0;
                    visited[start] = true;
                    queue[write++] = start;
                    while (read < write)
                    {
                        int index = queue[read++];
                        int x = index % GameConstants.BoardSize;
                        int y = index / GameConstants.BoardSize;
                        TryAddOpenNeighbor(x - 1, y, visited, queue, ref write);
                        TryAddOpenNeighbor(x + 1, y, visited, queue, ref write);
                        TryAddOpenNeighbor(x, y - 1, visited, queue, ref write);
                        TryAddOpenNeighbor(x, y + 1, visited, queue, ref write);
                    }
                }

                return regions;
            }

            public int CountLargestOpenRectangleForFlow()
            {
                int[] histogram = new int[GameConstants.BoardSize];
                int bestArea = 0;
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    for (int x = 0; x < GameConstants.BoardSize; x++)
                    {
                        histogram[x] = colors[Index(x, y)] >= 0 ? 0 : histogram[x] + 1;
                    }

                    for (int right = 0; right < GameConstants.BoardSize; right++)
                    {
                        int minHeight = int.MaxValue;
                        for (int left = right; left >= 0; left--)
                        {
                            minHeight = Mathf.Min(minHeight, histogram[left]);
                            bestArea = Mathf.Max(bestArea, minHeight * (right - left + 1));
                        }
                    }
                }

                return bestArea;
            }

            public int CountIsolatedHolesForFlow()
            {
                int holes = 0;
                for (int y = 1; y < GameConstants.BoardSize - 1; y++)
                {
                    for (int x = 1; x < GameConstants.BoardSize - 1; x++)
                    {
                        if (colors[Index(x, y)] >= 0)
                        {
                            continue;
                        }

                        if (colors[Index(x - 1, y)] >= 0
                            && colors[Index(x + 1, y)] >= 0
                            && colors[Index(x, y - 1)] >= 0
                            && colors[Index(x, y + 1)] >= 0)
                        {
                            holes++;
                        }
                    }
                }

                return holes;
            }

            public int CountNarrowCorridorCellsForFlow()
            {
                int corridors = 0;
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    for (int x = 0; x < GameConstants.BoardSize; x++)
                    {
                        if (colors[Index(x, y)] >= 0)
                        {
                            continue;
                        }

                        int emptyNeighbours = 0;
                        emptyNeighbours += IsInside(x - 1, y) && colors[Index(x - 1, y)] < 0 ? 1 : 0;
                        emptyNeighbours += IsInside(x + 1, y) && colors[Index(x + 1, y)] < 0 ? 1 : 0;
                        emptyNeighbours += IsInside(x, y - 1) && colors[Index(x, y - 1)] < 0 ? 1 : 0;
                        emptyNeighbours += IsInside(x, y + 1) && colors[Index(x, y + 1)] < 0 ? 1 : 0;
                        if (emptyNeighbours <= 2)
                        {
                            corridors++;
                        }
                    }
                }

                return corridors;
            }

            public int CountFutureFlowOptions()
            {
                int options = 0;
                for (int i = 0; i < SimFlowFutureShapeIds.Length; i++)
                {
                    PieceData data = PieceCatalog.Get(SimFlowFutureShapeIds[i]);
                    bool fits = false;
                    for (int y = 0; y <= GameConstants.BoardSize - data.height && !fits; y++)
                    {
                        for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                        {
                            if (!CanPlace(new PieceInstance(data.id, ChromaColor.Cyan), x, y))
                            {
                                continue;
                            }

                            fits = true;
                            break;
                        }
                    }

                    options += fits ? 1 : 0;
                }

                return options;
            }

            public bool CanPlace(PieceInstance piece, int originX, int originY)
            {
                PieceData data = piece.Data;
                for (int i = 0; i < data.cells.Length; i++)
                {
                    int x = originX + data.cells[i].x;
                    int y = originY + data.cells[i].y;
                    if (!IsInside(x, y) || colors[Index(x, y)] >= 0)
                    {
                        return false;
                    }
                }

                return true;
            }

            public int CountPlacementOptions(PieceInstance piece)
            {
                int options = 0;
                PieceData data = piece.Data;
                for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
                {
                    for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                    {
                        if (CanPlace(piece, x, y))
                        {
                            options++;
                        }
                    }
                }

                return options;
            }

            public PlacementProfile EvaluateGenerationProfile(PieceInstance piece)
            {
                PlacementProfile profile = default;
                PieceData data = piece.Data;
                int bestSetupSelectionScore = int.MinValue;
                for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
                {
                    for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                    {
                        if (!CanPlace(piece, x, y))
                        {
                            continue;
                        }

                        profile.placementOptions++;
                        int adjacencyContacts = CountExistingOrthogonalContacts(piece, x, y);
                        int completedLines = 0;
                        int lineProgress = 0;
                        int setupScore = 0;
                        for (int line = 0; line < GameConstants.BoardSize; line++)
                        {
                            int rowAdd = CountPieceCellsInRow(piece, x, y, line);
                            if (rowAdd > 0)
                            {
                                int afterFill = rowFill[line] + rowAdd;
                                completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                                lineProgress += ScoreGenerationLineProgress(rowFill[line], afterFill);
                                setupScore += ScoreLineFill(afterFill);
                            }

                            int columnAdd = CountPieceCellsInColumn(piece, x, y, line);
                            if (columnAdd > 0)
                            {
                                int afterFill = columnFill[line] + columnAdd;
                                completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                                lineProgress += ScoreGenerationLineProgress(columnFill[line], afterFill);
                                setupScore += ScoreLineFill(afterFill);
                            }
                        }

                        int isolatedHoles = CountIsolatedHolesAfterPlacement(piece, x, y);
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
                            profile.bestSetupX = x;
                            profile.bestSetupY = y;
                        }
                    }
                }

                return profile;
            }

            public int ScoreGenerationSetupPayoff(PieceInstance setupPiece, int setupX, int setupY, PieceInstance payoffPiece)
            {
                if (!CanPlace(setupPiece, setupX, setupY))
                {
                    return 0;
                }

                PieceData payoffData = payoffPiece.Data;
                int bestScore = 0;
                for (int y = 0; y <= GameConstants.BoardSize - payoffData.height; y++)
                {
                    for (int x = 0; x <= GameConstants.BoardSize - payoffData.width; x++)
                    {
                        if (!CanPlaceAfterVirtualPlacement(setupPiece, setupX, setupY, payoffPiece, x, y))
                        {
                            continue;
                        }

                        int completedLines = 0;
                        int lineProgress = 0;
                        for (int line = 0; line < GameConstants.BoardSize; line++)
                        {
                            int rowAdd = CountPieceCellsInRow(setupPiece, setupX, setupY, line)
                                + CountPieceCellsInRow(payoffPiece, x, y, line);
                            if (rowAdd > 0)
                            {
                                int afterFill = rowFill[line] + rowAdd;
                                completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                                lineProgress += ScoreGenerationLineProgress(rowFill[line], afterFill);
                            }

                            int columnAdd = CountPieceCellsInColumn(setupPiece, setupX, setupY, line)
                                + CountPieceCellsInColumn(payoffPiece, x, y, line);
                            if (columnAdd > 0)
                            {
                                int afterFill = columnFill[line] + columnAdd;
                                completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                                lineProgress += ScoreGenerationLineProgress(columnFill[line], afterFill);
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

            public TrioSequenceProfile EvaluateGenerationTrioSequence(
                PieceInstance firstPiece,
                int firstX,
                int firstY,
                PieceInstance secondPiece,
                PieceInstance thirdPiece)
            {
                TrioSequenceProfile result = default;
                if (firstPiece == null
                    || secondPiece == null
                    || thirdPiece == null
                    || !CanPlace(firstPiece, firstX, firstY))
                {
                    return result;
                }

                for (int i = 0; i < GenerationTrioSecondPlacementLimit; i++)
                {
                    generationTrioSecondScores[i] = int.MinValue;
                }

                PieceData secondData = secondPiece.Data;
                PieceData thirdData = thirdPiece.Data;
                for (int secondY = 0; secondY <= GameConstants.BoardSize - secondData.height; secondY++)
                {
                    for (int secondX = 0; secondX <= GameConstants.BoardSize - secondData.width; secondX++)
                    {
                        if (!CanPlaceAfterVirtualPlacement(firstPiece, firstX, firstY, secondPiece, secondX, secondY))
                        {
                            continue;
                        }

                        result.secondPlacementOptions++;
                        int completedLines = 0;
                        int lineProgress = 0;
                        for (int line = 0; line < GameConstants.BoardSize; line++)
                        {
                            int rowAdd = CountPieceCellsInRow(firstPiece, firstX, firstY, line)
                                + CountPieceCellsInRow(secondPiece, secondX, secondY, line);
                            if (rowAdd > 0)
                            {
                                int afterFill = rowFill[line] + rowAdd;
                                completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                                lineProgress += ScoreGenerationLineProgress(rowFill[line], afterFill);
                            }

                            int columnAdd = CountPieceCellsInColumn(firstPiece, firstX, firstY, line)
                                + CountPieceCellsInColumn(secondPiece, secondX, secondY, line);
                            if (columnAdd > 0)
                            {
                                int afterFill = columnFill[line] + columnAdd;
                                completedLines += afterFill >= GameConstants.BoardSize ? 1 : 0;
                                lineProgress += ScoreGenerationLineProgress(columnFill[line], afterFill);
                            }
                        }

                        InsertGenerationTrioSecondPlacement(
                            secondX,
                            secondY,
                            completedLines * 420 + lineProgress * 5,
                            completedLines);
                    }
                }

                int bestScore = int.MinValue;
                for (int secondIndex = 0; secondIndex < GenerationTrioSecondPlacementLimit; secondIndex++)
                {
                    if (generationTrioSecondScores[secondIndex] == int.MinValue)
                    {
                        break;
                    }

                    int thirdOptions = 0;
                    for (int thirdY = 0; thirdY <= GameConstants.BoardSize - thirdData.height; thirdY++)
                    {
                        for (int thirdX = 0; thirdX <= GameConstants.BoardSize - thirdData.width; thirdX++)
                        {
                            if (CanPlaceAfterTwoVirtualPlacements(
                                    firstPiece,
                                    firstX,
                                    firstY,
                                    secondPiece,
                                    generationTrioSecondX[secondIndex],
                                    generationTrioSecondY[secondIndex],
                                    thirdPiece,
                                    thirdX,
                                    thirdY))
                            {
                                thirdOptions++;
                            }
                        }
                    }

                    int sequenceScore = generationTrioSecondScores[secondIndex] + thirdOptions * 24;
                    if (sequenceScore > bestScore)
                    {
                        bestScore = sequenceScore;
                        result.score = sequenceScore;
                        result.thirdPlacementOptions = thirdOptions;
                        result.completedLines = generationTrioSecondCompletedLines[secondIndex];
                        result.hasCoherentSequence = thirdOptions > 0;
                    }
                }

                return result;
            }

            private void InsertGenerationTrioSecondPlacement(int x, int y, int score, int completedLines)
            {
                if (score <= generationTrioSecondScores[GenerationTrioSecondPlacementLimit - 1])
                {
                    return;
                }

                int insertIndex = GenerationTrioSecondPlacementLimit - 1;
                while (insertIndex > 0 && score > generationTrioSecondScores[insertIndex - 1])
                {
                    generationTrioSecondX[insertIndex] = generationTrioSecondX[insertIndex - 1];
                    generationTrioSecondY[insertIndex] = generationTrioSecondY[insertIndex - 1];
                    generationTrioSecondScores[insertIndex] = generationTrioSecondScores[insertIndex - 1];
                    generationTrioSecondCompletedLines[insertIndex] = generationTrioSecondCompletedLines[insertIndex - 1];
                    insertIndex--;
                }

                generationTrioSecondX[insertIndex] = x;
                generationTrioSecondY[insertIndex] = y;
                generationTrioSecondScores[insertIndex] = score;
                generationTrioSecondCompletedLines[insertIndex] = completedLines;
            }

            public int ScorePlacementSetup(PieceInstance piece, int originX, int originY)
            {
                return CountLinesAfterPlacement(piece, originX, originY, out _) * 120
                    + ScoreNearLinesAfterPlacement(piece, originX, originY);
            }

            public PlacementEvaluation EvaluatePlacement(PieceInstance piece, int originX, int originY)
            {
                int lines = CountLinesAfterPlacement(piece, originX, originY, out _);
                int lineProgress = ScoreNearLinesAfterPlacement(piece, originX, originY);
                int adjacency = CountExistingOrthogonalContacts(piece, originX, originY);
                int isolated = CountIsolatedHolesAfterPlacement(piece, originX, originY);
                int clearCells = CountCellsClearedAfterPlacement(piece, originX, originY);
                int occupiedAfterClear = occupiedCount + piece.Data.cells.Length - clearCells;
                HeadlessBoard clone = CloneAfterPlacement(piece, originX, originY);
                return new PlacementEvaluation
                {
                    lines = lines,
                    lineProgress = lineProgress,
                    adjacencyContacts = adjacency,
                    isolatedHoles = isolated,
                    occupiedAfterClear = occupiedAfterClear,
                    largestOpenArea = clone.CountLargestOpenArea()
                };
            }

            public HeadlessBoard CloneAfterPlacement(PieceInstance piece, int originX, int originY)
            {
                HeadlessBoard clone = new HeadlessBoard(this);
                clone.PlaceAndResolve(piece, originX, originY);
                return clone;
            }

            public ClearOutcome PlaceAndResolve(PieceInstance piece, int originX, int originY)
            {
                PieceData data = piece.Data;
                for (int i = 0; i < data.cells.Length; i++)
                {
                    int x = originX + data.cells[i].x;
                    int y = originY + data.cells[i].y;
                    SetCell(x, y, (int)piece.color);
                }

                bool[] rows = new bool[GameConstants.BoardSize];
                bool[] columns = new bool[GameConstants.BoardSize];
                ClearOutcome outcome = new ClearOutcome();
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    rows[y] = rowFill[y] == GameConstants.BoardSize;
                    if (rows[y])
                    {
                        outcome.lines++;
                        AddPureLineIfNeeded(outcome, true, y);
                    }
                }

                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    columns[x] = columnFill[x] == GameConstants.BoardSize;
                    if (columns[x])
                    {
                        outcome.lines++;
                        AddPureLineIfNeeded(outcome, false, x);
                    }
                }

                if (outcome.lines == 0)
                {
                    return outcome;
                }

                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    for (int x = 0; x < GameConstants.BoardSize; x++)
                    {
                        if (!rows[y] && !columns[x])
                        {
                            continue;
                        }

                        int color = colors[Index(x, y)];
                        if (color >= 0)
                        {
                            outcome.cellsCleared++;
                            outcome.clearedByColor[color]++;
                            SetCell(x, y, -1);
                        }
                    }
                }

                return outcome;
            }

            public int PopColor(ChromaColor color)
            {
                int popped = 0;
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    for (int x = 0; x < GameConstants.BoardSize; x++)
                    {
                        if (colors[Index(x, y)] == (int)color)
                        {
                            SetCell(x, y, -1);
                            popped++;
                        }
                    }
                }

                return popped;
            }

            public int CountColor(ChromaColor color)
            {
                int count = 0;
                for (int i = 0; i < colors.Length; i++)
                {
                    if (colors[i] == (int)color)
                    {
                        count++;
                    }
                }

                return count;
            }

            private int CountLinesAfterPlacement(PieceInstance piece, int originX, int originY, out int pureLines)
            {
                int lines = 0;
                pureLines = 0;
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    if (WouldRowBeFullAfterPlacement(y, piece, originX, originY))
                    {
                        lines++;
                        if (WouldRowBePureAfterPlacement(y, piece, originX, originY))
                        {
                            pureLines++;
                        }
                    }
                }

                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    if (WouldColumnBeFullAfterPlacement(x, piece, originX, originY))
                    {
                        lines++;
                        if (WouldColumnBePureAfterPlacement(x, piece, originX, originY))
                        {
                            pureLines++;
                        }
                    }
                }

                return lines;
            }

            private int CountCellsClearedAfterPlacement(PieceInstance piece, int originX, int originY)
            {
                bool[] rows = new bool[GameConstants.BoardSize];
                bool[] columns = new bool[GameConstants.BoardSize];
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    rows[y] = WouldRowBeFullAfterPlacement(y, piece, originX, originY);
                }

                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    columns[x] = WouldColumnBeFullAfterPlacement(x, piece, originX, originY);
                }

                int cells = 0;
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    for (int x = 0; x < GameConstants.BoardSize; x++)
                    {
                        if (rows[y] || columns[x])
                        {
                            cells++;
                        }
                    }
                }

                return cells;
            }

            private bool WouldRowBeFullAfterPlacement(int y, PieceInstance piece, int originX, int originY)
            {
                return rowFill[y] + CountPieceCellsInRow(piece, originX, originY, y) >= GameConstants.BoardSize;
            }

            private bool WouldColumnBeFullAfterPlacement(int x, PieceInstance piece, int originX, int originY)
            {
                return columnFill[x] + CountPieceCellsInColumn(piece, originX, originY, x) >= GameConstants.BoardSize;
            }

            private bool WouldRowBePureAfterPlacement(int y, PieceInstance piece, int originX, int originY)
            {
                int firstColor = -1;
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    int color = GetColorAfterPlacement(x, y, piece, originX, originY);
                    if (color < 0)
                    {
                        return false;
                    }

                    if (firstColor < 0)
                    {
                        firstColor = color;
                    }
                    else if (color != firstColor)
                    {
                        return false;
                    }
                }

                return firstColor >= 0;
            }

            private bool WouldColumnBePureAfterPlacement(int x, PieceInstance piece, int originX, int originY)
            {
                int firstColor = -1;
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    int color = GetColorAfterPlacement(x, y, piece, originX, originY);
                    if (color < 0)
                    {
                        return false;
                    }

                    if (firstColor < 0)
                    {
                        firstColor = color;
                    }
                    else if (color != firstColor)
                    {
                        return false;
                    }
                }

                return firstColor >= 0;
            }

            private int GetColorAfterPlacement(int x, int y, PieceInstance piece, int originX, int originY)
            {
                int current = colors[Index(x, y)];
                return current >= 0
                    ? current
                    : PieceOccupiesCell(piece, originX, originY, x, y) ? (int)piece.color : -1;
            }

            private bool CanPlaceAfterVirtualPlacement(PieceInstance setupPiece, int setupX, int setupY, PieceInstance payoffPiece, int payoffX, int payoffY)
            {
                PieceData data = payoffPiece.Data;
                for (int i = 0; i < data.cells.Length; i++)
                {
                    int x = payoffX + data.cells[i].x;
                    int y = payoffY + data.cells[i].y;
                    if (!IsInside(x, y)
                        || colors[Index(x, y)] >= 0
                        || PieceOccupiesCell(setupPiece, setupX, setupY, x, y))
                    {
                        return false;
                    }
                }

                return true;
            }

            private bool CanPlaceAfterTwoVirtualPlacements(
                PieceInstance firstPiece,
                int firstX,
                int firstY,
                PieceInstance secondPiece,
                int secondX,
                int secondY,
                PieceInstance candidatePiece,
                int candidateX,
                int candidateY)
            {
                PieceData data = candidatePiece.Data;
                for (int i = 0; i < data.cells.Length; i++)
                {
                    int x = candidateX + data.cells[i].x;
                    int y = candidateY + data.cells[i].y;
                    if (!IsInside(x, y)
                        || IsOccupied(x, y)
                        || PieceOccupiesCell(firstPiece, firstX, firstY, x, y)
                        || PieceOccupiesCell(secondPiece, secondX, secondY, x, y))
                    {
                        return false;
                    }
                }

                return true;
            }

            private int ScoreNearLinesAfterPlacement(PieceInstance piece, int originX, int originY)
            {
                int score = 0;
                for (int line = 0; line < GameConstants.BoardSize; line++)
                {
                    int rowAdd = CountPieceCellsInRow(piece, originX, originY, line);
                    if (rowAdd > 0)
                    {
                        score += ScoreLineFill(rowFill[line] + rowAdd);
                    }

                    int columnAdd = CountPieceCellsInColumn(piece, originX, originY, line);
                    if (columnAdd > 0)
                    {
                        score += ScoreLineFill(columnFill[line] + columnAdd);
                    }
                }

                return score;
            }

            private int CountExistingOrthogonalContacts(PieceInstance piece, int originX, int originY)
            {
                int contacts = 0;
                PieceData data = piece.Data;
                for (int i = 0; i < data.cells.Length; i++)
                {
                    int x = originX + data.cells[i].x;
                    int y = originY + data.cells[i].y;
                    contacts += IsOccupied(x - 1, y) ? 1 : 0;
                    contacts += IsOccupied(x + 1, y) ? 1 : 0;
                    contacts += IsOccupied(x, y - 1) ? 1 : 0;
                    contacts += IsOccupied(x, y + 1) ? 1 : 0;
                }

                return contacts;
            }

            private int CountIsolatedHolesAfterPlacement(PieceInstance piece, int originX, int originY)
            {
                int holes = 0;
                for (int y = 1; y < GameConstants.BoardSize - 1; y++)
                {
                    for (int x = 1; x < GameConstants.BoardSize - 1; x++)
                    {
                        if (colors[Index(x, y)] >= 0 || PieceOccupiesCell(piece, originX, originY, x, y))
                        {
                            continue;
                        }

                        if (IsOccupiedAfterPlacement(piece, originX, originY, x - 1, y)
                            && IsOccupiedAfterPlacement(piece, originX, originY, x + 1, y)
                            && IsOccupiedAfterPlacement(piece, originX, originY, x, y - 1)
                            && IsOccupiedAfterPlacement(piece, originX, originY, x, y + 1))
                        {
                            holes++;
                        }
                    }
                }

                return holes;
            }

            private bool IsOccupiedAfterPlacement(PieceInstance piece, int originX, int originY, int x, int y)
            {
                return IsOccupied(x, y) || PieceOccupiesCell(piece, originX, originY, x, y);
            }

            private int CountLargestOpenArea()
            {
                bool[] visited = new bool[BoardCellCount];
                int largest = 0;
                int[] queue = new int[BoardCellCount];
                for (int start = 0; start < BoardCellCount; start++)
                {
                    if (visited[start] || colors[start] >= 0)
                    {
                        continue;
                    }

                    int write = 0;
                    int read = 0;
                    queue[write++] = start;
                    visited[start] = true;
                    while (read < write)
                    {
                        int index = queue[read++];
                        int x = index % GameConstants.BoardSize;
                        int y = index / GameConstants.BoardSize;
                        TryAddOpenNeighbor(x - 1, y, visited, queue, ref write);
                        TryAddOpenNeighbor(x + 1, y, visited, queue, ref write);
                        TryAddOpenNeighbor(x, y - 1, visited, queue, ref write);
                        TryAddOpenNeighbor(x, y + 1, visited, queue, ref write);
                    }

                    largest = Mathf.Max(largest, write);
                }

                return largest;
            }

            private void TryAddOpenNeighbor(int x, int y, bool[] visited, int[] queue, ref int write)
            {
                if (!IsInside(x, y))
                {
                    return;
                }

                int index = Index(x, y);
                if (visited[index] || colors[index] >= 0)
                {
                    return;
                }

                visited[index] = true;
                queue[write++] = index;
            }

            private void AddPureLineIfNeeded(ClearOutcome outcome, bool row, int index)
            {
                int firstColor = -1;
                for (int step = 0; step < GameConstants.BoardSize; step++)
                {
                    int color = row ? colors[Index(step, index)] : colors[Index(index, step)];
                    if (color < 0)
                    {
                        return;
                    }

                    if (firstColor < 0)
                    {
                        firstColor = color;
                    }
                    else if (color != firstColor)
                    {
                        return;
                    }
                }

                if (firstColor >= 0)
                {
                    outcome.pureLines++;
                    outcome.pureLinesByColor[firstColor]++;
                }
            }

            private void SetCell(int x, int y, int color)
            {
                int index = Index(x, y);
                int previous = colors[index];
                if (previous == color)
                {
                    return;
                }

                if (previous >= 0)
                {
                    occupiedCount--;
                    rowFill[y]--;
                    columnFill[x]--;
                }

                colors[index] = color;
                if (color >= 0)
                {
                    occupiedCount++;
                    rowFill[y]++;
                    columnFill[x]++;
                }
            }

            private bool IsOccupied(int x, int y)
            {
                return IsInside(x, y) && colors[Index(x, y)] >= 0;
            }

            private static int CountPieceCellsInRow(PieceInstance piece, int originX, int originY, int row)
            {
                int count = 0;
                Vector2Int[] cells = piece.Data.cells;
                for (int i = 0; i < cells.Length; i++)
                {
                    if (originY + cells[i].y == row)
                    {
                        count++;
                    }
                }

                return count;
            }

            private static int CountPieceCellsInColumn(PieceInstance piece, int originX, int originY, int column)
            {
                int count = 0;
                Vector2Int[] cells = piece.Data.cells;
                for (int i = 0; i < cells.Length; i++)
                {
                    if (originX + cells[i].x == column)
                    {
                        count++;
                    }
                }

                return count;
            }

            private static bool PieceOccupiesCell(PieceInstance piece, int originX, int originY, int boardX, int boardY)
            {
                Vector2Int[] cells = piece.Data.cells;
                for (int i = 0; i < cells.Length; i++)
                {
                    if (originX + cells[i].x == boardX && originY + cells[i].y == boardY)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static int ScoreGenerationLineProgress(int beforeFill, int afterFill)
            {
                int beforeProgress = Mathf.Max(0, beforeFill - 4);
                int afterProgress = Mathf.Max(0, afterFill - 4);
                return Mathf.Max(0, afterProgress * afterProgress - beforeProgress * beforeProgress);
            }

            private static int ScoreLineFill(int filledCells)
            {
                if (filledCells >= GameConstants.BoardSize) return 90;
                if (filledCells == GameConstants.BoardSize - 1) return 54;
                if (filledCells == GameConstants.BoardSize - 2) return 22;
                return filledCells == GameConstants.BoardSize - 3 ? 8 : 0;
            }

            private static bool IsInside(int x, int y)
            {
                return x >= 0 && x < GameConstants.BoardSize && y >= 0 && y < GameConstants.BoardSize;
            }

            private static int Index(int x, int y)
            {
                return y * GameConstants.BoardSize + x;
            }
        }

        private readonly struct SimulationConfiguration
        {
            public readonly string name;
            public readonly bool pressureEnabled;
            public readonly bool popFatigueEnabled;
            public readonly bool reliefLoopEnabled;
            // Analysis-only equivalents of PieceSpawner's OPEN-board values.
            public readonly int openImmediateClearOffset;
            public readonly int openClearSaturationPenalty;
            public readonly bool openImmediateClearScoreEnabled;
            public readonly float openSetupPayoffMultiplier;
            // Analysis-only override for the clear-opportunity component of the
            // comeback bonus. Non-OPEN states deliberately stay at the current 1550f.
            public readonly float openComebackClearOpportunityWeight;
            // Phase 7G editor-only controls. They never exist in runtime PieceSpawner.
            public readonly bool pureSetupScoringOnOpen;
            public readonly int openPureSetupDiversityBonus;
            public readonly CandidateSelectionMode candidateSelectionMode;
            public readonly SimulatedPlayerPolicy playerPolicy;
            public readonly bool satisfactionCurationEnabled;
            public readonly bool satisfactionPostSelectionGuardsEnabled;
            public readonly bool curatedTargetMassEnabled;
            public readonly float shapeSatisfactionMultiplier;
            public readonly float cClassPenaltyMultiplier;
            public readonly float dClassPenaltyMultiplier;
            public readonly float fourCellPreferenceMultiplier;
            public readonly float satisfyingLargePenaltyMultiplier;
            public readonly float threeLargePenaltyMultiplier;
            public readonly float pureSetupCurationMultiplier;
            public readonly float trioCoherenceMultiplier;
            public readonly float fullSequenceBonusMultiplier;
            public readonly float flexibilityBonusMultiplier;
            public readonly bool pressureAwareCuration;
            public readonly bool perfectCurationStreakBreakerEnabled;
            public readonly bool immediateClearCurationGateEnabled;
            public readonly float perfectCurationStreakBreakerMultiplier;
            public readonly float pressureCurationMidLowMultiplier;
            public readonly float pressureCurationMidHighMultiplier;
            public readonly float pressureCurationLateMultiplier;

            public SimulationConfiguration(
                string name,
                bool pressureEnabled,
                bool popFatigueEnabled,
                bool reliefLoopEnabled,
                int openImmediateClearOffset,
                int openClearSaturationPenalty,
                bool openImmediateClearScoreEnabled,
                float openSetupPayoffMultiplier,
                float openComebackClearOpportunityWeight,
                bool pureSetupScoringOnOpen,
                int openPureSetupDiversityBonus,
                CandidateSelectionMode candidateSelectionMode,
                SimulatedPlayerPolicy playerPolicy,
                bool satisfactionCurationEnabled = false,
                bool satisfactionPostSelectionGuardsEnabled = true,
                bool curatedTargetMassEnabled = true,
                float shapeSatisfactionMultiplier = 1f,
                float cClassPenaltyMultiplier = 1f,
                float dClassPenaltyMultiplier = 1f,
                float fourCellPreferenceMultiplier = 1f,
                float satisfyingLargePenaltyMultiplier = 1f,
                float threeLargePenaltyMultiplier = 1f,
                float pureSetupCurationMultiplier = 1f,
                float trioCoherenceMultiplier = 1f,
                float fullSequenceBonusMultiplier = 1f,
                float flexibilityBonusMultiplier = 1f,
                bool pressureAwareCuration = false,
                bool perfectCurationStreakBreakerEnabled = false,
                bool immediateClearCurationGateEnabled = false,
                float perfectCurationStreakBreakerMultiplier = 0.70f,
                float pressureCurationMidLowMultiplier = 0.85f,
                float pressureCurationMidHighMultiplier = 0.70f,
                float pressureCurationLateMultiplier = 0.55f)
            {
                this.name = name;
                this.pressureEnabled = pressureEnabled;
                this.popFatigueEnabled = popFatigueEnabled;
                this.reliefLoopEnabled = reliefLoopEnabled;
                this.openImmediateClearOffset = openImmediateClearOffset;
                this.openClearSaturationPenalty = openClearSaturationPenalty;
                this.openImmediateClearScoreEnabled = openImmediateClearScoreEnabled;
                this.openSetupPayoffMultiplier = openSetupPayoffMultiplier;
                this.openComebackClearOpportunityWeight = openComebackClearOpportunityWeight;
                this.pureSetupScoringOnOpen = pureSetupScoringOnOpen;
                this.openPureSetupDiversityBonus = openPureSetupDiversityBonus;
                this.candidateSelectionMode = candidateSelectionMode;
                this.playerPolicy = playerPolicy;
                this.satisfactionCurationEnabled = satisfactionCurationEnabled;
                this.satisfactionPostSelectionGuardsEnabled = satisfactionPostSelectionGuardsEnabled;
                this.curatedTargetMassEnabled = curatedTargetMassEnabled;
                this.shapeSatisfactionMultiplier = shapeSatisfactionMultiplier;
                this.cClassPenaltyMultiplier = cClassPenaltyMultiplier;
                this.dClassPenaltyMultiplier = dClassPenaltyMultiplier;
                this.fourCellPreferenceMultiplier = fourCellPreferenceMultiplier;
                this.satisfyingLargePenaltyMultiplier = satisfyingLargePenaltyMultiplier;
                this.threeLargePenaltyMultiplier = threeLargePenaltyMultiplier;
                this.pureSetupCurationMultiplier = pureSetupCurationMultiplier;
                this.trioCoherenceMultiplier = trioCoherenceMultiplier;
                this.fullSequenceBonusMultiplier = fullSequenceBonusMultiplier;
                this.flexibilityBonusMultiplier = flexibilityBonusMultiplier;
                this.pressureAwareCuration = pressureAwareCuration;
                this.perfectCurationStreakBreakerEnabled = perfectCurationStreakBreakerEnabled;
                this.immediateClearCurationGateEnabled = immediateClearCurationGateEnabled;
                this.perfectCurationStreakBreakerMultiplier = perfectCurationStreakBreakerMultiplier;
                this.pressureCurationMidLowMultiplier = pressureCurationMidLowMultiplier;
                this.pressureCurationMidHighMultiplier = pressureCurationMidHighMultiplier;
                this.pressureCurationLateMultiplier = pressureCurationLateMultiplier;
            }

            public static SimulationConfiguration CreateCurrent(string name)
            {
                return new SimulationConfiguration(name, true, true, true, -50, 320, true, 1f, 1550f, true, 2500,
                    CandidateSelectionMode.SmartScore, SimulatedPlayerPolicy.ClearFirst);
            }

            public static SimulationConfiguration CreateModeratedSatisfyingTrayCuration(string name)
            {
                return new SimulationConfiguration(name, true, true, true, -50, 320, true, 1f, 1550f, true, 2500,
                    CandidateSelectionMode.SmartScore, SimulatedPlayerPolicy.ClearFirst, true,
                    fourCellPreferenceMultiplier: 0.55f,
                    satisfyingLargePenaltyMultiplier: 0.50f,
                    pureSetupCurationMultiplier: 0.65f,
                    trioCoherenceMultiplier: 0.55f,
                    fullSequenceBonusMultiplier: 0.60f,
                    flexibilityBonusMultiplier: 0.75f);
            }

            public static SimulationConfiguration CreatePressureAwareSatisfyingTrayCuration(string name)
            {
                return new SimulationConfiguration(name, true, true, true, -50, 320, true, 1f, 1550f, true, 2500,
                    CandidateSelectionMode.SmartScore, SimulatedPlayerPolicy.ClearFirst, true,
                    fourCellPreferenceMultiplier: 0.55f,
                    satisfyingLargePenaltyMultiplier: 0.50f,
                    pureSetupCurationMultiplier: 0.65f,
                    trioCoherenceMultiplier: 0.55f,
                    fullSequenceBonusMultiplier: 0.60f,
                    flexibilityBonusMultiplier: 0.75f,
                    pressureAwareCuration: true,
                    perfectCurationStreakBreakerEnabled: true);
            }

            public static SimulationConfiguration CreateLightCurationIntermediate(string name)
            {
                return new SimulationConfiguration(name, true, true, true, -50, 320, true, 1f, 1550f, true, 2500,
                    CandidateSelectionMode.SmartScore, SimulatedPlayerPolicy.ClearFirst, true,
                    satisfactionPostSelectionGuardsEnabled: false,
                    curatedTargetMassEnabled: false,
                    shapeSatisfactionMultiplier: 0.30f,
                    cClassPenaltyMultiplier: 0.45f,
                    dClassPenaltyMultiplier: 0.75f,
                    fourCellPreferenceMultiplier: 0.25f,
                    satisfyingLargePenaltyMultiplier: 0.20f,
                    threeLargePenaltyMultiplier: 0.30f,
                    pureSetupCurationMultiplier: 0.20f,
                    trioCoherenceMultiplier: 0.18f,
                    fullSequenceBonusMultiplier: 0.20f,
                    flexibilityBonusMultiplier: 0.35f,
                    pressureAwareCuration: true,
                    perfectCurationStreakBreakerEnabled: true,
                    perfectCurationStreakBreakerMultiplier: 0.50f,
                    pressureCurationMidLowMultiplier: 0.75f,
                    pressureCurationMidHighMultiplier: 0.50f,
                    pressureCurationLateMultiplier: 0.25f);
            }

            public static SimulationConfiguration CreateLightCurationClearGate(string name)
            {
                return new SimulationConfiguration(name, true, true, true, -50, 320, true, 1f, 1550f, true, 2500,
                    CandidateSelectionMode.SmartScore, SimulatedPlayerPolicy.ClearFirst, true,
                    satisfactionPostSelectionGuardsEnabled: false,
                    curatedTargetMassEnabled: false,
                    shapeSatisfactionMultiplier: 0.30f,
                    cClassPenaltyMultiplier: 0.45f,
                    dClassPenaltyMultiplier: 0.75f,
                    fourCellPreferenceMultiplier: 0.25f,
                    satisfyingLargePenaltyMultiplier: 0.20f,
                    threeLargePenaltyMultiplier: 0.30f,
                    pureSetupCurationMultiplier: 0.20f,
                    trioCoherenceMultiplier: 0.18f,
                    fullSequenceBonusMultiplier: 0.20f,
                    flexibilityBonusMultiplier: 0.35f,
                    pressureAwareCuration: true,
                    perfectCurationStreakBreakerEnabled: true,
                    immediateClearCurationGateEnabled: true,
                    perfectCurationStreakBreakerMultiplier: 0.50f,
                    pressureCurationMidLowMultiplier: 0.75f,
                    pressureCurationMidHighMultiplier: 0.50f,
                    pressureCurationLateMultiplier: 0.25f);
            }

            public static SimulationConfiguration CreateBalancedPlayer()
            {
                return new SimulationConfiguration("BALANCED HUMAN PLAYER", true, true, true, -50, 320, true, 1f, 1550f, false, 0,
                    CandidateSelectionMode.SmartScore, SimulatedPlayerPolicy.BalancedHuman);
            }

            public static SimulationConfiguration CreateRandomCandidateControl()
            {
                return new SimulationConfiguration("RANDOM VALID CANDIDATE", true, true, true, -50, 320, true, 1f, 1550f, false, 0,
                    CandidateSelectionMode.RandomValid, SimulatedPlayerPolicy.ClearFirst);
            }

            public static SimulationConfiguration CreateZeroOpenImmediateClear()
            {
                return new SimulationConfiguration("ZERO OPEN IMMEDIATE SCORE", true, true, true, -50, 0, false, 1f, 1550f, false, 0,
                    CandidateSelectionMode.SmartScore, SimulatedPlayerPolicy.ClearFirst);
            }

            public static SimulationConfiguration CreateSetupFirstOpenBoard()
            {
                // A three-times setup/payoff multiplier is analysis-only: it makes the
                // intended setup-first preference measurable without touching runtime values.
                return new SimulationConfiguration("SETUP-FIRST OPEN BOARD", true, true, true, -50, 0, false, 3f, 1550f, false, 0,
                    CandidateSelectionMode.SmartScore, SimulatedPlayerPolicy.ClearFirst);
            }

            public static SimulationConfiguration CreateComebackBonusVariant(string name, float openClearOpportunityWeight)
            {
                return new SimulationConfiguration(name, true, true, true, -50, 320, true, 1f,
                    openClearOpportunityWeight, false, 0, CandidateSelectionMode.SmartScore, SimulatedPlayerPolicy.ClearFirst);
            }

            public static SimulationConfiguration CreatePureSetupVariant(string name, bool usePureSetupScoring, int diversityBonus)
            {
                return new SimulationConfiguration(name, true, true, true, -50, 320, true, 1f,
                    1550f, usePureSetupScoring, diversityBonus, CandidateSelectionMode.SmartScore,
                    SimulatedPlayerPolicy.ClearFirst);
            }
        }

        private sealed class Aggregate
        {
            private readonly List<int> placements = new List<int>();
            private readonly List<int> popUses = new List<int>();
            private readonly List<int> popIntervals = new List<int>();
            public readonly SimulationConfiguration configuration;
            public readonly int runCount;
            public int ceilingHits;
            public int longestCompleted;
            public int maxPopUses;
            public int maxConsecutiveRelief;
            private long totalOccupancy;
            private long occupancySamples;
            private long totalPlacements;
            private long totalTraySamples;
            private long openTrays;
            private long balancedTrays;
            private long pressuredTrays;
            private long criticalTrays;
            private long totalClears;
            private long totalOneLineClearMoves;
            private long totalMultiClears;
            private long totalImmediateClearTrays;
            private long totalImmediateClearOpportunities;
            private long totalSetupPayoffTrays;
            private long totalSetupWithoutImmediateClearTrays;
            private long totalPureSetupTrays;
            private long totalPureSetupWithoutImmediateClearTrays;
            private long totalOpenDiversityBonusTrays;
            private long totalImmediateClearSetupOverlapTrays;
            private long totalSetupScoreWithImmediate;
            private long totalSetupScoreWithoutImmediate;
            private long totalSetupScoreWithImmediateTrays;
            private long totalSetupScoreWithoutImmediateTrays;
            private long totalImmediateOpportunitiesOnSetupTrays;
            private readonly long[] totalSetupClassifications = new long[4];
            private readonly long[] totalSelectionReasons = new long[8];
            private readonly List<string> openExamples = new List<string>(5);
            private readonly List<string> pureOpenExamples = new List<string>(5);
            private readonly List<string> curatedExamples = new List<string>(10);
            private readonly List<string> openImmediateLightCurationExamples = new List<string>(5);
            private readonly List<string> openNoImmediateLightCurationExamples = new List<string>(5);
            private readonly StageTrayStats[] curationStages =
            {
                new StageTrayStats(), new StageTrayStats(), new StageTrayStats()
            };
            private readonly List<string>[] curationStageExamples =
            {
                new List<string>(5), new List<string>(5), new List<string>(5)
            };
            private long totalGeneratedCurationTrays;
            private long totalPerfectlyCuratedTrays;
            private long totalPerfectCurationStreakBreakerActivations;
            private int maxConsecutivePerfectCuration;
            private long totalCurationChangedRankingTrays;
            private long totalPhase7HPrimaryWinnerTrays;
            private long totalCurationChangedImmediateClearTrays;
            private long totalCurationChangedNoImmediateClearTrays;
            private long totalReliefTrays;
            private long totalAntiReliefLoopActivations;
            private long totalChoiceTraySamples;
            private long totalMultipleReasonablePlacementTrays;
            private long totalReasonablePlacementOptions;
            private long totalCompletedChoiceTrays;
            private long totalSelectedImmediateClearTrays;
            private long totalSelectedSetupPayoffTrays;
            private long totalSelectedConnectivityTrays;
            private long totalSetupCarrierTrays;
            private long totalSameTraySetupPayoffs;
            private long totalNextTraySetupPayoffs;
            private long totalTraysToSetupPayoff;
            private long totalPostSelectionTrays;
            private long totalPrePostSelectionImmediateOpportunities;
            private long totalPostPostSelectionImmediateOpportunities;
            private long totalPostSelectionInjectedImmediateClearTrays;
            private long totalPops;
            private long totalFlowTargetsCreated;
            private long totalFlowContinuationEligibleTrays;
            private long totalFlowTargetContinuationTrays;
            private readonly long[] totalContinuationTraySamples = new long[4];
            private readonly long[] totalReadableContinuationTrays = new long[4];
            private readonly long[] totalConstructedContinuationTrays = new long[4];
            private readonly long[] totalTrayEaseScores = new long[5];
            private readonly long[] totalTrayEaseSamples = new long[5];
            private long totalLateChallengeTraySamples;
            private long totalChallengeBandFallbacks;
            private long totalCriticalChallengeBypasses;
            private long totalProjectedOccupiedCells;
            private long totalProjectedLargestOpenArea;
            private long totalProjectedFragmentation;
            private long totalProjectedTraySamples;
            private long totalScore;
            private long totalPopScore;
            private double totalPressureAtEnd;
            private readonly CandidatePoolStats[] rawCandidateStats = CreateCandidatePoolStats();
            private readonly ScoreTermStats selectedScoreTerms = new ScoreTermStats();
            private readonly PieceStats selectedPieces = new PieceStats();
            private readonly TrayQualityStats trayQuality = new TrayQualityStats();

            public float MeanPlacements { get; private set; }
            public float MedianPlacements { get; private set; }
            public float P75Placements { get; private set; }
            public float P90Placements { get; private set; }
            public float P95Placements { get; private set; }
            public float MeanOccupancy { get; private set; }
            public float OpenPercent { get; private set; }
            public float BalancedPercent { get; private set; }
            public float PressuredPercent { get; private set; }
            public float CriticalPercent { get; private set; }
            public float PlacementsPerClear { get; private set; }
            public float ImmediateClearTrayPercent { get; private set; }
            public float MeanImmediateClearOpportunitiesPerTray { get; private set; }
            public float MeanImmediateClearOpportunitiesPerSetupTray { get; private set; }
            public float SetupPayoffTrayPercent { get; private set; }
            public float SetupWithoutImmediateClearTrayPercent { get; private set; }
            public float PureSetupTrayPercent { get; private set; }
            public float PureSetupWithoutImmediateClearTrayPercent { get; private set; }
            public float OpenDiversityBonusTrayPercent { get; private set; }
            public float ImmediateClearSetupOverlapPercent { get; private set; }
            public float MeanSetupScoreWithImmediate { get; private set; }
            public float MeanSetupScoreWithoutImmediate { get; private set; }
            public float MultiClearPercent { get; private set; }
            public float OneLineClearMovePercent { get; private set; }
            public float MeanClears { get; private set; }
            public float ReliefTrayPercent { get; private set; }
            public float MultipleReasonablePlacementTrayPercent { get; private set; }
            public float MeanReasonablePlacementOptions { get; private set; }
            public float SelectedImmediateClearTrayPercent { get; private set; }
            public float SelectedSetupPayoffTrayPercent { get; private set; }
            public float SelectedConnectivityTrayPercent { get; private set; }
            public float SameTraySetupPayoffPercent { get; private set; }
            public float NextTraySetupPayoffPercent { get; private set; }
            public float MeanTraysToSetupPayoff { get; private set; }
            public float PrePostSelectionImmediateOpportunitiesPerTray { get; private set; }
            public float PostPostSelectionImmediateOpportunitiesPerTray { get; private set; }
            public float PostSelectionInjectedImmediateClearTrayPercent { get; private set; }
            public float MeanPopUses { get; private set; }
            public float MedianPopUses { get; private set; }
            public float P90PopUses { get; private set; }
            public float MeanPopInterval { get; private set; }
            public float MeanPressureAtEnd { get; private set; }
            public float MeanFinalScore { get; private set; }
            public float PopScorePercent { get; private set; }
            public float PerfectlyCuratedTrayPercent { get; private set; }
            public float PerfectCurationStreakBreakerFrequency { get; private set; }
            public float CurationChangedRankingPercent { get; private set; }
            public float Phase7HPrimaryWinnerPercent { get; private set; }
            public float CurationChangedImmediateClearPercent { get; private set; }
            public float CurationChangedNoImmediateClearPercent { get; private set; }
            public float FlowTargetContinuationPercent { get; private set; }
            public float ReadableContinuationPercent(int stage) => Percent(
                totalReadableContinuationTrays[stage], totalContinuationTraySamples[stage]);
            public float ConstructedContinuationPercent(int stage) => Percent(
                totalConstructedContinuationTrays[stage], totalContinuationTraySamples[stage]);
            public float MeanTrayEaseScore(int stage) => totalTrayEaseSamples[stage] == 0
                ? 0f
                : (float)totalTrayEaseScores[stage] / totalTrayEaseSamples[stage];
            public float ChallengeBandFallbackPercent => Percent(
                totalChallengeBandFallbacks, totalLateChallengeTraySamples);
            public float CriticalChallengeBypassPercent => Percent(
                totalCriticalChallengeBypasses, totalLateChallengeTraySamples);
            public float MeanProjectedOccupiedCells { get; private set; }
            public float MeanProjectedLargestOpenArea { get; private set; }
            public float MeanProjectedFragmentation { get; private set; }
            public float CeilingPercent => runCount == 0 ? 0f : ceilingHits * 100f / runCount;
            public int LongestCompletedRun => longestCompleted;
            public int MaxPopUses => maxPopUses;
            public int MaxConsecutiveRelief => maxConsecutiveRelief;
            public long AntiReliefLoopActivations => totalAntiReliefLoopActivations;
            public int MaxConsecutivePerfectCuration => maxConsecutivePerfectCuration;
            public CandidatePoolStats GetRawCandidateStats(OccupancyState state) => rawCandidateStats[(int)state];
            public ScoreTermStats SelectedScoreTerms => selectedScoreTerms;
            public PieceStats SelectedPieces => selectedPieces;
            public TrayQualityStats TrayQuality => trayQuality;
            public IReadOnlyList<string> OpenExamples => openExamples;
            public IReadOnlyList<string> PureOpenExamples => pureOpenExamples;
            public IReadOnlyList<string> CuratedExamples => curatedExamples;
            public IReadOnlyList<string> OpenImmediateLightCurationExamples => openImmediateLightCurationExamples;
            public IReadOnlyList<string> OpenNoImmediateLightCurationExamples => openNoImmediateLightCurationExamples;
            public StageTrayStats GetCurationStage(int index) => curationStages[index];
            public IReadOnlyList<string> GetCurationStageExamples(int index) => curationStageExamples[index];

            public float SetupClassificationPercent(SetupClassification classification)
            {
                return Percent(totalSetupClassifications[(int)classification], totalTraySamples);
            }

            public float SelectionReasonPercent(SelectionReason reason)
            {
                return Percent(totalSelectionReasons[(int)reason], totalTraySamples);
            }

            public Aggregate(SimulationConfiguration configuration, int runCount)
            {
                this.configuration = configuration;
                this.runCount = runCount;
            }

            public void Add(RunMetrics run)
            {
                placements.Add(run.placements);
                popUses.Add(run.popUses);
                popIntervals.AddRange(run.popPlacementIntervals);
                if (run.censored)
                {
                    ceilingHits++;
                }
                else
                {
                    longestCompleted = Math.Max(longestCompleted, run.placements);
                }

                maxPopUses = Math.Max(maxPopUses, run.popUses);
                maxConsecutiveRelief = Math.Max(maxConsecutiveRelief, run.maxConsecutiveReliefTrays);
                totalPlacements += run.placements;
                totalOccupancy += run.occupancyAfterPlacementTotal;
                occupancySamples += run.occupancyAfterPlacementSamples;
                totalTraySamples += run.traySamples;
                openTrays += run.openTrays;
                balancedTrays += run.balancedTrays;
                pressuredTrays += run.pressuredTrays;
                criticalTrays += run.criticalTrays;
                totalClears += run.clears;
                totalOneLineClearMoves += run.oneLineClearMoves;
                totalMultiClears += run.multiLineClears;
                totalImmediateClearTrays += run.immediateClearTrays;
                totalImmediateClearOpportunities += run.totalImmediateClearOpportunities;
                totalSetupPayoffTrays += run.setupPayoffTrays;
                totalSetupWithoutImmediateClearTrays += run.setupWithoutImmediateClearTrays;
                totalPureSetupTrays += run.pureSetupTrays;
                totalPureSetupWithoutImmediateClearTrays += run.pureSetupWithoutImmediateClearTrays;
                totalOpenDiversityBonusTrays += run.openDiversityBonusTrays;
                totalImmediateClearSetupOverlapTrays += run.immediateClearSetupOverlapTrays;
                totalSetupScoreWithImmediate += run.setupScoreWithImmediateTotal;
                totalSetupScoreWithoutImmediate += run.setupScoreWithoutImmediateTotal;
                totalSetupScoreWithImmediateTrays += run.setupScoreWithImmediateTrays;
                totalSetupScoreWithoutImmediateTrays += run.setupScoreWithoutImmediateTrays;
                totalImmediateOpportunitiesOnSetupTrays += run.immediateOpportunitiesOnSetupTrays;
                for (int i = 0; i < totalSetupClassifications.Length; i++)
                {
                    totalSetupClassifications[i] += run.setupClassifications[i];
                }
                for (int i = 0; i < totalSelectionReasons.Length; i++)
                {
                    totalSelectionReasons[i] += run.selectionReasons[i];
                }
                for (int i = 0; i < run.openExamples.Count && openExamples.Count < 5; i++)
                {
                    openExamples.Add(run.openExamples[i]);
                }
                for (int i = 0; i < run.pureOpenExamples.Count && pureOpenExamples.Count < 5; i++)
                {
                    pureOpenExamples.Add(run.pureOpenExamples[i]);
                }
                for (int i = 0; i < run.curatedExamples.Count && curatedExamples.Count < 10; i++)
                {
                    curatedExamples.Add(run.curatedExamples[i]);
                }
                for (int i = 0; i < run.openImmediateLightCurationExamples.Count && openImmediateLightCurationExamples.Count < 5; i++)
                {
                    openImmediateLightCurationExamples.Add(run.openImmediateLightCurationExamples[i]);
                }
                for (int i = 0; i < run.openNoImmediateLightCurationExamples.Count && openNoImmediateLightCurationExamples.Count < 5; i++)
                {
                    openNoImmediateLightCurationExamples.Add(run.openNoImmediateLightCurationExamples[i]);
                }
                totalGeneratedCurationTrays += run.generatedCurationTraySamples;
                totalPerfectlyCuratedTrays += run.perfectlyCuratedTrays;
                totalPerfectCurationStreakBreakerActivations += run.perfectCurationStreakBreakerActivations;
                maxConsecutivePerfectCuration = Math.Max(maxConsecutivePerfectCuration, run.maxConsecutivePerfectCurationTrays);
                totalCurationChangedRankingTrays += run.curationChangedRankingTrays;
                totalPhase7HPrimaryWinnerTrays += run.phase7HPrimaryWinnerTrays;
                totalCurationChangedImmediateClearTrays += run.curationChangedImmediateClearTrays;
                totalCurationChangedNoImmediateClearTrays += run.curationChangedNoImmediateClearTrays;
                for (int i = 0; i < curationStages.Length; i++)
                {
                    curationStages[i].Add(run.curationStages[i]);
                    for (int example = 0; example < run.curationStageExamples[i].Count && curationStageExamples[i].Count < 5; example++)
                    {
                        curationStageExamples[i].Add(run.curationStageExamples[i][example]);
                    }
                }
                totalReliefTrays += run.reliefBiasedTrays;
                totalAntiReliefLoopActivations += run.antiReliefLoopActivations;
                totalChoiceTraySamples += run.choiceTraySamples;
                totalMultipleReasonablePlacementTrays += run.multipleReasonablePlacementTrays;
                totalReasonablePlacementOptions += run.totalReasonablePlacementOptions;
                totalCompletedChoiceTrays += run.completedChoiceTrays;
                totalSelectedImmediateClearTrays += run.selectedImmediateClearTrays;
                totalSelectedSetupPayoffTrays += run.selectedSetupPayoffTrays;
                totalSelectedConnectivityTrays += run.selectedConnectivityTrays;
                totalSetupCarrierTrays += run.setupCarrierTrays;
                totalSameTraySetupPayoffs += run.sameTraySetupPayoffs;
                totalNextTraySetupPayoffs += run.nextTraySetupPayoffs;
                totalTraysToSetupPayoff += run.totalTraysToSetupPayoff;
                totalPostSelectionTrays += run.postSelectionTrays;
                totalPrePostSelectionImmediateOpportunities += run.prePostSelectionImmediateOpportunities;
                totalPostPostSelectionImmediateOpportunities += run.postPostSelectionImmediateOpportunities;
                totalPostSelectionInjectedImmediateClearTrays += run.postSelectionInjectedImmediateClearTrays;
                totalPops += run.popUses;
                totalFlowTargetsCreated += run.flowTargetsCreated;
                totalFlowContinuationEligibleTrays += run.flowContinuationEligibleTrays;
                totalFlowTargetContinuationTrays += run.flowTargetContinuationTrays;
                for (int i = 0; i < totalContinuationTraySamples.Length; i++)
                {
                    totalContinuationTraySamples[i] += run.continuationTraySamples[i];
                    totalReadableContinuationTrays[i] += run.readableContinuationTrays[i];
                    totalConstructedContinuationTrays[i] += run.constructedContinuationTrays[i];
                }
                for (int i = 0; i < totalTrayEaseScores.Length; i++)
                {
                    totalTrayEaseScores[i] += run.trayEaseScores[i];
                    totalTrayEaseSamples[i] += run.trayEaseSamples[i];
                }
                totalLateChallengeTraySamples += run.lateChallengeTraySamples;
                totalChallengeBandFallbacks += run.challengeBandFallbacks;
                totalCriticalChallengeBypasses += run.criticalChallengeBypasses;
                totalProjectedOccupiedCells += run.projectedOccupiedCells;
                totalProjectedLargestOpenArea += run.projectedLargestOpenArea;
                totalProjectedFragmentation += run.projectedFragmentation;
                totalProjectedTraySamples += run.projectedTraySamples;
                totalScore += run.finalScore;
                totalPopScore += run.popScore;
                totalPressureAtEnd += run.pressureAtEnd;
                for (int i = 0; i < rawCandidateStats.Length; i++)
                {
                    rawCandidateStats[i].Add(run.rawCandidateStats[i]);
                }

                selectedScoreTerms.Add(run.selectedScoreTerms);
                selectedPieces.Add(run.selectedPieces);
                trayQuality.Add(run.trayQuality);
            }

            public void FinalizeMetrics()
            {
                placements.Sort();
                popUses.Sort();
                MeanPlacements = Mean(placements);
                MedianPlacements = Percentile(placements, 0.50f);
                P75Placements = Percentile(placements, 0.75f);
                P90Placements = Percentile(placements, 0.90f);
                P95Placements = Percentile(placements, 0.95f);
                MeanOccupancy = occupancySamples == 0 ? 0f : (float)totalOccupancy / occupancySamples;
                OpenPercent = Percent(openTrays, totalTraySamples);
                BalancedPercent = Percent(balancedTrays, totalTraySamples);
                PressuredPercent = Percent(pressuredTrays, totalTraySamples);
                CriticalPercent = Percent(criticalTrays, totalTraySamples);
                PlacementsPerClear = totalClears == 0 ? 0f : (float)totalPlacements / totalClears;
                ImmediateClearTrayPercent = Percent(totalImmediateClearTrays, totalTraySamples);
                MeanImmediateClearOpportunitiesPerTray = totalTraySamples == 0 ? 0f : (float)totalImmediateClearOpportunities / totalTraySamples;
                MeanImmediateClearOpportunitiesPerSetupTray = totalImmediateClearSetupOverlapTrays == 0
                    ? 0f
                    : (float)totalImmediateOpportunitiesOnSetupTrays / totalImmediateClearSetupOverlapTrays;
                SetupPayoffTrayPercent = Percent(totalSetupPayoffTrays, totalTraySamples);
                SetupWithoutImmediateClearTrayPercent = Percent(totalSetupWithoutImmediateClearTrays, totalTraySamples);
                PureSetupTrayPercent = Percent(totalPureSetupTrays, totalTraySamples);
                PureSetupWithoutImmediateClearTrayPercent = Percent(totalPureSetupWithoutImmediateClearTrays, totalTraySamples);
                OpenDiversityBonusTrayPercent = Percent(totalOpenDiversityBonusTrays, totalTraySamples);
                ImmediateClearSetupOverlapPercent = Percent(totalImmediateClearSetupOverlapTrays, totalTraySamples);
                MeanSetupScoreWithImmediate = totalSetupScoreWithImmediateTrays == 0
                    ? 0f
                    : (float)totalSetupScoreWithImmediate / totalSetupScoreWithImmediateTrays;
                MeanSetupScoreWithoutImmediate = totalSetupScoreWithoutImmediateTrays == 0
                    ? 0f
                    : (float)totalSetupScoreWithoutImmediate / totalSetupScoreWithoutImmediateTrays;
                MultiClearPercent = Percent(totalMultiClears, totalClears);
                OneLineClearMovePercent = Percent(totalOneLineClearMoves, totalPlacements);
                MeanClears = runCount == 0 ? 0f : (float)totalClears / runCount;
                ReliefTrayPercent = Percent(totalReliefTrays, totalTraySamples);
                MultipleReasonablePlacementTrayPercent = Percent(totalMultipleReasonablePlacementTrays, totalChoiceTraySamples);
                MeanReasonablePlacementOptions = totalChoiceTraySamples == 0 ? 0f : (float)totalReasonablePlacementOptions / totalChoiceTraySamples;
                SelectedImmediateClearTrayPercent = Percent(totalSelectedImmediateClearTrays, totalCompletedChoiceTrays);
                SelectedSetupPayoffTrayPercent = Percent(totalSelectedSetupPayoffTrays, totalCompletedChoiceTrays);
                SelectedConnectivityTrayPercent = Percent(totalSelectedConnectivityTrays, totalCompletedChoiceTrays);
                SameTraySetupPayoffPercent = Percent(totalSameTraySetupPayoffs, totalSetupCarrierTrays);
                NextTraySetupPayoffPercent = Percent(totalNextTraySetupPayoffs, totalSetupCarrierTrays);
                long setupPayoffConversions = totalSameTraySetupPayoffs + totalNextTraySetupPayoffs;
                MeanTraysToSetupPayoff = setupPayoffConversions == 0
                    ? 0f
                    : (float)totalTraysToSetupPayoff / setupPayoffConversions;
                PrePostSelectionImmediateOpportunitiesPerTray = totalPostSelectionTrays == 0 ? 0f : (float)totalPrePostSelectionImmediateOpportunities / totalPostSelectionTrays;
                PostPostSelectionImmediateOpportunitiesPerTray = totalPostSelectionTrays == 0 ? 0f : (float)totalPostPostSelectionImmediateOpportunities / totalPostSelectionTrays;
                PostSelectionInjectedImmediateClearTrayPercent = Percent(totalPostSelectionInjectedImmediateClearTrays, totalPostSelectionTrays);
                MeanPopUses = runCount == 0 ? 0f : (float)totalPops / runCount;
                MedianPopUses = Percentile(popUses, 0.50f);
                P90PopUses = Percentile(popUses, 0.90f);
                MeanPopInterval = Mean(popIntervals);
                MeanPressureAtEnd = runCount == 0 ? 0f : (float)(totalPressureAtEnd / runCount);
                MeanFinalScore = runCount == 0 ? 0f : (float)totalScore / runCount;
                PopScorePercent = totalScore == 0 ? 0f : totalPopScore * 100f / totalScore;
                PerfectlyCuratedTrayPercent = Percent(totalPerfectlyCuratedTrays, totalGeneratedCurationTrays);
                PerfectCurationStreakBreakerFrequency = Percent(totalPerfectCurationStreakBreakerActivations, totalGeneratedCurationTrays);
                CurationChangedRankingPercent = Percent(totalCurationChangedRankingTrays, totalGeneratedCurationTrays);
                Phase7HPrimaryWinnerPercent = Percent(totalPhase7HPrimaryWinnerTrays, totalGeneratedCurationTrays);
                CurationChangedImmediateClearPercent = Percent(totalCurationChangedImmediateClearTrays, totalCurationChangedRankingTrays);
                CurationChangedNoImmediateClearPercent = Percent(totalCurationChangedNoImmediateClearTrays, totalCurationChangedRankingTrays);
                FlowTargetContinuationPercent = Percent(totalFlowTargetContinuationTrays, totalFlowContinuationEligibleTrays);
                MeanProjectedOccupiedCells = totalProjectedTraySamples == 0 ? 0f : (float)totalProjectedOccupiedCells / totalProjectedTraySamples;
                MeanProjectedLargestOpenArea = totalProjectedTraySamples == 0 ? 0f : (float)totalProjectedLargestOpenArea / totalProjectedTraySamples;
                MeanProjectedFragmentation = totalProjectedTraySamples == 0 ? 0f : (float)totalProjectedFragmentation / totalProjectedTraySamples;
            }

            private static float Mean(List<int> values)
            {
                if (values.Count == 0) return 0f;
                long total = 0;
                for (int i = 0; i < values.Count; i++) total += values[i];
                return (float)total / values.Count;
            }

            private static float Percentile(List<int> values, float percentile)
            {
                if (values.Count == 0) return 0f;
                int index = Mathf.Clamp(Mathf.CeilToInt(values.Count * percentile) - 1, 0, values.Count - 1);
                return values[index];
            }

            private static float Percent(long value, long total)
            {
                return total == 0 ? 0f : value * 100f / total;
            }

            private static CandidatePoolStats[] CreateCandidatePoolStats()
            {
                return new[]
                {
                    new CandidatePoolStats(), new CandidatePoolStats(), new CandidatePoolStats(), new CandidatePoolStats()
                };
            }
        }

        private sealed class RunMetrics
        {
            public int placements;
            public int trays;
            public int clears;
            public int oneLineClearMoves;
            public int multiLineClears;
            public int popUses;
            public int finalOccupiedCells;
            public int maxOccupiedCells;
            public long occupancyAfterPlacementTotal;
            public int occupancyAfterPlacementSamples;
            public int traySamples;
            public int openTrays;
            public int balancedTrays;
            public int pressuredTrays;
            public int criticalTrays;
            public int immediateClearTrays;
            public long totalImmediateClearOpportunities;
            public int setupPayoffTrays;
            public int setupWithoutImmediateClearTrays;
            public int pureSetupTrays;
            public int pureSetupWithoutImmediateClearTrays;
            public int openDiversityBonusTrays;
            public int immediateClearSetupOverlapTrays;
            public long setupScoreWithImmediateTotal;
            public long setupScoreWithoutImmediateTotal;
            public int setupScoreWithImmediateTrays;
            public int setupScoreWithoutImmediateTrays;
            public long immediateOpportunitiesOnSetupTrays;
            public readonly long[] setupClassifications = new long[4];
            public readonly long[] selectionReasons = new long[8];
            public readonly List<string> openExamples = new List<string>(5);
            public readonly List<string> pureOpenExamples = new List<string>(5);
            public readonly List<string> curatedExamples = new List<string>(10);
            public readonly List<string> openImmediateLightCurationExamples = new List<string>(5);
            public readonly List<string> openNoImmediateLightCurationExamples = new List<string>(5);
            public int generatedCurationTraySamples;
            public int perfectlyCuratedTrays;
            public int maxConsecutivePerfectCurationTrays;
            public int perfectCurationStreakBreakerActivations;
            public int curationChangedRankingTrays;
            public int phase7HPrimaryWinnerTrays;
            public int curationChangedImmediateClearTrays;
            public int curationChangedNoImmediateClearTrays;
            public readonly StageTrayStats[] curationStages =
            {
                new StageTrayStats(), new StageTrayStats(), new StageTrayStats()
            };
            public readonly List<string>[] curationStageExamples =
            {
                new List<string>(5), new List<string>(5), new List<string>(5)
            };
            public int reliefBiasedTrays;
            public int maxConsecutiveReliefTrays;
            public int antiReliefLoopActivations;
            public int choiceTraySamples;
            public int multipleReasonablePlacementTrays;
            public long totalReasonablePlacementOptions;
            public int completedChoiceTrays;
            public int selectedImmediateClearTrays;
            public int selectedSetupPayoffTrays;
            public int selectedConnectivityTrays;
            public int setupCarrierTrays;
            public int sameTraySetupPayoffs;
            public int nextTraySetupPayoffs;
            public long totalTraysToSetupPayoff;
            public int postSelectionTrays;
            public long prePostSelectionImmediateOpportunities;
            public long postPostSelectionImmediateOpportunities;
            public int postSelectionInjectedImmediateClearTrays;
            public int flowTargetsCreated;
            public int flowContinuationEligibleTrays;
            public int flowTargetContinuationTrays;
            public int generatedFlowMatchTrays;
            public readonly long[] continuationTraySamples = new long[4];
            public readonly long[] readableContinuationTrays = new long[4];
            public readonly long[] constructedContinuationTrays = new long[4];
            public readonly long[] trayEaseScores = new long[5];
            public readonly long[] trayEaseSamples = new long[5];
            public int lateChallengeTraySamples;
            public int challengeBandFallbacks;
            public int criticalChallengeBypasses;
            public long projectedOccupiedCells;
            public long projectedLargestOpenArea;
            public long projectedFragmentation;
            public int projectedTraySamples;
            public long flowScoreTotal;
            public readonly CandidatePoolStats[] rawCandidateStats =
            {
                new CandidatePoolStats(), new CandidatePoolStats(), new CandidatePoolStats(), new CandidatePoolStats()
            };
            public readonly ScoreTermStats selectedScoreTerms = new ScoreTermStats();
            public readonly PieceStats selectedPieces = new PieceStats();
            public readonly TrayQualityStats trayQuality = new TrayQualityStats();
            public float pressureAtEnd;
            public int popRequirementAtEnd;
            public int finalScore;
            public int normalScore;
            public int popScore;
            public bool censored;
            public readonly List<int> popPlacementIntervals = new List<int>();
        }

        private sealed class StageTrayStats
        {
            private long trays;
            private long totalCells;
            private long totalOccupancy;
            private long immediateClearTrays;
            private long pureSetupTrays;
            private readonly TrayQualityStats quality = new TrayQualityStats();

            public long Trays => trays;
            public float AveragePieceSize => trays == 0 ? 0f : (float)totalCells / (trays * GameConstants.TraySize);
            public float AverageOccupancy => trays == 0 ? 0f : (float)totalOccupancy / trays;
            public float ImmediateClearPercent => Percent(immediateClearTrays);
            public float PureSetupPercent => Percent(pureSetupTrays);
            public TrayQualityStats Quality => quality;

            public void Add(TrayGenerationResult generation)
            {
                trays++;
                totalOccupancy += generation.occupiedCells;
                for (int i = 0; i < generation.pieces.Length; i++)
                {
                    if (generation.pieces[i] != null)
                    {
                        totalCells += generation.pieces[i].Data.cells.Length;
                    }
                }

                if (generation.immediateClearOpportunities > 0) immediateClearTrays++;
                if (generation.pureSetupOpportunities > 0) pureSetupTrays++;
                quality.Add(generation.curation, generation.pieces);
            }

            public void Add(StageTrayStats other)
            {
                trays += other.trays;
                totalCells += other.totalCells;
                totalOccupancy += other.totalOccupancy;
                immediateClearTrays += other.immediateClearTrays;
                pureSetupTrays += other.pureSetupTrays;
                quality.Add(other.quality);
            }

            private float Percent(long value)
            {
                return trays == 0 ? 0f : value * 100f / trays;
            }
        }

        private sealed class CandidatePoolStats
        {
            public long candidateCount;
            public long anyImmediateClearCandidates;
            public long multipleImmediatePlacementCandidates;
            public long multipleImmediatePieceCandidates;
            public long noImmediateClearCandidates;
            public long setupWithoutImmediateClearCandidates;

            public float AnyImmediateClearPercent => Percent(anyImmediateClearCandidates);
            public float MultipleImmediatePlacementPercent => Percent(multipleImmediatePlacementCandidates);
            public float MultipleImmediatePiecePercent => Percent(multipleImmediatePieceCandidates);
            public float NoImmediateClearPercent => Percent(noImmediateClearCandidates);
            public float SetupWithoutImmediateClearPercent => Percent(setupWithoutImmediateClearCandidates);

            public void Add(CandidatePoolStats other)
            {
                candidateCount += other.candidateCount;
                anyImmediateClearCandidates += other.anyImmediateClearCandidates;
                multipleImmediatePlacementCandidates += other.multipleImmediatePlacementCandidates;
                multipleImmediatePieceCandidates += other.multipleImmediatePieceCandidates;
                noImmediateClearCandidates += other.noImmediateClearCandidates;
                setupWithoutImmediateClearCandidates += other.setupWithoutImmediateClearCandidates;
            }

            private float Percent(long value)
            {
                return candidateCount == 0 ? 0f : value * 100f / candidateCount;
            }
        }

        private sealed class ScoreTermStats
        {
            public readonly TermRange fitFairness = new TermRange();
            public readonly TermRange connectivity = new TermRange();
            public readonly TermRange lineProgress = new TermRange();
            public readonly TermRange immediateClear = new TermRange();
            public readonly TermRange setupPayoff = new TermRange();
            public readonly TermRange cleanliness = new TermRange();
            public readonly TermRange rescueRelief = new TermRange();
            public readonly TermRange pressure = new TermRange();
            public readonly TermRange pieceSize = new TermRange();
            public readonly TermRange other = new TermRange();

            public void Add(ScoreTerms terms)
            {
                fitFairness.Add(terms.fitFairness);
                connectivity.Add(terms.connectivity);
                lineProgress.Add(terms.lineProgress);
                immediateClear.Add(terms.immediateClear);
                setupPayoff.Add(terms.setupPayoff);
                cleanliness.Add(terms.cleanliness);
                rescueRelief.Add(terms.rescueRelief);
                pressure.Add(terms.pressure);
                pieceSize.Add(terms.pieceSize);
                other.Add(terms.other);
            }

            public void Add(ScoreTermStats other)
            {
                fitFairness.Add(other.fitFairness);
                connectivity.Add(other.connectivity);
                lineProgress.Add(other.lineProgress);
                immediateClear.Add(other.immediateClear);
                setupPayoff.Add(other.setupPayoff);
                cleanliness.Add(other.cleanliness);
                rescueRelief.Add(other.rescueRelief);
                pressure.Add(other.pressure);
                pieceSize.Add(other.pieceSize);
                this.other.Add(other.other);
            }
        }

        private sealed class TermRange
        {
            private long total;
            private int count;
            private int min = int.MaxValue;
            private int max = int.MinValue;
            public float Mean => count == 0 ? 0f : (float)total / count;
            public int Min => count == 0 ? 0 : min;
            public int Max => count == 0 ? 0 : max;

            public void Add(int value)
            {
                total += value;
                count++;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

            public void Add(TermRange source)
            {
                if (source.count == 0)
                {
                    return;
                }

                total += source.total;
                count += source.count;
                min = Math.Min(min, source.min);
                max = Math.Max(max, source.max);
            }
        }

        private sealed class PieceStats
        {
            private readonly long[] totalBySize = new long[5];
            private readonly long[] immediateClearBySize = new long[5];
            private readonly long[,] totalByStageAndSize = new long[4, 5];
            private readonly long[] totalByStairStage = new long[3];
            private readonly long[] stair5ByStage = new long[3];

            public void Add(int stage, int sizeBucket, bool hasImmediateClear, bool isStair5)
            {
                totalBySize[sizeBucket]++;
                totalByStageAndSize[stage, sizeBucket]++;
                int stairStage = stage <= 0 ? 0 : stage == 1 ? 1 : 2;
                totalByStairStage[stairStage]++;
                if (isStair5)
                {
                    stair5ByStage[stairStage]++;
                }
                if (hasImmediateClear)
                {
                    immediateClearBySize[sizeBucket]++;
                }
            }

            public void Add(PieceStats other)
            {
                for (int stage = 0; stage < 4; stage++)
                {
                    for (int size = 0; size < 5; size++)
                    {
                        totalByStageAndSize[stage, size] += other.totalByStageAndSize[stage, size];
                    }
                }

                for (int size = 0; size < 5; size++)
                {
                    totalBySize[size] += other.totalBySize[size];
                    immediateClearBySize[size] += other.immediateClearBySize[size];
                }

                for (int stage = 0; stage < totalByStairStage.Length; stage++)
                {
                    totalByStairStage[stage] += other.totalByStairStage[stage];
                    stair5ByStage[stage] += other.stair5ByStage[stage];
                }
            }

            public float SizePercent(int sizeBucket)
            {
                long total = 0;
                for (int i = 0; i < totalBySize.Length; i++) total += totalBySize[i];
                return total == 0 ? 0f : totalBySize[sizeBucket] * 100f / total;
            }

            public float ImmediateClearPercent(int sizeBucket)
            {
                return totalBySize[sizeBucket] == 0 ? 0f : immediateClearBySize[sizeBucket] * 100f / totalBySize[sizeBucket];
            }

            public float StageSizePercent(int stage, int sizeBucket)
            {
                long total = 0;
                for (int i = 0; i < 5; i++) total += totalByStageAndSize[stage, i];
                return total == 0 ? 0f : totalByStageAndSize[stage, sizeBucket] * 100f / total;
            }

            public float Stair5Frequency(int stage)
            {
                return totalByStairStage[stage] == 0
                    ? 0f
                    : stair5ByStage[stage] * 100f / totalByStairStage[stage];
            }
        }

        private sealed class TrayQualityStats
        {
            private readonly long[] piecesBySatisfactionClass = new long[4];
            private long trays;
            private long twoOrMoreHighSatisfaction;
            private long twoOrMoreAwkward;
            private long threeLargePieces;
            private long healthyFlexibility;
            private long coherentTwoPiece;
            private long coherentFullSequence;

            public float SatisfactionClassPercent(PieceSatisfactionClass satisfactionClass)
            {
                long total = 0;
                for (int i = 0; i < piecesBySatisfactionClass.Length; i++)
                {
                    total += piecesBySatisfactionClass[i];
                }

                return total == 0 ? 0f : piecesBySatisfactionClass[(int)satisfactionClass] * 100f / total;
            }

            public float TwoOrMoreHighSatisfactionPercent => Percent(twoOrMoreHighSatisfaction);
            public float TwoOrMoreAwkwardPercent => Percent(twoOrMoreAwkward);
            public float ThreeLargePiecesPercent => Percent(threeLargePieces);
            public float HealthyFlexibilityPercent => Percent(healthyFlexibility);
            public float CoherentTwoPiecePercent => Percent(coherentTwoPiece);
            public float CoherentFullSequencePercent => Percent(coherentFullSequence);

            public void Add(TrayCurationAnalysis analysis, PieceInstance[] pieces)
            {
                trays++;
                if (analysis.highSatisfactionPieces >= 2) twoOrMoreHighSatisfaction++;
                if (analysis.awkwardPieces >= 2) twoOrMoreAwkward++;
                if (analysis.largePieces >= 3) threeLargePieces++;
                if (analysis.healthyFlexibilityPieces >= 2) healthyFlexibility++;
                if (analysis.hasCoherentTwoPieceSequence) coherentTwoPiece++;
                if (analysis.hasCoherentFullSequence) coherentFullSequence++;
                for (int i = 0; i < pieces.Length; i++)
                {
                    if (pieces[i] != null)
                    {
                        PieceSatisfactionClass satisfactionClass = PieceCatalog.GetErgonomicProfile(pieces[i].Data).satisfactionClass;
                        piecesBySatisfactionClass[(int)satisfactionClass]++;
                    }
                }
            }

            public void Add(TrayQualityStats other)
            {
                trays += other.trays;
                twoOrMoreHighSatisfaction += other.twoOrMoreHighSatisfaction;
                twoOrMoreAwkward += other.twoOrMoreAwkward;
                threeLargePieces += other.threeLargePieces;
                healthyFlexibility += other.healthyFlexibility;
                coherentTwoPiece += other.coherentTwoPiece;
                coherentFullSequence += other.coherentFullSequence;
                for (int i = 0; i < piecesBySatisfactionClass.Length; i++)
                {
                    piecesBySatisfactionClass[i] += other.piecesBySatisfactionClass[i];
                }
            }

            private float Percent(long value)
            {
                return trays == 0 ? 0f : value * 100f / trays;
            }
        }

        private struct PlacementProfile
        {
            public int placementOptions;
            public int clearOpportunities;
            public int bestSetupScore;
            public int bestAdjacencyContacts;
            public int bestLineProgress;
            public int bestCleanlinessScore;
            public bool hasSetupOrigin;
            public int bestSetupX;
            public int bestSetupY;
        }

        private struct SetupPayoffAnalysis
        {
            public int currentScore;
            public int pureScore;
            public string setupShapeId;
            public string payoffShapeId;
            public int setupCells;
            public int payoffCells;

            public string Description => pureScore <= 0
                ? "none"
                : $"{setupShapeId} ({setupCells}) non-clearing build -> {payoffShapeId} ({payoffCells}) new line clear";
        }

        private struct TrioSequenceProfile
        {
            public int score;
            public int secondPlacementOptions;
            public int thirdPlacementOptions;
            public int completedLines;
            public bool hasCoherentSequence;
        }

        private struct TrayCurationAnalysis
        {
            public int satisfactionScore;
            public int flexibilityScore;
            public int compositionScore;
            public int pairCoherenceScore;
            public int sequenceScore;
            public int highSatisfactionPieces;
            public int awkwardPieces;
            public int largePieces;
            public int healthyFlexibilityPieces;
            public bool hasCoherentTwoPieceSequence;
            public bool hasCoherentFullSequence;

            public int BaseScore => satisfactionScore + flexibilityScore + compositionScore + pairCoherenceScore;
            public int TotalScore => BaseScore + sequenceScore;
        }

        private struct TrayGenerationResult
        {
            public PieceInstance[] pieces;
            public int[] piecePlacementOptions;
            public int selectedScore;
            public ScoreTerms scoreTerms;
            public TrayCurationAnalysis curation;
            public int occupiedCells;
            public OccupancyState occupancyState;
            public float runPressure;
            public int immediateClearOpportunities;
            public int setupOpportunities;
            public int setupPayoffOpportunities;
            public int pureSetupOpportunities;
            public int adjacencyContacts;
            public int lineProgress;
            public int cleanlinessScore;
            public bool immediateClearSetupOverlap;
            public bool usesPureSetupScoring;
            public bool openDiversityBonusApplied;
            public SetupClassification setupClassification;
            public string pureSetupDescription;
            public SelectionReason selectionReason;
            public bool reliefBiased;
            public int lightCurationScoreBeforeGate;
            public int lightCurationScoreAfterGate;
            public bool curationChangedRanking;
            public bool matchedFlowTarget;
            public int trayEaseScore;
            public bool challengeBandFallback;
            public bool criticalChallengeBypass;
        }

        private struct ScoreTerms
        {
            public int fitFairness;
            public int connectivity;
            public int lineProgress;
            public int immediateClear;
            public int setupPayoff;
            public int cleanliness;
            public int rescueRelief;
            public int pressure;
            public int pieceSize;
            public int other;
            public int phase8CurationBeforeGate;
            public int phase8CurationAfterGate;
            public int Total => fitFairness + connectivity + lineProgress + immediateClear + setupPayoff
                + cleanliness + rescueRelief + pressure + pieceSize + other;
        }

        // Selection-reason instrumentation: compare the final winning tray with the
        // mean score-term contribution across its original 56 candidates. This avoids
        // mistaking the universal all-fit base for the reason a particular tray won.
        private struct CandidateTermBaseline
        {
            private long fitFairness;
            private long connectivity;
            private long lineProgress;
            private long immediateClear;
            private long setupPayoff;
            private long cleanliness;
            private long rescueRelief;
            private long pressure;
            private long pieceSize;
            private long other;
            private int count;

            public void Add(ScoreTerms terms)
            {
                fitFairness += terms.fitFairness;
                connectivity += terms.connectivity;
                lineProgress += terms.lineProgress;
                immediateClear += terms.immediateClear;
                setupPayoff += terms.setupPayoff;
                cleanliness += terms.cleanliness;
                rescueRelief += terms.rescueRelief;
                pressure += terms.pressure;
                pieceSize += terms.pieceSize;
                other += terms.other;
                count++;
            }

            public SelectionReason GetDominantAdvantage(ScoreTerms winner)
            {
                if (count == 0)
                {
                    return GetDominantSelectionReason(winner);
                }

                float best = winner.fitFairness - fitFairness / (float)count;
                SelectionReason reason = SelectionReason.FitFairness;
                Compare(winner.setupPayoff - setupPayoff / (float)count, SelectionReason.SetupPayoff, ref best, ref reason);
                Compare(winner.immediateClear - immediateClear / (float)count, SelectionReason.ImmediateClear, ref best, ref reason);
                Compare(winner.connectivity - connectivity / (float)count, SelectionReason.Connectivity, ref best, ref reason);
                Compare(winner.lineProgress - lineProgress / (float)count, SelectionReason.LineProgress, ref best, ref reason);
                Compare(winner.rescueRelief - rescueRelief / (float)count, SelectionReason.Relief, ref best, ref reason);
                Compare(winner.pieceSize - pieceSize / (float)count, SelectionReason.PieceSize, ref best, ref reason);
                Compare(winner.other - other / (float)count, SelectionReason.Other, ref best, ref reason);
                return reason;
            }

            private static void Compare(float value, SelectionReason candidate, ref float best, ref SelectionReason reason)
            {
                if (value > best)
                {
                    best = value;
                    reason = candidate;
                }
            }
        }

        private struct PlacementDecision
        {
            public int trayIndex;
            public PieceInstance piece;
            public int x;
            public int y;
            public int adjacencyContacts;
        }

        private struct TrayChoiceTracker
        {
            private int immediateClearChoices;
            private int setupPayoffChoices;
            private int connectivityChoices;

            public void Record(int clearedLines, int setupScore, int adjacencyContacts)
            {
                if (clearedLines > 0)
                {
                    immediateClearChoices++;
                }
                else if (setupScore >= 54)
                {
                    setupPayoffChoices++;
                }
                else if (adjacencyContacts > 0)
                {
                    connectivityChoices++;
                }
                else
                {
                    connectivityChoices++;
                }
            }

            public void AddTo(RunMetrics metrics)
            {
                metrics.completedChoiceTrays++;
                if (immediateClearChoices >= setupPayoffChoices && immediateClearChoices >= connectivityChoices)
                {
                    metrics.selectedImmediateClearTrays++;
                }
                else if (setupPayoffChoices >= connectivityChoices)
                {
                    metrics.selectedSetupPayoffTrays++;
                }
                else
                {
                    metrics.selectedConnectivityTrays++;
                }
            }
        }

        // Observational instrumentation only. A setup is a non-clearing move with the
        // same threshold used by TrayChoiceTracker. It lets the report distinguish a
        // later clear within the same tray from one reached in a following tray without
        // influencing generation or player choice.
        private struct SetupPayoffExecutionTracker
        {
            private bool setupSeenThisTray;
            private bool payoffSeenThisTray;
            private bool hasPendingSetup;
            private int pendingSetupTray;
            private int setupCarrierTrays;
            private int sameTrayPayoffs;
            private int nextTrayPayoffs;
            private int totalTraysToPayoff;

            public void RecordMove(int setupScore, int clearedLines, int currentTray)
            {
                if (clearedLines > 0)
                {
                    if (setupSeenThisTray && !payoffSeenThisTray)
                    {
                        payoffSeenThisTray = true;
                        sameTrayPayoffs++;
                    }
                    else if (hasPendingSetup)
                    {
                        nextTrayPayoffs++;
                        totalTraysToPayoff += Math.Max(1, currentTray - pendingSetupTray);
                        hasPendingSetup = false;
                    }

                    return;
                }

                if (setupScore >= 54)
                {
                    setupSeenThisTray = true;
                }
            }

            public void CompleteTray(int currentTray)
            {
                if (setupSeenThisTray)
                {
                    setupCarrierTrays++;
                    if (!payoffSeenThisTray && !hasPendingSetup)
                    {
                        hasPendingSetup = true;
                        pendingSetupTray = currentTray;
                    }
                }

                setupSeenThisTray = false;
                payoffSeenThisTray = false;
            }

            public void AddTo(RunMetrics metrics)
            {
                metrics.setupCarrierTrays += setupCarrierTrays;
                metrics.sameTraySetupPayoffs += sameTrayPayoffs;
                metrics.nextTraySetupPayoffs += nextTrayPayoffs;
                metrics.totalTraysToSetupPayoff += totalTraysToPayoff;
            }
        }

        private struct PlacementEvaluation
        {
            public int lines;
            public int lineProgress;
            public int adjacencyContacts;
            public int isolatedHoles;
            public int occupiedAfterClear;
            public int largestOpenArea;
        }

        private sealed class ClearOutcome
        {
            public int lines;
            public int pureLines;
            public int cellsCleared;
            public readonly int[] clearedByColor = new int[GameConstants.ColorCount];
            public readonly int[] pureLinesByColor = new int[GameConstants.ColorCount];
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

        private readonly struct TrioSequenceKey : IEquatable<TrioSequenceKey>
        {
            private readonly string firstShapeId;
            private readonly string secondShapeId;
            private readonly string thirdShapeId;

            public TrioSequenceKey(string firstShapeId, string secondShapeId, string thirdShapeId)
            {
                this.firstShapeId = firstShapeId;
                this.secondShapeId = secondShapeId;
                this.thirdShapeId = thirdShapeId;
            }

            public bool Equals(TrioSequenceKey other)
            {
                return string.Equals(firstShapeId, other.firstShapeId, StringComparison.Ordinal)
                    && string.Equals(secondShapeId, other.secondShapeId, StringComparison.Ordinal)
                    && string.Equals(thirdShapeId, other.thirdShapeId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TrioSequenceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = firstShapeId == null ? 0 : firstShapeId.GetHashCode();
                    hash = hash * 397 ^ (secondShapeId == null ? 0 : secondShapeId.GetHashCode());
                    return hash * 397 ^ (thirdShapeId == null ? 0 : thirdShapeId.GetHashCode());
                }
            }
        }

        private enum OccupancyState
        {
            Open,
            Balanced,
            Pressured,
            Critical
        }

        private enum CandidateSelectionMode
        {
            SmartScore,
            RandomValid
        }

        private enum SimulatedPlayerPolicy
        {
            ClearFirst,
            BalancedHuman
        }

        private enum SetupClassification
        {
            PureTwoStep,
            ImmediateClearAndPayoff,
            DirectPayoff,
            GeneralFutureSetup
        }

        private enum SelectionReason
        {
            FitFairness,
            SetupPayoff,
            ImmediateClear,
            Connectivity,
            LineProgress,
            Relief,
            PieceSize,
            Other
        }
    }
}
