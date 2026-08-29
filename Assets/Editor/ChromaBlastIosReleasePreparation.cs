using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class ChromaBlastIosReleasePreparation
{
    private const string BundleIdentifier = "com.ardagames.chromablast";
    private const string ProductName = "Chroma Blast";
    private const string VersionName = "0.1.0";
    private const string BuildNumber = "1";
    private const string MinimumIosVersion = "15.0";
    private const string ApprovedAppIconPath = "Assets/Art/AppIcon.png";

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
}
