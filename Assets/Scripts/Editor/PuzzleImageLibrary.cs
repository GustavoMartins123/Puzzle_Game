using System;
using System.Collections.Generic;
using System.Globalization;
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
            if (AssetImporter.GetAtPath(paths[i]) is not TextureImporter importer)
                throw new InvalidOperationException(
                    $"Managed puzzle image '{paths[i]}' has no TextureImporter.");
            if (!NeedsReimport(importer)) continue;

            Apply(importer);
            importer.SaveAndReimport();
            changed++;
        }

        return changed;
    }

    public static int SynchronizeConfigs()
    {
        int added = 0;
        string[] configGuids = AssetDatabase.FindAssets("t:PuzzleConfig");
        if (configGuids.Length == 0)
            throw new InvalidOperationException("No PuzzleConfig asset exists.");
        foreach (string guid in configGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<PuzzleConfig>(assetPath);
            if (config == null)
                throw new InvalidOperationException(
                    $"PuzzleConfig asset '{assetPath}' could not be loaded.");
            added = checked(added + SynchronizeConfig(config));
        }

        if (added > 0) AssetDatabase.SaveAssets();
        return added;
    }

    public static int SynchronizeConfig(PuzzleConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (!config.TryValidate(out string configError))
            throw new InvalidOperationException(
                $"PuzzleConfig '{config.name}' is invalid: {configError}.");

        string[] folderPaths = BuildResourcePaths();
        var configuredPaths = new HashSet<string>(StringComparer.Ordinal);
        var configuredIds = new HashSet<string>(StringComparer.Ordinal);
        for (int collectionIndex = 0; collectionIndex < config.Collections.Count; collectionIndex++)
            for (int imageIndex = 0; imageIndex < config.Collections[collectionIndex].Images.Count; imageIndex++)
            {
                PuzzleImageDefinition image = config.Collections[collectionIndex].Images[imageIndex];
                configuredPaths.Add(image.ResourcePath);
                configuredIds.Add(image.Id);
            }

        var serializedConfig = new SerializedObject(config);
        SerializedProperty collections = serializedConfig.FindProperty("collections");
        if (collections == null)
            throw new InvalidOperationException("PuzzleConfig collections property is missing.");

        SerializedProperty importImages = null;
        int maximumRequirement = -1;
        for (int collectionIndex = 0; collectionIndex < collections.arraySize; collectionIndex++)
        {
            SerializedProperty collection = collections.GetArrayElementAtIndex(collectionIndex);
            if (collection.FindPropertyRelative("id").stringValue != config.ImportCollectionId)
                continue;
            importImages = collection.FindPropertyRelative("images");
            maximumRequirement = collection
                .FindPropertyRelative("requiredUniqueCompletions")
                .intValue;
            for (int imageIndex = 0; imageIndex < importImages.arraySize; imageIndex++)
            {
                int requirement = importImages.GetArrayElementAtIndex(imageIndex)
                    .FindPropertyRelative("requiredUniqueCompletions")
                    .intValue;
                maximumRequirement = Math.Max(maximumRequirement, requirement);
            }
            break;
        }

        if (importImages == null)
            throw new InvalidOperationException(
                $"Import collection '{config.ImportCollectionId}' was not found.");

        int added = 0;
        for (int i = 0; i < folderPaths.Length; i++)
        {
            string resourcePath = folderPaths[i];
            if (configuredPaths.Contains(resourcePath)) continue;

            ParseCanonicalName(resourcePath, out string imageId, out string displayName);
            if (!configuredIds.Add(imageId))
                throw new InvalidOperationException(
                    $"New resource '{resourcePath}' produces duplicate id '{imageId}'.");

            maximumRequirement = checked(maximumRequirement + 1);
            int newIndex = importImages.arraySize;
            importImages.arraySize = newIndex + 1;
            SerializedProperty image = importImages.GetArrayElementAtIndex(newIndex);
            image.FindPropertyRelative("id").stringValue = imageId;
            image.FindPropertyRelative("displayName").stringValue = displayName;
            image.FindPropertyRelative("resourcePath").stringValue = resourcePath;
            image.FindPropertyRelative("requiredUniqueCompletions").intValue =
                maximumRequirement;
            configuredPaths.Add(resourcePath);
            added++;
        }

        if (added == 0) return 0;
        serializedConfig.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        if (!config.TryValidate(out string synchronizedError))
            throw new InvalidOperationException(
                $"Synchronized PuzzleConfig '{config.name}' is invalid: {synchronizedError}.");
        return added;
    }

    public static void ValidateConfigs()
    {
        string[] configGuids = AssetDatabase.FindAssets("t:PuzzleConfig");
        if (configGuids.Length == 0)
            throw new InvalidOperationException("No PuzzleConfig asset exists.");
        foreach (string guid in configGuids)
        {
            var config = AssetDatabase.LoadAssetAtPath<PuzzleConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (config == null)
                throw new InvalidOperationException($"PuzzleConfig asset '{guid}' could not be loaded.");
            if (!TryValidateAssignments(config, out string error))
                throw new InvalidOperationException(
                    $"PuzzleConfig '{config.name}' image assignments are invalid: {error}.");
        }
    }

    public static bool TryValidateAssignments(PuzzleConfig config, out string error)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (!config.TryValidate(out error)) return false;

        string[] folderPaths = BuildResourcePaths();
        var configuredPaths = new HashSet<string>(StringComparer.Ordinal);
        for (int collectionIndex = 0; collectionIndex < config.Collections.Count; collectionIndex++)
            for (int imageIndex = 0; imageIndex < config.Collections[collectionIndex].Images.Count; imageIndex++)
                configuredPaths.Add(
                    config.Collections[collectionIndex].Images[imageIndex].ResourcePath);

        if (folderPaths.Length != configuredPaths.Count)
        {
            error = $"folder contains {folderPaths.Length} images but collections define " +
                    $"{configuredPaths.Count}";
            return false;
        }

        for (int i = 0; i < folderPaths.Length; i++)
            if (!configuredPaths.Contains(folderPaths[i]))
            {
                error = $"resource '{folderPaths[i]}' has no explicit collection assignment";
                return false;
            }

        error = string.Empty;
        return true;
    }

    private static string[] FindTexturePaths()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            throw new InvalidOperationException(
                $"Puzzle image folder '{Folder}' does not exist.");

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
            if (!path.StartsWith(ResourcesRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Managed puzzle image '{path}' is outside Resources.");

            int dot = path.LastIndexOf('.');
            if (dot < ResourcesRoot.Length)
                throw new InvalidOperationException(
                    $"Managed puzzle image '{path}' has no file extension.");

            resourcePaths.Add(path.Substring(ResourcesRoot.Length, dot - ResourcesRoot.Length));
        }

        return resourcePaths.ToArray();
    }

    private static void ParseCanonicalName(
        string resourcePath,
        out string imageId,
        out string displayName)
    {
        int slash = resourcePath.LastIndexOf('/');
        string fileName = slash >= 0 ? resourcePath.Substring(slash + 1) : resourcePath;
        int separator = fileName.IndexOf('_');
        if (separator <= 0 || separator == fileName.Length - 1 ||
            !int.TryParse(
                fileName.Substring(0, separator),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int numericId) ||
            numericId <= 0)
            throw new InvalidOperationException(
                $"Puzzle image '{resourcePath}' must start with a positive numeric id and underscore.");

        imageId = "img_" + numericId.ToString("D2", CultureInfo.InvariantCulture);
        string words = fileName.Substring(separator + 1).Replace('_', ' ');
        displayName = CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(words);
    }

}
