using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class ChromaBarView : MonoBehaviour
    {
        private static readonly bool CleanGameplayHud = true;
        private static readonly string[] PopButtonSpritePaths =
        {
            "Ocean/UI/Pop/PopButton_Cyan",
            "Ocean/UI/Pop/PopButton_Magenta",
            "Ocean/UI/Pop/PopButton_Lime",
            "Ocean/UI/Pop/PopButton_Amber"
        };

        private static readonly Sprite[] PopButtonSprites = new Sprite[GameConstants.ColorCount];
        private static readonly bool[] MissingPopButtonSpriteLogged = new bool[GameConstants.ColorCount];
        private static readonly Vector2 PopButtonSize = new Vector2(170f, 54f);

        [SerializeField] private ChromaColor color;
        [SerializeField] private Slider slider;
        [SerializeField] private Image fillImage;
        [SerializeField] private Button popButton;
        [SerializeField] private Image swatchImage;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text popButtonText;

        private Action<ChromaColor> onPop;
        private bool wasReady;
        private Coroutine readyPulseRoutine;

        public void Initialize(ChromaColor chromaColor, Action<ChromaColor> popCallback)
        {
            color = chromaColor;
            onPop = popCallback;

            if (fillImage != null)
            {
                fillImage.color = ChromaPalette.GetColor(color);
            }

            RefreshThemeColor();

            if (popButton != null)
            {
                popButton.onClick.RemoveAllListeners();
                popButton.onClick.AddListener(() => onPop?.Invoke(color));

                if (popButtonText == null)
                {
                    popButtonText = popButton.GetComponentInChildren<TMP_Text>(true);
                }

                ConfigurePopButtonVisual();
                DisableLegacyPopButtonText();
            }

            ApplyCleanHudVisibility(false);
        }

        public void Refresh(int amount, float normalized, bool ready)
        {
            Refresh(amount, normalized, ready, 0);
        }

        public void Refresh(int amount, float normalized, bool ready, int popTargetCount)
        {
            RefreshThemeColor();
            bool usablePop = ready && popTargetCount > 0;

            if (slider != null)
            {
                slider.value = normalized;
            }

            if (label != null)
            {
                label.text = usablePop
                    ? $"GATA x{popTargetCount}"
                    : ready
                    ? "GATA 0"
                    : $"{ShortColorName(color)} {amount}/{GameConstants.ChromaThreshold}";
            }

            if (popButton != null)
            {
                popButton.interactable = usablePop;
            }

            ApplyCleanHudVisibility(usablePop);

            if (usablePop && !wasReady)
            {
                transform.DOKill();
                transform.DOPunchScale(Vector3.one * 0.12f, 0.24f, 8, 0.7f);
            }

            if (usablePop)
            {
                StartReadyPulse();
            }
            else
            {
                StopReadyPulse();
            }

            wasReady = usablePop;
        }

        private void OnDestroy()
        {
            StopReadyPulse();
        }

        private void StartReadyPulse()
        {
            if (!MobilePerformance.UseFullJuice() || popButton == null)
            {
                return;
            }

            if (readyPulseRoutine != null)
            {
                return;
            }

            readyPulseRoutine = StartCoroutine(ReadyPulseRoutine());
        }

        private void StopReadyPulse()
        {
            if (readyPulseRoutine != null)
            {
                StopCoroutine(readyPulseRoutine);
                readyPulseRoutine = null;
            }

            if (popButton != null)
            {
                popButton.transform.localScale = Vector3.one;
            }
        }

        private IEnumerator ReadyPulseRoutine()
        {
            Transform pulseTarget = popButton == null ? null : popButton.transform;
            while (pulseTarget != null)
            {
                const float duration = 0.62f;
                float elapsed = 0f;
                while (elapsed < duration && pulseTarget != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float wave = Mathf.Sin(t * Mathf.PI);
                    pulseTarget.localScale = Vector3.one * Mathf.Lerp(1f, 1.045f, wave);
                    yield return null;
                }
            }

            readyPulseRoutine = null;
        }

        private void RefreshThemeColor()
        {
            Color themeColor = ChromaPalette.GetColor(color);
            Image rootImage = GetComponent<Image>();
            if (CleanGameplayHud && rootImage != null)
            {
                rootImage.enabled = false;
            }

            if (fillImage != null)
            {
                fillImage.color = themeColor;
            }

            if (swatchImage != null)
            {
                swatchImage.color = themeColor;
            }

            if (label != null)
            {
                label.color = themeColor;
            }
        }

        private static string ShortColorName(ChromaColor chromaColor)
        {
            switch (chromaColor)
            {
                case ChromaColor.Magenta:
                    return "M";
                case ChromaColor.Lime:
                    return "L";
                case ChromaColor.Amber:
                    return "A";
                default:
                    return "C";
            }
        }

        private void ApplyCleanHudVisibility(bool showPopButton)
        {
            if (!CleanGameplayHud)
            {
                return;
            }

            SetObjectVisible(slider, false);
            SetObjectVisible(fillImage, false);
            SetObjectVisible(swatchImage, false);
            SetObjectVisible(label, false);

            if (popButton != null)
            {
                popButton.gameObject.SetActive(showPopButton);
            }

            if (popButtonText != null)
            {
                popButtonText.gameObject.SetActive(false);
            }
        }

        private void ConfigurePopButtonVisual()
        {
            if (popButton == null)
            {
                return;
            }

            int index = Mathf.Clamp((int)color, 0, PopButtonSpritePaths.Length - 1);
            Sprite sprite = PopButtonSprites[index];
            if (sprite == null)
            {
                sprite = LoadPopButtonSprite(PopButtonSpritePaths[index]);
                PopButtonSprites[index] = sprite;
            }

            if (sprite == null)
            {
                if (!MissingPopButtonSpriteLogged[index])
                {
                    Debug.LogError($"Missing POP button sprite at Resources path: {PopButtonSpritePaths[index]}");
                    MissingPopButtonSpriteLogged[index] = true;
                }

                return;
            }

            RectTransform buttonRect = popButton.transform as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = buttonRect.anchorMin;
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = Vector2.zero;
                buttonRect.sizeDelta = PopButtonSize;
                buttonRect.localScale = Vector3.one;
            }

            Image buttonImage = popButton.image != null ? popButton.image : popButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.sprite = sprite;
                buttonImage.color = Color.white;
                buttonImage.type = Image.Type.Simple;
                buttonImage.preserveAspect = true;
                buttonImage.raycastTarget = true;
                popButton.targetGraphic = buttonImage;
            }

            // The supplied art already contains its outline, gloss and shadow.
            // Keep the existing scale-based press and ready-pulse feedback only.
            popButton.transition = Selectable.Transition.None;
            Shadow[] legacyEffects = popButton.GetComponents<Shadow>();
            for (int i = 0; i < legacyEffects.Length; i++)
            {
                if (legacyEffects[i] != null)
                {
                    legacyEffects[i].enabled = false;
                }
            }
        }

        private static Sprite LoadPopButtonSprite(string resourcePath)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites == null || sprites.Length == 0)
            {
                return null;
            }

            int separatorIndex = resourcePath.LastIndexOf('/');
            string expectedName = separatorIndex >= 0 ? resourcePath.Substring(separatorIndex + 1) : resourcePath;
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && string.Equals(sprites[i].name, expectedName, StringComparison.Ordinal))
                {
                    return sprites[i];
                }
            }

            return sprites[0];
        }

        private void DisableLegacyPopButtonText()
        {
            if (popButtonText == null && popButton != null)
            {
                popButtonText = popButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (popButtonText != null)
            {
                popButtonText.raycastTarget = false;
                popButtonText.gameObject.SetActive(false);
            }
        }

        private static void SetObjectVisible(Component component, bool visible)
        {
            if (component != null)
            {
                component.gameObject.SetActive(visible);
            }
        }
    }
}
