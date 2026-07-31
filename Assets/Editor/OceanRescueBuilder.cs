using System;
using System.Collections.Generic;
using System.IO;
using ChromaBlast;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OceanRescueBuilder
{
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private const string ArtRoot = "Assets/Art/Ocean/UI/OceanRescue/";
    private const string PanelPath = ArtRoot + "OceanRescuePanel.png";
    private const string TitlePath = ArtRoot + "OceanRescueTitle.png";
    private const string ContinuePath = ArtRoot + "ContinueWith3SmallBlocks.png";
    private const string PreviewPanelPath = ArtRoot + "RescuePreviewPanel.png";
    private const string ButtonPath = ArtRoot + "WatchAdButton_New.png";
    private const string RewardedIconPath = ArtRoot + "RewardedAdIcon_New.png";
    private const string WatchAdTextPath = ArtRoot + "WatchAdText.png";
    private const string NoThanksTextPath = ArtRoot + "NoThanksText.png";
    private const string TrimmedArtRoot = ArtRoot + "Trimmed/";
    private const string FeedbackFontPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const byte VisibleAlphaThreshold = 8;
    private const int TrimPaddingPixels = 4;
    private static readonly Vector4 PreviewPanelBorder =
        new Vector4(100f, 95f, 100f, 95f);
    private static readonly Vector4 WatchButtonBorder =
        new Vector4(120f, 65f, 120f, 65f);

    [MenuItem("Chroma Blast/UI/Build Ocean Rescue")]
    public static void Build()
    {
        string panelSpritePath = EnsureTrimmedSprite(PanelPath);
        string titleSpritePath = EnsureTrimmedSprite(TitlePath);
        string continueSpritePath = EnsureTrimmedSprite(ContinuePath);
        string previewPanelSpritePath = EnsureTrimmedSprite(PreviewPanelPath);
        string buttonSpritePath = EnsureTrimmedSprite(ButtonPath);
        string rewardedIconSpritePath = EnsureTrimmedSprite(RewardedIconPath);
        string watchAdTextSpritePath = EnsureTrimmedSprite(WatchAdTextPath);
        string noThanksTextSpritePath = EnsureTrimmedSprite(NoThanksTextPath);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        string[] spritePaths =
        {
            panelSpritePath,
            titleSpritePath,
            continueSpritePath,
            previewPanelSpritePath,
            buttonSpritePath,
            rewardedIconSpritePath,
            watchAdTextSpritePath,
            noThanksTextSpritePath
        };

        ImportSprite(ButtonPath, Vector4.zero, true);
        for (int i = 0; i < spritePaths.Length; i++)
        {
            Vector4 border = Vector4.zero;
            if (spritePaths[i] == previewPanelSpritePath)
            {
                border = PreviewPanelBorder;
            }
            else if (spritePaths[i] == buttonSpritePath)
            {
                border = WatchButtonBorder;
            }

            ImportSprite(
                spritePaths[i],
                border,
                spritePaths[i] == buttonSpritePath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int canvasCountBefore = CountSceneComponents<Canvas>(scene);
        int eventSystemCountBefore = CountSceneComponents<EventSystem>(scene);
        int gameManagerCountBefore = CountSceneComponents<GameManager>(scene);

        RectTransform safeArea = FindInScene(scene, "SafeArea") as RectTransform;
        GameManager gameManager = FindComponentInScene<GameManager>(scene);
        if (safeArea == null || gameManager == null)
        {
            throw new InvalidOperationException(
                "Game.unity must contain the existing SafeArea and GameManager.");
        }

        RectTransform overlay = GetSingleOverlay(scene, safeArea);
        Undo.RegisterFullObjectHierarchyUndo(overlay.gameObject, "Build Ocean Rescue");
        ConfigureStretch(overlay);
        overlay.name = "OceanRescueOverlay";

        RectTransform dimRect = GetOrMoveRect(overlay, "DimBackground");
        ConfigureStretch(dimRect);
        Image dim = GetOrAdd<Image>(dimRect.gameObject);
        dim.sprite = null;
        dim.type = Image.Type.Simple;
        dim.color = new Color(0.005f, 0.02f, 0.07f, 0.74f);
        dim.raycastTarget = true;
        dimRect.SetAsFirstSibling();

        RectTransform popup = GetOrMoveRect(overlay, "PopupRoot");
        ConfigureCentered(popup, new Vector2(0f, -10f), new Vector2(864f, 1200f));
        CanvasGroup popupGroup = GetOrAdd<CanvasGroup>(popup.gameObject);
        popupGroup.alpha = 1f;
        popupGroup.interactable = true;
        popupGroup.blocksRaycasts = true;
        popup.SetAsLastSibling();

        Image panel = ConfigureSpriteImage(
            GetOrMoveRect(popup, "OceanRescuePanel"),
            LoadSprite(panelSpritePath),
            Vector2.zero,
            new Vector2(864f, 1060f),
            false);

        Image title = ConfigureSpriteImage(
            GetOrMoveRect(popup, "OceanRescueTitle"),
            LoadSprite(titleSpritePath),
            new Vector2(0f, 303f),
            new Vector2(615f, 128f),
            false);

        Image continueText = ConfigureSpriteImage(
            GetOrMoveRect(popup, "ContinueText"),
            LoadSprite(continueSpritePath),
            new Vector2(0f, 208f),
            new Vector2(510f, 41f),
            false);

        RectTransform previewPanelRect = GetOrMoveRect(popup, "RescuePreviewPanel");
        Image previewPanel = ConfigureSpriteImage(
            previewPanelRect,
            LoadSprite(previewPanelSpritePath),
            new Vector2(0f, -23f),
            new Vector2(760f, 380f),
            false);
        previewPanel.type = Image.Type.Sliced;
        previewPanel.preserveAspect = false;
        previewPanel.fillCenter = true;

        RectTransform previewLeft = ConfigurePreviewRoot(
            GetOrMoveRect(previewPanelRect, "PreviewPieceLeft"),
            new Vector2(-235f, 0f));
        RectTransform previewMiddle = ConfigurePreviewRoot(
            GetOrMoveRect(previewPanelRect, "PreviewPieceMiddle"),
            Vector2.zero);
        RectTransform previewRight = ConfigurePreviewRoot(
            GetOrMoveRect(previewPanelRect, "PreviewPieceRight"),
            new Vector2(235f, 0f));

        RectTransform watchButtonRect = GetOrMoveRect(popup, "WatchAdButton");
        ConfigureCentered(
            watchButtonRect,
            new Vector2(0f, -305f),
            new Vector2(820f, 239f));
        Image watchHitArea = GetOrAdd<Image>(watchButtonRect.gameObject);
        watchHitArea.sprite = null;
        watchHitArea.color = Color.clear;
        watchHitArea.raycastTarget = true;
        Button watchButton = GetOrAdd<Button>(watchButtonRect.gameObject);
        ConfigureButton(watchButton, watchHitArea);

        Image watchVisual = ConfigureSpriteImage(
            GetOrMoveRect(watchButtonRect, "WatchAdButtonVisual"),
            LoadSprite(buttonSpritePath),
            Vector2.zero,
            new Vector2(608f, 191.2f),
            false);
        watchVisual.type = Image.Type.Simple;
        watchVisual.preserveAspect = true;

        Image rewardedIcon = ConfigureSpriteImage(
            GetOrMoveRect(watchButtonRect, "RewardedAdIcon"),
            LoadSprite(rewardedIconSpritePath),
            new Vector2(-164f, 0f),
            new Vector2(70.4f, 68.8f),
            false);

        Image watchText = ConfigureSpriteImage(
            GetOrMoveRect(watchButtonRect, "WatchAdText"),
            LoadSprite(watchAdTextSpritePath),
            new Vector2(40f, 0f),
            new Vector2(328f, 76.8f),
            false);

        RectTransform noThanksButtonRect = GetOrMoveRect(popup, "NoThanksButton");
        ConfigureCentered(
            noThanksButtonRect,
            new Vector2(0f, -450f),
            new Vector2(420f, 110f));
        Image noThanksHitArea = GetOrAdd<Image>(noThanksButtonRect.gameObject);
        noThanksHitArea.sprite = null;
        noThanksHitArea.color = Color.clear;
        noThanksHitArea.raycastTarget = true;
        Button noThanksButton = GetOrAdd<Button>(noThanksButtonRect.gameObject);
        ConfigureButton(noThanksButton, noThanksHitArea);

        Image noThanksText = ConfigureSpriteImage(
            GetOrMoveRect(noThanksButtonRect, "NoThanksText"),
            LoadSprite(noThanksTextSpritePath),
            Vector2.zero,
            new Vector2(200f, 60f),
            false);
        continueText.gameObject.SetActive(true);

        TMP_FontAsset feedbackFont =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FeedbackFontPath);
        TMP_Text feedback = ConfigureFeedbackText(
            GetOrMoveRect(popup, "FeedbackText"),
            feedbackFont);

        panel.rectTransform.SetSiblingIndex(0);
        title.rectTransform.SetSiblingIndex(1);
        continueText.rectTransform.SetSiblingIndex(2);
        previewPanelRect.SetSiblingIndex(3);
        watchButtonRect.SetSiblingIndex(4);
        noThanksButtonRect.SetSiblingIndex(5);
        feedback.rectTransform.SetSiblingIndex(6);

        OceanRescueUI ui = GetOrAdd<OceanRescueUI>(overlay.gameObject);
        SerializedObject uiSerialized = new SerializedObject(ui);
        uiSerialized.FindProperty("root").objectReferenceValue = overlay.gameObject;
        uiSerialized.FindProperty("dimBackground").objectReferenceValue = dim;
        uiSerialized.FindProperty("popupRoot").objectReferenceValue = popup;
        uiSerialized.FindProperty("popupCanvasGroup").objectReferenceValue = popupGroup;
        SerializedProperty previewRoots =
            uiSerialized.FindProperty("previewPieceRoots");
        previewRoots.arraySize = GameConstants.TraySize;
        previewRoots.GetArrayElementAtIndex(0).objectReferenceValue = previewLeft;
        previewRoots.GetArrayElementAtIndex(1).objectReferenceValue = previewMiddle;
        previewRoots.GetArrayElementAtIndex(2).objectReferenceValue = previewRight;
        uiSerialized.FindProperty("watchAdButton").objectReferenceValue = watchButton;
        uiSerialized.FindProperty("noThanksButton").objectReferenceValue = noThanksButton;
        uiSerialized.FindProperty("feedbackText").objectReferenceValue = feedback;
        uiSerialized.ApplyModifiedPropertiesWithoutUndo();

        OceanRescueController controller = GetSingleController(gameManager);
        SerializedObject controllerSerialized = new SerializedObject(controller);
        controllerSerialized.FindProperty("oceanRescueUI").objectReferenceValue = ui;
        controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject gameManagerSerialized = new SerializedObject(gameManager);
        SerializedProperty controllerProperty =
            gameManagerSerialized.FindProperty("oceanRescueController");
        if (controllerProperty == null)
        {
            throw new InvalidOperationException(
                "GameManager.oceanRescueController was not found. Allow scripts to compile first.");
        }

        controllerProperty.objectReferenceValue = controller;
        gameManagerSerialized.ApplyModifiedPropertiesWithoutUndo();

        panel.raycastTarget = false;
        title.raycastTarget = false;
        continueText.raycastTarget = false;
        previewPanel.raycastTarget = false;
        watchVisual.raycastTarget = false;
        rewardedIcon.raycastTarget = false;
        watchText.raycastTarget = false;
        noThanksText.raycastTarget = false;
        feedback.gameObject.SetActive(false);
        overlay.gameObject.SetActive(false);

        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(gameManager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Validate(
            scene,
            canvasCountBefore,
            eventSystemCountBefore,
            gameManagerCountBefore);
        Debug.Log(
            "OceanRescueBuilder: Ocean Rescue built, validated, and Game.unity saved.");
    }

    private static RectTransform GetSingleOverlay(
        Scene scene,
        RectTransform safeArea)
    {
        List<Transform> overlays = FindAllInScene(scene, "OceanRescueOverlay");
        RectTransform keep = overlays.Count > 0
            ? overlays[0] as RectTransform
            : null;
        if (keep == null)
        {
            GameObject created = new GameObject(
                "OceanRescueOverlay",
                typeof(RectTransform));
            keep = created.GetComponent<RectTransform>();
            keep.SetParent(safeArea, false);
        }

        if (keep.parent != safeArea)
        {
            keep.SetParent(safeArea, false);
        }

        for (int i = overlays.Count - 1; i >= 1; i--)
        {
            if (overlays[i] != null)
            {
                Undo.DestroyObjectImmediate(overlays[i].gameObject);
            }
        }

        return keep;
    }

    private static OceanRescueController GetSingleController(
        GameManager gameManager)
    {
        OceanRescueController[] controllers =
            gameManager.GetComponents<OceanRescueController>();
        OceanRescueController keep = controllers.Length > 0
            ? controllers[0]
            : Undo.AddComponent<OceanRescueController>(gameManager.gameObject);
        for (int i = controllers.Length - 1; i >= 1; i--)
        {
            Undo.DestroyObjectImmediate(controllers[i]);
        }

        return keep;
    }

    private static RectTransform ConfigurePreviewRoot(
        RectTransform rect,
        Vector2 position)
    {
        ConfigureCentered(rect, position, new Vector2(240f, 320f));
        rect.localScale = Vector3.one * 0.78f;
        Image oldImage = rect.GetComponent<Image>();
        if (oldImage != null)
        {
            UnityEngine.Object.DestroyImmediate(oldImage);
        }

        return rect;
    }

    private static TMP_Text ConfigureFeedbackText(
        RectTransform rect,
        TMP_FontAsset font)
    {
        Image oldImage = rect.GetComponent<Image>();
        if (oldImage != null)
        {
            UnityEngine.Object.DestroyImmediate(oldImage);
        }

        ConfigureCentered(rect, new Vector2(0f, -700f), new Vector2(760f, 80f));
        TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(rect.gameObject);
        text.text = "Ad unavailable. Try again.";
        text.font = font;
        if (font != null)
        {
            text.fontSharedMaterial = font.material;
        }

        text.fontSize = 38f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.82f, 0.98f, 1f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        Shadow shadow = GetOrAdd<Shadow>(rect.gameObject);
        shadow.effectColor = new Color(0f, 0.05f, 0.16f, 0.75f);
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = true;
        return text;
    }

    private static void ConfigureButton(Button button, Graphic target)
    {
        button.targetGraphic = target;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.88f, 0.94f, 0.82f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.58f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
    }

    private static Image ConfigureSpriteImage(
        RectTransform rect,
        Sprite sprite,
        Vector2 position,
        Vector2 size,
        bool raycast)
    {
        TMP_Text oldText = rect.GetComponent<TMP_Text>();
        if (oldText != null)
        {
            UnityEngine.Object.DestroyImmediate(oldText);
        }

        ConfigureCentered(rect, position, size);
        Image image = GetOrAdd<Image>(rect.gameObject);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = raycast;
        return image;
    }

    private static void ConfigureStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localPosition = Vector3.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void ConfigureCentered(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static RectTransform GetOrMoveRect(
        Transform parent,
        string objectName)
    {
        Transform found = FindDeep(parent, objectName);
        RectTransform rect = found as RectTransform;
        if (found != null && rect == null)
        {
            throw new InvalidOperationException(
                objectName + " exists but is not a RectTransform.");
        }

        if (rect == null)
        {
            GameObject created = new GameObject(
                objectName,
                typeof(RectTransform));
            rect = created.GetComponent<RectTransform>();
        }

        if (rect.parent != parent)
        {
            rect.SetParent(parent, false);
        }

        rect.name = objectName;
        return rect;
    }

    private static string EnsureTrimmedSprite(string sourcePath)
    {
        string outputPath = TrimmedArtRoot
            + Path.GetFileNameWithoutExtension(sourcePath)
            + "_Trimmed.png";
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string sourceAbsolutePath = Path.Combine(
            projectRoot,
            sourcePath.Replace('/', Path.DirectorySeparatorChar));
        string outputAbsolutePath = Path.Combine(
            projectRoot,
            outputPath.Replace('/', Path.DirectorySeparatorChar));
        byte[] sourceBytes = File.ReadAllBytes(sourceAbsolutePath);

        Texture2D sourceTexture = new Texture2D(
            2,
            2,
            TextureFormat.RGBA32,
            false,
            true);
        if (!ImageConversion.LoadImage(sourceTexture, sourceBytes, false))
        {
            UnityEngine.Object.DestroyImmediate(sourceTexture);
            throw new InvalidOperationException(
                "Could not decode Ocean Rescue sprite: " + sourcePath);
        }

        Color32[] sourcePixels = sourceTexture.GetPixels32();
        int sourceWidth = sourceTexture.width;
        int sourceHeight = sourceTexture.height;
        int minX = sourceWidth;
        int minY = sourceHeight;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < sourceHeight; y++)
        {
            int rowOffset = y * sourceWidth;
            for (int x = 0; x < sourceWidth; x++)
            {
                if (sourcePixels[rowOffset + x].a < VisibleAlphaThreshold)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            UnityEngine.Object.DestroyImmediate(sourceTexture);
            throw new InvalidOperationException(
                "Ocean Rescue sprite contains no visible pixels: " + sourcePath);
        }

        minX = Mathf.Max(0, minX - TrimPaddingPixels);
        minY = Mathf.Max(0, minY - TrimPaddingPixels);
        maxX = Mathf.Min(sourceWidth - 1, maxX + TrimPaddingPixels);
        maxY = Mathf.Min(sourceHeight - 1, maxY + TrimPaddingPixels);

        int trimmedWidth = maxX - minX + 1;
        int trimmedHeight = maxY - minY + 1;
        Color32[] trimmedPixels = new Color32[trimmedWidth * trimmedHeight];
        for (int y = 0; y < trimmedHeight; y++)
        {
            Array.Copy(
                sourcePixels,
                (minY + y) * sourceWidth + minX,
                trimmedPixels,
                y * trimmedWidth,
                trimmedWidth);
        }

        Texture2D trimmedTexture = new Texture2D(
            trimmedWidth,
            trimmedHeight,
            TextureFormat.RGBA32,
            false,
            true);
        trimmedTexture.SetPixels32(trimmedPixels);
        trimmedTexture.Apply(false, false);
        byte[] trimmedBytes = trimmedTexture.EncodeToPNG();

        Directory.CreateDirectory(Path.GetDirectoryName(outputAbsolutePath));
        if (!File.Exists(outputAbsolutePath)
            || !ByteArraysEqual(File.ReadAllBytes(outputAbsolutePath), trimmedBytes))
        {
            File.WriteAllBytes(outputAbsolutePath, trimmedBytes);
        }

        Debug.Log(
            $"OceanRescueBuilder trim: {Path.GetFileName(sourcePath)} "
            + $"{sourceWidth}x{sourceHeight} -> {trimmedWidth}x{trimmedHeight}, "
            + $"alpha>={VisibleAlphaThreshold}, padding={TrimPaddingPixels}px.");

        UnityEngine.Object.DestroyImmediate(trimmedTexture);
        UnityEngine.Object.DestroyImmediate(sourceTexture);
        return outputPath;
    }

    private static bool ByteArraysEqual(byte[] first, byte[] second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        if (first == null || second == null || first.Length != second.Length)
        {
            return false;
        }

        for (int i = 0; i < first.Length; i++)
        {
            if (first[i] != second[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void ImportSprite(
        string path,
        Vector4 border,
        bool useFullRectMesh)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Missing Ocean Rescue asset: " + path);
        }

        TextureImporterSettings textureSettings =
            new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        bool changed = importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || (useFullRectMesh
                && textureSettings.spriteMeshType != SpriteMeshType.FullRect)
            || !importer.alphaIsTransparency
            || importer.mipmapEnabled
            || importer.wrapMode != TextureWrapMode.Clamp
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.spriteBorder != border;
        if (useFullRectMesh)
        {
            textureSettings.spriteMode = (int)SpriteImportMode.Single;
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spriteBorder = border;
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
            throw new InvalidOperationException("Could not load Ocean Rescue sprite: " + path);
        }

        return sprite;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static T FindComponentInScene<T>(Scene scene)
        where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static Transform FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDeep(root.transform, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static List<Transform> FindAllInScene(
        Scene scene,
        string objectName)
    {
        List<Transform> matches = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            CollectNamed(root.transform, objectName, matches);
        }

        return matches;
    }

    private static Transform FindDeep(Transform parent, string objectName)
    {
        if (parent.name == objectName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void CollectNamed(
        Transform parent,
        string objectName,
        List<Transform> matches)
    {
        if (parent.name == objectName)
        {
            matches.Add(parent);
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            CollectNamed(parent.GetChild(i), objectName, matches);
        }
    }

    private static int CountSceneComponents<T>(Scene scene)
        where T : Component
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            count += root.GetComponentsInChildren<T>(true).Length;
        }

        return count;
    }

    private static void Validate(
        Scene scene,
        int canvasCountBefore,
        int eventSystemCountBefore,
        int gameManagerCountBefore)
    {
        int missingScripts = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                missingScripts +=
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transforms[i].gameObject);
            }
        }

        int overlayCount = FindAllInScene(scene, "OceanRescueOverlay").Count;
        int panelCount = FindAllInScene(scene, "OceanRescuePanel").Count;
        int canvasCount = CountSceneComponents<Canvas>(scene);
        int eventSystemCount = CountSceneComponents<EventSystem>(scene);
        int gameManagerCount = CountSceneComponents<GameManager>(scene);
        int controllerCount = CountSceneComponents<OceanRescueController>(scene);
        int uiCount = CountSceneComponents<OceanRescueUI>(scene);

        if (missingScripts != 0
            || overlayCount != 1
            || panelCount != 1
            || controllerCount != 1
            || uiCount != 1
            || canvasCount != canvasCountBefore
            || eventSystemCount != eventSystemCountBefore
            || gameManagerCount != gameManagerCountBefore)
        {
            throw new InvalidOperationException(
                "Ocean Rescue validation failed: "
                + $"missingScripts={missingScripts}, overlay={overlayCount}, "
                + $"panel={panelCount}, controller={controllerCount}, ui={uiCount}, "
                + $"Canvas={canvasCount}/{canvasCountBefore}, "
                + $"EventSystem={eventSystemCount}/{eventSystemCountBefore}, "
                + $"GameManager={gameManagerCount}/{gameManagerCountBefore}.");
        }

        BoardSnapshot emptyBoard = new BoardSnapshot();
        for (int i = 0; i < emptyBoard.colors.Length; i++)
        {
            emptyBoard.colors[i] = -1;
        }

        if (!OceanRescueController.TryFindRescueSet(
                emptyBoard,
                out PieceInstance[] testSet)
            || testSet == null
            || testSet.Length != GameConstants.TraySize)
        {
            throw new InvalidOperationException(
                "Ocean Rescue rescue-set simulation failed on an empty board.");
        }

        Debug.Log(
            "OceanRescueBuilder validation: missingScripts=0, "
            + "OceanRescueOverlay=1, OceanRescuePanel=1, "
            + $"Canvas={canvasCount}, EventSystem={eventSystemCount}, "
            + $"GameManager={gameManagerCount}, rescueSimulation=pass.");
    }
}
