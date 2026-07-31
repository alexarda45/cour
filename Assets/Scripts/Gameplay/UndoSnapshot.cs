using System;

namespace ChromaBlast
{
    [Serializable]
    public class UndoSnapshot
    {
        public BoardSnapshot board;
        public ScoreSnapshot score;
        public PieceInstance[] trayPieces;
        public float blitzTimeRemaining;
        public int movesSinceClear;
    }
}
