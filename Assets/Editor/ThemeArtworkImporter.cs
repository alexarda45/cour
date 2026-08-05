using UnityEditor;
using UnityEngine;

public sealed class ThemeArtworkImporter : AssetPostprocessor
{
    private const string ThemeArtworkRoot = "Assets/Resources/Themes/Artwork/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ThemeArtworkRoot, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TextureImporter importer = assetImporter as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
