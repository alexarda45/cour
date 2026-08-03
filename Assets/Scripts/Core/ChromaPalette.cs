using UnityEngine;

namespace ChromaBlast
{
    public static class ChromaPalette
    {
        // Index order matches the ChromaColor enum (Cyan, Magenta, Lime, Amber).
        private static readonly string[] TileResourcePaths =
        {
            "Ocean/Tiles/Tile_Reference_Cyan",
            "Ocean/Tiles/Tile_Reference_Pink",
            "Ocean/Tiles/Tile_Reference_Blue",
            "Ocean/Tiles/Tile_Reference_Yellow"
        };

        private static readonly Sprite[] TileSprites = new Sprite[TileResourcePaths.Length];
        private static readonly bool[] MissingTileLogged = new bool[TileResourcePaths.Length];

        // Fixed colours sampled from the board's Tile_Reference_* artwork.
        // Unlike ThemeColors, these stay aligned with the visual tile mapping:
        // Cyan, Pink (Magenta), Blue (Lime), Yellow (Amber).
        private static readonly Color32[] TileArtworkColors =
        {
            new Color32(6, 213, 233, 255),
            new Color32(233, 5, 43, 255),
            new Color32(5, 73, 233, 255),
            new Color32(233, 178, 0, 255)
        };

        private static readonly Color[][] ThemeColors =
        {
            new[] { Hex("#17E6FF"), Hex("#FF4FD8"), Hex("#A6FF42"), Hex("#FFD166") },
            new[] { Hex("#7DEBFF"), Hex("#FF7BCB"), Hex("#B8FF6A"), Hex("#FFB86B") },
            new[] { Hex("#35D6FF"), Hex("#5C7CFA"), Hex("#43F5B6"), Hex("#C7F9FF") },
            new[] { Hex("#FFE066"), Hex("#FF9F1C"), Hex("#F72585"), Hex("#8AC926") },
            new[] { Hex("#EAF8FF"), Hex("#A26CFF"), Hex("#2357D8"), Hex("#E9155E") },
            new[] { Hex("#5BE7FF"), Hex("#7CFFDE"), Hex("#62E88A"), Hex("#F6F7FF") },
            new[] { Hex("#59A8FF"), Hex("#B376FF"), Hex("#72C66B"), Hex("#FFD65A") },
            new[] { Hex("#21D7FF"), Hex("#FA49E9"), Hex("#6DFF4A"), Hex("#FFC338") },
            new[] { Hex("#3F7BFF"), Hex("#AE59FF"), Hex("#4BE878"), Hex("#F6A63B") },
            new[] { Hex("#69E6FF"), Hex("#B54BFF"), Hex("#2F4CFF"), Hex("#F7F8FF") },
            new[] { Hex("#B7C77A"), Hex("#8FA25E"), Hex("#C48B55"), Hex("#D8BE72") },
            new[] { Hex("#1F9BFF"), Hex("#934BFF"), Hex("#F4F8FF"), Hex("#FFD32E") },
            new[] { Hex("#20D6DD"), Hex("#F24E42"), Hex("#FF8A22"), Hex("#FFD338") }
        };

        private static readonly Color[] ThemeTopColors =
        {
            Hex("#070716"),
            Hex("#3B1630"),
            Hex("#04234A"),
            Hex("#2E1608"),
            Hex("#061B20"),
            Hex("#042E4A"),
            Hex("#F5F2DF"),
            Hex("#090A17"),
            Hex("#1E1510"),
            Hex("#05051E"),
            Hex("#F3EFE3"),
            Hex("#061235"),
            Hex("#381A16")
        };

        private static readonly Color[] ThemeBottomColors =
        {
            Hex("#13051F"),
            Hex("#1B4D57"),
            Hex("#031226"),
            Hex("#140D08"),
            Hex("#02090D"),
            Hex("#031125"),
            Hex("#E8DDBF"),
            Hex("#171015"),
            Hex("#090706"),
            Hex("#12002A"),
            Hex("#D9CFB5"),
            Hex("#09061A"),
            Hex("#111B29")
        };

        private static readonly Color[] ThemeGridColors =
        {
            Hex("#17E6FF"),
            Hex("#FF7BCB"),
            Hex("#5BE7FF"),
            Hex("#FFD166"),
            Hex("#A7F7FF"),
            Hex("#5BE7FF"),
            Hex("#4A4A4A"),
            Hex("#21D7FF"),
            Hex("#D8A45A"),
            Hex("#B54BFF"),
            Hex("#B28A4D"),
            Hex("#1F9BFF"),
            Hex("#FFB042")
        };

        private static readonly string[] RomanianNames =
        {
            "Cyan",
            "Magenta",
            "Lime",
            "Auriu"
        };

        private static readonly string[] ThemeNames =
        {
            "NEON",
            "CANDY",
            "OCEAN",
            "GOLD",
            "CRYSTAL",
            "AQUA",
            "DOODLE",
            "ARCADE",
            "ARCANE",
            "COSMIC",
            "ZEN",
            "STORM",
            "SUNSET"
        };

