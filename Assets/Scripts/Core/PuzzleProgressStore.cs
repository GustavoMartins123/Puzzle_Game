using System;
using System.Collections.Generic;
using UnityEngine;

public static class PuzzleProgressSerializer
{
    public static string Serialize(PuzzleProgressData progress, PuzzleConfig config)
    {
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        progress.Validate(config);
        string json = JsonUtility.ToJson(progress, true);
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Progress serialization produced no data.");
        return json;
    }

    public static PuzzleProgressData Deserialize(string json, PuzzleConfig config)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Progress data is empty.");
        if (config == null) throw new ArgumentNullException(nameof(config));

        PuzzleProgressData progress;
        try
        {
            progress = JsonUtility.FromJson<PuzzleProgressData>(json);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Progress JSON is malformed.", exception);
        }

        if (progress == null)
            throw new InvalidOperationException("Progress JSON did not create a data model.");
        progress.Validate(config);
        return progress;
    }
}

public sealed class PuzzleProgressStore : IDisposable
{
    private readonly GameDatabase database;
    private readonly bool ownsDatabase;

    public PuzzleProgressStore()
        : this(GameDatabase.DefaultDatabasePath, true)
    {
    }

    public PuzzleProgressStore(string databasePath)
        : this(databasePath, true)
    {
    }

    public PuzzleProgressStore(GameDatabase sharedDatabase)
    {
        database = sharedDatabase ?? throw new ArgumentNullException(nameof(sharedDatabase));
        ownsDatabase = false;
    }

    private PuzzleProgressStore(string databasePath, bool ownsDatabase)
    {
        database = new GameDatabase(databasePath);
        this.ownsDatabase = ownsDatabase;
    }

    public GameDatabase Database => database;

    public PuzzleProgressData LoadOrCreate(PuzzleConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (!config.TryValidate(out string configError))
            throw new InvalidOperationException($"Invalid PuzzleConfig: {configError}.");

        bool initialized = database.IsProfileInitialized();
        PuzzleProgressData progress = LoadPersisted(config);
        if (!initialized)
        {
            if (progress != null || database.HasUserData())
                throw new InvalidOperationException(
                    "SQLite contains user data without the canonical initialization marker.");
            progress = PuzzleProgressData.CreateNew(config);
            Save(progress, config);
        }
        else if (progress == null)
        {
            throw new InvalidOperationException(
                "SQLite profile is initialized but its progress_state row is missing.");
        }
        ValidateAchievementStates(config);
        return progress;
    }

    public void Save(PuzzleProgressData progress, PuzzleConfig config)
    {
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        progress.Validate(config);
        database.SaveProgressState(ToPersistedState(progress));
    }

    public PuzzleCompletionDatabaseResult CompleteSession(
        PuzzleProgressData progress,
        PuzzleConfig config,
        PuzzleSessionDefinition session,
        PuzzleSessionMetrics metrics,
        PuzzleScoreBreakdown score,
        PuzzleProgressUpdate update)
    {
        ValidateCompletedSession(progress, config, session, metrics, score, update);

        IReadOnlyDictionary<string, AchievementState> states =
            BuildAchievementStateMap(config);
        IReadOnlyList<PuzzleAchievementProgressChange> changes =
            PuzzleAchievementEvaluator.EvaluateCompletion(
                config.Achievements,
                states,
                progress,
                session,
                metrics,
                score);

        var record = new PuzzleSessionRecord
        {
            ImageId = session.ImageId,
            Difficulty = (int)session.Difficulty.Difficulty,
            CutStyle = (int)session.CutStyle,
            LayoutDivisions = session.Layout.divisions,
            TotalPieces = metrics.TotalPieces,
            CutSeed = session.CutSeed,
            TraySeed = session.TraySeed,
            ElapsedSeconds = metrics.ElapsedSeconds,
            MovesStarted = metrics.MovesStarted,
            IncorrectAttempts = metrics.IncorrectAttempts,
            HintsUsed = metrics.HintsUsed,
            Score = score.Total,
            Medal = (int)update.Medal,
            IsNewRecord = update.NewBestScore,
        };
        return database.SaveCompletedSession(
            ToPersistedState(progress),
            record,
            changes);
    }

