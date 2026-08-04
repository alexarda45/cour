using System;
using System.Collections.Generic;
using ChromaBlast;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameOverPremiumBuilder
{
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private const string ArtRoot = "Assets/Art/Ocean/UI/GameOver/";
    private const string BackgroundPath = ArtRoot + "BG_GameOver_Ocean_v2.png";
    private const string CrownPath = ArtRoot + "ChatGPT Image Jul 25, 2026, 02_06_15 AM (5).png";
    private const string NewBestPath = ArtRoot + "ChatGPT Image Jul 25, 2026, 02_06_15 AM (6).png";
    private const string CapsulePath = ArtRoot + "BestScoreCapsule_Cropped.png";
    private const string PlayButtonPath = ArtRoot + "PlayButton_Cropped.png";
    private const string PlayIconPath = ArtRoot + "PlayIcon_Cropped.png";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Jost-ExtraBold SDF.asset";
    private const string MaterialFolder = ArtRoot + "Materials";
    private const string TitleMaterialPath = MaterialFolder + "/GameOverTitle_Jost.mat";
    private const string ScoreMaterialPath = MaterialFolder + "/GameOverScore_Jost.mat";
    private const string BestValueMaterialPath = MaterialFolder + "/GameOverBestValue_Jost.mat";
    private const string LabelMaterialPath = MaterialFolder + "/GameOverLabel_Jost.mat";

    [MenuItem("Chroma Blast/UI/Build Premium Game Over")]
    public static void Build()
    {
        ImportSprite(BackgroundPath, false);
        ImportSprite(CrownPath, true);
        ImportSprite(NewBestPath, true);
        ImportSprite(CapsulePath, true);
        ImportSprite(PlayButtonPath, true);
        ImportSprite(PlayIconPath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        TMP_FontAsset premiumFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (premiumFont == null)
        {
            throw new InvalidOperationException("Missing premium TMP font: " + FontPath);
        }

        EnsureAssetFolder(MaterialFolder);
        Material titleMaterial = ConfigureTmpMaterial(
            TitleMaterialPath,
            premiumFont.material,
            new Color32(255, 255, 255, 255),
            new Color32(23, 110, 234, 255),
            0.145f,
            new Color32(0, 21, 64, 176),
            -0.58f,
            0.16f,
            new Color32(82, 233, 255, 28),
            0.01f,
            0.055f,
            0.92f);
        Material scoreMaterial = ConfigureTmpMaterial(
            ScoreMaterialPath,
            premiumFont.material,
            new Color32(255, 255, 255, 255),
            new Color32(12, 96, 242, 255),
            0.18f,
            new Color32(0, 18, 60, 180),
            -0.66f,
            0.21f,
            new Color32(72, 236, 255, 26),
            0.008f,
            0.05f,
            0.94f);
        Material bestValueMaterial = ConfigureTmpMaterial(
            BestValueMaterialPath,
            premiumFont.material,
            new Color32(255, 255, 255, 255),
            new Color32(23, 89, 197, 255),
            0.105f,
            new Color32(0, 18, 58, 171),
            -0.44f,
            0.13f,
            new Color32(108, 239, 255, 20),
            0.006f,
            0.035f,
            0.96f);
        Material labelMaterial = ConfigureTmpMaterial(
            LabelMaterialPath,
            premiumFont.material,
            Color.white,
            new Color32(6, 53, 122, 225),
            0.055f,
            new Color32(0, 18, 56, 210),
            -0.28f,
            0.06f,
            new Color32(112, 247, 255, 28),
            0.01f,
            0.04f,
            0.92f);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform overlay = FindInScene(scene, "GameOverOverlay");
        if (overlay == null)
        {
            throw new InvalidOperationException("GameOverOverlay was not found in Assets/Scenes/Game.unity.");
        }

        Undo.RegisterFullObjectHierarchyUndo(overlay.gameObject, "Build Premium Game Over");
        ConfigureStretch(overlay as RectTransform);
        overlay.name = "GameOverOverlay";

        Image background = GetOrAdd<Image>(overlay.gameObject);
        background.sprite = LoadSprite(BackgroundPath);
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = Color.white;
        background.raycastTarget = true;

        RectTransform panel = GetOrMoveRect(overlay, "GameOverPanel");
        ConfigureStretch(panel);
        panel.localScale = Vector3.one;
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = Color.clear;
            panelImage.raycastTarget = false;
            panelImage.enabled = false;
        }

        TMP_Text title = ConfigureText(
            GetOrMoveRect(panel, "GameOverTitle"),
            "GAME OVER",
            new Vector2(0f, 550f),
            new Vector2(980f, 210f),
            118f,
            Color.white);
        title.enableAutoSizing = true;
        title.fontSizeMin = 100f;
        title.fontSizeMax = 118f;
        title.characterSpacing = 2f;
        ConfigurePremiumText(
            title,
            premiumFont,
            titleMaterial,
            new VertexGradient(
                new Color(1f, 1f, 1f, 1f),
                new Color(1f, 1f, 1f, 1f),
                new Color(0.76f, 0.90f, 1f, 1f),
                new Color(0.76f, 0.90f, 1f, 1f)),
            new Vector2(0f, -7f));

        Image newBest = ConfigureImage(
            GetOrMoveRect(panel, "NewBestAccent"),
            LoadSprite(NewBestPath),
            new Vector2(0f, 335f),
            new Vector2(790f, 527f),
            true,
            false);

        TMP_Text finalScore = ConfigureText(
            GetOrMoveRect(panel, "FinalScore"),
            "0",
            new Vector2(0f, 180f),
            new Vector2(900f, 400f),
            300f,
            new Color(0.96f, 0.99f, 1f, 1f));
        finalScore.enableAutoSizing = true;
        finalScore.fontSizeMin = 96f;
        finalScore.fontSizeMax = 300f;
        finalScore.margin = new Vector4(28f, 10f, 28f, 10f);
        finalScore.overflowMode = TextOverflowModes.Truncate;
        ConfigurePremiumText(
            finalScore,
            premiumFont,
            scoreMaterial,
            new VertexGradient(
                Color.white,
                Color.white,
                new Color(0.78f, 0.91f, 1f, 1f),
                new Color(0.78f, 0.91f, 1f, 1f)),
            new Vector2(0f, -8f));

        TMP_Text bestLabel = ConfigureText(
            GetOrMoveRect(panel, "BestScoreLabel"),
            "BEST SCORE",
            new Vector2(0f, -115f),
            new Vector2(560f, 84f),
            52f,
            new Color(0.05f, 0.94f, 1f, 1f));
        bestLabel.characterSpacing = 4f;
        ConfigurePremiumText(
            bestLabel,
            premiumFont,
            labelMaterial,
            new VertexGradient(new Color(0.25f, 1f, 1f, 1f), new Color(0.25f, 1f, 1f, 1f),
                new Color(0f, 0.72f, 1f, 1f), new Color(0f, 0.72f, 1f, 1f)),
            new Vector2(0f, -3f));

        RectTransform capsule = GetOrMoveRect(panel, "BestScoreCapsule");
        ConfigureCentered(capsule, new Vector2(0f, -290f), new Vector2(576f, 233f));
        Image capsuleImage = GetOrAdd<Image>(capsule.gameObject);
        capsuleImage.sprite = LoadSprite(CapsulePath);
        capsuleImage.type = Image.Type.Simple;
        capsuleImage.preserveAspect = true;
        capsuleImage.color = Color.white;
        capsuleImage.raycastTarget = false;
        CanvasGroup capsuleGroup = GetOrAdd<CanvasGroup>(capsule.gameObject);
        capsuleGroup.alpha = 1f;
        capsuleGroup.interactable = false;
        capsuleGroup.blocksRaycasts = false;

        Image crown = ConfigureImage(
            GetOrMoveRect(capsule, "CrownIcon"),
            LoadSprite(CrownPath),
            new Vector2(-170f, 10f),
            new Vector2(144f, 216f),
            true,
            false);

        RectTransform bestValueRect = FindDeep(overlay, "FinalHighScore") as RectTransform;
        if (bestValueRect == null)
        {
            bestValueRect = GetOrMoveRect(capsule, "BestScoreValue");
        }
        else
        {
            bestValueRect.name = "BestScoreValue";
            bestValueRect.SetParent(capsule, false);
        }

        TMP_Text bestValue = ConfigureText(
            bestValueRect,
            "0",
            new Vector2(75f, 10f),
            new Vector2(310f, 132f),
            108f,
            Color.white);
        bestValue.enableAutoSizing = true;
        bestValue.fontSizeMin = 56f;
        bestValue.fontSizeMax = 108f;
        bestValue.margin = new Vector4(12f, 4f, 12f, 4f);
        bestValue.overflowMode = TextOverflowModes.Truncate;
        ConfigurePremiumText(
            bestValue,
            premiumFont,
            bestValueMaterial,
            new VertexGradient(
                Color.white,
                Color.white,
                new Color(0.80f, 0.92f, 1f, 1f),
                new Color(0.80f, 0.92f, 1f, 1f)),
            new Vector2(0f, -4f));

        RectTransform restartRect = GetOrMoveRect(panel, "RestartButton");
        ConfigureCentered(restartRect, new Vector2(0f, -610f), new Vector2(580f, 248f));
        Image restartImage = GetOrAdd<Image>(restartRect.gameObject);
        restartImage.sprite = LoadSprite(PlayButtonPath);
        restartImage.type = Image.Type.Simple;
        restartImage.preserveAspect = true;
        restartImage.color = Color.white;
        restartImage.raycastTarget = true;

        Button restart = GetOrAdd<Button>(restartRect.gameObject);
        restart.targetGraphic = restartImage;
        restart.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = restart.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.86f, 0.94f, 0.80f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.55f);
        colors.fadeDuration = 0.08f;
        restart.colors = colors;
        CanvasGroup restartGroup = GetOrAdd<CanvasGroup>(restartRect.gameObject);
        restartGroup.alpha = 1f;
        restartGroup.interactable = true;
        restartGroup.blocksRaycasts = true;

        Image playIcon = ConfigureImage(
            GetOrMoveRect(restartRect, "PlayIcon"),
            LoadSprite(PlayIconPath),
            new Vector2(0f, 20f),
            new Vector2(104f, 117f),
            true,
            false);

        DisableAllExcept(panel, new HashSet<string>
        {
            "GameOverTitle",
            "NewBestAccent",
            "FinalScore",
            "BestScoreLabel",
            "BestScoreCapsule",
            "RestartButton"
        });
        DisableAllExcept(capsule, new HashSet<string> { "CrownIcon", "BestScoreValue" });
        DisableAllExcept(restartRect, new HashSet<string> { "PlayIcon" });

        title.gameObject.SetActive(true);
        newBest.gameObject.SetActive(false);
        finalScore.gameObject.SetActive(true);
        bestLabel.gameObject.SetActive(true);
        capsule.gameObject.SetActive(true);
        crown.gameObject.SetActive(true);
        bestValue.gameObject.SetActive(true);
        restartRect.gameObject.SetActive(true);
        playIcon.gameObject.SetActive(true);

        GameOverUI ui = overlay.GetComponent<GameOverUI>();
        if (ui == null)
        {
            ui = overlay.gameObject.AddComponent<GameOverUI>();
        }

        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("root").objectReferenceValue = overlay.gameObject;
        serialized.FindProperty("backgroundImage").objectReferenceValue = background;
        serialized.FindProperty("titleText").objectReferenceValue = title;
        serialized.FindProperty("newBestAccent").objectReferenceValue = newBest;
        serialized.FindProperty("scoreValueText").objectReferenceValue = finalScore;
        serialized.FindProperty("bestLabelText").objectReferenceValue = bestLabel;
        serialized.FindProperty("bestScoreCapsuleGroup").objectReferenceValue = capsuleGroup;
        serialized.FindProperty("bestValueText").objectReferenceValue = bestValue;
        serialized.FindProperty("restartButton").objectReferenceValue = restart;
        serialized.FindProperty("restartButtonGroup").objectReferenceValue = restartGroup;
        serialized.FindProperty("normalScorePosition").vector2Value = new Vector2(0f, 180f);
        serialized.FindProperty("newBestScorePosition").vector2Value = new Vector2(0f, 110f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        ValidateScene(scene, overlay, panel, restart);
        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(overlay.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("GameOverPremiumBuilder: premium Game Over screen built and scene saved.");
    }

    private static void ValidateScene(
        Scene scene,
        Transform overlay,
        RectTransform panel,
        Button restart)
    {
        int missingScripts = 0;
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            Transform[] transforms = sceneRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform item in transforms)
            {
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject);
            }
        }

        if (missingScripts != 0)
        {
            throw new InvalidOperationException(
                "Game Over validation found " + missingScripts + " missing script component(s).");
        }

        string[] requiredNames =
        {
            "GameOverTitle",
            "NewBestAccent",
            "FinalScore",
            "BestScoreLabel",
            "BestScoreCapsule",
            "CrownIcon",
            "BestScoreValue",
            "RestartButton",
            "PlayIcon"
        };
        foreach (string requiredName in requiredNames)
        {
            if (CountNamed(overlay, requiredName) != 1)
            {
                throw new InvalidOperationException(
                    "Game Over validation expected exactly one " + requiredName + ".");
            }
        }

        string[] hiddenNames =
        {
            "ScoreCaption",
            "ReviveButton",
            "MenuButton",
            "RoundSummaryText",
            "XPText",
            "RankHintText",
            "RankProgress"
        };
        foreach (string hiddenName in hiddenNames)
        {
            Transform hidden = FindDeep(panel, hiddenName);
            if (hidden != null && hidden.gameObject.activeSelf)
            {
                throw new InvalidOperationException(hiddenName + " must be disabled in GameOverPanel.");
            }
        }

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null && panelImage.enabled)
        {
            throw new InvalidOperationException("The legacy GameOverPanel Image must remain disabled.");
        }

        int canvasCount = CountSceneComponents<Canvas>(scene);
        int eventSystemCount = CountSceneComponents<EventSystem>(scene);
        int gameManagerCount = CountSceneComponents<GameManager>(scene);
        Debug.Log(
            "GameOverPremiumBuilder validation: missingScripts=0, requiredObjects=9, "
            + "canvasCount=" + canvasCount
            + ", eventSystemCount=" + eventSystemCount
            + ", gameManagerCount=" + gameManagerCount
            + ", restartPersistentListeners=" + restart.onClick.GetPersistentEventCount()
            + ", legacyGameOverObjectsHidden=true.");
    }

    private static int CountNamed(Transform root, string name)
    {
        int count = root.name == name ? 1 : 0;
        for (int i = 0; i < root.childCount; i++)
        {
            count += CountNamed(root.GetChild(i), name);
        }

        return count;
    }

    private static int CountSceneComponents<T>(Scene scene) where T : Component
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            count += root.GetComponentsInChildren<T>(true).Length;
        }

        return count;
    }

    private static void ImportSprite(string path, bool alpha)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Missing Game Over texture: " + path);
        }

        bool changed = importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || importer.alphaIsTransparency != alpha
            || importer.mipmapEnabled;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = alpha;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new InvalidOperationException("Could not load sprite: " + path);
        }

        return sprite;
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = folderPath.Substring(0, folderPath.LastIndexOf('/'));
        string name = folderPath.Substring(folderPath.LastIndexOf('/') + 1);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureAssetFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    private static Material ConfigureTmpMaterial(
        string path,
        Material source,
        Color faceColor,
        Color outlineColor,
        float outlineWidth,
        Color underlayColor,
        float underlayOffsetY,
        float underlayDilate,
        Color glowColor,
        float glowInner,
        float glowOuter,
        float glowPower)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source);
            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.CopyPropertiesFromMaterial(source);
            material.shaderKeywords = source.shaderKeywords;
        }

        SetMaterialColor(material, "_FaceColor", faceColor);
        SetMaterialFloat(material, "_FaceDilate", 0.08f);
        SetMaterialColor(material, "_OutlineColor", outlineColor);
        SetMaterialFloat(material, "_OutlineWidth", outlineWidth);
        SetMaterialFloat(material, "_OutlineSoftness", 0.025f);
        SetMaterialColor(material, "_UnderlayColor", underlayColor);
        SetMaterialFloat(material, "_UnderlayOffsetX", 0f);
        SetMaterialFloat(material, "_UnderlayOffsetY", underlayOffsetY);
        SetMaterialFloat(material, "_UnderlayDilate", underlayDilate);
        SetMaterialFloat(material, "_UnderlaySoftness", 0.08f);
        SetMaterialColor(material, "_GlowColor", glowColor);
        SetMaterialFloat(material, "_GlowInner", glowInner);
        SetMaterialFloat(material, "_GlowOffset", 0f);
        SetMaterialFloat(material, "_GlowOuter", glowOuter);
        SetMaterialFloat(material, "_GlowPower", glowPower);
        SetMaterialFloat(material, "_Bevel", 0.22f);
        SetMaterialFloat(material, "_BevelWidth", 0.32f);
        SetMaterialFloat(material, "_BevelClamp", 0.22f);
        material.EnableKeyword("OUTLINE_ON");
        material.EnableKeyword("UNDERLAY_ON");
        material.EnableKeyword("GLOW_ON");
        if (material.HasProperty("_Bevel"))
        {
            material.EnableKeyword("BEVEL_ON");
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterialColor(Material material, string property, Color value)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, value);
        }
    }

    private static void SetMaterialFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = FindDeep(root.transform, name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform match = FindDeep(parent.GetChild(i), name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static RectTransform GetOrMoveRect(Transform parent, string name)
    {
        Transform found = FindDeep(parent, name);
        if (found != null)
        {
            RectTransform existing = found as RectTransform;
            if (existing == null)
            {
                throw new InvalidOperationException(name + " exists but is not a RectTransform.");
            }

            if (existing.parent != parent)
            {
                existing.SetParent(parent, false);
            }

            existing.name = name;
            return existing;
        }

        GameObject created = new GameObject(name, typeof(RectTransform));
        RectTransform rect = created.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void ConfigureStretch(RectTransform rect)
    {
        if (rect == null)
        {
            throw new InvalidOperationException("Expected a RectTransform.");
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }

    private static void ConfigureCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static TMP_Text ConfigureText(
        RectTransform rect,
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color)
    {
        Image image = rect.GetComponent<Image>();
        if (image != null)
        {
            UnityEngine.Object.DestroyImmediate(image);
        }

        TMP_Text text = GetOrAdd<TextMeshProUGUI>(rect.gameObject);
        ConfigureCentered(rect, position, size);
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static Image ConfigureImage(
        RectTransform rect,
        Sprite sprite,
        Vector2 position,
        Vector2 size,
        bool preserveAspect,
        bool raycast)
    {
        TMP_Text text = rect.GetComponent<TMP_Text>();
        if (text != null)
        {
            UnityEngine.Object.DestroyImmediate(text);
        }

        ConfigureCentered(rect, position, size);
        Image image = GetOrAdd<Image>(rect.gameObject);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        image.color = Color.white;
        image.raycastTarget = raycast;
        return image;
    }

    private static void ConfigurePremiumText(
        TMP_Text text,
        TMP_FontAsset font,
        Material material,
        VertexGradient gradient,
        Vector2 shadowDistance)
    {
        text.font = font;
        text.fontSharedMaterial = material;
        text.fontStyle = FontStyles.Normal;
        text.enableVertexGradient = true;
        text.colorGradient = gradient;
        text.extraPadding = true;

        Outline[] outlines = text.GetComponents<Outline>();
        foreach (Outline outline in outlines)
        {
            UnityEngine.Object.DestroyImmediate(outline);
        }

        Shadow keep = null;
        Shadow[] shadows = text.GetComponents<Shadow>();
        foreach (Shadow shadow in shadows)
        {
            if (keep == null)
            {
                keep = shadow;
                continue;
            }

            UnityEngine.Object.DestroyImmediate(shadow);
        }

        if (keep == null)
        {
            keep = text.gameObject.AddComponent<Shadow>();
        }

        keep.effectColor = new Color(0f, 0.025f, 0.12f, 0.70f);
        keep.effectDistance = shadowDistance;
        keep.useGraphicAlpha = true;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void DisableAllExcept(RectTransform parent, HashSet<string> allowed)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            child.gameObject.SetActive(allowed.Contains(child.name));
        }
    }
}
