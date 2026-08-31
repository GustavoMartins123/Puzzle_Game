using System;
using System.Collections.Generic;
using UnityEngine;

public enum PuzzleCutStyle
{
    Square,
    Round,
    Ellipse,
    Rectangle,
    Hexagon,
    Triangle,
    Diamond,
    Wave,
    Zigzag,
    DoubleLobe,
    Organic,
    Procedural,
    FullyRandom,
}

[Serializable]
public sealed class PuzzleImageDefinition
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private string resourcePath;
    [SerializeField, Min(0)] private int requiredUniqueCompletions;

    public string Id => id;

    public string DisplayName => displayName;

    public string ResourcePath => resourcePath;

    public int RequiredUniqueCompletions => requiredUniqueCompletions;

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "image id is empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = $"image '{id}' display name is empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(resourcePath) ||
            !resourcePath.StartsWith("PuzzleImages/", StringComparison.Ordinal))
        {
            error = $"image '{id}' must use a PuzzleImages resource path";
            return false;
        }

        if (requiredUniqueCompletions < 0)
        {
            error = $"image '{id}' has a negative unlock requirement";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class PuzzleCollectionDefinition
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, Min(0)] private int requiredUniqueCompletions;
    [SerializeField] private PuzzleImageDefinition[] images;

    public string Id => id;

    public string DisplayName => displayName;

    public int RequiredUniqueCompletions => requiredUniqueCompletions;

    public IReadOnlyList<PuzzleImageDefinition> Images => images;

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "collection id is empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = $"collection '{id}' display name is empty";
            return false;
        }

        if (requiredUniqueCompletions < 0)
        {
            error = $"collection '{id}' has a negative unlock requirement";
            return false;
        }

        if (images == null || images.Length == 0)
        {
            error = $"collection '{id}' has no images";
            return false;
        }

        for (int i = 0; i < images.Length; i++)
        {
            PuzzleImageDefinition image = images[i];
            if (image == null)
            {
                error = $"collection '{id}' image {i} is missing";
                return false;
            }

            if (!image.TryValidate(out error)) return false;
            if (image.RequiredUniqueCompletions < requiredUniqueCompletions)
            {
                error = $"image '{image.Id}' unlocks before collection '{id}'";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public struct PuzzleCutStyleUnlock
{
    public PuzzleCutStyle style;
    [Min(0)] public int requiredUniqueCompletions;
}

[CreateAssetMenu(fileName = "PuzzleConfig", menuName = "Puzzle/Puzzle Config")]
public class PuzzleConfig : ScriptableObject
{
    [Header("Difficulty")]
    [SerializeField] private PuzzleDifficultyProfile[] difficultyProfiles;
    [SerializeField] private PuzzleDifficulty defaultDifficulty = PuzzleDifficulty.Casual;

    [Header("Session Seeds")]
    [SerializeField] private bool useFixedCutSeed;
    [SerializeField] private int cutSeed = 1729;

    [Header("Progression")]
    [SerializeField] private string defaultImageId;
    [SerializeField] private string importCollectionId;
    [SerializeField] private PuzzleCollectionDefinition[] collections;
    [SerializeField] private PuzzleCutStyleUnlock[] cutStyleUnlocks;

    [Header("Achievements")]
    [SerializeField] private PuzzleAchievementDefinition[] achievements;

    public IReadOnlyList<PuzzleDifficultyProfile> DifficultyProfiles => difficultyProfiles;

    public PuzzleDifficultyProfile DefaultDifficulty => GetDifficulty(defaultDifficulty);

    public IReadOnlyList<PuzzleCollectionDefinition> Collections => collections;

    public string DefaultImageId => defaultImageId;

    public string ImportCollectionId => importCollectionId;

    public IReadOnlyList<PuzzleAchievementDefinition> Achievements => achievements;

    public int ImageCount
    {
        get
        {
            if (collections == null)
                throw new InvalidOperationException("Puzzle collections are not configured.");
            int count = 0;
            for (int i = 0; i < collections.Length; i++)
            {
                if (collections[i] == null || collections[i].Images == null)
                    throw new InvalidOperationException(
                        $"Puzzle collection {i} is invalid.");
                count += collections[i].Images.Count;
            }
            return count;
        }
    }

    public bool TryValidate(out string error)
    {
        if (difficultyProfiles == null || difficultyProfiles.Length == 0)
        {
            error = "at least one difficulty profile is required";
            return false;
        }

        var difficulties = new HashSet<PuzzleDifficulty>();
        bool hasDefaultDifficulty = false;
        for (int i = 0; i < difficultyProfiles.Length; i++)
        {
            PuzzleDifficultyProfile profile = difficultyProfiles[i];
            if (profile == null)
            {
                error = $"difficulty profile {i} is missing";
                return false;
            }

            if (!profile.TryValidate(out string profileError))
            {
                error = $"difficulty profile '{profile.name}' is invalid: {profileError}";
                return false;
            }

            if (!difficulties.Add(profile.Difficulty))
            {
                error = $"difficulty {profile.Difficulty} is duplicated";
                return false;
            }

            if (profile.Difficulty == defaultDifficulty) hasDefaultDifficulty = true;
        }

        if (!hasDefaultDifficulty)
        {
            error = $"default difficulty {defaultDifficulty} is missing";
            return false;
        }

        if (GetConfiguredDifficultyRequirement(defaultDifficulty) != 0)
        {
            error = "default difficulty must be unlocked for a new profile";
            return false;
        }

        if (useFixedCutSeed && cutSeed <= 0)
        {
            error = "fixed cut seed must be positive";
            return false;
        }

        if (collections == null || collections.Length == 0)
        {
            error = "at least one image collection is required";
            return false;
        }

        var collectionIds = new HashSet<string>(StringComparer.Ordinal);
        var imageIds = new HashSet<string>(StringComparer.Ordinal);
        var resourcePaths = new HashSet<string>(StringComparer.Ordinal);
        int initiallyUnlockedImages = 0;
        bool hasDefaultImage = false;
        bool hasImportCollection = false;
        for (int collectionIndex = 0; collectionIndex < collections.Length; collectionIndex++)
        {
            PuzzleCollectionDefinition collection = collections[collectionIndex];
            if (collection == null)
            {
                error = $"collection {collectionIndex} is missing";
                return false;
            }

            if (!collection.TryValidate(out error)) return false;
            if (!collectionIds.Add(collection.Id))
            {
                error = $"collection id '{collection.Id}' is duplicated";
                return false;
            }
            if (collection.Id == importCollectionId) hasImportCollection = true;

            for (int imageIndex = 0; imageIndex < collection.Images.Count; imageIndex++)
            {
                PuzzleImageDefinition image = collection.Images[imageIndex];
                if (!imageIds.Add(image.Id))
                {
                    error = $"image id '{image.Id}' is duplicated";
                    return false;
                }

                if (!resourcePaths.Add(image.ResourcePath))
                {
                    error = $"image resource path '{image.ResourcePath}' is duplicated";
                    return false;
                }

                if (collection.RequiredUniqueCompletions == 0 &&
                    image.RequiredUniqueCompletions == 0)
                    initiallyUnlockedImages++;
                if (image.Id == defaultImageId)
                {
                    hasDefaultImage = true;
                    if (collection.RequiredUniqueCompletions != 0 ||
                        image.RequiredUniqueCompletions != 0)
                    {
                        error = "default image must be unlocked for a new profile";
                        return false;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(defaultImageId) || !hasDefaultImage)
        {
            error = $"default image '{defaultImageId}' is not configured";
            return false;
        }

        if (string.IsNullOrWhiteSpace(importCollectionId) || !hasImportCollection)
        {
            error = $"import collection '{importCollectionId}' is not configured";
            return false;
        }

        if (initiallyUnlockedImages < 2)
        {
            error = "at least two images must be initially unlocked so Retry can rotate images";
            return false;
        }

        int styleCount = Enum.GetValues(typeof(PuzzleCutStyle)).Length;
        if (cutStyleUnlocks == null || cutStyleUnlocks.Length != styleCount)
        {
            error = $"exactly {styleCount} cut style unlock rules are required";
            return false;
        }

        var unlockStyles = new HashSet<PuzzleCutStyle>();
        for (int i = 0; i < cutStyleUnlocks.Length; i++)
        {
            PuzzleCutStyleUnlock rule = cutStyleUnlocks[i];
            if (!Enum.IsDefined(typeof(PuzzleCutStyle), rule.style))
            {
                error = $"cut style unlock rule {i} is invalid";
                return false;
            }

            if (rule.requiredUniqueCompletions < 0)
            {
                error = $"cut style {rule.style} has a negative unlock requirement";
                return false;
            }

            if (!unlockStyles.Add(rule.style))
            {
                error = $"cut style {rule.style} has duplicate unlock rules";
                return false;
            }
        }

        PuzzleDifficultyProfile defaultProfile = null;
        for (int i = 0; i < difficultyProfiles.Length; i++)
        {
            if (GetStyleRequirement(difficultyProfiles[i].DefaultCutStyle) >
                difficultyProfiles[i].RequiredUniqueCompletions)
            {
                error = $"default cut style for {difficultyProfiles[i].DisplayName} unlocks after the difficulty";
                return false;
            }
            if (difficultyProfiles[i].Difficulty == defaultDifficulty)
                defaultProfile = difficultyProfiles[i];
        }
        if (defaultProfile == null ||
            GetStyleRequirement(defaultProfile.DefaultCutStyle) != 0)
        {
            error = "default cut style must be unlocked for a new profile";
            return false;
        }

        if (achievements == null || achievements.Length == 0)
        {
            error = "at least one achievement definition is required";
            return false;
        }
        var achievementIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < achievements.Length; i++)
        {
            PuzzleAchievementDefinition achievement = achievements[i];
            if (achievement == null)
            {
                error = $"achievement definition {i} is missing";
                return false;
            }
            if (!achievement.TryValidate(out error)) return false;
            if (!achievementIds.Add(achievement.Id))
            {
                error = $"achievement id '{achievement.Id}' is duplicated";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public PuzzleDifficultyProfile GetDifficulty(PuzzleDifficulty difficulty)
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid PuzzleConfig: {error}.");

        for (int i = 0; i < difficultyProfiles.Length; i++)
            if (difficultyProfiles[i].Difficulty == difficulty) return difficultyProfiles[i];

        throw new InvalidOperationException($"Difficulty {difficulty} is not configured.");
    }

    public PuzzleImageDefinition GetImage(string imageId)
    {
        if (string.IsNullOrWhiteSpace(imageId))
            throw new ArgumentException("Image id is required.", nameof(imageId));
        if (collections == null)
            throw new InvalidOperationException("Puzzle collections are not configured.");

        for (int collectionIndex = 0; collectionIndex < collections.Length; collectionIndex++)
        {
            PuzzleCollectionDefinition collection = collections[collectionIndex];
            if (collection == null || collection.Images == null)
                throw new InvalidOperationException(
                    $"Puzzle collection {collectionIndex} is invalid.");
            for (int imageIndex = 0; imageIndex < collection.Images.Count; imageIndex++)
                if (collection.Images[imageIndex].Id == imageId)
                    return collection.Images[imageIndex];
        }

        throw new InvalidOperationException($"Puzzle image id '{imageId}' is not configured.");
    }

    public PuzzleAchievementDefinition GetAchievement(string achievementId)
    {
        if (string.IsNullOrWhiteSpace(achievementId))
            throw new ArgumentException("Achievement id is required.", nameof(achievementId));
        if (achievements == null)
            throw new InvalidOperationException("Puzzle achievements are not configured.");

        for (int i = 0; i < achievements.Length; i++)
        {
            PuzzleAchievementDefinition definition = achievements[i] ??
                throw new InvalidOperationException($"Puzzle achievement {i} is invalid.");
            if (definition.Id == achievementId) return definition;
        }

        throw new InvalidOperationException(
            $"Puzzle achievement id '{achievementId}' is not configured.");
    }

    public PuzzleCollectionDefinition GetCollectionForImage(string imageId)
    {
        if (string.IsNullOrWhiteSpace(imageId))
            throw new ArgumentException("Image id is required.", nameof(imageId));
        if (collections == null)
            throw new InvalidOperationException("Puzzle collections are not configured.");

        for (int collectionIndex = 0; collectionIndex < collections.Length; collectionIndex++)
        {
            PuzzleCollectionDefinition collection = collections[collectionIndex];
            if (collection == null || collection.Images == null)
                throw new InvalidOperationException(
                    $"Puzzle collection {collectionIndex} is invalid.");
            for (int imageIndex = 0; imageIndex < collection.Images.Count; imageIndex++)
                if (collection.Images[imageIndex].Id == imageId) return collection;
        }

        throw new InvalidOperationException($"Puzzle image id '{imageId}' is not configured.");
    }

    public Texture2D LoadImage(PuzzleImageDefinition image)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        PuzzleImageDefinition configuredImage = GetImage(image.Id);
        if (!ReferenceEquals(configuredImage, image))
            throw new InvalidOperationException(
                $"Image definition '{image.Id}' does not belong to this configuration.");

        Texture2D texture = Resources.Load<Texture2D>(image.ResourcePath);
        if (texture == null)
            throw new InvalidOperationException(
                $"Puzzle image '{image.ResourcePath}' could not be loaded.");
        return texture;
    }

    public PuzzleImageDefinition PickDifferentUnlockedImage(
        string currentImageId,
        PuzzleProgressData progress)
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid PuzzleConfig: {error}.");
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        progress.Validate(this);
        if (!progress.IsImageUnlocked(currentImageId))
            throw new InvalidOperationException(
                $"Current puzzle image '{currentImageId}' is not unlocked.");

        var unlocked = new List<PuzzleImageDefinition>();
        for (int collectionIndex = 0; collectionIndex < collections.Length; collectionIndex++)
            for (int imageIndex = 0; imageIndex < collections[collectionIndex].Images.Count; imageIndex++)
            {
                PuzzleImageDefinition image = collections[collectionIndex].Images[imageIndex];
                if (progress.IsImageUnlocked(image.Id)) unlocked.Add(image);
            }

        if (unlocked.Count < 2)
            throw new InvalidOperationException("Retry requires at least two unlocked images.");

        int currentIndex = unlocked.FindIndex(image => image.Id == currentImageId);
        if (currentIndex < 0)
            throw new InvalidOperationException(
                $"Current puzzle image '{currentImageId}' is absent from the unlocked library.");

        int offset = UnityEngine.Random.Range(1, unlocked.Count);
        return unlocked[(currentIndex + offset) % unlocked.Count];
    }

    public int GetStyleRequirement(PuzzleCutStyle style)
    {
        if (!Enum.IsDefined(typeof(PuzzleCutStyle), style))
            throw new ArgumentOutOfRangeException(nameof(style));
        if (cutStyleUnlocks == null)
            throw new InvalidOperationException("Cut style unlock rules are not configured.");

        for (int i = 0; i < cutStyleUnlocks.Length; i++)
            if (cutStyleUnlocks[i].style == style)
                return cutStyleUnlocks[i].requiredUniqueCompletions;

        throw new InvalidOperationException($"Cut style {style} has no unlock rule.");
    }

    public bool IsStyleUnlocked(PuzzleCutStyle style, int uniqueCompletions)
    {
        if (uniqueCompletions < 0)
            throw new ArgumentOutOfRangeException(nameof(uniqueCompletions));
        return uniqueCompletions >= GetStyleRequirement(style);
    }

    public bool IsCollectionUnlocked(
        PuzzleCollectionDefinition collection,
        int uniqueCompletions)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        if (uniqueCompletions < 0)
            throw new ArgumentOutOfRangeException(nameof(uniqueCompletions));

        bool configured = false;
        for (int i = 0; i < collections.Length; i++)
            if (ReferenceEquals(collections[i], collection)) configured = true;
        if (!configured)
            throw new InvalidOperationException(
                $"Collection '{collection.Id}' does not belong to this configuration.");
        return uniqueCompletions >= collection.RequiredUniqueCompletions;
    }

    public int CreateCutSeed() =>
        useFixedCutSeed ? cutSeed : UnityEngine.Random.Range(1, int.MaxValue);

    public int CreateTraySeed() => UnityEngine.Random.Range(1, int.MaxValue);

    private int GetConfiguredDifficultyRequirement(PuzzleDifficulty difficulty)
    {
        for (int i = 0; i < difficultyProfiles.Length; i++)
            if (difficultyProfiles[i] != null && difficultyProfiles[i].Difficulty == difficulty)
                return difficultyProfiles[i].RequiredUniqueCompletions;
        throw new InvalidOperationException($"Difficulty {difficulty} is not configured.");
    }
}
