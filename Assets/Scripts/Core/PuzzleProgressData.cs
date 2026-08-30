using System;
using System.Collections.Generic;
using UnityEngine;

public enum PuzzleMedal
{
    Bronze = 1,
    Silver = 2,
    Gold = 3,
}

public static class PuzzleMedalCalculator
{
    public static PuzzleMedal Calculate(PuzzleSessionMetrics metrics)
    {
        if (metrics == null || !metrics.IsActive || !metrics.IsComplete)
            throw new ArgumentException("Completed session metrics are required.", nameof(metrics));

        if (metrics.IncorrectAttempts == 0 && metrics.HintsUsed == 0 &&
            metrics.ElapsedSeconds <= metrics.TotalPieces * 20f)
            return PuzzleMedal.Gold;

        int silverErrorLimit = Math.Max(1, metrics.TotalPieces / 5);
        if (metrics.IncorrectAttempts <= silverErrorLimit && metrics.HintsUsed <= 1 &&
            metrics.ElapsedSeconds <= metrics.TotalPieces * 45f)
            return PuzzleMedal.Silver;

        return PuzzleMedal.Bronze;
    }

    public static string Label(PuzzleMedal medal) => medal switch
    {
        PuzzleMedal.Bronze => "BRONZE",
        PuzzleMedal.Silver => "PRATA",
        PuzzleMedal.Gold => "OURO",
        _ => throw new ArgumentOutOfRangeException(nameof(medal)),
    };
}

[Serializable]
public sealed class PuzzleBestResultRecord
{
    [SerializeField] private string imageId;
    [SerializeField] private PuzzleDifficulty difficulty;
    [SerializeField] private int bestScore;
    [SerializeField] private float bestSeconds;
    [SerializeField] private PuzzleMedal bestMedal;
    [SerializeField] private int completions;

    public PuzzleBestResultRecord(
        string imageId,
        PuzzleDifficulty difficulty,
        int score,
        float seconds,
        PuzzleMedal medal)
    {
        this.imageId = imageId;
        this.difficulty = difficulty;
        bestScore = score;
        bestSeconds = seconds;
        bestMedal = medal;
        completions = 1;
    }

    public string ImageId => imageId;

    public PuzzleDifficulty Difficulty => difficulty;

    public int BestScore => bestScore;

    public float BestSeconds => bestSeconds;

    public PuzzleMedal BestMedal => bestMedal;

    public int Completions => completions;

    public static PuzzleBestResultRecord FromPersisted(
        string imageId,
        PuzzleDifficulty difficulty,
        int bestScore,
        float bestSeconds,
        PuzzleMedal bestMedal,
        int completions)
    {
        if (string.IsNullOrWhiteSpace(imageId))
            throw new ArgumentException("Image id is required.", nameof(imageId));
        if (!Enum.IsDefined(typeof(PuzzleDifficulty), difficulty))
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        if (!float.IsFinite(bestSeconds) || bestSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(bestSeconds));
        if (!Enum.IsDefined(typeof(PuzzleMedal), bestMedal))
            throw new ArgumentOutOfRangeException(nameof(bestMedal));
        if (completions <= 0)
            throw new ArgumentOutOfRangeException(nameof(completions));

        var record = new PuzzleBestResultRecord(imageId, difficulty, bestScore, bestSeconds, bestMedal);
        record.completions = completions;
        return record;
    }

    public bool Record(int score, float seconds, PuzzleMedal medal)
    {
        if (!float.IsFinite(seconds) || seconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!Enum.IsDefined(typeof(PuzzleMedal), medal))
            throw new ArgumentOutOfRangeException(nameof(medal));

        bool newBestScore = score > bestScore;
        if (newBestScore) bestScore = score;
        if (seconds < bestSeconds) bestSeconds = seconds;
        if (medal > bestMedal) bestMedal = medal;
        completions = checked(completions + 1);
        return newBestScore;
    }

