using System;

namespace ChromaBlast
{
    [Serializable]
    public class ClearResult
    {
        public int linesCleared;
        public int pureLines;
        public int cellsCleared;
        public int[] clearedByColor = new int[GameConstants.ColorCount];
        public int[] pureLinesByColor = new int[GameConstants.ColorCount];

        public bool HasClear => linesCleared > 0 || cellsCleared > 0;

        public void AddClearedCell(ChromaColor color)
        {
            cellsCleared++;
            clearedByColor[(int)color]++;
        }

        public void AddPureLine(ChromaColor color)
        {
            pureLines++;
            pureLinesByColor[(int)color]++;
        }
    }
}