    public IReadOnlyList<PuzzleAchievementCatalogEntry> GetAchievementCatalog(PuzzleConfig config)
    {
        IReadOnlyDictionary<string, AchievementState> states =
            BuildAchievementStateMap(config);
        var catalog = new List<PuzzleAchievementCatalogEntry>(config.Achievements.Count);
        for (int i = 0; i < config.Achievements.Count; i++)
        {
            PuzzleAchievementDefinition definition = config.Achievements[i];
            states.TryGetValue(definition.Id, out AchievementState state);
            catalog.Add(new PuzzleAchievementCatalogEntry(
                definition,
                state?.Progress ?? 0,
                state?.UnlockedAtUtc));
        }
        return catalog;
    }

    public IReadOnlyList<PuzzleAchievementNotification> GetPendingAchievementNotifications(
        PuzzleConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        ValidateAchievementStates(config);
        IReadOnlyList<AchievementNotificationRecord> records =
            database.GetPendingAchievementNotifications();
        var notifications = new List<PuzzleAchievementNotification>(records.Count);
        for (int i = 0; i < records.Count; i++)
        {
            AchievementNotificationRecord record = records[i];
            notifications.Add(new PuzzleAchievementNotification(
                record.Id,
                config.GetAchievement(record.AchievementId),
                record.CreatedAtUtc));
        }
        return notifications;
    }

    public void AcknowledgeAchievementNotification(long notificationId) =>
        database.AcknowledgeAchievementNotification(notificationId);

    public IReadOnlyList<PuzzleSessionRecord> GetRecentSessions(int limit) =>
        database.GetRecentSessions(limit);

    public IReadOnlyList<PuzzleSessionRecord> GetTopScoreSessions(int limit) =>
        database.GetTopScoreSessions(limit);

    public IReadOnlyList<PuzzleSessionRecord> GetBestSessionsForImage(string imageId, int limit) =>
        database.GetBestSessionsForImage(imageId, limit);

    public int GetSessionCount() => database.GetSessionCount();

    public void Dispose()
    {
        if (ownsDatabase) database.Dispose();
    }

    private PuzzleProgressData LoadPersisted(PuzzleConfig config)
    {
        PersistedProgressState state = database.LoadProgressState();
        if (state == null) return null;

        var bestResults = new List<PuzzleBestResultRecord>(state.BestResults.Count);
        for (int i = 0; i < state.BestResults.Count; i++)
        {
            PersistedBestResult persisted = state.BestResults[i];
            bestResults.Add(PuzzleBestResultRecord.FromPersisted(
                persisted.ImageId,
                (PuzzleDifficulty)persisted.Difficulty,
                persisted.BestScore,
                persisted.BestSeconds,
                (PuzzleMedal)persisted.BestMedal,
                persisted.Completions));
        }

        PuzzleProgressData progress = PuzzleProgressData.CreateFromPersisted(
            state.LastSelectedImageId,
            (PuzzleDifficulty)state.LastSelectedDifficulty,
            (PuzzleCutStyle)state.LastSelectedCutStyle,
            state.UnlockedImageIds,
            state.CompletedImageIds,
            bestResults);
        progress.Validate(config);
        return progress;
    }

