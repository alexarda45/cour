using UnityEngine;

namespace ChromaBlast
{
    [CreateAssetMenu(fileName = "Theme_New", menuName = "Chroma Blast/Theme Asset Set")]
    public sealed class ThemeAssetSet : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private ThemeType themeType;
        [SerializeField] private string displayName;
        [Min(0)] [SerializeField] private int coinCost;
        [SerializeField] private Sprite previewSprite;
        [SerializeField] private Sprite cardThumbnailSprite;

        [Header("Gameplay Artwork")]
        [SerializeField] private Sprite menuBackground;
        [SerializeField] private Sprite gameplayBackground;
        [SerializeField] private Sprite gameOverBackground;
        [SerializeField] private Sprite themesPanelBackgroundSprite;
        [SerializeField] private Sprite boardSurfaceSprite;

        [Header("Tile Artwork")]
        [SerializeField] private Sprite tileCyan;
        [SerializeField] private Sprite tileMagenta;
        [SerializeField] private Sprite tileLime;
        [SerializeField] private Sprite tileAmber;

        [Header("Gameplay Effect Colours")]
        [SerializeField] private Color effectColorCyan = new Color(0.0235f, 0.8353f, 0.9137f, 1f);
        [SerializeField] private Color effectColorMagenta = new Color(0.9137f, 0.0196f, 0.1686f, 1f);
        [SerializeField] private Color effectColorLime = new Color(0.0196f, 0.2863f, 0.9137f, 1f);
        [SerializeField] private Color effectColorAmber = new Color(0.9137f, 0.6980f, 0f, 1f);

        [Header("HUD Theme Colours")]
        [SerializeField] private Color capsuleTintColor = Color.white;
        [SerializeField] private Color crownTintColor = Color.white;

        [Header("HUD Theme Artwork")]
        [SerializeField] private Sprite capsuleSprite;
        [SerializeField] private Sprite crownSprite;

        [Header("Themes Popup Artwork")]
        [SerializeField] private Sprite themesCoinBalanceSprite;
        [SerializeField] private Sprite themesCloseButtonSprite;
        [SerializeField] private Sprite themesSelectedRibbonSprite;
        [SerializeField] private Sprite themesApplyButtonSprite;

        [Header("Settings Artwork")]
        [SerializeField] private Sprite settingsPanelSprite;
        [SerializeField] private Sprite settingsHeaderSprite;
        [SerializeField] private Sprite settingsBackButtonSprite;
        [SerializeField] private Sprite settingsToggleOnSprite;
        [SerializeField] private Sprite settingsToggleOffSprite;
        [SerializeField] private Sprite settingsBottomDecorationSprite;
        [SerializeField] private Sprite settingsChevronSprite;
        [SerializeField] private Sprite settingsMusicIconSprite;
        [SerializeField] private Sprite settingsSoundIconSprite;
        [SerializeField] private Sprite settingsVibrationIconSprite;
        [SerializeField] private Sprite settingsPrivacyIconSprite;
        [SerializeField] private Sprite settingsTermsIconSprite;
        [SerializeField] private Sprite settingsAboutIconSprite;
        [SerializeField] private Sprite settingsRestartIconSprite;
        [SerializeField] private Sprite settingsMainMenuIconSprite;
        [SerializeField] private Sprite settingsMusicRowSprite;
        [SerializeField] private Sprite settingsSoundRowSprite;
        [SerializeField] private Sprite settingsVibrationRowSprite;
        [SerializeField] private Sprite settingsPrivacyRowSprite;
        [SerializeField] private Sprite settingsTermsRowSprite;
        [SerializeField] private Sprite settingsAboutRowSprite;
        [SerializeField] private Sprite settingsRestartRowSprite;
        [SerializeField] private Sprite settingsMainMenuRowSprite;