        private static readonly int[] ThemeUnlockPoints =
        {
            0,
            1500,
            5000,
            12000,
            18000,
            26000,
            34000,
            43000,
            54000,
            68000,
            82000,
            98000,
            115000
        };

        private static readonly int[] ThemeCoinCosts =
        {
            0,
            250,
            700,
            1500,
            2200,
            3000,
            3800,
            4800,
            6200,
            7800,
            9200,
            11000,
            13000
        };

        public static Color GetColor(ChromaColor color)
        {
            return GetColor(color, CurrentTheme);
        }

        public static Color GetTileArtworkColor(ChromaColor color)
        {
            int colorIndex = Mathf.Clamp((int)color, 0, TileArtworkColors.Length - 1);
            return TileArtworkColors[colorIndex];
        }

        public static Color GetColor(ChromaColor color, ThemeType theme)
        {
            int themeIndex = Mathf.Clamp((int)theme, 0, ThemeColors.Length - 1);
            return ThemeColors[themeIndex][(int)color];
        }

        public static Color GetDimColor(ChromaColor color)
        {
            Color source = GetColor(color);
            return new Color(source.r * 0.45f, source.g * 0.45f, source.b * 0.45f, 0.72f);
        }

        public static Sprite GetTileSprite(ChromaColor color)
        {
            int index = Mathf.Clamp((int)color, 0, TileResourcePaths.Length - 1);
            if (TileSprites[index] != null)
            {
                return TileSprites[index];
            }

            string resourcePath = TileResourcePaths[index];
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                if (!MissingTileLogged[index])
                {
                    Debug.LogError($"Missing Ocean tile sprite at Resources path: {resourcePath}");
                    MissingTileLogged[index] = true;
                }

                return null;
            }

            TileSprites[index] = sprite;
            return sprite;
        }

        public static string GetRomanianName(ChromaColor color)
        {
            return RomanianNames[(int)color];
        }

        public static string GetThemeName(ThemeType theme)
        {
            int index = Mathf.Clamp((int)theme, 0, ThemeNames.Length - 1);
            return ThemeNames[index];
        }

        public static int GetThemeUnlockPoints(ThemeType theme)
        {
            int index = Mathf.Clamp((int)theme, 0, ThemeUnlockPoints.Length - 1);
            return ThemeUnlockPoints[index];
        }

        public static int GetThemeCoinCost(ThemeType theme)
        {
            int index = Mathf.Clamp((int)theme, 0, ThemeCoinCosts.Length - 1);
            return ThemeCoinCosts[index];
        }

        public static Color GetThemeTopColor(ThemeType theme)
        {
            int index = Mathf.Clamp((int)theme, 0, ThemeTopColors.Length - 1);
            return ThemeTopColors[index];
        }

        public static Color GetThemeBottomColor(ThemeType theme)
        {
            int index = Mathf.Clamp((int)theme, 0, ThemeBottomColors.Length - 1);
            return ThemeBottomColors[index];
        }

        public static Color GetThemeGridColor(ThemeType theme)
        {
            int index = Mathf.Clamp((int)theme, 0, ThemeGridColors.Length - 1);
            Color color = ThemeGridColors[index];
            color.a = theme == ThemeType.Doodle || theme == ThemeType.Zen ? 0.18f : 0.13f;
            return color;
        }

        public static Color GetThemeButtonColor()
        {
            return GetColor(ChromaColor.Cyan, CurrentTheme);
        }

        public static Color GetThemeGlowColor(ThemeType theme)
        {
            Color color = GetColor(ChromaColor.Cyan, theme);
            color.a = 0.35f;
            return color;
        }

        public static bool IsThemeUnlocked(ThemeType theme, int rankPoints)
        {
            return rankPoints >= GetThemeUnlockPoints(theme);
        }

        public static bool IsThemeUnlocked(ThemeType theme, int rankPoints, bool cosmeticPackOwned)
        {
            return cosmeticPackOwned || IsThemeUnlocked(theme, rankPoints);
        }

        public static ThemeType GetNextLockedTheme(int rankPoints)
        {
            for (int i = 0; i < ThemeUnlockPoints.Length; i++)
            {
                if (rankPoints < ThemeUnlockPoints[i])
                {
                    return (ThemeType)i;
                }
            }

            return ThemeType.Neon;
        }

        public static ThemeType CurrentTheme
        {
            get
            {
                if (SaveManager.Instance == null)
                {
                    return ThemeType.Neon;
                }

                SaveManager save = SaveManager.Instance;
                SaveData data = save.Data;
                ThemeType selected = (ThemeType)Mathf.Clamp(data.selectedTheme, 0, ThemeNames.Length - 1);
                return save.IsThemeUnlocked(selected) ? selected : ThemeType.Neon;
            }
        }

        public static int ThemeCount => ThemeNames.Length;

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out Color color);
            return color;
        }
    }
}
