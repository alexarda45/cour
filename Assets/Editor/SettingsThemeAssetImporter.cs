using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChromaBlast.Editor
{
    public static class SettingsThemeAssetImporter
    {
        private const string SettingsFolder = "Assets/Resources/Themes/Settings/";
        private const string CandyPiecesFolder = "Assets/Art/UI/Settings/Candy/";
        private const string GardenPiecesFolder = "Assets/Resources/Themes/Settings/Garden/";

        private static readonly IReadOnlyDictionary<string, string> ThemeToSprite =
            new Dictionary<string, string>
            {
                { "Theme_Ocean", "Settings_Ocean" },
                { "Theme_Crystal", "Settings_Crystal" },
                { "Theme_Neon", "Settings_Neon" },
                { "Theme_Gold", "Settings_Gold" },
                { "Theme_Candy", "Settings_Candy" },
                { "Theme_Aqua", "Settings_Aqua" }
            };

        [MenuItem("Chroma Blast/UI/Import Settings Theme Artwork")]
        public static void ImportAndAssign()
        {
            foreach (KeyValuePair<string, string> entry in ThemeToSprite)
            {
                string texturePath = SettingsFolder + entry.Value + ".png";
                ConfigureTexture(texturePath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (KeyValuePair<string, string> entry in ThemeToSprite)
            {
                string themePath = "Assets/Resources/Themes/" + entry.Key + ".asset";
                string texturePath = SettingsFolder + entry.Value + ".png";
                ThemeAssetSet theme = AssetDatabase.LoadAssetAtPath<ThemeAssetSet>(themePath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                if (theme == null || sprite == null)
                {
                    throw new InvalidOperationException(
                        $"Settings artwork assignment failed. Theme='{themePath}', Sprite='{texturePath}'.");
                }

                SerializedObject serializedTheme = new SerializedObject(theme);
                SerializedProperty property = serializedTheme.FindProperty("settingsPanelSprite");
                if (property == null)
                {
                    throw new MissingFieldException(typeof(ThemeAssetSet).FullName, "settingsPanelSprite");
                }

                property.objectReferenceValue = sprite;
                serializedTheme.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(theme);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Settings Theme Import] Assigned all six theme-specific Settings panels.");
        }

        [MenuItem("Chroma Blast/UI/Import Candy Settings Pieces")]
        public static void ImportCandySeparatedAssets()
        {
            string[] assetNames =
            {
                "candy_panel_long_close",
                "candy_panel",
                "candy_settings_header",
                "candy_back_button",
                "candy_toggle_on",
                "candy_toggle_off",
                "candy_bottom_decoration",
                "candy_chevron_light",
                "candy_chevron_pink",
                "icon_music",
                "icon_sound",
                "icon_vibration",
                "icon_privacy",
                "icon_terms",
                "icon_about",
                "icon_restart",
                "icon_home",
                "row_music",
                "row_sound",
                "row_vibration",
                "row_privacy",
                "row_terms",
                "row_about",
                "row_restart",
                "row_main_menu"
            };

            foreach (string assetName in assetNames)
            {
                ConfigureTexture(CandyPiecesFolder + assetName + ".png");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ThemeAssetSet candyTheme = AssetDatabase.LoadAssetAtPath<ThemeAssetSet>(
                "Assets/Resources/Themes/Theme_Candy.asset");
            if (candyTheme == null)
            {
                throw new InvalidOperationException("Candy ThemeAssetSet could not be loaded.");
            }

            SerializedObject serializedTheme = new SerializedObject(candyTheme);
            AssignSprite(serializedTheme, "settingsPanelSprite", "candy_panel_long_close");
            AssignSprite(serializedTheme, "settingsHeaderSprite", "candy_settings_header");
            AssignSprite(serializedTheme, "settingsBackButtonSprite", "candy_back_button");
            AssignSprite(serializedTheme, "settingsToggleOnSprite", "candy_toggle_on");
            AssignSprite(serializedTheme, "settingsToggleOffSprite", "candy_toggle_off");
            AssignSprite(serializedTheme, "settingsBottomDecorationSprite", "candy_bottom_decoration");
            AssignSprite(serializedTheme, "settingsChevronSprite", "candy_chevron_pink");
            AssignSprite(serializedTheme, "settingsMusicIconSprite", "icon_music");
            AssignSprite(serializedTheme, "settingsSoundIconSprite", "icon_sound");
            AssignSprite(serializedTheme, "settingsVibrationIconSprite", "icon_vibration");
            AssignSprite(serializedTheme, "settingsPrivacyIconSprite", "icon_privacy");
            AssignSprite(serializedTheme, "settingsTermsIconSprite", "icon_terms");
            AssignSprite(serializedTheme, "settingsAboutIconSprite", "icon_about");
            AssignSprite(serializedTheme, "settingsRestartIconSprite", "icon_restart");
            AssignSprite(serializedTheme, "settingsMainMenuIconSprite", "icon_home");
            AssignSprite(serializedTheme, "settingsMusicRowSprite", "row_music");
            AssignSprite(serializedTheme, "settingsSoundRowSprite", "row_sound");
            AssignSprite(serializedTheme, "settingsVibrationRowSprite", "row_vibration");
            AssignSprite(serializedTheme, "settingsPrivacyRowSprite", "row_privacy");
            AssignSprite(serializedTheme, "settingsTermsRowSprite", "row_terms");
            AssignSprite(serializedTheme, "settingsAboutRowSprite", "row_about");
            AssignSprite(serializedTheme, "settingsRestartRowSprite", "row_restart");
            AssignSprite(serializedTheme, "settingsMainMenuRowSprite", "row_main_menu");
            serializedTheme.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(candyTheme);
            AssetDatabase.SaveAssets();
            Debug.Log("[Settings Theme Import] Assigned the separated Candy Settings artwork.");
        }

        [MenuItem("Chroma Blast/UI/Import Garden Settings Pieces")]
        public static void ImportGardenSeparatedAssets()
        {
            string[] assetNames =
            {
                "garden_panel_medium",
                "garden_x",
                "garden_toggle_on",
                "garden_toggle_off",
                "garden_icon_music",
                "garden_icon_sound",
                "garden_icon_vibration",
                "garden_icon_privacy",
                "garden_icon_terms",
                "garden_icon_about",
                "garden_icon_restart",
                "garden_icon_home",
                "garden_chevron",
                "garden_settings_title",
                "garden_decor_flower_cluster",
                "garden_decor_flower_single",
                "garden_decor_leaves"
            };

            foreach (string assetName in assetNames)
            {
                ConfigureTexture(GardenPiecesFolder + assetName + ".png");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Settings Theme Import] Imported Garden Settings pieces.");
        }

        private static void AssignSprite(SerializedObject serializedTheme, string propertyName, string assetName)
        {
            SerializedProperty property = serializedTheme.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingFieldException(typeof(ThemeAssetSet).FullName, propertyName);
            }

            string assetPath = CandyPiecesFolder + assetName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException($"Candy Settings sprite could not be loaded: '{assetPath}'.");
            }

            property.objectReferenceValue = sprite;
        }

        private static void ConfigureTexture(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"TextureImporter missing for '{assetPath}'.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