    public void Validate(PuzzleConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        config.GetImage(imageId);
        if (!Enum.IsDefined(typeof(PuzzleDifficulty), difficulty))
            throw new InvalidOperationException(
                $"Progress result for '{imageId}' has invalid difficulty {difficulty}.");
        config.GetDifficulty(difficulty);
        if (!float.IsFinite(bestSeconds) || bestSeconds < 0f)
            throw new InvalidOperationException(
                $"Progress result for '{imageId}' has invalid best time.");
        if (!Enum.IsDefined(typeof(PuzzleMedal), bestMedal))
            throw new InvalidOperationException(
                $"Progress result for '{imageId}' has invalid medal {bestMedal}.");
        if (completions <= 0)
            throw new InvalidOperationException(
                $"Progress result for '{imageId}' must contain a completion.");
    }
}

public sealed class PuzzleProgressUpdate
{
    public PuzzleProgressUpdate(
        PuzzleMedal medal,
        bool newBestScore,
        IReadOnlyList<string> unlockMessages)
    {
        Medal = medal;
        NewBestScore = newBestScore;
        UnlockMessages = unlockMessages ?? throw new ArgumentNullException(nameof(unlockMessages));
    }

    public PuzzleMedal Medal { get; }

    public bool NewBestScore { get; }

    public IReadOnlyList<string> UnlockMessages { get; }

    public string BuildSummary()
    {
        string summary = $"MEDALHA  {PuzzleMedalCalculator.Label(Medal)}";
        if (NewBestScore) summary += "\nNOVO RECORDE DE PONTUAÇÃO";
        if (UnlockMessages.Count == 0) return summary;

        summary += "\n\nDESBLOQUEIOS";
        for (int i = 0; i < UnlockMessages.Count; i++)
            summary += $"\n• {UnlockMessages[i]}";
        return summary;
    }
}

[Serializable]
public sealed class PuzzleProgressData
{
    public const int CurrentVersion = 1;
    public const string CurrentSchema = "procedural-puzzle-progress/v1";

    [SerializeField] private string schema;
    [SerializeField] private int version;
    [SerializeField] private string lastSelectedImageId;
    [SerializeField] private PuzzleDifficulty lastSelectedDifficulty;
    [SerializeField] private PuzzleCutStyle lastSelectedCutStyle;
    [SerializeField] private List<string> unlockedImageIds;
    [SerializeField] private List<string> completedImageIds;
    [SerializeField] private List<PuzzleBestResultRecord> bestResults;

    public int Version => version;

    public string Schema => schema;

    public string LastSelectedImageId => lastSelectedImageId;

    public PuzzleDifficulty LastSelectedDifficulty => lastSelectedDifficulty;

    public PuzzleCutStyle LastSelectedCutStyle => lastSelectedCutStyle;

    public IReadOnlyList<string> UnlockedImageIds => unlockedImageIds;

    public IReadOnlyList<string> CompletedImageIds => completedImageIds;

    public IReadOnlyList<PuzzleBestResultRecord> BestResults => bestResults;

    public int UniqueCompletedImages
    {
        get
        {
            RequireLists();
            return completedImageIds.Count;
        }
    }

    public static PuzzleProgressData CreateNew(PuzzleConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (!config.TryValidate(out string error))
            throw new InvalidOperationException($"Invalid PuzzleConfig: {error}.");

        var progress = new PuzzleProgressData
        {
            schema = CurrentSchema,
            version = CurrentVersion,
            lastSelectedImageId = config.DefaultImageId,
            lastSelectedDifficulty = config.DefaultDifficulty.Difficulty,
            lastSelectedCutStyle = config.DefaultDifficulty.DefaultCutStyle,
            unlockedImageIds = new List<string>(),
            completedImageIds = new List<string>(),
            bestResults = new List<PuzzleBestResultRecord>(),
        };
        progress.RefreshUnlockedImages(config);
        progress.Validate(config);
        return progress;
    }

