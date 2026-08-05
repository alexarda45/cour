using System;
using UnityEngine;

namespace ChromaBlast
{
    public static class ThemeCatalog
    {
        private const string ThemeResourcePrefix = "Themes/Theme_";
        private static readonly ThemeAssetSet[] Definitions = new ThemeAssetSet[13];
        private static readonly bool[] DefinitionLoadAttempted = new bool[13];
        private static readonly bool[] FallbackInfoLogged = new bool[13];

        private static ThemeType cachedRequestedTheme = (ThemeType)(-1);
        private static ThemeAssetSet cachedCurrent;

        public static event Action<ThemeType, ThemeAssetSet> ThemeChanged;

        public static ThemeType RequestedTheme => ChromaPalette.CurrentTheme;

        public static ThemeAssetSet Current
        {
            get
            {
                ThemeType requested = RequestedTheme;
                if (cachedCurrent == null || cachedRequestedTheme != requested)
                {
                    cachedRequestedTheme = requested;
                    cachedCurrent = ResolveCompleteSet(requested);
                }

                return cachedCurrent;
            }
        }

        public static ThemeAssetSet GetDefinition(ThemeType theme)
        {
            int index = Mathf.Clamp((int)theme, 0, Definitions.Length - 1);
            if (!DefinitionLoadAttempted[index])
            {
                DefinitionLoadAttempted[index] = true;
                Definitions[index] = Resources.Load<ThemeAssetSet>($"{ThemeResourcePrefix}{theme}");
            }

            return Definitions[index];
        }

        public static Sprite GetTileSprite(ChromaColor color)
        {
            ThemeAssetSet set = Current;
            return set == null ? null : set.GetTileSprite(color);
        }

        public static Color GetEffectColor(ChromaColor color, Color fallback)
        {
            ThemeAssetSet set = Current;
            return set == null ? fallback : set.GetEffectColor(color);
        }

        public static void NotifyThemeChanged()
        {
            cachedRequestedTheme = (ThemeType)(-1);
            cachedCurrent = null;
            ThemeType requested = RequestedTheme;
            ThemeAssetSet resolved = Current;
            ThemeChanged?.Invoke(requested, resolved);
        }

        private static ThemeAssetSet ResolveCompleteSet(ThemeType requested)
        {
            ThemeAssetSet requestedSet = GetDefinition(requested);
            if (requestedSet != null && requestedSet.HasCompleteCoreArtwork)
            {
                return requestedSet;
            }

            LogIncompleteThemeOnce(requested, requestedSet);

            ThemeAssetSet ocean = GetDefinition(ThemeType.Ocean);
            if (ocean != null && ocean.HasCompleteCoreArtwork)
            {
                return ocean;
            }

            ThemeAssetSet neon = GetDefinition(ThemeType.Neon);
            if (neon != null && neon.HasCompleteCoreArtwork)
            {
                return neon;
            }

            string oceanMissing = ocean == null ? "asset missing" : ocean.MissingCoreArtwork;
            string neonMissing = neon == null ? "asset missing" : neon.MissingCoreArtwork;
            Debug.LogError($"[ThemeCatalog] No complete Ocean or Neon fallback ThemeAssetSet is available. Ocean: {oceanMissing}. Neon: {neonMissing}.");
            return requestedSet ?? ocean ?? neon;
        }

        private static void LogIncompleteThemeOnce(ThemeType theme, ThemeAssetSet set)
        {
            int index = Mathf.Clamp((int)theme, 0, FallbackInfoLogged.Length - 1);
            if (theme == ThemeType.Ocean || FallbackInfoLogged[index])
            {
                return;
            }

            FallbackInfoLogged[index] = true;
            string reason = set == null ? "asset is missing" : "core artwork is incomplete";
            Debug.Log($"[ThemeCatalog] Theme {theme} {reason}; using the Ocean fallback artwork.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Array.Clear(Definitions, 0, Definitions.Length);
            Array.Clear(DefinitionLoadAttempted, 0, DefinitionLoadAttempted.Length);
            Array.Clear(FallbackInfoLogged, 0, FallbackInfoLogged.Length);
            cachedRequestedTheme = (ThemeType)(-1);
            cachedCurrent = null;
            ThemeChanged = null;
        }
    }
}
