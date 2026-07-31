using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public sealed class DailyRewardView : MonoBehaviour
    {
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

            public void Configure(
                Button cardButton,
                Image cardBackground,
                Image highlight,
                Image rewardArtwork,
                Image statusArea,
                TMP_Text day,
                TMP_Text amount,
                TMP_Text status,
                Outline cardOutline,
                Shadow cardShadow)
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

            public void ApplyState(int dayIndex, int rewardAmount, bool available, bool claimed)
            {
                bool daySeven = dayIndex == SaveManager.DailyRewardDayCount - 1;
                bool locked = !available && !claimed;

                if (dayText != null)
                {
                    dayText.text = $"Day {dayIndex + 1}";
                    dayText.color = daySeven
                        ? new Color(1f, 0.87f, 0.56f, 1f)
                        : available
                            ? Color.white
                            : new Color(0.80f, 0.92f, 0.98f, claimed ? 0.94f : 0.80f);
                }

                if (amountText != null)
                {
                    amountText.text = rewardAmount.ToString();
                    amountText.color = daySeven
                        ? new Color(1f, 0.82f, 0.34f, 1f)
                        : available
                            ? Color.white
                            : new Color(0.88f, 0.96f, 1f, claimed ? 0.90f : 0.82f);
                }

                if (stateText != null)
                {
                    stateText.text = available ? "CLAIM" : claimed ? "CLAIMED" : "LOCKED";
                    stateText.enableAutoSizing = false;
                    stateText.fontSize = daySeven ? 42f : 29f;
                    stateText.characterSpacing = 0f;
                    stateText.color = available
                        ? Color.white
                        : claimed
                            ? new Color(0.82f, 0.98f, 1f, 1f)
                            : daySeven
                                ? new Color(0.92f, 0.81f, 0.60f, 1f)
                                : new Color(0.75f, 0.87f, 0.94f, 1f);
                }

                if (background != null)
                {
                    background.color = daySeven
                        ? available
                            ? new Color(0.015f, 0.25f, 0.39f, 0.99f)
                            : new Color(0.008f, 0.07f, 0.14f, 0.99f)
                        : available
                            ? new Color(0.020f, 0.45f, 0.68f, 0.99f)
                            : claimed
                                ? new Color(0.014f, 0.15f, 0.25f, 0.98f)
                                : new Color(0.010f, 0.085f, 0.165f, 0.99f);
                }

                if (innerHighlight != null)
                {
                    innerHighlight.color = daySeven
                        ? available
                            ? new Color(0.72f, 1f, 1f, 0.12f)
                            : claimed
                                ? new Color(0.34f, 0.78f, 0.90f, 0.07f)
                                : new Color(0.22f, 0.58f, 0.72f, 0.045f)
                        : available
                            ? new Color(0.76f, 1f, 1f, 0.24f)
                            : claimed
                                ? new Color(0.34f, 0.78f, 0.90f, 0.10f)
                                : new Color(0.22f, 0.58f, 0.72f, 0.075f);
                }

                if (stateArea != null)
                {
                    stateArea.color = daySeven
                        ? available
                            ? new Color(0.03f, 0.52f, 0.62f, 0.98f)
                            : claimed
                                ? new Color(0.05f, 0.24f, 0.29f, 0.98f)
                                : new Color(0.025f, 0.09f, 0.14f, 0.99f)
                        : available
                            ? new Color(0.02f, 0.62f, 0.72f, 0.98f)
                            : claimed
                                ? new Color(0.05f, 0.29f, 0.38f, 0.98f)
                                : new Color(0.018f, 0.08f, 0.14f, 0.99f);
                }

                if (artwork != null)
                {
                    artwork.color = available
                        ? Color.white
                        : claimed
                            ? new Color(1f, 1f, 1f, 0.82f)
                            : new Color(0.78f, 0.88f, 0.94f, daySeven ? 0.82f : 0.68f);
                }

                if (outline != null)
                {
                    outline.effectColor = available
                        ? daySeven
                            ? new Color(1f, 0.78f, 0.28f, 0.98f)
                            : new Color(0.42f, 1f, 1f, 1f)
                        : daySeven
                            ? new Color(1f, 0.72f, 0.22f, 0.72f)
                            : new Color(0.25f, 0.72f, 0.86f, claimed ? 0.52f : 0.36f);
                    outline.effectDistance = available ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
                }

                if (shadow != null)
                {
                    shadow.effectColor = available
                        ? new Color(0f, 0.02f, 0.08f, 0.62f)
                        : new Color(0f, 0.015f, 0.055f, 0.44f);
                    shadow.effectDistance = available ? new Vector2(0f, -5f) : new Vector2(0f, -3f);
                }

                if (button != null)
                {
                    ColorBlock colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = Color.white;
                    colors.pressedColor = available ? new Color(0.82f, 0.94f, 0.98f, 1f) : Color.white;
                    colors.selectedColor = Color.white;
                    colors.disabledColor = Color.white;
                    colors.colorMultiplier = 1f;
                    colors.fadeDuration = 0.08f;
                    button.colors = colors;
                }

                if (locked && stateArea != null)
                {
                    stateArea.color = daySeven
                        ? new Color(0.025f, 0.08f, 0.12f, 0.99f)
                        : new Color(0.012f, 0.065f, 0.12f, 0.99f);
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

        [Header("Rewarded Ad")]
        [SerializeField] private Button rewardedAdButton;
        [SerializeField] private Image rewardedAdBackground;
        [SerializeField] private Image rewardedAdGloss;
        [SerializeField] private Image rewardedAdIcon;
        [SerializeField] private TMP_Text rewardedAdText;
        [SerializeField] private Outline rewardedAdOutline;
        [SerializeField] private Shadow rewardedAdShadow;

        public RectTransform Panel => panel;
        public TMP_Text TitleText => titleText;
        public Image BalanceIcon => balanceIcon;
        public TMP_Text BalanceText => balanceText;
        public Button CloseButton => closeButton;
        public RewardCard[] RewardCards => rewardCards;
        public TMP_Text FeedbackText => feedbackText;
        public Button RewardedAdButton => rewardedAdButton;
        public TMP_Text RewardedAdText => rewardedAdText;

        public void Configure(
            RectTransform panelRect,
            TMP_Text title,
            Image coinIcon,
            TMP_Text coinBalance,
            Button close,
            RewardCard[] cards,
            TMP_Text feedback,
            Button adButton,
            Image adBackground,
            Image adGloss,
            Image adIcon,
            TMP_Text adText,
            Outline adOutline,
            Shadow adShadow)
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

        public bool HasCompleteBindings => panel != null
            && titleText != null
            && balanceIcon != null
            && balanceText != null
            && closeButton != null
            && rewardCards != null
            && rewardCards.Length == SaveManager.DailyRewardDayCount
            && rewardedAdButton != null
            && rewardedAdText != null;

        public void SetBalance(int coins)
        {
            if (balanceText != null)
            {
                balanceText.text = Mathf.Max(0, coins).ToString();
            }
        }

        public void SetFeedback(string message)
        {
            if (feedbackText == null)
            {
                return;
            }

            feedbackText.text = message ?? string.Empty;
            feedbackText.gameObject.SetActive(!string.IsNullOrWhiteSpace(feedbackText.text));
        }

        public void ApplyRewardedAdState(string label, bool available, bool limitReached)
        {
            if (rewardedAdText != null)
            {
                rewardedAdText.text = label;
                rewardedAdText.fontStyle = FontStyles.Bold;
                rewardedAdText.alignment = TextAlignmentOptions.Center;
                rewardedAdText.rectTransform.anchorMin = new Vector2(available || limitReached ? 0.21f : 0.10f, available || limitReached ? 0.06f : 0.08f);
                rewardedAdText.rectTransform.anchorMax = new Vector2(available || limitReached ? 0.93f : 0.90f, available || limitReached ? 0.94f : 0.92f);
                rewardedAdText.rectTransform.offsetMin = Vector2.zero;
                rewardedAdText.rectTransform.offsetMax = Vector2.zero;
                rewardedAdText.fontSize = available || limitReached ? 31f : 33f;
                rewardedAdText.color = available
                    ? Color.white
                    : limitReached
                        ? new Color(1f, 0.86f, 0.58f, 1f)
                        : new Color(0.90f, 0.96f, 1f, 1f);
            }

            if (rewardedAdBackground != null)
            {
                rewardedAdBackground.color = available
                    ? new Color(0.006f, 0.55f, 0.82f, 1f)
                    : limitReached
                        ? new Color(0.025f, 0.115f, 0.19f, 1f)
                        : new Color(0.018f, 0.12f, 0.20f, 1f);
            }

            if (rewardedAdGloss != null)
            {
                rewardedAdGloss.color = available
                    ? new Color(0.88f, 1f, 1f, 0.34f)
                    : limitReached
                        ? new Color(1f, 0.82f, 0.42f, 0.065f)
                        : new Color(0.48f, 0.80f, 0.86f, 0.095f);
            }

            if (rewardedAdIcon != null)
            {
                rewardedAdIcon.gameObject.SetActive(available || limitReached);
                rewardedAdIcon.rectTransform.anchorMin = available || limitReached
                    ? new Vector2(0.08f, 0.16f)
                    : new Vector2(0.27f, 0.18f);
                rewardedAdIcon.rectTransform.anchorMax = available || limitReached
                    ? new Vector2(0.19f, 0.84f)
                    : new Vector2(0.35f, 0.82f);
                rewardedAdIcon.rectTransform.offsetMin = Vector2.zero;
                rewardedAdIcon.rectTransform.offsetMax = Vector2.zero;
                rewardedAdIcon.preserveAspect = true;
                rewardedAdIcon.color = available
                    ? Color.white
                    : limitReached
                        ? new Color(1f, 0.84f, 0.50f, 0.72f)
                        : new Color(0.70f, 0.82f, 0.88f, 0.48f);
            }

            if (rewardedAdOutline != null)
            {
                rewardedAdOutline.effectColor = available
                    ? new Color(0.40f, 0.96f, 1f, 0.96f)
                    : limitReached
                        ? new Color(1f, 0.76f, 0.32f, 0.40f)
                        : new Color(0.32f, 0.72f, 0.80f, 0.40f);
                rewardedAdOutline.effectDistance = available ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }

            if (rewardedAdShadow != null)
            {
                rewardedAdShadow.effectColor = available
                    ? new Color(0f, 0.02f, 0.08f, 0.62f)
                    : new Color(0f, 0.015f, 0.055f, limitReached ? 0.38f : 0.28f);
            }

            if (rewardedAdButton != null)
            {
                ColorBlock colors = rewardedAdButton.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = available ? new Color(0.84f, 0.94f, 0.98f, 1f) : Color.white;
                colors.selectedColor = Color.white;
                colors.disabledColor = Color.white;
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                rewardedAdButton.colors = colors;
            }
        }
    }
}