    public static PuzzleProgressData CreateFromPersisted(
        string lastSelectedImageId,
        PuzzleDifficulty lastSelectedDifficulty,
        PuzzleCutStyle lastSelectedCutStyle,
        IReadOnlyList<string> unlockedImageIds,
        IReadOnlyList<string> completedImageIds,
        IReadOnlyList<PuzzleBestResultRecord> bestResults)
    {
        if (string.IsNullOrWhiteSpace(lastSelectedImageId))
            throw new ArgumentException("Last selected image id is required.", nameof(lastSelectedImageId));
        if (!Enum.IsDefined(typeof(PuzzleDifficulty), lastSelectedDifficulty))
            throw new ArgumentOutOfRangeException(nameof(lastSelectedDifficulty));
        if (!Enum.IsDefined(typeof(PuzzleCutStyle), lastSelectedCutStyle))
            throw new ArgumentOutOfRangeException(nameof(lastSelectedCutStyle));
        if (unlockedImageIds == null) throw new ArgumentNullException(nameof(unlockedImageIds));
        if (completedImageIds == null) throw new ArgumentNullException(nameof(completedImageIds));
        if (bestResults == null) throw new ArgumentNullException(nameof(bestResults));

        return new PuzzleProgressData
        {
            schema = CurrentSchema,
            version = CurrentVersion,
            lastSelectedImageId = lastSelectedImageId,
            lastSelectedDifficulty = lastSelectedDifficulty,
            lastSelectedCutStyle = lastSelectedCutStyle,
            unlockedImageIds = new List<string>(unlockedImageIds),
            completedImageIds = new List<string>(completedImageIds),
            bestResults = new List<PuzzleBestResultRecord>(bestResults),
        };
    }

    public bool IsImageUnlocked(string imageId)
    {
        if (string.IsNullOrWhiteSpace(imageId))
            throw new ArgumentException("Image id is required.", nameof(imageId));
        RequireLists();
        return unlockedImageIds.Contains(imageId);
    }

