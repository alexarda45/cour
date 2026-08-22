using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChromaBlast
{
#if UNITY_EDITOR
    // Phase 8's shape-classification data is retained only for the Editor-only
    // balance report. It is excluded from player builds and never participates
    // in production tray selection.
    public enum PieceSatisfactionClass
    {
        A,
        B,
        C,
        D
    }

    public readonly struct PieceErgonomicProfile
    {
        public readonly int satisfactionScore;
        public readonly PieceSatisfactionClass satisfactionClass;

        public PieceErgonomicProfile(int satisfactionScore, PieceSatisfactionClass satisfactionClass)
        {
            this.satisfactionScore = satisfactionScore;
            this.satisfactionClass = satisfactionClass;
        }
    }
#endif

    public static class PieceCatalog
    {
        private static readonly List<PieceData> Pieces = new List<PieceData>
        {
            P("single", V(0, 0)),
            P("line2_h", V(0, 0), V(1, 0)),
            P("line2_v", V(0, 0), V(0, 1)),
            P("line3_h", V(0, 0), V(1, 0), V(2, 0)),
            P("line3_v", V(0, 0), V(0, 1), V(0, 2)),
            P("line4_h", V(0, 0), V(1, 0), V(2, 0), V(3, 0)),
            P("line4_v", V(0, 0), V(0, 1), V(0, 2), V(0, 3)),
            P("line5_h", V(0, 0), V(1, 0), V(2, 0), V(3, 0), V(4, 0)),
            P("line5_v", V(0, 0), V(0, 1), V(0, 2), V(0, 3), V(0, 4)),
            P("square2", V(0, 0), V(1, 0), V(0, 1), V(1, 1)),
            P("square3",
                V(0, 0), V(1, 0), V(2, 0),
                V(0, 1), V(1, 1), V(2, 1),
                V(0, 2), V(1, 2), V(2, 2)),
            P("rect2x3", V(0, 0), V(1, 0), V(0, 1), V(1, 1), V(0, 2), V(1, 2)),
            P("rect3x2", V(0, 0), V(1, 0), V(2, 0), V(0, 1), V(1, 1), V(2, 1)),
            P("corner3", V(0, 0), V(0, 1), V(1, 0)),
            P("corner3_m", V(0, 0), V(1, 0), V(1, 1)),
            P("l4", V(0, 0), V(0, 1), V(0, 2), V(1, 0)),
            P("l4_m", V(1, 0), V(1, 1), V(1, 2), V(0, 0)),
            P("l4_r", V(0, 0), V(1, 0), V(2, 0), V(0, 1)),
            P("l4_rm", V(0, 0), V(1, 0), V(2, 0), V(2, 1)),
            P("t4", V(0, 0), V(1, 0), V(2, 0), V(1, 1)),
            P("t4_v", V(0, 0), V(0, 1), V(0, 2), V(1, 1)),
            P("s4", V(1, 0), V(2, 0), V(0, 1), V(1, 1)),
            P("z4", V(0, 0), V(1, 0), V(1, 1), V(2, 1)),
            P("plus5", V(1, 0), V(0, 1), V(1, 1), V(2, 1), V(1, 2)),
            P("stair5", V(0, 0), V(1, 0), V(1, 1), V(2, 1), V(2, 2))
        };

        private static readonly Dictionary<string, PieceData> ById = BuildLookup();
#if UNITY_EDITOR
        private static readonly Dictionary<string, PieceErgonomicProfile> EditorErgonomicProfiles = BuildEditorErgonomicProfiles();
#endif

        public static IReadOnlyList<PieceData> All => Pieces;

        public static PieceData Get(string id)
        {
            if (ById.TryGetValue(id, out PieceData data))
            {
                return data;
            }

            return ById["single"];
        }

#if UNITY_EDITOR
        public static PieceErgonomicProfile GetErgonomicProfile(PieceData data)
        {
            return data != null && EditorErgonomicProfiles.TryGetValue(data.id, out PieceErgonomicProfile profile)
                ? profile
                : default;
        }
#endif

        public static PieceInstance RandomPiece(System.Random random)
        {
            return RandomPiece(random, 0.5f);
        }

        public static PieceInstance RandomPiece(System.Random random, float difficulty01)
        {
            return RandomPiece(random, difficulty01, allowStair5: true);
        }

        public static PieceInstance RandomPiece(System.Random random, float difficulty01, bool allowStair5)
        {
            PieceData data = WeightedRandomPiece(random, Mathf.Clamp01(difficulty01), allowStair5);
            ChromaColor color = (ChromaColor)random.Next(GameConstants.ColorCount);
            return new PieceInstance(data.id, color);
        }

        public static PieceInstance[] RandomSet(System.Random random)
        {
            return RandomSet(random, 0.5f);
        }

        public static PieceInstance[] RandomSet(System.Random random, float difficulty01)
        {
            PieceInstance[] set = new PieceInstance[GameConstants.TraySize];
            FillRandomSet(set, random, difficulty01);
            return set;
        }

        public static void FillRandomSet(PieceInstance[] destination, System.Random random, float difficulty01)
        {
            FillRandomSet(destination, random, difficulty01, allowStair5: true);
        }

        public static void FillRandomSet(
            PieceInstance[] destination,
            System.Random random,
            float difficulty01,
            bool allowStair5)
        {
            if (destination == null || random == null)
            {
                return;
            }

            int count = Mathf.Min(destination.Length, GameConstants.TraySize);
            for (int i = 0; i < count; i++)
            {
                destination[i] = RandomPiece(random, difficulty01, allowStair5);
            }
        }

        private static PieceData WeightedRandomPiece(System.Random random, float difficulty01, bool allowStair5)
        {
            float totalWeight = 0f;
            for (int i = 0; i < Pieces.Count; i++)
            {
                // plus5 stays in the catalog for serialized ID stability, but
                // human playtesting rejected it as a runtime gameplay shape.
                // Keep this exclusion at the source of every random gameplay
                // pool so Classic, Blitz, and fallback generation agree.
                if (Pieces[i].id == "plus5"
                    || (!allowStair5 && Pieces[i].id == "stair5"))
                {
                    continue;
                }

                totalWeight += WeightFor(Pieces[i], difficulty01);
            }

            float roll = (float)(random.NextDouble() * totalWeight);
            for (int i = 0; i < Pieces.Count; i++)
            {
                if (Pieces[i].id == "plus5"
                    || (!allowStair5 && Pieces[i].id == "stair5"))
                {
                    continue;
                }

                roll -= WeightFor(Pieces[i], difficulty01);
                if (roll <= 0f)
                {
                    return Pieces[i];
                }
            }

            for (int i = Pieces.Count - 1; i >= 0; i--)
            {
                if (Pieces[i].id != "plus5"
                    && (allowStair5 || Pieces[i].id != "stair5"))
                {
                    return Pieces[i];
                }
            }

            return Pieces[0];
        }

        private static float WeightFor(PieceData data, float difficulty01)
        {
            int cellCount = data.cells.Length;
            float targetSize = Mathf.Lerp(4.15f, 5.65f, difficulty01);
            float distance = Mathf.Abs(cellCount - targetSize);
            float weight = Mathf.Max(0.15f, 6.0f - distance * 1.25f);

            if (cellCount == 1)
            {
                weight *= Mathf.Lerp(0.24f, 0.10f, difficulty01);
            }
            else if (cellCount == 2)
            {
                weight *= Mathf.Lerp(0.50f, 0.28f, difficulty01);
            }
            else if (cellCount == 3)
            {
                weight *= Mathf.Lerp(0.88f, 0.62f, difficulty01);
            }
            else if (cellCount >= 4 && cellCount <= 5)
            {
                weight *= Mathf.Lerp(1.46f, 1.62f, difficulty01);
            }
            else if (cellCount >= 6)
            {
                weight *= Mathf.Lerp(1.02f, 1.34f, difficulty01);
            }

            if (data.id == "square2")
            {
                weight *= 2.35f;
            }
            else if (data.id == "square3")
            {
                weight *= 4.50f;
            }
            else if (data.id.StartsWith("rect", StringComparison.Ordinal))
            {
                weight *= 2.10f;
            }
            else if (data.id.StartsWith("line4", StringComparison.Ordinal)
                || data.id.StartsWith("line5", StringComparison.Ordinal))
            {
                weight *= 1.30f;
            }

            if (data.id == "plus5" || data.id == "stair5")
            {
                weight *= 0.28f;
            }
            else if (data.id.StartsWith("corner3", StringComparison.Ordinal)
                || data.id.StartsWith("l4", StringComparison.Ordinal)
                || data.id.StartsWith("t4", StringComparison.Ordinal)
                || data.id == "s4"
                || data.id == "z4")
            {
                weight *= 0.48f;
            }

            if (difficulty01 < 0.14f && cellCount >= 6)
            {
                weight *= 0.45f;
            }
            else if (difficulty01 < 0.30f && cellCount >= 6)
            {
                weight *= 0.70f;
            }

            if (difficulty01 > 0.70f && cellCount >= 5)
            {
                weight *= 1.22f;
            }

            return weight;
        }

        private static Dictionary<string, PieceData> BuildLookup()
        {
            Dictionary<string, PieceData> lookup = new Dictionary<string, PieceData>();
            for (int i = 0; i < Pieces.Count; i++)
            {
                lookup[Pieces[i].id] = Pieces[i];
            }

            return lookup;
        }

#if UNITY_EDITOR
        private static Dictionary<string, PieceErgonomicProfile> BuildEditorErgonomicProfiles()
        {
            Dictionary<string, PieceErgonomicProfile> profiles = new Dictionary<string, PieceErgonomicProfile>(Pieces.Count);
            for (int pieceIndex = 0; pieceIndex < Pieces.Count; pieceIndex++)
            {
                PieceData data = Pieces[pieceIndex];
                int turns = 0;
                int branches = 0;
                for (int cellIndex = 0; cellIndex < data.cells.Length; cellIndex++)
                {
                    Vector2Int cell = data.cells[cellIndex];
                    int neighbours = 0;
                    bool hasHorizontalNeighbour = false;
                    bool hasVerticalNeighbour = false;
                    for (int otherIndex = 0; otherIndex < data.cells.Length; otherIndex++)
                    {
                        if (cellIndex == otherIndex)
                        {
                            continue;
                        }

                        Vector2Int other = data.cells[otherIndex];
                        int deltaX = other.x - cell.x;
                        int deltaY = other.y - cell.y;
                        if (Mathf.Abs(deltaX) + Mathf.Abs(deltaY) != 1)
                        {
                            continue;
                        }

                        neighbours++;
                        hasHorizontalNeighbour |= deltaX != 0;
                        hasVerticalNeighbour |= deltaY != 0;
                    }

                    if (neighbours >= 3)
                    {
                        branches++;
                    }
                    else if (neighbours == 2 && hasHorizontalNeighbour && hasVerticalNeighbour)
                    {
                        turns++;
                    }
                }

                int footprint = data.width * data.height;
                int gaps = Mathf.Max(0, footprint - data.cells.Length);
                int compactness = footprint <= 0 ? 0 : Mathf.RoundToInt(data.cells.Length * 100f / footprint);
                bool isStraight = data.width == 1 || data.height == 1;
                bool isFullRectangle = gaps == 0;
                int ergonomicTurns = isFullRectangle ? 0 : turns;
                int ergonomicBranches = isFullRectangle ? 0 : branches;
                int score = 28
                    + Mathf.RoundToInt(compactness * 0.30f)
                    + (isFullRectangle ? 24 : 0)
                    + (isStraight ? 18 : 0)
                    + (data.cells.Length >= 3 && data.cells.Length <= 5 ? 13 : 0)
                    + (!isStraight && ergonomicBranches == 0 ? 14 : 0)
                    - gaps * 4
                    - ergonomicTurns * 7
                    - ergonomicBranches * 10;

                if (ergonomicTurns >= 2 && compactness < 65)
                {
                    score -= 20;
                }

                if (data.cells.Length >= 8)
                {
                    score -= 25;
                }
                else if (data.cells.Length >= 6)
                {
                    score -= 4;
                }

                score = Mathf.Clamp(score, 0, 100);
                PieceSatisfactionClass satisfactionClass = score >= 84
                    ? PieceSatisfactionClass.A
                    : score >= 55
                        ? PieceSatisfactionClass.B
                        : score >= 38
                            ? PieceSatisfactionClass.C
                            : PieceSatisfactionClass.D;
                profiles.Add(data.id, new PieceErgonomicProfile(score, satisfactionClass));
            }

            return profiles;
        }
#endif

        private static PieceData P(string id, params Vector2Int[] cells)
        {
            return new PieceData(id, cells);
        }

        private static Vector2Int V(int x, int y)
        {
            return new Vector2Int(x, y);
        }
    }
}
