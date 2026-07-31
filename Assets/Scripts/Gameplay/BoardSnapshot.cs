using System;

namespace ChromaBlast
{
    [Serializable]
    public class BoardSnapshot
    {
        public int[] colors = new int[GameConstants.BoardSize * GameConstants.BoardSize];

        public BoardSnapshot Clone()
        {
            BoardSnapshot clone = new BoardSnapshot();
            Array.Copy(colors, clone.colors, colors.Length);
            return clone;
        }
    }
}
