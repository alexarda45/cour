using System;
using UnityEngine;

namespace ChromaBlast
{
    [Serializable]
    public class PieceData
    {
        public string id;
        public Vector2Int[] cells;
        public int width;
        public int height;

        public PieceData(string id, params Vector2Int[] cells)
        {
            this.id = id;
            this.cells = cells;
            RecalculateBounds();
        }

        public void RecalculateBounds()
        {
            int maxX = 0;
            int maxY = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                maxX = Mathf.Max(maxX, cells[i].x);
                maxY = Mathf.Max(maxY, cells[i].y);
            }

            width = maxX + 1;
            height = maxY + 1;
        }
    }
}
