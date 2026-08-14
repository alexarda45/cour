using System.IO;
using System.Text;
using ChromaBlast;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ChromaBlastProjectBuilder
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string SceneFolder = "Assets/Scenes";
    private const string ArtFolder = "Assets/Art";
    private const string CompanyName = "Arda Games";
    private const string ProductName = "Chroma Blast";
    private const string BundleId = "com.ardagames.chromablast";
    private const string VersionName = "0.1.0";
    private const int VersionCode = 1;

    [MenuItem("Chroma Blast/Genereaza scene si prefab-uri")]
    public static void GenerateProjectContent()
    {
        EnsureFolders();

        BoardCell cellPrefab = CreateBoardCellPrefab();
        BlockView blockPrefab = CreateBlockPrefab();
        PieceView piecePrefab = CreatePiecePrefab(blockPrefab);
        ParticleSystem[] particlePrefabs = CreateParticlePrefabs();
        CreateGeneratedAppIcon();
        ConfigurePlayerSettings();

        CreateBootScene();
        CreateMenuScene();
        CreateGameScene(cellPrefab, blockPrefab, piecePrefab, particlePrefabs);
        ConfigureBuildScenes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Chroma Blast", "Scenele si prefab-urile au fost generate.", "OK");
    }

    [MenuItem("Chroma Blast/Pregateste Android Release")]
    public static void PrepareAndroidRelease()
    {
        EnsureFolders();
        CreateGeneratedAppIcon();
        ConfigurePlayerSettings();
        ConfigureAndroidReleaseSettings();
        ConfigureBuildScenes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Chroma Blast - Android Release", BuildAndroidReadinessReport(), "OK");
    }

    [MenuItem("Chroma Blast/UI/Apply Gameplay Reference Overhaul")]
    public static void ApplyGameplayReferenceOverhaul()
    {
        string gameScenePath = $"{SceneFolder}/Game.unity";
        Scene scene = EditorSceneManager.OpenScene(gameScenePath, OpenSceneMode.Single);
        GameObject canvasObject = GameObject.Find("GameCanvas");
        RectTransform safeRoot = canvasObject == null
            ? null
            : canvasObject.transform.Find("SafeArea") as RectTransform;
        if (safeRoot == null)
        {
            Debug.LogError("Gameplay reference overhaul could not find GameCanvas/SafeArea.");
            return;
        }

        RectTransform board = safeRoot.Find("Board") as RectTransform;
        RectTransform tray = safeRoot.Find("Tray") as RectTransform;
        RectTransform score = safeRoot.Find("ScoreText") as RectTransform;
        RectTransform timer = safeRoot.Find("TimerText") as RectTransform;
        RectTransform chain = safeRoot.Find("ChainText") as RectTransform;
        RectTransform menuButton = safeRoot.Find("MenuButton") as RectTransform;

        SetFixedRect(board, 0.50f, 0.50f, new Vector2(940f, 940f));
        SetFixedRect(score, 0.50f, 0.85f, new Vector2(780f, 190f));
        SetFixedRect(timer, 0.50f, 0.945f, new Vector2(170f, 64f));
        SetFixedRect(chain, 0.50f, 0.786f, new Vector2(420f, 38f));
        SetFixedRect(menuButton, 0.946f, 0.967f, new Vector2(100f, 100f));
        SetFixedRect(tray, 0.50f, 0.15f, new Vector2(960f, 280f));

        if (score != null)
        {
            TMP_Text scoreText = score.GetComponent<TMP_Text>();
            if (scoreText != null)
            {
                scoreText.color = Color.white;
                scoreText.fontStyle |= FontStyles.Bold;
                scoreText.enableAutoSizing = true;
                scoreText.fontSize = 170f;
                scoreText.fontSizeMax = 170f;
                scoreText.fontSizeMin = 104f;
                scoreText.characterSpacing = -4f;
                scoreText.alignment = TextAlignmentOptions.Center;
            }
        }

        if (board != null)
        {
            Image boardImage = board.GetComponent<Image>();
            if (boardImage != null)
            {
                boardImage.enabled = false;
                boardImage.raycastTarget = false;
            }
        }

        ConfigureTransparentTray(tray);

        float[] barAnchors = { 0.185f, 0.395f, 0.605f, 0.815f };
        for (int i = 0; i < barAnchors.Length; i++)
        {
            RectTransform bar = safeRoot.Find($"ChromaBar_{(ChromaColor)i}") as RectTransform;
            if (bar != null)
            {
                SetFixedRect(bar, barAnchors[i], 0.762f, new Vector2(185f, 36f));
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, gameScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Gameplay reference overhaul applied to Game.unity.");
    }

    private static void ConfigureTransparentTray(RectTransform tray)
    {
        if (tray == null)
        {
            return;
        }

        Image trayImage = tray.GetComponent<Image>();
        if (trayImage != null)
        {
            trayImage.color = Color.clear;
            trayImage.enabled = false;
            trayImage.raycastTarget = false;
        }

        Outline trayOutline = tray.GetComponent<Outline>();
        if (trayOutline != null)
        {
            trayOutline.enabled = false;
        }

        Shadow[] trayShadows = tray.GetComponents<Shadow>();
        for (int i = 0; i < trayShadows.Length; i++)
        {
            if (trayShadows[i] != null)
            {
                trayShadows[i].enabled = false;
            }
        }

        for (int i = 0; i < GameConstants.TraySize; i++)
        {
            RectTransform slot = tray.Find($"TraySlot_{i}") as RectTransform;
            if (slot == null)
            {
                continue;
            }

            slot.anchorMin = new Vector2(0.5f, 0.5f);
            slot.anchorMax = slot.anchorMin;
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = new Vector2((i - 1) * 310f, 0f);
            slot.sizeDelta = new Vector2(300f, 260f);
            slot.localScale = Vector3.one;

            Image slotImage = slot.GetComponent<Image>();
            if (slotImage != null)
            {
                slotImage.color = Color.clear;
                slotImage.raycastTarget = false;
            }

            string[] decorativeLayers =
            {
                "TraySlotGlow",
                "TraySlotPanel",
                "TraySlotInnerShadow",
                "TraySlotRim"
            };
            for (int layerIndex = 0; layerIndex < decorativeLayers.Length; layerIndex++)
            {
                Transform layer = slot.Find(decorativeLayers[layerIndex]);
                if (layer != null)
                {
                    layer.gameObject.SetActive(false);
                }
            }
        }
    }

#pragma warning disable 0618
    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;
        PlayerSettings.bundleVersion = VersionName;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/AppIcon.png");
        if (icon != null)
        {
            System.Reflection.PropertyInfo defaultIconProperty = typeof(PlayerSettings).GetProperty(
                "defaultIcon",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (defaultIconProperty != null && defaultIconProperty.CanWrite)
            {
                defaultIconProperty.SetValue(null, icon);
            }
        }

        PlayerSettings.iOS.buildNumber = VersionCode.ToString();
    }
#pragma warning restore 0618

    private static void ConfigureAndroidReleaseSettings()
    {
        PlayerSettings.Android.bundleVersionCode = VersionCode;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.stripEngineCode = true;

        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;
    }

    private static void ConfigureBuildScenes()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene($"{SceneFolder}/Boot.unity", true),
            new EditorBuildSettingsScene($"{SceneFolder}/Menu.unity", true),
            new EditorBuildSettingsScene($"{SceneFolder}/Game.unity", true)
        };
    }

    private static string BuildAndroidReadinessReport()
    {
        bool iconOk = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/AppIcon.png") != null;
        bool androidSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
        bool scenesOk = EditorBuildSettings.scenes.Length == 3
            && EditorBuildSettings.scenes[0].path == $"{SceneFolder}/Boot.unity"
            && EditorBuildSettings.scenes[1].path == $"{SceneFolder}/Menu.unity"
            && EditorBuildSettings.scenes[2].path == $"{SceneFolder}/Game.unity";

        StringBuilder report = new StringBuilder();
        report.AppendLine("Setari aplicate:");
        report.AppendLine($"- App: {PlayerSettings.productName}");
        report.AppendLine($"- Package: {PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)}");
        report.AppendLine($"- Versiune: {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode})");
        report.AppendLine($"- Portrait: {(PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait ? "OK" : "verifica")}");
        report.AppendLine($"- ARM64 + IL2CPP: {(PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64 ? "OK" : "verifica")}");
        report.AppendLine($"- Build AAB: {(EditorUserBuildSettings.buildAppBundle ? "OK" : "verifica")}");
        report.AppendLine($"- Scene Boot/Menu/Game: {(scenesOk ? "OK" : "verifica")}");
        report.AppendLine($"- Icon: {(iconOk ? "OK" : "lipseste")}");
        report.AppendLine($"- Modul Android instalat: {(androidSupported ? "OK" : "lipseste")}");
        report.AppendLine();
        report.AppendLine("Mai trebuie inainte de Google Play:");
        report.AppendLine("- seteaza un keystore release propriu");
        report.AppendLine("- inlocuieste ID-urile reale pentru Ads/IAP cand le ai");
        report.AppendLine("- testeaza 10 minute pe telefon");
        report.AppendLine("- fa build .aab din Build Profiles");
        return report.ToString();
    }

    private static BoardCell CreateBoardCellPrefab()
    {
        GameObject root = new GameObject("BoardCell", typeof(RectTransform), typeof(Image), typeof(BoardCell));
        Image background = root.GetComponent<Image>();
        background.color = Hex("#D4EAF8");
        Outline gridLine = root.AddComponent<Outline>();
        gridLine.effectColor = new Color(0.16f, 0.50f, 0.69f, 0.30f);
        gridLine.effectDistance = new Vector2(1f, -1f);

        GameObject previewObject = new GameObject("Preview", typeof(RectTransform), typeof(Image));
        previewObject.transform.SetParent(root.transform, false);
        RectTransform previewRect = (RectTransform)previewObject.transform;
        Stretch(previewRect, Vector2.one * 4f, Vector2.one * -4f);
        Image previewImage = previewObject.GetComponent<Image>();
        previewImage.raycastTarget = false;
        previewImage.enabled = false;

        BoardCell cell = root.GetComponent<BoardCell>();
        SetObject(cell, "background", background);
        SetObject(cell, "preview", previewImage);

        BoardCell prefab = SavePrefab<BoardCell>(root, $"{PrefabFolder}/BoardCell.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static BlockView CreateBlockPrefab()
    {
        GameObject root = new GameObject("Block", typeof(RectTransform), typeof(Image), typeof(BlockView));
        Image image = root.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;

        Image inner = CreateChildImage("Inner", root.transform, new Vector2(4f, 4f), new Vector2(-4f, -4f), Color.white);
        Image shine = CreateChildImage("Shine", root.transform, new Vector2(8f, -8f), new Vector2(-8f, -24f), new Color(1f, 1f, 1f, 0.14f));

        BlockView block = root.GetComponent<BlockView>();
        SetObject(block, "image", image);
        SetObject(block, "innerImage", inner);
        SetObject(block, "shineImage", shine);

        BlockView prefab = SavePrefab<BlockView>(root, $"{PrefabFolder}/Block.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static PieceView CreatePiecePrefab(BlockView blockPrefab)
    {
        GameObject root = new GameObject("Piece", typeof(RectTransform), typeof(CanvasGroup), typeof(PieceView));
        RectTransform rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(180f, 180f);
        PieceView piece = root.GetComponent<PieceView>();
        SetObject(piece, "blockRoot", rect);
        SetObject(piece, "blockPrefab", blockPrefab);

        PieceView prefab = SavePrefab<PieceView>(root, $"{PrefabFolder}/Piece.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static ParticleSystem[] CreateParticlePrefabs()
    {
        ParticleSystem[] prefabs = new ParticleSystem[GameConstants.ColorCount];
        for (int i = 0; i < GameConstants.ColorCount; i++)
        {
            ChromaColor chromaColor = (ChromaColor)i;
            GameObject root = new GameObject($"Particles_{chromaColor}", typeof(ParticleSystem));
            ParticleSystem particles = root.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.startLifetime = 0.32f;
            main.startSpeed = 2.35f;
            main.startSize = 0.075f;
            main.startColor = ChromaPalette.GetColor(chromaColor);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)14) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.22f;

            prefabs[i] = SavePrefab<ParticleSystem>(root, $"{PrefabFolder}/Particles_{chromaColor}.prefab");
            Object.DestroyImmediate(root);
        }

        return prefabs;
    }

    private static void CreateGeneratedAppIcon()
    {
        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color top = Hex("#12163A");
        Color bottom = Hex("#050711");
        Color cyan = Hex("#17E6FF");
        Color magenta = Hex("#FF4FD8");
        Color lime = Hex("#A6FF42");
        Color gold = Hex("#FFD166");

        for (int y = 0; y < size; y++)
        {
            float vertical = y / (float)(size - 1);
            for (int x = 0; x < size; x++)
            {
                float horizontal = x / (float)(size - 1);
                float vignette = Mathf.Abs(horizontal - 0.5f) + Mathf.Abs(vertical - 0.5f);
                Color pixel = Color.Lerp(bottom, top, vertical);
                pixel = Color.Lerp(pixel, Color.black, Mathf.Clamp01(vignette * 0.55f));
                texture.SetPixel(x, y, pixel);
            }
        }

        DrawIconBlock(texture, 154, 150, 70, cyan);
        DrawIconBlock(texture, 224, 150, 70, magenta);
        DrawIconBlock(texture, 294, 150, 70, lime);
        DrawIconBlock(texture, 154, 220, 70, gold);
        DrawIconBlock(texture, 224, 220, 70, cyan);
        DrawIconBlock(texture, 154, 290, 70, magenta);
        DrawIconBlock(texture, 224, 290, 70, lime);
        DrawIconBlock(texture, 294, 290, 70, gold);

        texture.Apply();
        File.WriteAllBytes($"{ArtFolder}/AppIcon.png", texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset($"{ArtFolder}/AppIcon.png", ImportAssetOptions.ForceUpdate);
    }

    private static void DrawIconBlock(Texture2D texture, int startX, int startY, int size, Color color)
    {
        int maxX = Mathf.Min(texture.width, startX + size);
        int maxY = Mathf.Min(texture.height, startY + size);
        for (int y = Mathf.Max(0, startY); y < maxY; y++)
        {
            for (int x = Mathf.Max(0, startX); x < maxX; x++)
            {
                int inset = 8;
                bool border = x < startX + inset || y < startY + inset || x >= maxX - inset || y >= maxY - inset;
                Color pixel = border ? Color.Lerp(color, Color.black, 0.18f) : Color.Lerp(color, Color.white, 0.08f);
                if (y > startY + size * 0.63f)
                {
                    pixel = Color.Lerp(pixel, Color.white, 0.18f);
                }

                texture.SetPixel(x, y, pixel);
            }
        }
    }

    private static void CreateBootScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject managers = new GameObject("PersistentManagers");
        SaveManager saveManager = managers.AddComponent<SaveManager>();
        AudioManager audioManager = managers.AddComponent<AudioManager>();
        AudioListener audioListener = managers.AddComponent<AudioListener>();
        managers.AddComponent<AdManager>();
        managers.AddComponent<IAPManager>();
        managers.AddComponent<AnalyticsManager>();
        managers.AddComponent<BootLoader>();

        GameObject music = new GameObject("MusicSource", typeof(AudioSource));
        music.transform.SetParent(managers.transform, false);
        GameObject sfx = new GameObject("SfxSource", typeof(AudioSource));
        sfx.transform.SetParent(managers.transform, false);
        SetObject(audioManager, "musicSource", music.GetComponent<AudioSource>());
        SetObject(audioManager, "sfxSource", sfx.GetComponent<AudioSource>());
        SetObject(audioManager, "audioListener", audioListener);

        _ = saveManager;
        EditorSceneManager.SaveScene(scene, $"{SceneFolder}/Boot.unity");
    }

    private static void CreateMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateEventSystem();
        RectTransform safeRoot = CreateCanvas("MenuCanvas");

        Image background = safeRoot.gameObject.AddComponent<Image>();
        background.color = Hex("#070A14");
        safeRoot.gameObject.AddComponent<NeonBackdrop>();

        CreateLogoMosaic(safeRoot, "MenuLogo", new Vector2(0.38f, 0.86f), new Vector2(0.62f, 0.95f));

        TMP_Text title = CreateText("Title", safeRoot, "CHROMA BLAST", 78, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.04f, 0.780f), new Vector2(0.96f, 0.855f), Vector2.zero, Vector2.zero);
        title.color = Hex("#17E6FF");
        AddTextGlow(title.gameObject, Hex("#113D72"), new Vector2(0f, -3f));

        TMP_Text subtitle = CreateText("Subtitle", safeRoot, "PURE  /  CHAIN  /  POP", 24, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(subtitle.rectTransform, new Vector2(0.08f, 0.735f), new Vector2(0.92f, 0.775f), Vector2.zero, Vector2.zero);
        subtitle.color = Hex("#EAF1FF");

        Button classic = CreateButton("ClassicButton", safeRoot, "CLASSIC\nfara limita + undo", Hex("#122145"), Hex("#17E6FF"));
        SetRect((RectTransform)classic.transform, new Vector2(0.09f, 0.620f), new Vector2(0.91f, 0.716f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(classic, 28);

        Button newClassic = CreateButton("NewClassicButton", safeRoot, "JOC\nNOU", Hex("#10182E"), Hex("#FFD166"));
        SetRect((RectTransform)newClassic.transform, new Vector2(0.67f, 0.620f), new Vector2(0.91f, 0.716f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(newClassic, 18);
        newClassic.gameObject.SetActive(false);

        Button blitz = CreateButton("BlitzButton", safeRoot, "BLITZ 90\nrapid si intens", Hex("#261445"), Hex("#FF4FD8"));
        SetRect((RectTransform)blitz.transform, new Vector2(0.09f, 0.505f), new Vector2(0.91f, 0.602f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(blitz, 28);

        Button daily = CreateButton("DailyButton", safeRoot, "ZILNIC\naceeasi provocare azi", Hex("#1E2A17"), Hex("#A6FF42"));
        SetRect((RectTransform)daily.transform, new Vector2(0.09f, 0.397f), new Vector2(0.91f, 0.494f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(daily, 27);

        TMP_Text dailyInfo = CreateText("DailyInfo", safeRoot, "AZI 20.06  BEST 0  MEDALIE -  SERIE x0  INCERCARI 0", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(dailyInfo.rectTransform, new Vector2(0.08f, 0.374f), new Vector2(0.92f, 0.397f), Vector2.zero, Vector2.zero);
        dailyInfo.color = Hex("#A6FF42");

        Button dailyGift = CreateButton("DailyGiftButton", safeRoot, "CADOU ZILNIC\n+25 MONEDE", Hex("#10182E"), Hex("#FFD166"));
        SetRect((RectTransform)dailyGift.transform, new Vector2(0.16f, 0.330f), new Vector2(0.84f, 0.371f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(dailyGift, 18);

        RectTransform scorePanel = CreatePanel("ScorePanel", safeRoot, Hex("#0B1123"));
        AddFrame(scorePanel.gameObject, Hex("#20345D"), new Color(0f, 0f, 0f, 0.36f));
        SetRect(scorePanel, new Vector2(0.09f, 0.202f), new Vector2(0.91f, 0.322f), Vector2.zero, Vector2.zero);

        TMP_Text scores = CreateText("HighScores", scorePanel, "MONEDE 0   RANG NOVICE   REALIZARI 0/8\nRECORDURI  C 0   B 0   Z 0\nLINII 0   PURE 0   POP 0", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(scores.rectTransform, Vector2.one * 14f, Vector2.one * -14f);

        RankProgressView rank = CreateRankProgress(safeRoot, new Vector2(0.09f, 0.142f), new Vector2(0.91f, 0.194f));

        Button theme = CreateButton("ThemeButton", safeRoot, "TEMA\nNEON", Hex("#10182E"), Hex("#17E6FF"));
        SetRect((RectTransform)theme.transform, new Vector2(0.10f, 0.137f), new Vector2(0.49f, 0.232f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(theme, 22);
        Image[] themeSwatches = CreateThemeSwatches((RectTransform)theme.transform);

        TMP_Text themeHint = CreateText("ThemeHint", safeRoot, "Urmatoarea: CANDY  1,500 XP sau 250 monede", 14, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(themeHint.rectTransform, new Vector2(0.08f, 0.077f), new Vector2(0.92f, 0.093f), Vector2.zero, Vector2.zero);
        themeHint.color = Hex("#A6FF42");

        Button achievements = CreateButton("AchievementsButton", safeRoot, "REALIZARI", Hex("#10182E"), Hex("#FFD166"));
        SetRect((RectTransform)achievements.transform, new Vector2(0.29f, 0.020f), new Vector2(0.52f, 0.071f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(achievements, 18);

        Button shop = CreateButton("ShopButton", safeRoot, "MAGAZIN", Hex("#10182E"), Hex("#EAF1FF"));
        SetRect((RectTransform)shop.transform, new Vector2(0.51f, 0.137f), new Vector2(0.90f, 0.232f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(shop, 22);

        Button quit = CreateButton("QuitButton", safeRoot, "IESIRE", Hex("#10182E"), Hex("#FF4FD8"));
        SetRect((RectTransform)quit.transform, new Vector2(0.75f, 0.020f), new Vector2(0.95f, 0.071f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(quit, 18);

        GameObject shopOverlay = CreateShopOverlay(
            safeRoot,
            out Button shopClose,
            out Button shopRemoveAds,
            out Button shopCosmetics,
            out Button shopRestore,
            out TMP_Text shopStatus);

        MenuUI menuUI = safeRoot.gameObject.AddComponent<MenuUI>();
        SetObject(menuUI, "classicButton", classic);
        SetObject(menuUI, "newClassicButton", newClassic);
        SetObject(menuUI, "blitzButton", blitz);
        SetObject(menuUI, "dailyButton", daily);
        SetObject(menuUI, "dailyInfoText", dailyInfo);
        SetObject(menuUI, "dailyGiftButton", dailyGift);
        SetObject(menuUI, "dailyGiftButtonText", dailyGift.GetComponentInChildren<TMP_Text>());
        SetObject(menuUI, "themeButton", theme);
        SetObject(menuUI, "themeButtonText", theme.GetComponentInChildren<TMP_Text>());
        SetObject(menuUI, "themeHintText", themeHint);
        SetObjectArray(menuUI, "themeSwatches", themeSwatches);
        SetObject(menuUI, "shopButton", shop);
        SetObject(menuUI, "shopRoot", shopOverlay);
        SetObject(menuUI, "shopStatusText", shopStatus);
        SetObject(menuUI, "shopCloseButton", shopClose);
        SetObject(menuUI, "removeAdsButton", shopRemoveAds);
        SetObject(menuUI, "shopCosmeticsButton", shopCosmetics);
        SetObject(menuUI, "shopRestoreButton", shopRestore);
        SetObject(menuUI, "achievementsButton", achievements);
        SetObject(menuUI, "quitButton", quit);
        SetObject(menuUI, "highScoresText", scores);
        SetObject(menuUI, "rankProgressView", rank);

        EditorSceneManager.SaveScene(scene, $"{SceneFolder}/Menu.unity");
    }

    private static RectTransform CreateLogoMosaic(Transform parent, string name, Vector2 min, Vector2 max)
    {
        RectTransform root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        SetRect(root, min, max, Vector2.zero, Vector2.zero);

        Vector2Int[] cells =
        {
            new Vector2Int(1, 0),
            new Vector2Int(2, 0),
            new Vector2Int(1, 1),
            new Vector2Int(2, 1),
            new Vector2Int(0, 2),
            new Vector2Int(1, 2),
            new Vector2Int(2, 2),
            new Vector2Int(3, 2),
            new Vector2Int(0, 3),
            new Vector2Int(3, 3)
        };

        ChromaColor[] colors =
        {
            ChromaColor.Cyan,
            ChromaColor.Magenta,
            ChromaColor.Amber,
            ChromaColor.Lime,
            ChromaColor.Cyan,
            ChromaColor.Lime,
            ChromaColor.Magenta,
            ChromaColor.Amber,
            ChromaColor.Magenta,
            ChromaColor.Cyan
        };

        for (int i = 0; i < cells.Length; i++)
        {
            RectTransform block = CreatePanel($"LogoBlock_{i}", root, ChromaPalette.GetColor(colors[i], ThemeType.Neon));
            float x = cells[i].x / 4f;
            float y = cells[i].y / 4f;
            SetRect(block, new Vector2(x, y), new Vector2(x + 0.25f, y + 0.25f), Vector2.one * 3f, Vector2.one * -3f);
            AddFrame(block.gameObject, Hex("#1A2F5B"), new Color(0f, 0f, 0f, 0.22f));
        }

        return root;
    }

    private static Image[] CreateThemeSwatches(RectTransform parent)
    {
        RectTransform root = new GameObject("ThemeSwatches", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        SetRect(root, new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.22f), Vector2.zero, Vector2.zero);

        Image[] swatches = new Image[GameConstants.ColorCount];
        for (int i = 0; i < swatches.Length; i++)
        {
            RectTransform swatch = CreatePanel($"ThemeSwatch_{i}", root, ChromaPalette.GetColor((ChromaColor)i, ThemeType.Neon));
            float left = i / (float)swatches.Length;
            float right = (i + 1) / (float)swatches.Length;
            SetRect(swatch, new Vector2(left, 0f), new Vector2(right, 1f), new Vector2(4f, 0f), new Vector2(-4f, 0f));
            Image image = swatch.GetComponent<Image>();
            image.raycastTarget = false;
            AddFrame(swatch.gameObject, new Color(1f, 1f, 1f, 0.15f), new Color(0f, 0f, 0f, 0.16f));
            swatches[i] = image;
        }

        return swatches;
    }

    private static GameObject CreateSettingsOverlay(
        RectTransform safeRoot,
        out Button close,
        out Button sound,
        out Button haptics,
        out Button performance,
        out Button resetTutorial,
        out TMP_Text status)
    {
        RectTransform overlay = CreatePanel("SettingsOverlay", safeRoot, new Color(0f, 0f, 0f, 0.74f));
        Stretch(overlay, Vector2.zero, Vector2.zero);
        overlay.SetAsLastSibling();

        RectTransform panel = CreatePanel("SettingsPanel", overlay, Hex("#0B1123"));
        AddFrame(panel.gameObject, Hex("#24345E"), new Color(0f, 0f, 0f, 0.48f));
        SetRect(panel, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);

        TMP_Text title = CreateText("SettingsTitle", panel, "SETARI", 56, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.96f), Vector2.zero, Vector2.zero);
        title.color = Hex("#17E6FF");

        status = CreateText("SettingsStatus", panel, "Sunet on   Vibratii on   Efecte AUTO\nAUTO alege singur dupa telefon.", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(status.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
        status.color = Hex("#EAF1FF");

        sound = CreateButton("SettingsSoundButton", panel, "SUNET\nPORNIT", Hex("#10182E"), Hex("#A6FF42"));
        SetRect((RectTransform)sound.transform, new Vector2(0.10f, 0.58f), new Vector2(0.90f, 0.69f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(sound, 24);

        haptics = CreateButton("SettingsHapticsButton", panel, "VIBRATII\nPORNITE", Hex("#10182E"), Hex("#A6FF42"));
        SetRect((RectTransform)haptics.transform, new Vector2(0.10f, 0.45f), new Vector2(0.90f, 0.56f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(haptics, 24);

        performance = CreateButton("SettingsPerformanceButton", panel, "EFECTE\nAUTO", Hex("#10182E"), Hex("#17E6FF"));
        SetRect((RectTransform)performance.transform, new Vector2(0.10f, 0.32f), new Vector2(0.90f, 0.43f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(performance, 24);

        resetTutorial = CreateButton("SettingsResetTutorialButton", panel, "RESET TUTORIAL", Hex("#15213F"), Hex("#17E6FF"));
        SetRect((RectTransform)resetTutorial.transform, new Vector2(0.10f, 0.19f), new Vector2(0.90f, 0.30f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(resetTutorial, 24);

        close = CreateButton("SettingsCloseButton", panel, "INCHIDE", Hex("#10182E"), Hex("#EAF1FF"));
        SetRect((RectTransform)close.transform, new Vector2(0.10f, 0.06f), new Vector2(0.90f, 0.17f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(close, 25);

        overlay.gameObject.SetActive(false);
        return overlay.gameObject;
    }

    private static GameObject CreateShopOverlay(
        RectTransform safeRoot,
        out Button close,
        out Button removeAds,
        out Button cosmetics,
        out Button restore,
        out TMP_Text status)
    {
        RectTransform overlay = CreatePanel("ShopOverlay", safeRoot, new Color(0f, 0f, 0f, 0.74f));
        Stretch(overlay, Vector2.zero, Vector2.zero);
        overlay.SetAsLastSibling();

        RectTransform panel = CreatePanel("ShopPanel", overlay, Hex("#0B1123"));
        AddFrame(panel.gameObject, Hex("#24345E"), new Color(0f, 0f, 0f, 0.48f));
        SetRect(panel, new Vector2(0.07f, 0.14f), new Vector2(0.93f, 0.86f), Vector2.zero, Vector2.zero);

        TMP_Text title = CreateText("ShopTitle", panel, "MAGAZIN", 58, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.06f, 0.875f), new Vector2(0.94f, 0.965f), Vector2.zero, Vector2.zero);
        title.color = Hex("#17E6FF");

        status = CreateText("ShopStatus", panel, "PORTOFEL: 0 MONEDE   REALIZARI 0/8\nFARA RECLAME: NECUMPARAT   PACHET TEME: NECUMPARAT\nReclamele raman optionale: reward doar cand alegi tu.", 21, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(status.rectTransform, new Vector2(0.06f, 0.705f), new Vector2(0.94f, 0.865f), Vector2.zero, Vector2.zero);
        status.color = Hex("#EAF1FF");

        removeAds = CreateButton("ShopRemoveAdsButton", panel, "REMOVE ADS\nopreste interstitialele", Hex("#15213F"), Hex("#17E6FF"));
        SetRect((RectTransform)removeAds.transform, new Vector2(0.09f, 0.555f), new Vector2(0.91f, 0.685f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(removeAds, 24);

        cosmetics = CreateButton("ShopCosmeticsButton", panel, "PACHET TEME\npalete neon extra", Hex("#241545"), Hex("#FF4FD8"));
        SetRect((RectTransform)cosmetics.transform, new Vector2(0.09f, 0.395f), new Vector2(0.91f, 0.525f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(cosmetics, 24);

        restore = CreateButton("ShopRestoreButton", panel, "RESTORE\nrecupereaza cumparaturile", Hex("#10182E"), Hex("#FFD166"));
        SetRect((RectTransform)restore.transform, new Vector2(0.09f, 0.235f), new Vector2(0.91f, 0.365f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(restore, 23);

        close = CreateButton("ShopCloseButton", panel, "INCHIDE", Hex("#10182E"), Hex("#EAF1FF"));
        SetRect((RectTransform)close.transform, new Vector2(0.09f, 0.075f), new Vector2(0.91f, 0.185f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(close, 25);

        overlay.gameObject.SetActive(false);
        return overlay.gameObject;
    }

    private static void CreateGameScene(BoardCell cellPrefab, BlockView blockPrefab, PieceView piecePrefab, ParticleSystem[] particles)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        CreateEventSystem();
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.backgroundColor = Hex("#050711");
            camera.orthographic = true;
            camera.orthographicSize = 5f;
        }

        CameraShake shake = new GameObject("CameraShake").AddComponent<CameraShake>();
        RectTransform safeRoot = CreateCanvas("GameCanvas");
        Image background = safeRoot.gameObject.AddComponent<Image>();
        background.color = Hex("#070A14");
        safeRoot.gameObject.AddComponent<NeonBackdrop>();

        GameObject systems = new GameObject("GameSystems");
        GameManager gameManager = systems.AddComponent<GameManager>();
        ScoreManager scoreManager = systems.AddComponent<ScoreManager>();
        PieceSpawner pieceSpawner = systems.AddComponent<PieceSpawner>();

        TMP_Text modeText = CreateText("ModeText", safeRoot, "Classic", 26, FontStyles.Bold, TextAlignmentOptions.Left);
        SetFixedRect(modeText.rectTransform, 0.16f, 0.905f, new Vector2(250f, 50f));

        TMP_Text timerText = CreateText("TimerText", safeRoot, "90", 38, FontStyles.Bold, TextAlignmentOptions.Center);
        SetFixedRect(timerText.rectTransform, 0.50f, 0.945f, new Vector2(170f, 64f));
        timerText.color = Hex("#FFD166");

        Button menuButton = CreateButton("MenuButton", safeRoot, "MENIU", Hex("#10182E"), Hex("#17E6FF"));
        SetFixedRect((RectTransform)menuButton.transform, 0.64f, 0.905f, new Vector2(180f, 50f));

        Button muteButton = CreateButton("MuteButton", safeRoot, "SUNET", Hex("#10182E"), Hex("#EAF1FF"));
        SetFixedRect((RectTransform)muteButton.transform, 0.84f, 0.905f, new Vector2(180f, 50f));

        TMP_Text scoreText = CreateText("ScoreText", safeRoot, "0", 170, FontStyles.Bold, TextAlignmentOptions.Center);
        SetFixedRect(scoreText.rectTransform, 0.50f, 0.85f, new Vector2(780f, 190f));
        scoreText.color = Color.white;
        scoreText.fontSizeMin = 104f;
        scoreText.characterSpacing = -4f;
        AddTextGlow(scoreText.gameObject, Hex("#0A3C55"), new Vector2(0f, -3f));

        TMP_Text highScoreText = CreateText("HighScoreText", safeRoot, "Record 0   Bonus 0/1,000", 21, FontStyles.Bold, TextAlignmentOptions.Center);
        SetFixedRect(highScoreText.rectTransform, 0.50f, 0.810f, new Vector2(820f, 34f));

        TMP_Text chainText = CreateText("ChainText", safeRoot, "CHAIN -", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        SetFixedRect(chainText.rectTransform, 0.50f, 0.786f, new Vector2(420f, 38f));
        chainText.color = Hex("#FF4FD8");

        ChromaBarView[] bars = new ChromaBarView[GameConstants.ColorCount];
        for (int i = 0; i < GameConstants.ColorCount; i++)
        {
            RectTransform barRect = CreateChromaBar(safeRoot, (ChromaColor)i, out ChromaBarView bar);
            SetFixedRect(barRect, 0.185f + i * 0.21f, 0.762f, new Vector2(185f, 36f));
            bars[i] = bar;
        }

        TMP_Text missionText = CreateText("MissionText", safeRoot, "MISIUNE: CLEAR 0/6", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        SetFixedRect(missionText.rectTransform, 0.50f, 0.315f, new Vector2(700f, 38f));
        missionText.color = Hex("#EAF1FF");

        RectTransform boardRect = CreatePanel("Board", safeRoot, Color.clear);
        Image boardImage = boardRect.GetComponent<Image>();
        boardImage.enabled = false;
        boardImage.raycastTarget = false;
        SetFixedRect(boardRect, 0.50f, 0.50f, new Vector2(940f, 940f));
        RectTransform blockLayer = new GameObject("BlockLayer", typeof(RectTransform)).GetComponent<RectTransform>();
        blockLayer.SetParent(boardRect, false);
        Stretch(blockLayer, Vector2.zero, Vector2.zero);

        BoardManager boardManager = boardRect.gameObject.AddComponent<BoardManager>();
        SetObject(boardManager, "boardRoot", boardRect);
        SetObject(boardManager, "blockLayer", blockLayer);
        SetObject(boardManager, "cellPrefab", cellPrefab);
        SetObject(boardManager, "blockPrefab", blockPrefab);
        SetObject(boardManager, "cameraShake", shake);
        SetObjectArray(boardManager, "clearParticlesByColor", particles);

        RectTransform tray = CreatePanel("Tray", safeRoot, Color.clear);
        Image trayImage = tray.GetComponent<Image>();
        trayImage.enabled = false;
        trayImage.raycastTarget = false;
        SetFixedRect(tray, 0.50f, 0.15f, new Vector2(960f, 280f));

        TraySlot[] slots = new TraySlot[GameConstants.TraySize];
        for (int i = 0; i < GameConstants.TraySize; i++)
        {
            RectTransform slotRect = CreatePanel($"TraySlot_{i}", tray, Color.clear);
            Image slotImage = slotRect.GetComponent<Image>();
            slotImage.raycastTarget = false;
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = slotRect.anchorMin;
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = new Vector2((i - 1) * 310f, 0f);
            slotRect.sizeDelta = new Vector2(300f, 260f);
            slots[i] = slotRect.gameObject.AddComponent<TraySlot>();
            SetObject(slots[i], "pieceContainer", slotRect);
        }

        Button undoButton = CreateButton("UndoButton", safeRoot, "UNDO", Hex("#10182E"), Hex("#17E6FF"));
        SetFixedRect((RectTransform)undoButton.transform, 0.39f, 0.905f, new Vector2(150f, 50f));
        Button chromaRewardButton = CreateButton("RewardedChromaButton", safeRoot, "+CHROMA", Hex("#10182E"), Hex("#A6FF42"));
        SetFixedRect((RectTransform)chromaRewardButton.transform, 0.50f, -0.20f, new Vector2(300f, 66f));
        chromaRewardButton.gameObject.SetActive(false);

        RankProgressView rank = CreateRankProgress(safeRoot, new Vector2(0.68f, 0.048f), new Vector2(0.94f, 0.108f));
        rank.gameObject.SetActive(false);

        RectTransform dragLayer = new GameObject("DragLayer", typeof(RectTransform)).GetComponent<RectTransform>();
        dragLayer.SetParent(safeRoot, false);
        Stretch(dragLayer, Vector2.zero, Vector2.zero);
        dragLayer.SetAsLastSibling();

        RectTransform juiceLayerRect = new GameObject("JuicePopupLayer", typeof(RectTransform), typeof(JuicePopupLayer)).GetComponent<RectTransform>();
        juiceLayerRect.SetParent(safeRoot, false);
        Stretch(juiceLayerRect, Vector2.zero, Vector2.zero);
        juiceLayerRect.SetAsLastSibling();
        JuicePopupLayer juiceLayer = juiceLayerRect.GetComponent<JuicePopupLayer>();

        TMP_Text feedbackText = CreateText("FeedbackText", safeRoot, "NU INCAPE", 56, FontStyles.Bold, TextAlignmentOptions.Center);
        SetFixedRect(feedbackText.rectTransform, 0.50f, 0.532f, new Vector2(620f, 110f));
        feedbackText.color = Hex("#FF2E46");
        feedbackText.gameObject.SetActive(false);

        RectTransform flashRect = CreatePanel("ScreenFlash", safeRoot, new Color(1f, 1f, 1f, 0f));
        Stretch(flashRect, Vector2.zero, Vector2.zero);
        Image screenFlash = flashRect.GetComponent<Image>();
        screenFlash.raycastTarget = false;
        flashRect.gameObject.SetActive(false);

        GameObject pauseRoot = CreatePauseOverlay(
            safeRoot,
            out Button resumeButton,
            out Button pauseRestartButton,
            out Button pauseMenuButton,
            out Button pauseMuteButton,
            out Button pauseHapticsButton,
            out Button pauseResetTutorialButton);

        GameHUD hud = safeRoot.gameObject.AddComponent<GameHUD>();
        SetObject(hud, "modeText", modeText);
        SetObject(hud, "scoreText", scoreText);
        SetObject(hud, "highScoreText", highScoreText);
        SetObject(hud, "timerText", timerText);
        SetObject(hud, "chainText", chainText);
        SetObject(hud, "missionText", missionText);
        SetObject(hud, "feedbackText", feedbackText);
        SetObject(hud, "undoButton", undoButton);
        SetObject(hud, "menuButton", menuButton);
        SetObject(hud, "muteButton", muteButton);
        SetObject(hud, "muteButtonText", muteButton.GetComponentInChildren<TMP_Text>());
        SetObject(hud, "rewardedChromaButton", chromaRewardButton);
        SetObject(hud, "pauseRoot", pauseRoot);
        SetObject(hud, "resumeButton", resumeButton);
        SetObject(hud, "pauseRestartButton", pauseRestartButton);
        SetObject(hud, "pauseMenuButton", pauseMenuButton);
        SetObject(hud, "pauseMuteButton", pauseMuteButton);
        SetObject(hud, "pauseMuteButtonText", pauseMuteButton.GetComponentInChildren<TMP_Text>());
        SetObject(hud, "pauseHapticsButton", pauseHapticsButton);
        SetObject(hud, "pauseHapticsButtonText", pauseHapticsButton.GetComponentInChildren<TMP_Text>());
        SetObject(hud, "pauseResetTutorialButton", pauseResetTutorialButton);
        SetObjectArray(hud, "chromaBars", bars);
        SetObject(hud, "rankProgressView", rank);
        SetObject(hud, "dragLayer", dragLayer);
        SetObject(hud, "popupLayer", juiceLayer);
        SetObject(hud, "screenFlashImage", screenFlash);

        GameOverUI gameOver = CreateGameOver(safeRoot);
        TutorialOverlay tutorial = CreateTutorialOverlay(safeRoot);

        SetObjectArray(pieceSpawner, "traySlots", slots);
        SetObject(pieceSpawner, "piecePrefab", piecePrefab);
        SetObject(pieceSpawner, "pieceBlockPrefab", blockPrefab);
        SetObject(pieceSpawner, "dragLayer", dragLayer);

        SetObject(gameManager, "board", boardManager);
        SetObject(gameManager, "pieceSpawner", pieceSpawner);
        SetObject(gameManager, "scoreManager", scoreManager);
        SetObject(gameManager, "hud", hud);
        SetObject(gameManager, "gameOverUI", gameOver);
        SetObject(gameManager, "tutorialOverlay", tutorial);

        EditorSceneManager.SaveScene(scene, $"{SceneFolder}/Game.unity");
    }

    private static GameObject CreatePauseOverlay(
        RectTransform safeRoot,
        out Button resume,
        out Button restart,
        out Button menu,
        out Button mute,
        out Button haptics,
        out Button resetTutorial)
    {
        RectTransform overlay = CreatePanel("PauseOverlay", safeRoot, new Color(0f, 0f, 0f, 0.72f));
        Stretch(overlay, Vector2.zero, Vector2.zero);
        overlay.SetAsLastSibling();

        RectTransform panel = CreatePanel("PausePanel", overlay, Hex("#0B1123"));
        AddFrame(panel.gameObject, Hex("#24345E"), new Color(0f, 0f, 0f, 0.45f));
        SetRect(panel, new Vector2(0.10f, 0.18f), new Vector2(0.90f, 0.82f), Vector2.zero, Vector2.zero);

        TMP_Text title = CreateText("PauseTitle", panel, "PAUZA", 58, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.97f), Vector2.zero, Vector2.zero);
        title.color = Hex("#17E6FF");

        resume = CreateButton("ResumeButton", panel, "CONTINUA", Hex("#15213F"), Hex("#17E6FF"));
        SetRect((RectTransform)resume.transform, new Vector2(0.10f, 0.74f), new Vector2(0.90f, 0.83f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(resume, 26);

        restart = CreateButton("PauseRestartButton", panel, "RESTART", Hex("#241545"), Hex("#FF4FD8"));
        SetRect((RectTransform)restart.transform, new Vector2(0.10f, 0.62f), new Vector2(0.90f, 0.71f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(restart, 26);

        menu = CreateButton("PauseMenuButton", panel, "MENIU MODURI", Hex("#10182E"), Hex("#FFD166"));
        SetRect((RectTransform)menu.transform, new Vector2(0.10f, 0.50f), new Vector2(0.90f, 0.59f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(menu, 25);

        mute = CreateButton("PauseMuteButton", panel, "SUNET: ON", Hex("#10182E"), Hex("#EAF1FF"));
        SetRect((RectTransform)mute.transform, new Vector2(0.10f, 0.38f), new Vector2(0.90f, 0.47f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(mute, 24);

        haptics = CreateButton("PauseHapticsButton", panel, "VIBRATII: ON", Hex("#10182E"), Hex("#A6FF42"));
        SetRect((RectTransform)haptics.transform, new Vector2(0.10f, 0.26f), new Vector2(0.90f, 0.35f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(haptics, 24);

        resetTutorial = CreateButton("PauseResetTutorialButton", panel, "RESET TUTORIAL", Hex("#10182E"), Hex("#17E6FF"));
        SetRect((RectTransform)resetTutorial.transform, new Vector2(0.10f, 0.14f), new Vector2(0.90f, 0.23f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(resetTutorial, 24);

        overlay.gameObject.SetActive(false);
        return overlay.gameObject;
    }

    private static GameOverUI CreateGameOver(RectTransform safeRoot)
    {
        RectTransform overlay = CreatePanel("GameOverOverlay", safeRoot, new Color(0f, 0f, 0f, 0.78f));
        Stretch(overlay, Vector2.zero, Vector2.zero);
        overlay.SetAsLastSibling();

        RectTransform panel = CreatePanel("GameOverPanel", overlay, Hex("#0B1123"));
        AddFrame(panel.gameObject, Hex("#24345E"), new Color(0f, 0f, 0f, 0.48f));
        SetRect(panel, new Vector2(0.08f, 0.19f), new Vector2(0.92f, 0.81f), Vector2.zero, Vector2.zero);

        TMP_Text title = CreateText("GameOverTitle", panel, "JOC TERMINAT", 50, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.96f), Vector2.zero, Vector2.zero);
        title.color = Hex("#FF4FD8");

        TMP_Text scoreCaption = CreateText("ScoreCaption", panel, "SCOR", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(scoreCaption.rectTransform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.84f), Vector2.zero, Vector2.zero);
        scoreCaption.color = Hex("#EAF1FF");

        TMP_Text score = CreateText("FinalScore", panel, "0", 76, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(score.rectTransform, new Vector2(0.08f, 0.65f), new Vector2(0.92f, 0.79f), Vector2.zero, Vector2.zero);
        score.color = Hex("#17E6FF");
        AddTextGlow(score.gameObject, Hex("#0A3C55"), new Vector2(0f, -3f));

        TMP_Text high = CreateText("FinalHighScore", panel, "Record: 0", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(high.rectTransform, new Vector2(0.08f, 0.59f), new Vector2(0.92f, 0.65f), Vector2.zero, Vector2.zero);
        high.color = Hex("#FFD166");

        TMP_Text roundSummary = CreateText("RoundSummaryText", panel, "LINII 0   PURE 0   POP 0   CHAIN x0", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(roundSummary.rectTransform, new Vector2(0.06f, 0.545f), new Vector2(0.94f, 0.59f), Vector2.zero, Vector2.zero);
        roundSummary.color = Hex("#EAF1FF");

        TMP_Text xp = CreateText("XPText", panel, "+0 XP", 26, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(xp.rectTransform, new Vector2(0.08f, 0.49f), new Vector2(0.92f, 0.545f), Vector2.zero, Vector2.zero);
        xp.color = Hex("#A6FF42");

        TMP_Text rankHint = CreateText("RankHintText", panel, "1,500 XP pana la Ucenic", 21, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(rankHint.rectTransform, new Vector2(0.08f, 0.445f), new Vector2(0.92f, 0.49f), Vector2.zero, Vector2.zero);
        rankHint.color = Hex("#EAF1FF");

        RankProgressView rank = CreateRankProgress(panel, new Vector2(0.10f, 0.355f), new Vector2(0.90f, 0.44f));

        Button restart = CreateButton("RestartButton", panel, "JOACA DIN NOU", Hex("#2A164D"), Hex("#FF4FD8"));
        SetRect((RectTransform)restart.transform, new Vector2(0.10f, 0.255f), new Vector2(0.90f, 0.36f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(restart, 29);
        Button revive = CreateButton("ReviveButton", panel, "CONTINUA CU AD", Hex("#15213F"), Hex("#17E6FF"));
        SetRect((RectTransform)revive.transform, new Vector2(0.10f, 0.145f), new Vector2(0.90f, 0.235f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(revive, 25);
        Button menu = CreateButton("MenuButton", panel, "MENIU MODURI", Hex("#10182E"), Hex("#EAF1FF"));
        SetRect((RectTransform)menu.transform, new Vector2(0.10f, 0.045f), new Vector2(0.90f, 0.125f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(menu, 23);

        GameOverUI gameOver = overlay.gameObject.AddComponent<GameOverUI>();
        SetObject(gameOver, "root", overlay.gameObject);
        SetObject(gameOver, "panelTransform", panel);
        SetObject(gameOver, "titleText", title);
        SetObject(gameOver, "scoreText", score);
        SetObject(gameOver, "highScoreText", high);
        SetObject(gameOver, "roundSummaryText", roundSummary);
        SetObject(gameOver, "xpText", xp);
        SetObject(gameOver, "rankHintText", rankHint);
        SetObject(gameOver, "rankProgressView", rank);
        SetObject(gameOver, "restartButton", restart);
        SetObject(gameOver, "menuButton", menu);
        SetObject(gameOver, "reviveButton", revive);
        overlay.gameObject.SetActive(false);
        return gameOver;
    }

    private static TutorialOverlay CreateTutorialOverlay(RectTransform safeRoot)
    {
        RectTransform overlay = CreatePanel("TutorialOverlay", safeRoot, new Color(0f, 0f, 0f, 0.72f));
        Stretch(overlay, Vector2.zero, Vector2.zero);
        overlay.SetAsLastSibling();
        overlay.GetComponent<Image>().raycastTarget = false;

        RectTransform panel = CreatePanel("TutorialPanel", overlay, Hex("#0B1123"));
        AddFrame(panel.gameObject, Hex("#24345E"), new Color(0f, 0f, 0f, 0.48f));
        SetRect(panel, new Vector2(0.09f, 0.27f), new Vector2(0.91f, 0.73f), Vector2.zero, Vector2.zero);
        panel.GetComponent<Image>().raycastTarget = false;

        TMP_Text title = CreateText("TutorialTitle", panel, "CUM JOCI", 58, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.93f), Vector2.zero, Vector2.zero);
        title.color = Hex("#17E6FF");
        title.raycastTarget = false;

        TMP_Text body = CreateText("TutorialBody", panel, "Trage piesele luminoase pe tabla.\nPiesele stinse nu incap momentan.\nFa randuri sau coloane complete.\nCand vezi POP!, apasa culoarea ca sa o detonezi.", 27, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(body.rectTransform, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.72f), Vector2.zero, Vector2.zero);
        body.color = Hex("#EAF1FF");
        body.raycastTarget = false;

        Button close = CreateButton("TutorialCloseButton", panel, "AM INTELES", Hex("#15213F"), Hex("#A6FF42"));
        SetRect((RectTransform)close.transform, new Vector2(0.13f, 0.10f), new Vector2(0.87f, 0.25f), Vector2.zero, Vector2.zero);
        SetButtonTextSize(close, 30);

        TutorialOverlay tutorial = overlay.gameObject.AddComponent<TutorialOverlay>();
        SetObject(tutorial, "root", overlay.gameObject);
        SetObject(tutorial, "titleText", title);
        SetObject(tutorial, "bodyText", body);
        SetObject(tutorial, "closeButton", close);
        overlay.gameObject.SetActive(false);
        return tutorial;
    }

    private static RectTransform CreateChromaBar(RectTransform parent, ChromaColor color, out ChromaBarView view)
    {
        RectTransform root = CreatePanel($"ChromaBar_{color}", parent, Hex("#0D1428"));
        AddFrame(root.gameObject, Hex("#152646"), new Color(0f, 0f, 0f, 0.22f));
        view = root.gameObject.AddComponent<ChromaBarView>();

        Image swatch = new GameObject("Swatch", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        swatch.transform.SetParent(root, false);
        swatch.color = ChromaPalette.GetColor(color);
        SetRect((RectTransform)swatch.transform, new Vector2(0.05f, 0.22f), new Vector2(0.22f, 0.78f), Vector2.zero, Vector2.zero);

        Slider slider = CreateSlider("Slider", root, ChromaPalette.GetColor(color));
        SetRect((RectTransform)slider.transform, new Vector2(0.26f, 0.20f), new Vector2(0.62f, 0.40f), Vector2.zero, Vector2.zero);

        TMP_Text value = CreateText("ValueLabel", root, $"0/{GameConstants.ChromaThreshold}", 13, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(value.rectTransform, new Vector2(0.23f, 0.46f), new Vector2(0.64f, 0.96f), Vector2.zero, Vector2.zero);
        value.color = ChromaPalette.GetColor(color);

        Button pop = CreateButton("PopButton", root, "POP", Hex("#1A2544"), ChromaPalette.GetColor(color));
        SetRect((RectTransform)pop.transform, new Vector2(0.64f, 0.12f), new Vector2(0.97f, 0.88f), Vector2.zero, Vector2.zero);

        SetObject(view, "slider", slider);
        SetObject(view, "fillImage", slider.fillRect.GetComponent<Image>());
        SetObject(view, "popButton", pop);
        SetObject(view, "swatchImage", swatch);
        SetObject(view, "label", value);
        return root;
    }

    private static RankProgressView CreateRankProgress(RectTransform parent, Vector2 min, Vector2 max)
    {
        RectTransform root = CreatePanel("RankProgress", parent, Hex("#0D1428"));
        AddFrame(root.gameObject, Hex("#182A4D"), new Color(0f, 0f, 0f, 0.22f));
        SetRect(root, min, max, Vector2.zero, Vector2.zero);
        RankProgressView view = root.gameObject.AddComponent<RankProgressView>();

        TMP_Text rank = CreateText("RankLabel", root, "Novice", 18, FontStyles.Bold, TextAlignmentOptions.Left);
        SetRect(rank.rectTransform, new Vector2(0.03f, 0.45f), new Vector2(0.4f, 1f), Vector2.zero, Vector2.zero);

        TMP_Text progress = CreateText("RankProgressText", root, "0/1500", 14, FontStyles.Normal, TextAlignmentOptions.Right);
        SetRect(progress.rectTransform, new Vector2(0.45f, 0.45f), new Vector2(0.97f, 1f), Vector2.zero, Vector2.zero);

        Slider slider = CreateSlider("RankSlider", root, Hex("#FFD166"));
        SetRect((RectTransform)slider.transform, new Vector2(0.03f, 0.12f), new Vector2(0.97f, 0.36f), Vector2.zero, Vector2.zero);

        SetObject(view, "rankLabel", rank);
        SetObject(view, "progressLabel", progress);
        SetObject(view, "progressSlider", slider);
        return view;
    }

    private static RectTransform CreateCanvas(string name)
    {
        GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform safeRoot = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeArea)).GetComponent<RectTransform>();
        safeRoot.SetParent(canvasObject.transform, false);
        Stretch(safeRoot, Vector2.zero, Vector2.zero);
        return safeRoot;
    }

    private static void CreateEventSystem()
    {
        EventSystem existingEventSystem = Object.FindAnyObjectByType<EventSystem>();
        if (existingEventSystem != null)
        {
            existingEventSystem.pixelDragThreshold = 1;
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        eventSystem.GetComponent<EventSystem>().pixelDragThreshold = 1;
        string inputModuleName = "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";
        System.Type inputModule = System.Type.GetType(inputModuleName);
        if (inputModule != null)
        {
            eventSystem.AddComponent(inputModule);
        }
        else
        {
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }

    private static Button CreateButton(string name, Transform parent, string label, Color backgroundColor, Color accentColor)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.32f);
        outline.effectDistance = new Vector2(2f, -2f);
        Shadow shadow = buttonObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.38f);
        shadow.effectDistance = new Vector2(0f, -5f);

        Button button = buttonObject.GetComponent<Button>();
        buttonObject.AddComponent<UIButtonFeedback>();
        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = Color.Lerp(backgroundColor, accentColor, 0.25f);
        colors.pressedColor = accentColor;
        colors.selectedColor = backgroundColor;
        colors.disabledColor = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0.35f);
        button.colors = colors;

        TMP_Text text = CreateText("Label", (RectTransform)buttonObject.transform, label, 26, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
        text.color = accentColor;
        return button;
    }

    private static void SetButtonTextSize(Button button, float size)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text == null)
        {
            return;
        }

        text.fontSize = size;
        text.fontSizeMax = size;
        text.fontSizeMin = Mathf.Max(12f, size * 0.55f);
    }

    private static Slider CreateSlider(string name, Transform parent, Color fillColor)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        Slider slider = root.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;

        Image background = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        background.transform.SetParent(root.transform, false);
        background.color = Hex("#202A46");
        Stretch((RectTransform)background.transform, Vector2.zero, Vector2.zero);

        RectTransform fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(root.transform, false);
        Stretch(fillArea, Vector2.zero, Vector2.zero);

        Image fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(fillArea, false);
        fill.color = fillColor;
        Stretch((RectTransform)fill.transform, Vector2.zero, Vector2.zero);

        slider.fillRect = (RectTransform)fill.transform;
        slider.targetGraphic = fill;
        return slider;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text tmp = textObject.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = Hex("#EAF1FF");
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = size;
        tmp.fontSizeMin = Mathf.Max(12f, size * 0.55f);
        return tmp;
    }

    private static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        return (RectTransform)panel.transform;
    }

    private static Image CreateChildImage(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)child.transform;
        if (name == "Shine")
        {
            rect.anchorMin = new Vector2(0f, 0.58f);
            rect.anchorMax = new Vector2(1f, 0.94f);
            rect.offsetMin = new Vector2(7f, 0f);
            rect.offsetMax = new Vector2(-7f, 0f);
        }
        else
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void AddFrame(GameObject target, Color outlineColor, Color shadowColor)
    {
        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
        {
            outline = target.AddComponent<Outline>();
        }

        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        Shadow shadow = GetExactShadow(target);
        if (shadow == null)
        {
            shadow = target.AddComponent<Shadow>();
        }

        shadow.effectColor = shadowColor;
        shadow.effectDistance = new Vector2(0f, -6f);
    }

    private static void AddTextGlow(GameObject target, Color color, Vector2 distance)
    {
        Shadow shadow = GetExactShadow(target);
        if (shadow == null)
        {
            shadow = target.AddComponent<Shadow>();
        }

        shadow.effectColor = color;
        shadow.effectDistance = distance;
    }

    private static Shadow GetExactShadow(GameObject target)
    {
        Shadow[] shadows = target.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i].GetType() == typeof(Shadow))
            {
                return shadows[i];
            }
        }

        return null;
    }

    private static void EnsureFolders()
    {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory(SceneFolder);
        Directory.CreateDirectory(ArtFolder);
        AssetDatabase.Refresh();
    }

    private static T SavePrefab<T>(GameObject root, string path) where T : Component
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        return prefab.GetComponent<T>();
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetFixedRect(RectTransform rect, float centerX, float centerY, Vector2 size)
    {
        rect.anchorMin = new Vector2(centerX, centerY);
        rect.anchorMax = new Vector2(centerX, centerY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        SetRect(rect, Vector2.zero, Vector2.one, offsetMin, offsetMax);
    }

    private static void SetObject(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetObjectArray(Object target, string propertyName, Object[] values)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.arraySize = values == null ? 0 : values.Length;
        for (int i = 0; i < property.arraySize; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }
}
