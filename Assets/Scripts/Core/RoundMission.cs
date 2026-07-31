using System;

namespace ChromaBlast
{
    public enum RoundMissionType
    {
        ClearLines,
        UsePop,
        ReachChain,
        MakePure,
        ScorePoints
    }

    [Serializable]
    public class RoundMission
    {
        public RoundMissionType type;
        public int target;
        public int progress;
        public int rewardScore;
        public bool completed;

        public static RoundMission Create(GameMode mode, System.Random random)
        {
            if (random == null)
            {
                random = new System.Random();
            }

            RoundMissionType[] pool;
            switch (mode)
            {
                case GameMode.Blitz:
                    pool = new[] { RoundMissionType.ClearLines, RoundMissionType.ReachChain, RoundMissionType.UsePop, RoundMissionType.ScorePoints };
                    break;
                case GameMode.Daily:
                    pool = new[] { RoundMissionType.ClearLines, RoundMissionType.MakePure, RoundMissionType.ScorePoints, RoundMissionType.ReachChain };
                    break;
                default:
                    pool = new[] { RoundMissionType.ClearLines, RoundMissionType.UsePop, RoundMissionType.ReachChain, RoundMissionType.MakePure, RoundMissionType.ScorePoints };
                    break;
            }

            RoundMission mission = new RoundMission
            {
                type = pool[random.Next(pool.Length)],
                rewardScore = mode == GameMode.Blitz ? 180 : 250
            };

            mission.target = mission.GetTargetForMode(mode);
            return mission;
        }

        public bool RegisterMove(ClearResult clearResult, int chain, int score)
        {
            if (completed)
            {
                return false;
            }

            switch (type)
            {
                case RoundMissionType.ClearLines:
                    progress += clearResult == null ? 0 : clearResult.linesCleared;
                    break;
                case RoundMissionType.ReachChain:
                    progress = Math.Max(progress, chain);
                    break;
                case RoundMissionType.MakePure:
                    progress += clearResult == null ? 0 : clearResult.pureLines;
                    break;
                case RoundMissionType.ScorePoints:
                    progress = Math.Max(progress, score);
                    break;
            }

            return CheckCompleted();
        }

        public bool RegisterPop(int cellsPopped, int score)
        {
            if (completed)
            {
                return false;
            }

            if (type == RoundMissionType.UsePop && cellsPopped > 0)
            {
                progress++;
            }
            else if (type == RoundMissionType.ScorePoints)
            {
                progress = Math.Max(progress, score);
            }

            return CheckCompleted();
        }

        public string GetHudText()
        {
            return completed
                ? $"MISIUNE COMPLETA +{rewardScore}"
                : $"MISIUNE: {GetLabel()} {Math.Min(progress, target)}/{target}";
        }

        private bool CheckCompleted()
        {
            if (progress < target)
            {
                return false;
            }

            completed = true;
            progress = target;
            return true;
        }

        private int GetTargetForMode(GameMode mode)
        {
            switch (type)
            {
                case RoundMissionType.ClearLines:
                    return mode == GameMode.Blitz ? 4 : 6;
                case RoundMissionType.UsePop:
                    return 1;
                case RoundMissionType.ReachChain:
                    return mode == GameMode.Blitz ? 2 : 3;
                case RoundMissionType.MakePure:
                    return 1;
                case RoundMissionType.ScorePoints:
                    return mode == GameMode.Blitz ? 1500 : mode == GameMode.Daily ? 1800 : 2200;
                default:
                    return 1;
            }
        }

        private string GetLabel()
        {
            switch (type)
            {
                case RoundMissionType.ClearLines:
                    return "CLEAR";
                case RoundMissionType.UsePop:
                    return "POP";
                case RoundMissionType.ReachChain:
                    return "CHAIN";
                case RoundMissionType.MakePure:
                    return "PURE";
                case RoundMissionType.ScorePoints:
                    return "SCOR";
                default:
                    return "TASK";
            }
        }
    }
}
