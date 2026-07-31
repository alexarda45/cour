using System;
using UnityEngine;

namespace ChromaBlast
{
    [Serializable]
    public struct DailyMedalInfo
    {
        public int index;
        public string name;
        public string nextName;
        public int currentThreshold;
        public int nextThreshold;
        public float progress01;
        public bool isMaxMedal;
    }

    public static class DailyMedalSystem
    {
        private static readonly string[] Names =
        {
            "Fara medalie",
            "Bronz",
            "Argint",
            "Aur",
            "Chroma"
        };

        private static readonly int[] RewardCoins =
        {
            0,
            18,
            42,
            80,
            140
        };

        private static readonly int[] Thresholds =
        {
            0,
            600,
            1800,
            3600,
            6500
        };

        public static DailyMedalInfo GetInfo(int score)
        {
            int index = 0;
            for (int i = 0; i < Thresholds.Length; i++)
            {
                if (score >= Thresholds[i])
                {
                    index = i;
                }
            }

            bool isMax = index >= Thresholds.Length - 1;
            int current = Thresholds[index];
            int next = isMax ? current : Thresholds[index + 1];

            return new DailyMedalInfo
            {
                index = index,
                name = Names[index],
                nextName = isMax ? Names[index] : Names[index + 1],
                currentThreshold = current,
                nextThreshold = next,
                progress01 = isMax ? 1f : Mathf.InverseLerp(current, next, score),
                isMaxMedal = isMax
            };
        }

        public static int GetRewardCoins(int medalIndex)
        {
            int index = Mathf.Clamp(medalIndex, 0, RewardCoins.Length - 1);
            return RewardCoins[index];
        }
    }
}
