using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    /// <summary>
    /// Drop-in component: turns a plain UI panel Image into a glossy rounded panel
    /// at runtime (using the procedural sprite factory), with an optional top gloss
    /// stripe and a soft themed glow. Lets the scene builder author premium panels
    /// without baking any sprites. Safe and self-contained.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class RoundedPanel : MonoBehaviour
    {
        [SerializeField] private float radius = 0.3f;
        [SerializeField] private bool gloss = true;
        [SerializeField] private bool themedGlow = false;
        [SerializeField] private float glowExpand = 34f;

        private void Awake()
        {
            Image img = GetComponent<Image>();
            if (img != null)
            {
                UISpriteFactory.ApplyRounded(img, radius);
            }

            if (themedGlow)
            {
                UISkin.AddGlow((RectTransform)transform, ChromaPalette.GetThemeGlowColor(ChromaPalette.CurrentTheme), glowExpand, "__panelGlow");
            }

            if (gloss)
            {
                UISkin.AddGloss((RectTransform)transform);
            }
        }
    }
}
