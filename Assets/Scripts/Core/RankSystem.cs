using System;
using UnityEngine;

namespace ChromaBlast
{
    [Serializable]
    public struct RankInfo
    {
        public string name;
        public string nextName;
        public int currentThreshold;
        public int nextThreshold;
        public float progress01;
        public int pointsToNext;
        public bool isMaxRank;
    }

    public static class RankSystem
    {
        private static readonly string[] Names =
        {
            "Novice",
            "Ucenic",
            "Strateg",
            "Expert",
            "Maestru"
        };

        private static readonly int[] Thresholds =
        {
            0,
            1500,
            5000,
            12000,
            25000
        };

        public static RankInfo GetInfo(int rankPoints)
        {
            int index = 0;
            for (int i = 0; i < Thresholds.Length; i++)
            {
                if (rankPoints >= Thresholds[i])
                {
                    index = i;
                }
            }

            int current = Thresholds[index];
            int next = index >= Thresholds.Length - 1 ? Thresholds[index] : Thresholds[index + 1];
            float progress = next == current ? 1f : Mathf.InverseLerp(current, next, rankPoints);
            bool isMaxRank = index >= Thresholds.Length - 1;

            return new RankInfo
            {
                name = Names[index],
                nextName = isMaxRank ? Names[index] : Names[index + 1],
                currentThreshold = current,
                nextThreshold = next,
                progress01 = progress,
                pointsToNext = isMaxRank ? 0 : Mathf.Max(0, next - rankPoints),
                isMaxRank = isMaxRank
            };
        }
    }
}
