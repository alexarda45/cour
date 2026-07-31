using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class RankProgressView : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankLabel;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private Slider progressSlider;

        public void Refresh(int rankPoints)
        {
            RankInfo info = RankSystem.GetInfo(rankPoints);
            if (rankLabel != null)
            {
                rankLabel.text = info.name;
            }

            if (progressLabel != null)
            {
                progressLabel.text = info.nextThreshold == info.currentThreshold
                    ? "Rang maxim"
                    : $"{rankPoints}/{info.nextThreshold}";
            }

            if (progressSlider != null)
            {
                progressSlider.value = info.progress01;
            }
        }
    }
}
