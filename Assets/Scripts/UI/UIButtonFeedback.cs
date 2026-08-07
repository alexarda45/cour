using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChromaBlast
{
    [RequireComponent(typeof(Button))]
    public class UIButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float pressedScale = 0.96f;
        [SerializeField] private float duration = 0.055f;

        private Button button;
        private RectTransform rectTransform;
        private Graphic pulseGraphic;
        private Color pulseBaseColor;
        private Coroutine glowRoutine;

        public void Configure(float scale, float pressDuration, Graphic glowGraphic = null)
        {
            pressedScale = Mathf.Clamp(scale, 0.85f, 1f);
            duration = Mathf.Clamp(pressDuration, 0.03f, 0.16f);
            pulseGraphic = glowGraphic;
            if (pulseGraphic != null)
            {
                pulseBaseColor = pulseGraphic.color;
            }
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            rectTransform = (RectTransform)transform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (gameObject.name.Contains("Theme"))
            {
                Debug.Log($"[ThemeButton] UIButtonFeedback.OnPointerDown on '{gameObject.name}' (path={GetHierarchyPath(transform)}), button.interactable={(button == null ? "no Button component" : button.interactable.ToString())}.");
            }

            if (button != null && !button.interactable)
            {
                return;
            }

            rectTransform.DOKill();
            rectTransform.DOScale(pressedScale, duration).SetEase(Ease.OutQuad);
            SetGlowPulse(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (gameObject.name.Contains("Theme"))
            {
                Debug.Log($"[ThemeButton] UIButtonFeedback.OnPointerUp on '{gameObject.name}' (path={GetHierarchyPath(transform)}).");
            }

            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private static string GetHierarchyPath(Transform t)
        {
            if (t == null)
            {
                return "<null>";
            }

            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        private void OnDisable()
        {
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one;
            }

            if (pulseGraphic != null)
            {
                if (glowRoutine != null)
                {
                    StopCoroutine(glowRoutine);
                    glowRoutine = null;
                }

                pulseGraphic.color = pulseBaseColor;
            }
        }

        private void Release()
        {
            rectTransform.DOKill();
            rectTransform.DOScale(1f, duration).SetEase(Ease.OutQuad);
            SetGlowPulse(false);
        }

        private void SetGlowPulse(bool pressed)
        {
            if (pulseGraphic == null)
            {
                return;
            }

            Color target = pulseBaseColor;
            target.a = pressed ? Mathf.Min(0.34f, pulseBaseColor.a * 1.8f) : pulseBaseColor.a;
            if (glowRoutine != null)
            {
                StopCoroutine(glowRoutine);
            }

            glowRoutine = StartCoroutine(AnimateGlow(target));
        }

        private IEnumerator AnimateGlow(Color target)
        {
            Color start = pulseGraphic.color;
            float elapsed = 0f;
            while (elapsed < duration && pulseGraphic != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                pulseGraphic.color = Color.Lerp(start, target, eased);
                yield return null;
            }

            if (pulseGraphic != null)
            {
                pulseGraphic.color = target;
            }

            glowRoutine = null;
        }
    }
}
