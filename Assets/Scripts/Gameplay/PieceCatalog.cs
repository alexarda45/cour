using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChromaBlast
{
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

        public static IReadOnlyList<PieceData> All => Pieces;

        public static PieceData Get(string id)
        {
            if (ById.TryGetValue(id, out PieceData data))
            {
                return data;
            }

            return ById["single"];
        }

        public static PieceInstance RandomPiece(System.Random random)
        {
            return RandomPiece(random, 0.5f);
        }

        public static PieceInstance RandomPiece(System.Random random, float difficulty01)
        {
            PieceData data = WeightedRandomPiece(random, Mathf.Clamp01(difficulty01));
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
            for (int i = 0; i < set.Length; i++)
            {
                set[i] = RandomPiece(random, difficulty01);
            }

            return set;
        }

        private static PieceData WeightedRandomPiece(System.Random random, float difficulty01)
        {
            float totalWeight = 0f;
            for (int i = 0; i < Pieces.Count; i++)
            {
                totalWeight += WeightFor(Pieces[i], difficulty01);
            }

            float roll = (float)(random.NextDouble() * totalWeight);
            for (int i = 0; i < Pieces.Count; i++)
            {
                roll -= WeightFor(Pieces[i], difficulty01);
                if (roll <= 0f)
                {
                    return Pieces[i];
                }
            }

            return Pieces[Pieces.Count - 1];
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

            if (data.id.StartsWith("line4", StringComparison.Ordinal)
                || data.id.StartsWith("line5", StringComparison.Ordinal)
                || data.id.StartsWith("rect", StringComparison.Ordinal)
                || data.id == "square2")
            {
                weight *= 1.30f;
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
