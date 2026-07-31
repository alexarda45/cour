#if !CHROMA_USE_DOTWEEN
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DG.Tweening
{
    public enum Ease
    {
        OutBack,
        InBack,
        OutQuad
    }

    public delegate void TweenCallback();

    public class Tween
    {
        protected MonoBehaviour runner;
        protected Coroutine coroutine;
        protected TweenCallback onComplete;
        private bool completed;

        public Tween()
        {
        }

        public Tween(MonoBehaviour runner, Coroutine coroutine)
        {
            this.runner = runner;
            this.coroutine = coroutine;
        }

        public Tween SetEase(Ease ease)
        {
            return this;
        }

        public Tween SetDelay(float delay)
        {
            return this;
        }

        public Tween OnComplete(TweenCallback callback)
        {
            onComplete += callback;
            if (completed)
            {
                callback?.Invoke();
            }

            return this;
        }

        public void Complete()
        {
            completed = true;
            onComplete?.Invoke();
        }

        internal void SetCoroutine(Coroutine value)
        {
            coroutine = value;
        }
    }

    public class Sequence : Tween
    {
        public Sequence Append(Tween tween)
        {
            return this;
        }

        public Sequence Join(Tween tween)
        {
            return this;
        }
    }

    public static class DOTween
    {
        public static Sequence Sequence()
        {
            return new Sequence();
        }
    }

    public static class ShortcutExtensions
    {
        public static Tween DOScale(this Transform target, float endValue, float duration)
        {
            return DOScale(target, Vector3.one * endValue, duration);
        }

        public static Tween DOScale(this Transform target, Vector3 endValue, float duration)
        {
            TweenRunner runner = TweenRunner.Get(target.gameObject);
            return runner.Run(ScaleRoutine(target, target.localScale, endValue, duration));
        }

        public static Tween DOPunchScale(this Transform target, Vector3 punch, float duration, int vibrato, float elasticity)
        {
            TweenRunner runner = TweenRunner.Get(target.gameObject);
            return runner.Run(PunchScaleRoutine(target, punch, duration));
        }

        public static Tween DOAnchorPos(this RectTransform target, Vector2 endValue, float duration)
        {
            TweenRunner runner = TweenRunner.Get(target.gameObject);
            return runner.Run(AnchorPosRoutine(target, target.anchoredPosition, endValue, duration));
        }

        public static Tween DOFade(this Graphic target, float endValue, float duration)
        {
            TweenRunner runner = TweenRunner.Get(target.gameObject);
            return runner.Run(FadeRoutine(target, target.color.a, endValue, duration));
        }

        public static Tween DOShakePosition(this Transform target, float duration, float strength, int vibrato, float randomness)
        {
            TweenRunner runner = TweenRunner.Get(target.gameObject);
            return runner.Run(ShakeRoutine(target, target.localPosition, duration, strength));
        }

        public static void DOKill(this Transform target)
        {
            TweenRunner runner = target == null ? null : target.GetComponent<TweenRunner>();
            if (runner != null)
            {
                runner.StopAllCoroutines();
            }
        }

        private static IEnumerator ScaleRoutine(Transform target, Vector3 start, Vector3 end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                target.localScale = Vector3.Lerp(start, end, Smooth(t));
                yield return null;
            }

            if (target != null)
            {
                target.localScale = end;
            }
        }

        private static IEnumerator PunchScaleRoutine(Transform target, Vector3 punch, float duration)
        {
            Vector3 start = target.localScale;
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                float wave = Mathf.Sin(t * Mathf.PI) * (1f - t);
                target.localScale = start + punch * wave;
                yield return null;
            }

            if (target != null)
            {
                target.localScale = start;
            }
        }

        private static IEnumerator AnchorPosRoutine(RectTransform target, Vector2 start, Vector2 end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                target.anchoredPosition = Vector2.Lerp(start, end, Smooth(t));
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = end;
            }
        }

        private static IEnumerator FadeRoutine(Graphic target, float start, float end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                Color color = target.color;
                color.a = Mathf.Lerp(start, end, Smooth(t));
                target.color = color;
                yield return null;
            }

            if (target != null)
            {
                Color color = target.color;
                color.a = end;
                target.color = color;
            }
        }

        private static IEnumerator ShakeRoutine(Transform target, Vector3 start, float duration, float strength)
        {
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float fade = 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                Vector2 offset = Random.insideUnitCircle * strength * fade;
                target.localPosition = start + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            if (target != null)
            {
                target.localPosition = start;
            }
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }

    public class TweenRunner : MonoBehaviour
    {
        public static TweenRunner Get(GameObject gameObject)
        {
            TweenRunner runner = gameObject.GetComponent<TweenRunner>();
            if (runner == null)
            {
                runner = gameObject.AddComponent<TweenRunner>();
            }

            return runner;
        }

        public Tween Run(IEnumerator routine)
        {
            Tween tween = new Tween(this, null);
            Coroutine coroutine = StartCoroutine(Wrap(routine, tween));
            tween.SetCoroutine(coroutine);
            return tween;
        }

        private IEnumerator Wrap(IEnumerator routine, Tween tween)
        {
            yield return routine;
            tween.Complete();
        }
    }
}
#endif
