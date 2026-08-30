using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PuzzleImageLibrary
{
    public const string Folder = "Assets/Resources/PuzzleImages";
    public const int MaxTextureSize = 2048;
    public const int CompressionQuality = 85;

    private const string ResourcesRoot = "Assets/Resources/";

    public static bool IsManaged(string assetPath) =>
        !string.IsNullOrEmpty(assetPath) &&
        assetPath.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase);

    public static void Apply(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 1;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = MaxTextureSize;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.crunchedCompression = true;
        importer.compressionQuality = CompressionQuality;
    }

    public static bool NeedsReimport(TextureImporter importer) =>
        importer.textureType != TextureImporterType.Sprite ||
        importer.spriteImportMode != SpriteImportMode.Single ||
        importer.npotScale != TextureImporterNPOTScale.None ||
        importer.mipmapEnabled ||
        importer.isReadable ||
        importer.wrapMode != TextureWrapMode.Clamp ||
        importer.maxTextureSize != MaxTextureSize ||
        importer.textureCompression != TextureImporterCompression.Compressed ||
        !importer.crunchedCompression ||
        importer.compressionQuality != CompressionQuality;

    public static int NormalizeImportSettings()
    {
        string[] paths = FindTexturePaths();
        int changed = 0;

        for (int i = 0; i < paths.Length; i++)
        {
            if (AssetImporter.GetAtPath(paths[i]) is not TextureImporter importer) continue;
            if (!NeedsReimport(importer)) continue;

            Apply(importer);
            importer.SaveAndReimport();
            changed++;
        }

        return changed;
    }

    public static bool SyncConfigs()
    {
        string[] resourcePaths = BuildResourcePaths();
        bool anyChanged = false;

        foreach (string guid in AssetDatabase.FindAssets("t:PuzzleConfig"))
        {
            var config = AssetDatabase.LoadAssetAtPath<PuzzleConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (config == null) continue;
            anyChanged |= Write(config, resourcePaths);
        }

        if (anyChanged) AssetDatabase.SaveAssets();
        return anyChanged;
    }

    private static string[] FindTexturePaths()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) return Array.Empty<string>();

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Folder });
        var paths = new string[guids.Length];
        for (int i = 0; i < guids.Length; i++) paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
        Array.Sort(paths, StringComparer.Ordinal);
        return paths;
    }

    private static string[] BuildResourcePaths()
    {
        string[] assetPaths = FindTexturePaths();
        var resourcePaths = new List<string>(assetPaths.Length);

        for (int i = 0; i < assetPaths.Length; i++)
        {
            string path = assetPaths[i];
            if (!path.StartsWith(ResourcesRoot, StringComparison.OrdinalIgnoreCase)) continue;

            int dot = path.LastIndexOf('.');
            if (dot < ResourcesRoot.Length) continue;

            resourcePaths.Add(path.Substring(ResourcesRoot.Length, dot - ResourcesRoot.Length));
        }

        return resourcePaths.ToArray();
    }

    private static bool Write(PuzzleConfig config, string[] resourcePaths)
    {
        var serialized = new SerializedObject(config);
        SerializedProperty array = serialized.FindProperty("imagePaths");
        if (array == null) return false;

        if (array.arraySize == resourcePaths.Length)
        {
            bool identical = true;
            for (int i = 0; i < resourcePaths.Length && identical; i++)
                identical = array.GetArrayElementAtIndex(i).stringValue == resourcePaths[i];

            if (identical) return false;
        }

        array.arraySize = resourcePaths.Length;
        for (int i = 0; i < resourcePaths.Length; i++)
            array.GetArrayElementAtIndex(i).stringValue = resourcePaths[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return true;
    }
}
