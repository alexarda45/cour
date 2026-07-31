using System;

namespace ChromaBlast
{
    [Serializable]
    public class PieceInstance
    {
        public string shapeId;
        public ChromaColor color;

        public PieceInstance()
        {
        }

        public PieceInstance(string shapeId, ChromaColor color)
        {
            this.shapeId = shapeId;
            this.color = color;
        }

        public PieceData Data => PieceCatalog.Get(shapeId);

        public PieceInstance Clone()
        {
            return new PieceInstance(shapeId, color);
        }
    }
}
