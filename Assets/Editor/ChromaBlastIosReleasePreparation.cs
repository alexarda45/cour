using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
#if UNITY_IOS
using System.IO;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

public static class ChromaBlastIosReleasePreparation
{
    private const string BundleIdentifier = "com.ardagames.chromablast";
    private const string ProductName = "Chroma Blast";
    private const string VersionName = "0.1.0";
    private const string BuildNumber = "1";
    private const string MinimumIosVersion = "15.0";
    private const string ApprovedAppIconPath = "Assets/Art/AppIcon.png";
    private const string IosAdMobApplicationId =
        "ca-app-pub-4005517283749109~1562398609";
    private const string TrackingUsageDescription =
        "We use device identifiers to provide and measure ads and help keep Chroma Blast free.";

    [MenuItem("Chroma Blast/Prepare iOS Release - Phase 1")]
    public static void ApplyPhaseOne()
    {
        PlayerSettings.productName = ProductName;
        PlayerSettings.bundleVersion = VersionName;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleIdentifier);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);

        PlayerSettings.iOS.buildNumber = BuildNumber;
        PlayerSettings.iOS.targetOSVersionString = MinimumIosVersion;
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        AssignApprovedIosApplicationIcon();
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Chroma Blast iOS Phase 1 settings applied: iPhone-only, iOS 15.0, IL2CPP, device SDK, "
            + "and approved app icon. Signing and platform ad IDs remain intentionally unconfigured.");
    }

    private static void AssignApprovedIosApplicationIcon()
    {
        Texture2D approvedIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(ApprovedAppIconPath);
        if (approvedIcon == null)
        {
            throw new MissingReferenceException($"Approved app icon was not found at {ApprovedAppIconPath}.");
        }

        int[] iconSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.iOS, IconKind.Application);
        if (iconSizes == null || iconSizes.Length == 0)
        {
            throw new System.InvalidOperationException("Unity did not expose any iOS application icon slots.");
        }

        Texture2D[] icons = new Texture2D[iconSizes.Length];
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i] = approvedIcon;
        }

        PlayerSettings.SetIcons(NamedBuildTarget.iOS, icons, IconKind.Application);
    }

#if UNITY_IOS
    [PostProcessBuild(1000)]
    private static void ConfigureIosPrivacy(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string plistPath = Path.Combine(buildPath, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        // PlistDocument stores dictionary keys uniquely; SetString replaces any earlier
        // value instead of appending a duplicate GADApplicationIdentifier entry.
        plist.root.SetString("GADApplicationIdentifier", IosAdMobApplicationId);
        plist.root.SetString("NSUserTrackingUsageDescription", TrackingUsageDescription);
        File.WriteAllText(plistPath, plist.WriteToString());

        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);
        string mainTargetGuid = project.GetUnityMainTargetGuid();
        project.AddFrameworkToProject(mainTargetGuid, "AppTrackingTransparency.framework", false);
        project.WriteToFile(projectPath);
    }
#endif
}
