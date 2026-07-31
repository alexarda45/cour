using DG.Tweening;
using UnityEngine;

namespace ChromaBlast
{
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private Transform target;

        private Vector3 startPosition;

        private void Awake()
        {
            if (target == null && Camera.main != null)
            {
                target = Camera.main.transform;
            }

            if (target != null)
            {
                startPosition = target.localPosition;
            }
        }

        public void Shake(float strength, float duration)
        {
            if (!MobilePerformance.UseFullJuice())
            {
                return;
            }

            if (target == null)
            {
                return;
            }

            target.DOKill();
            target.localPosition = startPosition;
            target.DOShakePosition(duration, strength, 18, 70f).OnComplete(() =>
            {
                if (target != null)
                {
                    target.localPosition = startPosition;
                }
            });
        }
    }
}
