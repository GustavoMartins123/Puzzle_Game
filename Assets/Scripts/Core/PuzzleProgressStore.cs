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
    public const string LegacyPlayerPrefsKey = "Puzzle.Progress.v1";

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

        PuzzleProgressData progress = LoadPersisted(config);
        if (progress == null) progress = MigrateLegacyProgress(config);
        if (progress == null)
        {
            progress = PuzzleProgressData.CreateNew(config);
            Save(progress, config);
        }
        return progress;
    }

    public void Save(PuzzleProgressData progress, PuzzleConfig config)
    {
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        progress.Validate(config);
        database.SaveProgressState(ToPersistedState(progress));
    }

    public long RecordSession(
        PuzzleSessionDefinition session,
        PuzzleSessionMetrics metrics,
        PuzzleScoreBreakdown score,
        PuzzleProgressUpdate update)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (session.Texture == null)
            throw new ArgumentException("Session texture is required.", nameof(session));
        if (metrics == null || !metrics.IsActive || !metrics.IsComplete)
            throw new ArgumentException("Completed session metrics are required.", nameof(metrics));
        if (score == null) throw new ArgumentNullException(nameof(score));
        if (update == null) throw new ArgumentNullException(nameof(update));

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
        return database.InsertSessionRecord(record);
    }

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

    private PuzzleProgressData MigrateLegacyProgress(PuzzleConfig config)
    {
        if (!PlayerPrefs.HasKey(LegacyPlayerPrefsKey)) return null;

        try
        {
            PuzzleProgressData progress = PuzzleProgressSerializer.Deserialize(
                PlayerPrefs.GetString(LegacyPlayerPrefsKey), config);
            Save(progress, config);
            PlayerPrefs.DeleteKey(LegacyPlayerPrefsKey);
            PlayerPrefs.Save();
            return progress;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Legacy PlayerPrefs progress could not be migrated to the SQLite database.",
                exception);
        }
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
