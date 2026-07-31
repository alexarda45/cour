using UnityEngine;

namespace ChromaBlast
{
    public sealed class OceanTileSpriteSet : ScriptableObject
    {
        [SerializeField] private Sprite blue;
        [SerializeField] private Sprite cyan;
        [SerializeField] private Sprite teal;
        [SerializeField] private Sprite pearlWhite;

        public Sprite GetSprite(ChromaColor color)
        {
            switch (color)
            {
                case ChromaColor.Cyan:
                    return cyan != null ? cyan : blue;
                case ChromaColor.Magenta:
                    return blue != null ? blue : cyan;
                case ChromaColor.Lime:
                    return teal != null ? teal : cyan;
                case ChromaColor.Amber:
                    return pearlWhite != null ? pearlWhite : cyan;
                default:
                    return cyan != null ? cyan : blue;
            }
        }
    }
}
