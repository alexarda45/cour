namespace ChromaBlast
{
    public enum AchievementId
    {
        FirstMove = 0,
        FirstClear = 1,
        FirstPure = 2,
        FirstPop = 3,
        ChainThree = 4,
        ScoreThousand = 5,
        TripleClear = 6,
        FirstDaily = 7,
        BoardSweep = 8
    }

    public struct AchievementReward
    {
        public AchievementId id;
        public string title;
        public string description;
        public int coins;
    }

    public static class AchievementSystem
    {
        public const int Total = 9;

        public static AchievementReward Get(AchievementId id)
        {
            switch (id)
            {
                case AchievementId.FirstClear:
                    return Reward(id, "PRIMA LINIE", "Ai curatat prima linie", 18);
                case AchievementId.FirstPure:
                    return Reward(id, "PURE!", "Ai facut o linie dintr-o singura culoare", 32);
                case AchievementId.FirstPop:
                    return Reward(id, "PRIMUL POP", "Ai detonat o culoare", 30);
                case AchievementId.ChainThree:
                    return Reward(id, "CHAIN x3", "Ai curatat linii in 3 mutari cu scor mare", 26);
                case AchievementId.ScoreThousand:
                    return Reward(id, "1000 SCOR", "Ai trecut de 1000 puncte", 24);
                case AchievementId.TripleClear:
                    return Reward(id, "TRIPLE CLEAR", "Ai curatat 3 linii dintr-o mutare", 42);
                case AchievementId.FirstDaily:
                    return Reward(id, "DAILY START", "Ai intrat in provocarea zilnica", 20);
                case AchievementId.BoardSweep:
                    return Reward(id, "TABLA CURATA", "Ai lasat tabla aproape goala dupa o curatare mare", 44);
                default:
                    return Reward(id, "PRIMA MUTARE", "Ai pus prima piesa", 10);
            }
        }

        public static int CountUnlocked(long mask)
        {
            int count = 0;
            for (int i = 0; i < Total; i++)
            {
                if ((mask & (1L << i)) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static AchievementReward Reward(AchievementId id, string title, string description, int coins)
        {
            return new AchievementReward
            {
                id = id,
                title = title,
                description = description,
                coins = coins
            };
        }
    }
}