    public bool IsDifficultyUnlocked(PuzzleConfig config, PuzzleDifficultyProfile profile)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (!ReferenceEquals(config.GetDifficulty(profile.Difficulty), profile))
            throw new InvalidOperationException(
                $"Difficulty profile '{profile.name}' does not belong to this configuration.");
        return UniqueCompletedImages >= profile.RequiredUniqueCompletions;
    }

    public bool IsStyleUnlocked(PuzzleConfig config, PuzzleCutStyle style)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        return config.IsStyleUnlocked(style, UniqueCompletedImages);
    }

    public void SelectImage(PuzzleConfig config, string imageId)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        Validate(config);
        config.GetImage(imageId);
        if (!IsImageUnlocked(imageId))
            throw new InvalidOperationException($"Puzzle image '{imageId}' is locked.");
        lastSelectedImageId = imageId;
        Validate(config);
    }

    public void SelectRules(
        PuzzleConfig config,
        PuzzleDifficultyProfile difficulty,
        PuzzleCutStyle cutStyle)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (difficulty == null) throw new ArgumentNullException(nameof(difficulty));
        Validate(config);
        if (!IsDifficultyUnlocked(config, difficulty))
            throw new InvalidOperationException(
                $"Difficulty {difficulty.DisplayName} is locked.");
        if (!difficulty.AllowsStyle(cutStyle))
            throw new InvalidOperationException(
                $"Cut style {cutStyle} is unavailable for {difficulty.DisplayName}.");
        if (!IsStyleUnlocked(config, cutStyle))
            throw new InvalidOperationException($"Cut style {cutStyle} is locked.");

        lastSelectedDifficulty = difficulty.Difficulty;
        lastSelectedCutStyle = cutStyle;
        Validate(config);
    }

    public bool TryGetBestResult(
        string imageId,
        PuzzleDifficulty difficulty,
        out PuzzleBestResultRecord result)
    {
        if (string.IsNullOrWhiteSpace(imageId))
            throw new ArgumentException("Image id is required.", nameof(imageId));
        if (!Enum.IsDefined(typeof(PuzzleDifficulty), difficulty))
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        RequireLists();

        for (int i = 0; i < bestResults.Count; i++)
        {
            PuzzleBestResultRecord candidate = bestResults[i];
            if (candidate.ImageId == imageId && candidate.Difficulty == difficulty)
            {
                result = candidate;
                return true;
            }
        }

        result = null;
        return false;
    }

    public PuzzleProgressUpdate RecordCompletion(
        PuzzleConfig config,
        PuzzleSessionDefinition session,
        PuzzleSessionMetrics metrics,
        PuzzleScoreBreakdown score)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (metrics == null || !metrics.IsActive || !metrics.IsComplete)
            throw new ArgumentException("Completed session metrics are required.", nameof(metrics));
        if (score == null) throw new ArgumentNullException(nameof(score));
        Validate(config);

        PuzzleImageDefinition image = config.GetImage(session.ImageId);
        if (!session.TryValidate(out string sessionError))
            throw new InvalidOperationException(
                $"Completed puzzle session is invalid: {sessionError}.");
        PuzzleDifficultyProfile configuredDifficulty =
            config.GetDifficulty(session.Difficulty.Difficulty);
        if (!ReferenceEquals(configuredDifficulty, session.Difficulty))
            throw new InvalidOperationException(
                "Completed puzzle difficulty does not belong to this configuration.");
        if (!IsDifficultyUnlocked(config, session.Difficulty))
            throw new InvalidOperationException(
                $"Completed difficulty {session.Difficulty.DisplayName} is locked.");
        if (!IsStyleUnlocked(config, session.CutStyle))
            throw new InvalidOperationException(
                $"Completed cut style {session.CutStyle} is locked.");
        if (config.LoadImage(image) != session.Texture)
            throw new InvalidOperationException(
                $"Completed puzzle texture does not match image '{image.Id}'.");
        int expectedPieceCount = checked(session.Layout.divisions * session.Layout.divisions);
        if (metrics.TotalPieces != expectedPieceCount)
            throw new InvalidOperationException(
                "Completed session metrics do not match the selected layout.");
        PuzzleScoreBreakdown expectedScore = PuzzleScoreCalculator.Calculate(session, metrics);
        if (score.Total != expectedScore.Total)
            throw new InvalidOperationException(
                "Completed session score does not match its canonical calculation.");
        if (!IsImageUnlocked(image.Id))
            throw new InvalidOperationException(
                $"Completed puzzle image '{image.Id}' is not unlocked.");

        int previousUniqueCompletions = UniqueCompletedImages;
        PuzzleMedal medal = PuzzleMedalCalculator.Calculate(metrics);
        bool newBestScore;
        if (TryGetBestResult(image.Id, session.Difficulty.Difficulty, out PuzzleBestResultRecord record))
        {
            newBestScore = record.Record(score.Total, metrics.ElapsedSeconds, medal);
        }
        else
        {
            record = new PuzzleBestResultRecord(
                image.Id,
                session.Difficulty.Difficulty,
                score.Total,
                metrics.ElapsedSeconds,
                medal);
            bestResults.Add(record);
            newBestScore = true;
        }

        if (!completedImageIds.Contains(image.Id)) completedImageIds.Add(image.Id);
        RefreshUnlockedImages(config);

        var unlockMessages = new List<string>();
        if (UniqueCompletedImages > previousUniqueCompletions)
        {
            for (int collectionIndex = 0; collectionIndex < config.Collections.Count; collectionIndex++)
            {
                PuzzleCollectionDefinition collection = config.Collections[collectionIndex];
                if (collection.RequiredUniqueCompletions > previousUniqueCompletions &&
                    collection.RequiredUniqueCompletions <= UniqueCompletedImages)
                    unlockMessages.Add($"Coleção {collection.DisplayName}");

                for (int imageIndex = 0; imageIndex < collection.Images.Count; imageIndex++)
                {
                    PuzzleImageDefinition unlockedImage = collection.Images[imageIndex];
                    if (unlockedImage.RequiredUniqueCompletions > previousUniqueCompletions &&
                        unlockedImage.RequiredUniqueCompletions <= UniqueCompletedImages)
                        unlockMessages.Add($"Imagem {unlockedImage.DisplayName}");
                }
            }

            for (int i = 0; i < config.DifficultyProfiles.Count; i++)
            {
                PuzzleDifficultyProfile profile = config.DifficultyProfiles[i];
                if (profile.RequiredUniqueCompletions > previousUniqueCompletions &&
                    profile.RequiredUniqueCompletions <= UniqueCompletedImages)
                    unlockMessages.Add($"Dificuldade {profile.DisplayName}");
            }

            foreach (PuzzleCutStyle style in Enum.GetValues(typeof(PuzzleCutStyle)))
            {
                int requirement = config.GetStyleRequirement(style);
                if (requirement > previousUniqueCompletions &&
                    requirement <= UniqueCompletedImages)
                    unlockMessages.Add($"Formato {style}");
            }
        }

        Validate(config);
        return new PuzzleProgressUpdate(medal, newBestScore, unlockMessages);
    }

    public void Validate(PuzzleConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (!config.TryValidate(out string configError))
            throw new InvalidOperationException($"Invalid PuzzleConfig: {configError}.");
        if (schema != CurrentSchema)
            throw new InvalidOperationException(
                $"Progress schema '{schema}' is unsupported; expected '{CurrentSchema}'.");
        if (version != CurrentVersion)
            throw new InvalidOperationException(
                $"Progress version {version} is unsupported; expected {CurrentVersion}.");
        RequireLists();

        var completedIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < completedImageIds.Count; i++)
        {
            string imageId = completedImageIds[i];
            config.GetImage(imageId);
            if (!completedIds.Add(imageId))
                throw new InvalidOperationException(
                    $"Progress contains duplicate completed image '{imageId}'.");
        }

        var expectedUnlockedIds = BuildExpectedUnlockedImageIds(config, completedImageIds.Count);
        if (unlockedImageIds.Count != expectedUnlockedIds.Count)
            throw new InvalidOperationException(
                "Progress unlocked image list does not match the configured progression rules.");
        for (int i = 0; i < expectedUnlockedIds.Count; i++)
            if (unlockedImageIds[i] != expectedUnlockedIds[i])
                throw new InvalidOperationException(
                    "Progress unlocked image ordering does not match the configured progression rules.");

        config.GetImage(lastSelectedImageId);
        if (!unlockedImageIds.Contains(lastSelectedImageId))
            throw new InvalidOperationException(
                $"Last selected image '{lastSelectedImageId}' is locked.");

        if (!Enum.IsDefined(typeof(PuzzleDifficulty), lastSelectedDifficulty))
            throw new InvalidOperationException(
                $"Last selected difficulty {lastSelectedDifficulty} is invalid.");
        PuzzleDifficultyProfile selectedDifficulty =
            config.GetDifficulty(lastSelectedDifficulty);
        if (!IsDifficultyUnlocked(config, selectedDifficulty))
            throw new InvalidOperationException(
                $"Last selected difficulty {selectedDifficulty.DisplayName} is locked.");
        if (!Enum.IsDefined(typeof(PuzzleCutStyle), lastSelectedCutStyle) ||
            !selectedDifficulty.AllowsStyle(lastSelectedCutStyle) ||
            !IsStyleUnlocked(config, lastSelectedCutStyle))
            throw new InvalidOperationException(
                $"Last selected cut style {lastSelectedCutStyle} is unavailable.");

        var resultKeys = new HashSet<string>(StringComparer.Ordinal);
        var resultImageIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < bestResults.Count; i++)
        {
            PuzzleBestResultRecord result = bestResults[i];
            if (result == null)
                throw new InvalidOperationException($"Progress result {i} is missing.");
            result.Validate(config);
            string key = $"{result.ImageId}|{(int)result.Difficulty}";
            if (!resultKeys.Add(key))
                throw new InvalidOperationException($"Progress result '{key}' is duplicated.");
            if (!completedIds.Contains(result.ImageId))
                throw new InvalidOperationException(
                    $"Progress result '{key}' belongs to an incomplete image.");
            resultImageIds.Add(result.ImageId);
        }

        foreach (string completedId in completedIds)
            if (!resultImageIds.Contains(completedId))
                throw new InvalidOperationException(
                    $"Completed image '{completedId}' has no result record.");
    }

    private void RefreshUnlockedImages(PuzzleConfig config)
    {
        unlockedImageIds.Clear();
        unlockedImageIds.AddRange(BuildExpectedUnlockedImageIds(config, UniqueCompletedImages));
    }

    private static List<string> BuildExpectedUnlockedImageIds(
        PuzzleConfig config,
        int uniqueCompletions)
    {
        var result = new List<string>();
        for (int collectionIndex = 0; collectionIndex < config.Collections.Count; collectionIndex++)
        {
            PuzzleCollectionDefinition collection = config.Collections[collectionIndex];
            if (!config.IsCollectionUnlocked(collection, uniqueCompletions)) continue;
            for (int imageIndex = 0; imageIndex < collection.Images.Count; imageIndex++)
            {
                PuzzleImageDefinition image = collection.Images[imageIndex];
                if (uniqueCompletions >= image.RequiredUniqueCompletions) result.Add(image.Id);
            }
        }

        return result;
    }

    private void RequireLists()
    {
        if (unlockedImageIds == null || completedImageIds == null || bestResults == null)
            throw new InvalidOperationException("Progress collections are missing.");
    }
}