        public ThemeType ThemeType => themeType;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? themeType.ToString().ToUpperInvariant() : displayName;
        public int CoinCost => Mathf.Max(0, coinCost);
        public Sprite PreviewSprite => previewSprite != null ? previewSprite : boardSurfaceSprite;
        public Sprite CardThumbnailSprite => cardThumbnailSprite;
        public Sprite MenuBackground => menuBackground;
        public Sprite GameplayBackground => gameplayBackground;
        public Sprite GameOverBackground => gameOverBackground;
        public Sprite ThemesPanelBackgroundSprite => themesPanelBackgroundSprite;
        public Sprite BoardSurfaceSprite => boardSurfaceSprite;
        public Color CapsuleTintColor => WithOpaqueAlpha(capsuleTintColor);
        public Color CrownTintColor => WithOpaqueAlpha(crownTintColor);
        public Sprite CapsuleSprite => capsuleSprite;
        public Sprite CrownSprite => crownSprite;
        public Sprite ThemesCoinBalanceSprite => themesCoinBalanceSprite;
        public Sprite ThemesCloseButtonSprite => themesCloseButtonSprite;
        public Sprite ThemesSelectedRibbonSprite => themesSelectedRibbonSprite;
        public Sprite ThemesApplyButtonSprite => themesApplyButtonSprite;
        public Sprite SettingsPanelSprite => settingsPanelSprite;
        public Sprite SettingsHeaderSprite => settingsHeaderSprite;
        public Sprite SettingsBackButtonSprite => settingsBackButtonSprite;
        public Sprite SettingsToggleOnSprite => settingsToggleOnSprite;
        public Sprite SettingsToggleOffSprite => settingsToggleOffSprite;
        public Sprite SettingsBottomDecorationSprite => settingsBottomDecorationSprite;
        public Sprite SettingsChevronSprite => settingsChevronSprite;
        public Sprite SettingsMusicIconSprite => settingsMusicIconSprite;
        public Sprite SettingsSoundIconSprite => settingsSoundIconSprite;
        public Sprite SettingsVibrationIconSprite => settingsVibrationIconSprite;
        public Sprite SettingsPrivacyIconSprite => settingsPrivacyIconSprite;
        public Sprite SettingsTermsIconSprite => settingsTermsIconSprite;
        public Sprite SettingsAboutIconSprite => settingsAboutIconSprite;
        public Sprite SettingsRestartIconSprite => settingsRestartIconSprite;
        public Sprite SettingsMainMenuIconSprite => settingsMainMenuIconSprite;
        public Sprite SettingsMusicRowSprite => settingsMusicRowSprite;
        public Sprite SettingsSoundRowSprite => settingsSoundRowSprite;
        public Sprite SettingsVibrationRowSprite => settingsVibrationRowSprite;
        public Sprite SettingsPrivacyRowSprite => settingsPrivacyRowSprite;
        public Sprite SettingsTermsRowSprite => settingsTermsRowSprite;
        public Sprite SettingsAboutRowSprite => settingsAboutRowSprite;
        public Sprite SettingsRestartRowSprite => settingsRestartRowSprite;
        public Sprite SettingsMainMenuRowSprite => settingsMainMenuRowSprite;

        public bool HasCompleteCoreArtwork =>
            menuBackground != null &&
            gameplayBackground != null &&
            gameOverBackground != null &&
            boardSurfaceSprite != null &&
            tileCyan != null &&
            tileMagenta != null &&
            tileLime != null &&
            tileAmber != null;

        public string MissingCoreArtwork
        {
            get
            {
                string missing = string.Empty;
                AppendMissing(ref missing, menuBackground, nameof(MenuBackground));
                AppendMissing(ref missing, gameplayBackground, nameof(GameplayBackground));
                AppendMissing(ref missing, gameOverBackground, nameof(GameOverBackground));
                AppendMissing(ref missing, boardSurfaceSprite, nameof(BoardSurfaceSprite));
                AppendMissing(ref missing, tileCyan, nameof(tileCyan));
                AppendMissing(ref missing, tileMagenta, nameof(tileMagenta));
                AppendMissing(ref missing, tileLime, nameof(tileLime));
                AppendMissing(ref missing, tileAmber, nameof(tileAmber));
                return missing;
            }
        }

        public Sprite GetTileSprite(ChromaColor color)
        {
            switch (color)
            {
                case ChromaColor.Cyan:
                    return tileCyan;
                case ChromaColor.Magenta:
                    return tileMagenta;
                case ChromaColor.Lime:
                    return tileLime;
                case ChromaColor.Amber:
                    return tileAmber;
                default:
                    return tileCyan;
            }
        }

        public Color GetEffectColor(ChromaColor color)
        {
            Color result;
            switch (color)
            {
                case ChromaColor.Cyan:
                    result = effectColorCyan;
                    break;
                case ChromaColor.Magenta:
                    result = effectColorMagenta;
                    break;
                case ChromaColor.Lime:
                    result = effectColorLime;
                    break;
                case ChromaColor.Amber:
                    result = effectColorAmber;
                    break;
                default:
                    result = effectColorCyan;
                    break;
            }

            result.a = 1f;
            return result;
        }

        private static void AppendMissing(ref string result, Object value, string label)
        {
            if (value != null)
            {
                return;
            }

            result = string.IsNullOrEmpty(result) ? label : $"{result}, {label}";
        }

        private static Color WithOpaqueAlpha(Color color)
        {
            color.a = 1f;
            return color;
        }
    }
}
