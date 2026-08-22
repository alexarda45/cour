using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ChromaBlast
{
    [Serializable]
    public class ScoreSnapshot
    {
        public int score;
        public int chain;
        public int[] chroma = new int[GameConstants.ColorCount];
    }

    public struct ScoreDelta
    {
        public int totalAdded;
        public int placementScore;
        public int lineScore;
        public int pureBonus;
        public int popScore;
        public bool hadClear;
        public bool hadPure;
    }

    public class ScoreManager : MonoBehaviour
    {
        private const int PlacementScorePerCell = 5;
        private const int LineScoreBase = 150;
        private const int PureLineBonusBase = 650;
        private const int ClearedCellBonus = 12;
        private const int PopScorePerCell = 90;
        private const int LargePopBonus = 300;
        private const float MinimumPopRechargeMultiplier = 1f;

        private sealed class PopReadySnapshot
        {
            public readonly bool[] ready = new bool[GameConstants.ColorCount];
        }

        // Undo snapshots stay schema-compatible while retaining exact per-color
        // readiness in memory. Persisted legacy snapshots are reconstructed
        // conservatively in Restore without adding fields to SaveData.
        private static readonly ConditionalWeakTable<ScoreSnapshot, PopReadySnapshot> PopReadySnapshots =
            new ConditionalWeakTable<ScoreSnapshot, PopReadySnapshot>();

        public event Action Changed;

        public int Score { get; private set; }
        public int Chain { get; private set; }
        public float PopRechargeMultiplier { get; private set; } = MinimumPopRechargeMultiplier;
        public int CurrentPopRequirement => Mathf.CeilToInt(GameConstants.ChromaThreshold * PopRechargeMultiplier);

        private readonly int[] chroma = new int[GameConstants.ColorCount];
        private readonly bool[] popReadyLatched = new bool[GameConstants.ColorCount];

        public void ResetScore()
        {
            Score = 0;
            Chain = 0;
            for (int i = 0; i < chroma.Length; i++)
            {
                chroma[i] = 0;
                popReadyLatched[i] = false;
            }

            Changed?.Invoke();
        }

        public ScoreDelta ApplyMove(ClearResult clearResult)
        {
            return ApplyMove(clearResult, 0);
        }

        public ScoreDelta ApplyMove(ClearResult clearResult, int placedCells)
        {
            ScoreDelta delta = new ScoreDelta();
            delta.placementScore = Mathf.Max(0, placedCells) * PlacementScorePerCell;

            if (clearResult == null || clearResult.linesCleared <= 0)
            {
                Chain = 0;
                delta.totalAdded = delta.placementScore;
                Score += delta.totalAdded;
                Changed?.Invoke();
                return delta;
            }

            Chain++;
            delta.hadClear = true;
            delta.hadPure = clearResult.pureLines > 0;
            float chainMultiplier = GetChainScoreMultiplier(Chain);
            delta.lineScore = Mathf.RoundToInt(clearResult.linesCleared * LineScoreBase * chainMultiplier);
            delta.pureBonus = Mathf.RoundToInt(clearResult.pureLines * PureLineBonusBase * chainMultiplier);
            int cellBonus = clearResult.cellsCleared * ClearedCellBonus;
            delta.totalAdded = delta.placementScore + delta.lineScore + delta.pureBonus + cellBonus;
            Score += delta.totalAdded;

            for (int i = 0; i < GameConstants.ColorCount; i++)
            {
                if (clearResult.clearedByColor[i] > 0)
                {
                    AddChroma((ChromaColor)i, clearResult.clearedByColor[i]);
                }

                if (clearResult.pureLinesByColor[i] > 0)
                {
                    AddChroma((ChromaColor)i, clearResult.pureLinesByColor[i] * 6);
                }
            }

            Changed?.Invoke();
            return delta;
        }

        public static float GetChainScoreMultiplier(int chain)
        {
            switch (Mathf.Max(1, chain))
            {
                case 1:
                    return 1f;
                case 2:
                    return 1.6f;
                case 3:
                    return 2.4f;
                case 4:
                    return 3.4f;
                case 5:
                    return 4.6f;
                default:
                    return 4.6f + (chain - 5) * 1.4f;
            }
        }

        public ScoreDelta ApplyPop(ChromaColor color, int cellsPopped, bool notifyChanged = true)
        {
            ScoreDelta delta = new ScoreDelta();
            if (cellsPopped <= 0)
            {
                chroma[(int)color] = 0;
                popReadyLatched[(int)color] = false;
                if (notifyChanged)
                {
                    Changed?.Invoke();
                }
                return delta;
            }

            delta.popScore = cellsPopped * PopScorePerCell + (cellsPopped >= 8 ? LargePopBonus : 0);
            delta.totalAdded = delta.popScore;
            Score += delta.totalAdded;
            chroma[(int)color] = 0;
            popReadyLatched[(int)color] = false;
            if (notifyChanged)
            {
                Changed?.Invoke();
            }
            return delta;
        }

        public void AddBonusScore(int amount)
        {
            int bonus = Mathf.Max(0, amount);
            if (bonus <= 0)
            {
                return;
            }

            Score += bonus;
            Changed?.Invoke();
        }

        public void AddRewardedChroma(ChromaColor color)
        {
            chroma[(int)color] = CurrentPopRequirement;
            popReadyLatched[(int)color] = true;
            Changed?.Invoke();
        }

        public void SetPopRechargeMultiplier(float multiplier, bool notifyChanged = true)
        {
            // Fatigue applies to future charging only. A color that already
            // reached READY stays latched until that exact color is consumed.
            // Do not rewrite sibling charge values when the global requirement
            // changes; that was the source of the all-POP-disappear bug.
            PopRechargeMultiplier = Mathf.Max(MinimumPopRechargeMultiplier, multiplier);
            if (notifyChanged)
            {
                Changed?.Invoke();
            }
        }

        public int GetChroma(ChromaColor color)
        {
            return chroma[(int)color];
        }

        public float GetChroma01(ChromaColor color)
        {
            if (popReadyLatched[(int)color])
            {
                return 1f;
            }

            return Mathf.Clamp01(chroma[(int)color] / (float)CurrentPopRequirement);
        }

        public bool IsPopReady(ChromaColor color)
        {
            return popReadyLatched[(int)color];
        }

        public ScoreSnapshot CreateSnapshot()
        {
            ScoreSnapshot snapshot = new ScoreSnapshot
            {
                score = Score,
                chain = Chain,
                chroma = new int[GameConstants.ColorCount]
            };
            Array.Copy(chroma, snapshot.chroma, chroma.Length);
            PopReadySnapshot latchSnapshot = PopReadySnapshots.GetOrCreateValue(snapshot);
            Array.Copy(popReadyLatched, latchSnapshot.ready, popReadyLatched.Length);
            return snapshot;
        }

        public void Restore(ScoreSnapshot snapshot)
        {
            if (snapshot == null)
            {
                ResetScore();
                return;
            }

            Score = snapshot.score;
            Chain = snapshot.chain;
            for (int i = 0; i < chroma.Length; i++)
            {
                chroma[i] = snapshot.chroma != null && i < snapshot.chroma.Length ? snapshot.chroma[i] : 0;
            }

            if (PopReadySnapshots.TryGetValue(snapshot, out PopReadySnapshot latchSnapshot))
            {
                Array.Copy(latchSnapshot.ready, popReadyLatched, popReadyLatched.Length);
            }
            else
            {
                // Older/on-disk snapshots have no latch sidecar. Preserve an
                // already-earned READY state player-favorably without changing
                // the serialized schema. New charge still uses the current
                // fatigue requirement after that color is consumed.
                for (int i = 0; i < popReadyLatched.Length; i++)
                {
                    popReadyLatched[i] = chroma[i] >= GameConstants.ChromaThreshold;
                }
            }

            Changed?.Invoke();
        }

        private void AddChroma(ChromaColor color, int amount)
        {
            int index = (int)color;
            if (popReadyLatched[index])
            {
                return;
            }

            chroma[index] = Mathf.Min(CurrentPopRequirement, chroma[index] + amount);
            if (chroma[index] >= CurrentPopRequirement)
            {
                popReadyLatched[index] = true;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugGetPopReadyLatch(ChromaColor color)
        {
            return popReadyLatched[(int)color];
        }

        public void DebugSetPopState(ChromaColor color, int amount, bool readyLatched)
        {
            int index = (int)color;
            chroma[index] = Mathf.Max(0, amount);
            popReadyLatched[index] = readyLatched;
        }
#endif
    }
}
