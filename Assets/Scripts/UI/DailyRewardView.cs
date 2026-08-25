using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public sealed class DailyRewardView : MonoBehaviour
    {
        private const string FinalArtRoot = "Ocean/DailyRewards/Final/";

        [Serializable]
        public sealed class RewardCard
        {
            [SerializeField] private Button button;
            [SerializeField] private Image background;
            [SerializeField] private Image innerHighlight;
            [SerializeField] private Image artwork;
            [SerializeField] private Image stateArea;
            [SerializeField] private TMP_Text dayText;
            [SerializeField] private TMP_Text amountText;
            [SerializeField] private TMP_Text stateText;
            [SerializeField] private Outline outline;
            [SerializeField] private Shadow shadow;

            public Button Button => button;
            public Image Background => background;
            public Image Artwork => artwork;
            public TMP_Text DayText => dayText;
            public TMP_Text AmountText => amountText;
            public TMP_Text StateText => stateText;

            public void Configure(Button cardButton, Image cardBackground, Image highlight, Image rewardArtwork,
                Image statusArea, TMP_Text day, TMP_Text amount, TMP_Text status, Outline cardOutline, Shadow cardShadow)
            {
                button = cardButton;
                background = cardBackground;
                innerHighlight = highlight;
                artwork = rewardArtwork;
                stateArea = statusArea;
                dayText = day;
                amountText = amount;
                stateText = status;
                outline = cardOutline;
                shadow = cardShadow;
            }

            public void ApplyFinalArtwork(int dayIndex, Sprite cardSprite, Sprite stateSprite, RectTransform panel)
            {
                if (button == null || panel == null)
                {
                    return;
                }

                RectTransform rect = button.transform as RectTransform;
                if (rect != null && rect.parent != panel)
                {
                    rect.SetParent(panel, false);
                }

                if (background != null)
                {
                    background.sprite = cardSprite;
                    background.color = Color.white;
                    background.preserveAspect = true;
                    background.raycastTarget = true;
                    button.targetGraphic = background;
                }

                SetVisible(innerHighlight, false);
                SetVisible(artwork, false);
                SetTextVisible(dayText, false);
                SetTextVisible(amountText, false);
                SetTextVisible(stateText, false);
                if (outline != null) outline.enabled = false;
                if (shadow != null) shadow.enabled = false;

                DisableLegacyStateImages(button.transform, stateArea);

                if (stateArea != null)
                {
                    bool showState = dayIndex < SaveManager.DailyRewardDayCount - 1;
                    stateArea.gameObject.SetActive(showState);
                    stateArea.sprite = stateSprite;
                    stateArea.color = Color.white;
                    stateArea.preserveAspect = true;
                    stateArea.raycastTarget = false;
                    if (showState)
                    {
                        RectTransform stateRect = stateArea.rectTransform;
                        stateRect.anchorMin = new Vector2(0.5f, 0f);
                        stateRect.anchorMax = new Vector2(0.5f, 0f);
                        stateRect.pivot = new Vector2(0.5f, 0f);
                        // The final card artwork contains a baked reference-state button. Align the
                        // single live state image over that exact area so none of the baked state can
                        // peek out around CLAIMED / CLAIM / LOCKED.
                        stateRect.anchoredPosition = new Vector2(0f, 19f);
                        stateRect.sizeDelta = new Vector2(206f, 71f);
                        stateRect.SetAsLastSibling();
                    }
                }

                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
                colors.selectedColor = Color.white;
                colors.disabledColor = Color.white;
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.06f;
                button.colors = colors;
            }

            public void ApplyState(int dayIndex, int rewardAmount, bool available, bool claimed)
            {
                Sprite cardSprite = Resources.Load<Sprite>(FinalArtRoot +
                    (dayIndex == SaveManager.DailyRewardDayCount - 1
                        ? "DailyRewardDay7_Final"
                        : $"DailyRewardDay{dayIndex + 1}_Final"));
                Sprite stateSprite = Resources.Load<Sprite>(FinalArtRoot +
                    (available ? "DailyRewardStateClaim_Final" : claimed
                        ? "DailyRewardStateClaimed_Final"
                        : "DailyRewardStateLocked_Final"));
                ApplyFinalArtwork(dayIndex, cardSprite, stateSprite,
                    button == null ? null : button.transform.parent as RectTransform);
            }

            private static void SetVisible(Image image, bool visible)
            {
                if (image != null) image.gameObject.SetActive(visible);
            }

            private static void SetTextVisible(TMP_Text text, bool visible)
            {
                if (text != null) text.gameObject.SetActive(visible);
            }

            private static void DisableLegacyStateImages(Transform cardRoot, Image activeState)
            {
                if (cardRoot == null) return;

                Image[] images = cardRoot.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    Image image = images[i];
                    if (image == null || image == activeState) continue;

                    string objectName = image.gameObject.name;
                    if (objectName.IndexOf("State", StringComparison.OrdinalIgnoreCase) >= 0
                        || objectName.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        image.gameObject.SetActive(false);
                    }
                }
            }
        }

        [Header("Overlay")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image balanceIcon;
        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private Button closeButton;

        [Header("Rewards")]
        [SerializeField] private RewardCard[] rewardCards;
        [SerializeField] private TMP_Text feedbackText;

        [Header("Claim")]
        [SerializeField] private Button rewardedAdButton;
        [SerializeField] private Image rewardedAdBackground;
        [SerializeField] private Image rewardedAdGloss;
        [SerializeField] private Image rewardedAdIcon;
        [SerializeField] private TMP_Text rewardedAdText;
        [SerializeField] private Outline rewardedAdOutline;
        [SerializeField] private Shadow rewardedAdShadow;

        private TMP_Text resetTimerText;
        private bool rewardAvailable;
        private int lastTimerSecond = -1;

        public RectTransform Panel => panel;
        public TMP_Text TitleText => titleText;
        public Image BalanceIcon => balanceIcon;
        public TMP_Text BalanceText => balanceText;
        public Button CloseButton => closeButton;
        public RewardCard[] RewardCards => rewardCards;
        public TMP_Text FeedbackText => feedbackText;
        public Button RewardedAdButton => rewardedAdButton;
        public TMP_Text RewardedAdText => rewardedAdText;

        public void Configure(RectTransform panelRect, TMP_Text title, Image coinIcon, TMP_Text coinBalance,
            Button close, RewardCard[] cards, TMP_Text feedback, Button adButton, Image adBackground,
            Image adGloss, Image adIcon, TMP_Text adText, Outline adOutline, Shadow adShadow)
        {
            panel = panelRect;
            titleText = title;
            balanceIcon = coinIcon;
            balanceText = coinBalance;
            closeButton = close;
            rewardCards = cards;
            feedbackText = feedback;
            rewardedAdButton = adButton;
            rewardedAdBackground = adBackground;
            rewardedAdGloss = adGloss;
            rewardedAdIcon = adIcon;
            rewardedAdText = adText;
            rewardedAdOutline = adOutline;
            rewardedAdShadow = adShadow;
        }

        public bool HasCompleteBindings => panel != null && closeButton != null && rewardCards != null
            && rewardCards.Length == SaveManager.DailyRewardDayCount && rewardedAdButton != null;

        private void OnEnable()
        {
            ApplyFinalVisualLayout();
            lastTimerSecond = -1;
        }

        private void Update()
        {
            if (resetTimerText == null) return;
            int second = DateTime.Now.Second;
            if (second == lastTimerSecond) return;
            lastTimerSecond = second;
            if (rewardAvailable)
            {
                resetTimerText.text = "Reward available now";
                return;
            }

            TimeSpan remaining = DateTime.Today.AddDays(1) - DateTime.Now;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            resetTimerText.text = $"Next reward in {remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        public void SetBalance(int coins)
        {
            if (balanceText != null) balanceText.text = Mathf.Max(0, coins).ToString("N0");
        }

        public void SetFeedback(string message)
        {
            if (feedbackText == null) return;
            feedbackText.text = message ?? string.Empty;
            bool showFeedback = !string.IsNullOrWhiteSpace(feedbackText.text);
            feedbackText.gameObject.SetActive(showFeedback);
            if (resetTimerText != null)
            {
                // Status and countdown share one contained footer line. Showing
                // both was the source of the overlapping/out-of-frame device text.
                resetTimerText.gameObject.SetActive(!showFeedback);
            }
        }

        public void SetClaimAvailability(bool available)
        {
            rewardAvailable = available;
            lastTimerSecond = -1;
        }

        public void ApplyDailyClaimState(bool available)
        {
            if (rewardedAdButton == null) return;
            rewardedAdButton.gameObject.SetActive(true);
            rewardedAdButton.interactable = available;
            if (rewardedAdBackground != null)
            {
                rewardedAdBackground.sprite = Resources.Load<Sprite>(FinalArtRoot + "DailyRewardClaim_Final");
                rewardedAdBackground.color = available ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
                rewardedAdBackground.preserveAspect = true;
                rewardedAdBackground.raycastTarget = true;
                rewardedAdButton.targetGraphic = rewardedAdBackground;
            }
        }

        public void ApplyRewardedAdState(string label, bool available, bool limitReached)
        {
            ApplyDailyClaimState(available);
        }

        public void ApplyFinalVisualLayout()
        {
            if (panel == null) return;

            SetFixedRect(panel, Vector2.zero, new Vector2(900f, 1580f));
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.sprite = Resources.Load<Sprite>(FinalArtRoot + "DailyRewardsPanel_Final");
                panelImage.color = Color.white;
                panelImage.preserveAspect = true;
                panelImage.raycastTarget = false;
            }

            Image header = FindOrCreateImage("FinalDailyRewardsHeader", panel);
            header.sprite = Resources.Load<Sprite>(FinalArtRoot + "DailyRewardsHeader_Final");
            header.color = Color.white;
            header.preserveAspect = true;
            header.raycastTarget = false;
            SetFixedRect(header.rectTransform, new Vector2(0f, 590f), new Vector2(650f, 300f));
            header.transform.SetAsLastSibling();

            SetNamedChildInactive(panel, "BalanceBadge");
            SetNamedChildInactive(panel, "HeaderArea");
            SetInactive(titleText);
            SetInactive(balanceIcon);
            SetInactive(balanceText);

            if (closeButton != null)
            {
                closeButton.transform.SetParent(panel, false);
                Image closeImage = closeButton.GetComponent<Image>();
                if (closeImage != null)
                {
                    closeImage.sprite = Resources.Load<Sprite>(FinalArtRoot + "DailyRewardsClose_Final");
                    closeImage.color = Color.white;
                    closeImage.preserveAspect = true;
                    closeImage.raycastTarget = true;
                    closeButton.targetGraphic = closeImage;
                }
                RectTransform closeRect = closeButton.transform as RectTransform;
                closeRect.anchorMin = Vector2.one;
                closeRect.anchorMax = Vector2.one;
                closeRect.pivot = new Vector2(0.5f, 0.5f);
                closeRect.anchoredPosition = new Vector2(-68f, -68f);
                closeRect.sizeDelta = new Vector2(82f, 88f);
                closeRect.SetAsLastSibling();
            }

            if (rewardCards != null && rewardCards.Length == SaveManager.DailyRewardDayCount)
            {
                float[] xs = { -270f, 0f, 270f };
                float[] ys = { 260f, -95f };
                for (int i = 0; i < 6; i++)
                {
                    RewardCard card = rewardCards[i];
                    if (card?.Button == null) continue;
                    RectTransform rect = card.Button.transform as RectTransform;
                    rect.SetParent(panel, false);
                    SetFixedRect(rect, new Vector2(xs[i % 3], ys[i / 3]), new Vector2(242f, 314f));
                    card.ApplyFinalArtwork(i, Resources.Load<Sprite>(FinalArtRoot + $"DailyRewardDay{i + 1}_Final"),
                        Resources.Load<Sprite>(FinalArtRoot + "DailyRewardStateLocked_Final"), panel);
                }

                RewardCard daySeven = rewardCards[6];
                if (daySeven?.Button != null)
                {
                    RectTransform rect = daySeven.Button.transform as RectTransform;
                    rect.SetParent(panel, false);
                    SetFixedRect(rect, new Vector2(0f, -390f), new Vector2(750f, 270f));
                    daySeven.ApplyFinalArtwork(6,
                        Resources.Load<Sprite>(FinalArtRoot + "DailyRewardDay7_Final"), null, panel);
                }
            }

            if (rewardedAdButton != null)
            {
                rewardedAdButton.transform.SetParent(panel, false);
                SetFixedRect(rewardedAdButton.transform as RectTransform, new Vector2(0f, -610f), new Vector2(520f, 142f));
                rewardedAdButton.transform.SetAsLastSibling();
            }
            SetInactive(rewardedAdGloss);
            SetInactive(rewardedAdIcon);
            SetInactive(rewardedAdText);
            if (rewardedAdOutline != null) rewardedAdOutline.enabled = false;
            if (rewardedAdShadow != null) rewardedAdShadow.enabled = false;

            if (feedbackText != null)
            {
                feedbackText.transform.SetParent(panel, false);
                SetFixedRect(feedbackText.rectTransform, new Vector2(0f, -720f), new Vector2(700f, 42f));
                feedbackText.alignment = TextAlignmentOptions.Center;
                feedbackText.color = new Color32(102, 55, 13, 255);
            }

            resetTimerText = FindOrCreateTimerText();
            SetFixedRect(resetTimerText.rectTransform, new Vector2(0f, -720f), new Vector2(700f, 42f));
            bool feedbackVisible = feedbackText != null
                && !string.IsNullOrWhiteSpace(feedbackText.text);
            resetTimerText.gameObject.SetActive(!feedbackVisible);
            resetTimerText.transform.SetAsLastSibling();

            ConfigureFinalRaycasts();
        }

        private void ConfigureFinalRaycasts()
        {
            if (panel == null) return;

            Graphic[] graphics = panel.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                {
                    graphics[i].raycastTarget = false;
                }
            }

            EnableButtonRaycast(closeButton);
            EnableButtonRaycast(rewardedAdButton);

            if (rewardCards == null) return;
            for (int i = 0; i < rewardCards.Length; i++)
            {
                EnableButtonRaycast(rewardCards[i]?.Button);
            }
        }

        private static void EnableButtonRaycast(Button button)
        {
            if (button == null) return;

            button.enabled = true;

            CanvasGroup[] groups = button.GetComponentsInParent<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                if (group == null) continue;
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            Graphic target = button.targetGraphic;
            if (target == null)
            {
                target = button.GetComponent<Graphic>();
                button.targetGraphic = target;
            }

            if (target != null)
            {
                target.enabled = true;
                target.raycastTarget = true;
            }
        }

        private TMP_Text FindOrCreateTimerText()
        {
            Transform existing = panel.Find("DailyRewardResetTimer");
            TMP_Text timer = existing == null ? null : existing.GetComponent<TMP_Text>();
            if (timer == null)
            {
                GameObject go = new GameObject("DailyRewardResetTimer", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(panel, false);
                timer = go.GetComponent<TextMeshProUGUI>();
            }
            timer.raycastTarget = false;
            timer.alignment = TextAlignmentOptions.Center;
            timer.fontSize = 25f;
            timer.fontStyle = FontStyles.Bold;
            timer.color = new Color32(112, 69, 29, 255);
            if (feedbackText != null && feedbackText.font != null) timer.font = feedbackText.font;
            return timer;
        }

        private static Image FindOrCreateImage(string name, RectTransform parent)
        {
            Transform existing = parent.Find(name);
            Image image = existing == null ? null : existing.GetComponent<Image>();
            if (image == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                image = go.GetComponent<Image>();
            }
            return image;
        }

        private static void SetFixedRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void SetInactive(Component component)
        {
            if (component != null) component.gameObject.SetActive(false);
        }

        private static void SetNamedChildInactive(RectTransform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName)) return;

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform descendant = descendants[i];
                if (descendant != null && descendant != root
                    && string.Equals(descendant.gameObject.name, objectName, StringComparison.Ordinal))
                {
                    descendant.gameObject.SetActive(false);
                }
            }
        }
    }
}
