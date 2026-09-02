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

        private static readonly bool[] MissingPopButtonSpriteLogged = new bool[GameConstants.ColorCount];
        private static readonly Vector2 PopButtonSize = new Vector2(154f, 54f);
        private const string BlossomPopVisualName = "BlossomPopVisual";
        private const float BlossomPopVisualScale = 1.18f;
        private const float CandyPopVisualScale = 1.12f;

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
        private Image blossomPopVisual;
        private Vector3 authoritativeRootScale = Vector3.one;
        private Vector3 authoritativeButtonScale = Vector3.one;
        private bool authoritativeScalesCaptured;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public ChromaColor DebugColor => color;
        public bool DebugPopVisible => popButton != null && popButton.gameObject.activeInHierarchy;
        public bool DebugPopInteractable => popButton != null && popButton.interactable;
#endif

        private void Awake()
        {
            CaptureAuthoritativeScales();
        }

        private void OnEnable()
        {
            CaptureAuthoritativeScales();
            RestoreAuthoritativeScales();
            ThemeCatalog.ThemeChanged += HandleThemeChanged;
            ConfigurePopButtonVisual();
        }

        private void OnDisable()
        {
            ThemeCatalog.ThemeChanged -= HandleThemeChanged;
            StopReadyPulse();
            transform.DOKill();
            transform.localScale = authoritativeRootScale;
        }

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

            ApplyCleanHudVisibility();
            SetPopAvailability(false);
        }

        public void Refresh(int amount, float normalized, bool ready)
        {
            Refresh(amount, normalized, ready, 0);
        }

        public void Refresh(int amount, float normalized, bool ready, int popTargetCount)
        {
            RefreshThemeColor();
            // This is deliberately per-color state. A POP is visible only when
            // this exact color can be used on the live board; one consumed color
            // must never alter a ready sibling's root, hit area, or interaction.
            bool usablePop = ready && popTargetCount > 0;

            if (slider != null)
            {
                slider.value = normalized;
            }

            if (label != null)
            {
                label.text = usablePop
                    ? $"READY x{popTargetCount}"
                    : ready
                    ? "READY 0"
                    : $"{ShortColorName(color)} {amount}/{GameConstants.ChromaThreshold}";
            }

            ApplyCleanHudVisibility();
            SetPopAvailability(usablePop);

            if (usablePop && !wasReady)
            {
                PlayFeedbackPunch(0.12f, 0.24f, 8, 0.7f);
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
                popButton.transform.localScale = authoritativeButtonScale;
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
                    pulseTarget.localScale = authoritativeButtonScale * Mathf.Lerp(1f, 1.045f, wave);
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

        private void ApplyCleanHudVisibility()
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
                ConfigurePopButtonVisual();
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
            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            Sprite sprite = activeTheme == null ? null : activeTheme.GetPopButtonSprite(color);
            if (sprite == null)
            {
                ThemeAssetSet oceanTheme = ThemeCatalog.GetDefinition(ThemeType.Ocean);
                sprite = oceanTheme == null ? null : oceanTheme.GetPopButtonSprite(color);
            }

            if (sprite == null)
            {
                sprite = LoadPopButtonSprite(PopButtonSpritePaths[index]);
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
                buttonRect.localScale = authoritativeButtonScale;
            }

            popButton.transition = Selectable.Transition.None;
            ColorBlock colors = popButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0f;
            popButton.colors = colors;

            Image buttonImage = popButton.GetComponent<Image>();
            if (buttonImage == null)
            {
                buttonImage = popButton.targetGraphic as Image;
            }

            if (buttonImage != null)
            {
                buttonImage.sprite = sprite;
                buttonImage.type = Image.Type.Simple;
                buttonImage.preserveAspect = true;
                buttonImage.raycastTarget = true;
                popButton.targetGraphic = buttonImage;

                bool useScaledThemeVisual = activeTheme != null
                    && (activeTheme.ThemeType == ThemeType.Neon
                        || activeTheme.ThemeType == ThemeType.Crystal
                        || activeTheme.ThemeType == ThemeType.Gold
                        || activeTheme.ThemeType == ThemeType.Aqua
                        || activeTheme.ThemeType == ThemeType.Candy);
                if (useScaledThemeVisual)
                {
                    Color transparentWhite = new Color(1f, 1f, 1f, 0f);
                    buttonImage.color = transparentWhite;
                    buttonImage.canvasRenderer.SetColor(transparentWhite);
                    float visualScale = activeTheme.ThemeType == ThemeType.Candy
                        ? CandyPopVisualScale
                        : BlossomPopVisualScale;
                    ConfigureBlossomPopVisual(sprite, visualScale);
                }
                else
                {
                    buttonImage.color = Color.white;
                    buttonImage.canvasRenderer.SetColor(Color.white);
                    SetBlossomPopVisualActive(false);
                }
            }

            // The supplied art already contains its outline, gloss and shadow.
            // Keep the existing scale-based press and ready-pulse feedback only.
            Shadow[] legacyEffects = popButton.GetComponents<Shadow>();
            for (int i = 0; i < legacyEffects.Length; i++)
            {
                if (legacyEffects[i] != null)
                {
                    legacyEffects[i].enabled = false;
                }
            }
        }

        private void ConfigureBlossomPopVisual(Sprite sprite, float visualScale)
        {
            if (blossomPopVisual == null)
            {
                Transform existingVisual = popButton.transform.Find(BlossomPopVisualName);
                if (existingVisual != null)
                {
                    blossomPopVisual = existingVisual.GetComponent<Image>();
                }
            }

            if (blossomPopVisual == null)
            {
                GameObject visualObject = new GameObject(
                    BlossomPopVisualName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                visualObject.transform.SetParent(popButton.transform, false);
                blossomPopVisual = visualObject.GetComponent<Image>();
            }

            RectTransform visualRect = blossomPopVisual.rectTransform;
            visualRect.anchorMin = new Vector2(0.5f, 0.5f);
            visualRect.anchorMax = visualRect.anchorMin;
            visualRect.pivot = new Vector2(0.5f, 0.5f);
            visualRect.anchoredPosition = Vector2.zero;
            visualRect.sizeDelta = PopButtonSize * visualScale;
            visualRect.localRotation = Quaternion.identity;
            visualRect.localScale = Vector3.one;

            blossomPopVisual.sprite = sprite;
            blossomPopVisual.color = Color.white;
            blossomPopVisual.type = Image.Type.Simple;
            blossomPopVisual.preserveAspect = true;
            blossomPopVisual.raycastTarget = false;
            blossomPopVisual.canvasRenderer.SetColor(Color.white);
            blossomPopVisual.transform.SetAsLastSibling();
            blossomPopVisual.gameObject.SetActive(true);
        }

        private void SetBlossomPopVisualActive(bool active)
        {
            if (blossomPopVisual == null && popButton != null)
            {
                Transform existingVisual = popButton.transform.Find(BlossomPopVisualName);
                if (existingVisual != null)
                {
                    blossomPopVisual = existingVisual.GetComponent<Image>();
                }
            }

            if (blossomPopVisual != null)
            {
                blossomPopVisual.gameObject.SetActive(active);
            }
        }

        private void HandleThemeChanged(ThemeType requestedTheme, ThemeAssetSet resolvedTheme)
        {
            RestoreAuthoritativeScales();
            ConfigurePopButtonVisual();
        }

        public void PlayFeedbackPunch(float amplitude = 0.10f, float duration = 0.20f, int vibrato = 8, float elasticity = 0.75f)
        {
            CaptureAuthoritativeScales();
            transform.DOKill();
            transform.localScale = authoritativeRootScale;
            Vector3 punch = authoritativeRootScale * Mathf.Max(0f, amplitude);
            transform.DOPunchScale(punch, duration, vibrato, elasticity)
                .OnComplete(RestoreRootScaleAfterPunch);
        }

        private void RestoreRootScaleAfterPunch()
        {
            if (this != null)
            {
                transform.localScale = authoritativeRootScale;
            }
        }

        private void CaptureAuthoritativeScales()
        {
            if (authoritativeScalesCaptured)
            {
                return;
            }

            authoritativeRootScale = transform.localScale;
            if (popButton != null)
            {
                authoritativeButtonScale = popButton.transform.localScale;
            }

            authoritativeScalesCaptured = true;
        }

        private void RestoreAuthoritativeScales()
        {
            CaptureAuthoritativeScales();
            transform.DOKill();
            transform.localScale = authoritativeRootScale;
            if (popButton != null)
            {
                popButton.transform.localScale = authoritativeButtonScale;
            }
        }

        private void SetPopAvailability(bool usablePop)
        {
            if (popButton == null)
            {
                return;
            }

            // Visibility, interactability and pulse ownership are all derived
            // from this ChromaBarView's own color only. Inactive roots are not
            // dimmed placeholders and remain out of the raycast/layout path.
            if (!usablePop)
            {
                StopReadyPulse();
            }

            popButton.interactable = usablePop;
            popButton.gameObject.SetActive(usablePop);
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