    private IReadOnlyDictionary<string, AchievementState> BuildAchievementStateMap(
        PuzzleConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        var states = new Dictionary<string, AchievementState>(StringComparer.Ordinal);
        IReadOnlyList<AchievementState> persisted = database.GetAchievements();
        for (int i = 0; i < persisted.Count; i++)
        {
            AchievementState state = persisted[i] ??
                throw new InvalidOperationException($"Persisted achievement {i} is missing.");
            PuzzleAchievementDefinition definition = config.GetAchievement(state.AchievementId);
            if (state.Progress < 0 || state.Progress > definition.Target)
                throw new InvalidOperationException(
                    $"Persisted achievement '{state.AchievementId}' has invalid progress {state.Progress}.");
            if (state.IsUnlocked != (state.Progress == definition.Target))
                throw new InvalidOperationException(
                    $"Persisted achievement '{state.AchievementId}' has an inconsistent unlock state.");
            if (!states.TryAdd(state.AchievementId, state))
                throw new InvalidOperationException(
                    $"Persisted achievement '{state.AchievementId}' is duplicated.");
        }
        return states;
    }

    private void ValidateAchievementStates(PuzzleConfig config) =>
        BuildAchievementStateMap(config);

    private static void ValidateCompletedSession(
        PuzzleProgressData progress,
        PuzzleConfig config,
        PuzzleSessionDefinition session,
        PuzzleSessionMetrics metrics,
        PuzzleScoreBreakdown score,
        PuzzleProgressUpdate update)
    {
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (metrics == null || !metrics.IsActive || !metrics.IsComplete)
            throw new ArgumentException("Completed session metrics are required.", nameof(metrics));
        if (score == null) throw new ArgumentNullException(nameof(score));
        if (update == null) throw new ArgumentNullException(nameof(update));

        progress.Validate(config);
        if (!session.TryValidate(out string sessionError))
            throw new InvalidOperationException($"Completed session is invalid: {sessionError}.");
        PuzzleImageDefinition image = config.GetImage(session.ImageId);
        if (config.LoadImage(image) != session.Texture)
            throw new InvalidOperationException(
                $"Completed puzzle texture does not match image '{image.Id}'.");
        int expectedPieces = checked(session.Layout.divisions * session.Layout.divisions);
        if (metrics.TotalPieces != expectedPieces || metrics.PlacedPieces != expectedPieces)
            throw new InvalidOperationException(
                "Completed session metrics do not match the selected layout.");
        PuzzleScoreBreakdown canonicalScore = PuzzleScoreCalculator.Calculate(session, metrics);
        if (score.Total != canonicalScore.Total ||
            score.BaseScore != canonicalScore.BaseScore ||
            score.ComplexityBonus != canonicalScore.ComplexityBonus ||
            score.RotationBonus != canonicalScore.RotationBonus ||
            score.ReferenceBonus != canonicalScore.ReferenceBonus ||
            score.TimePenalty != canonicalScore.TimePenalty ||
            score.ErrorPenalty != canonicalScore.ErrorPenalty ||
            score.HintPenalty != canonicalScore.HintPenalty)
            throw new InvalidOperationException("Completed session score is not canonical.");
        PuzzleMedal canonicalMedal = PuzzleMedalCalculator.Calculate(metrics);
        if (update.Medal != canonicalMedal)
            throw new InvalidOperationException("Completed session medal is not canonical.");
    }

    private static PersistedProgressState ToPersistedState(PuzzleProgressData progress)
    {
        var unlocked = new List<string>(progress.UnlockedImageIds);
        var completed = new List<string>(progress.CompletedImageIds);
        var bestResults = new List<PersistedBestResult>(progress.BestResults.Count);
        for (int i = 0; i < progress.BestResults.Count; i++)
        {
            PuzzleBestResultRecord record = progress.BestResults[i];
            bestResults.Add(new PersistedBestResult
            {
                ImageId = record.ImageId,
                Difficulty = (int)record.Difficulty,
                BestScore = record.BestScore,
                BestSeconds = record.BestSeconds,
                BestMedal = (int)record.BestMedal,
                Completions = record.Completions,
            });
        }

        return new PersistedProgressState
        {
            LastSelectedImageId = progress.LastSelectedImageId,
            LastSelectedDifficulty = (int)progress.LastSelectedDifficulty,
            LastSelectedCutStyle = (int)progress.LastSelectedCutStyle,
            UnlockedImageIds = unlocked,
            CompletedImageIds = completed,
            BestResults = bestResults,
        };
    }
}
