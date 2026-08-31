using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public sealed class PersistedBestResult
{
    public string ImageId;
    public int Difficulty;
    public int BestScore;
    public float BestSeconds;
    public int BestMedal;
    public int Completions;
}

public sealed class PersistedProgressState
{
    public string LastSelectedImageId;
    public int LastSelectedDifficulty;
    public int LastSelectedCutStyle;
    public List<string> UnlockedImageIds;
    public List<string> CompletedImageIds;
    public List<PersistedBestResult> BestResults;
}

public sealed class PuzzleSessionRecord
{
    public long Id;
    public DateTime CompletedAtUtc;
    public string ImageId;
    public int Difficulty;
    public int CutStyle;
    public int LayoutDivisions;
    public int TotalPieces;
    public int CutSeed;
    public int TraySeed;
    public float ElapsedSeconds;
    public int MovesStarted;
    public int IncorrectAttempts;
    public int HintsUsed;
    public int Score;
    public int Medal;
    public bool IsNewRecord;
}

public sealed class AchievementState
{
    public string AchievementId;
    public DateTime? UnlockedAtUtc;
    public int Progress;

    public bool IsUnlocked => UnlockedAtUtc != null;
}

public sealed class AchievementNotificationRecord
{
    public long Id;
    public string AchievementId;
    public DateTime CreatedAtUtc;
    public DateTime? AcknowledgedAtUtc;
}

public sealed class PuzzleCompletionDatabaseResult
{
    public PuzzleCompletionDatabaseResult(long sessionId, IReadOnlyList<string> unlockedAchievementIds)
    {
        if (sessionId <= 0) throw new ArgumentOutOfRangeException(nameof(sessionId));
        SessionId = sessionId;
        UnlockedAchievementIds = unlockedAchievementIds ??
            throw new ArgumentNullException(nameof(unlockedAchievementIds));
    }

    public long SessionId { get; }

    public IReadOnlyList<string> UnlockedAchievementIds { get; }
}

public sealed class GameDatabase : IDisposable
{
    public const int SchemaVersion = 2;

    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

    private const string SessionTableSql =
        "CREATE TABLE session_history (" +
        " id INTEGER PRIMARY KEY AUTOINCREMENT," +
        " completed_at_utc TEXT NOT NULL," +
        " image_id TEXT NOT NULL CHECK (length(trim(image_id)) > 0)," +
        " difficulty INTEGER NOT NULL CHECK (difficulty BETWEEN 0 AND 2)," +
        " cut_style INTEGER NOT NULL CHECK (cut_style BETWEEN 0 AND 12)," +
        " layout_divisions INTEGER NOT NULL CHECK (layout_divisions > 0)," +
        " total_pieces INTEGER NOT NULL CHECK (total_pieces = layout_divisions * layout_divisions)," +
        " cut_seed INTEGER NOT NULL CHECK (cut_seed > 0)," +
        " tray_seed INTEGER NOT NULL CHECK (tray_seed > 0)," +
        " elapsed_seconds REAL NOT NULL CHECK (elapsed_seconds >= 0)," +
        " moves_started INTEGER NOT NULL CHECK (moves_started >= 0)," +
        " incorrect_attempts INTEGER NOT NULL CHECK (incorrect_attempts >= 0)," +
        " hints_used INTEGER NOT NULL CHECK (hints_used >= 0)," +
        " score INTEGER NOT NULL CHECK (score >= 0)," +
        " medal INTEGER NOT NULL CHECK (medal BETWEEN 1 AND 3)," +
        " is_new_record INTEGER NOT NULL CHECK (is_new_record IN (0, 1)));";

    private const string SessionIndexesSql =
        "CREATE INDEX idx_session_history_completed_at" +
        " ON session_history (completed_at_utc DESC, id DESC);" +
        "CREATE INDEX idx_session_history_score" +
        " ON session_history (score DESC, elapsed_seconds ASC, id ASC);" +
        "CREATE INDEX idx_session_history_image" +
        " ON session_history (image_id, score DESC, elapsed_seconds ASC, id ASC);";

    private const string AchievementTableSql =
        "CREATE TABLE achievements (" +
        " achievement_id TEXT PRIMARY KEY NOT NULL CHECK (length(trim(achievement_id)) > 0)," +
        " unlocked_at_utc TEXT," +
        " progress INTEGER NOT NULL CHECK (progress >= 0));";

    private const string AchievementNotificationTableSql =
        "CREATE TABLE achievement_notifications (" +
        " id INTEGER PRIMARY KEY AUTOINCREMENT," +
        " achievement_id TEXT NOT NULL UNIQUE," +
        " created_at_utc TEXT NOT NULL," +
        " acknowledged_at_utc TEXT," +
        " FOREIGN KEY (achievement_id) REFERENCES achievements(achievement_id)" +
        " ON UPDATE RESTRICT ON DELETE RESTRICT);" +
        "CREATE INDEX idx_achievement_notifications_pending" +
        " ON achievement_notifications (acknowledged_at_utc, id);";

    private const string SchemaV2Sql =
        "CREATE TABLE meta (" +
        " key TEXT PRIMARY KEY NOT NULL CHECK (length(trim(key)) > 0)," +
        " value TEXT NOT NULL);" +
        "CREATE TABLE progress_state (" +
        " id INTEGER PRIMARY KEY CHECK (id = 1)," +
        " last_selected_image_id TEXT NOT NULL CHECK (length(trim(last_selected_image_id)) > 0)," +
        " last_selected_difficulty INTEGER NOT NULL CHECK (last_selected_difficulty BETWEEN 0 AND 2)," +
        " last_selected_cut_style INTEGER NOT NULL CHECK (last_selected_cut_style BETWEEN 0 AND 12));" +
        "CREATE TABLE unlocked_images (" +
        " image_id TEXT PRIMARY KEY NOT NULL CHECK (length(trim(image_id)) > 0)," +
        " position INTEGER NOT NULL UNIQUE CHECK (position >= 0));" +
        "CREATE TABLE completed_images (" +
        " image_id TEXT PRIMARY KEY NOT NULL CHECK (length(trim(image_id)) > 0));" +
        "CREATE TABLE best_results (" +
        " image_id TEXT NOT NULL CHECK (length(trim(image_id)) > 0)," +
        " difficulty INTEGER NOT NULL CHECK (difficulty BETWEEN 0 AND 2)," +
        " best_score INTEGER NOT NULL CHECK (best_score >= 0)," +
        " best_seconds REAL NOT NULL CHECK (best_seconds >= 0)," +
        " best_medal INTEGER NOT NULL CHECK (best_medal BETWEEN 1 AND 3)," +
        " completions INTEGER NOT NULL CHECK (completions > 0)," +
        " PRIMARY KEY (image_id, difficulty));" +
        SessionTableSql +
        SessionIndexesSql +
        AchievementTableSql +
        AchievementNotificationTableSql;

    private const string MigrationV1ToV2Sql =
        "DROP INDEX idx_session_history_completed_at;" +
        "DROP INDEX idx_session_history_score;" +
        "DROP INDEX idx_session_history_image;" +
        "ALTER TABLE session_history RENAME TO session_history_v1;" +
        SessionTableSql +
        "INSERT INTO session_history (" +
        " id, completed_at_utc, image_id, difficulty, cut_style, layout_divisions, total_pieces," +
        " cut_seed, tray_seed, elapsed_seconds, moves_started, incorrect_attempts, hints_used," +
        " score, medal, is_new_record)" +
        " SELECT id, completed_at_utc, image_id, difficulty, cut_style, layout_divisions, total_pieces," +
        " cut_seed, tray_seed, elapsed_seconds, moves_started, incorrect_attempts, hints_used," +
        " score, medal, is_new_record FROM session_history_v1;" +
        "DROP TABLE session_history_v1;" +
        SessionIndexesSql +
        "ALTER TABLE achievements RENAME TO achievements_v1;" +
        AchievementTableSql +
        "INSERT INTO achievements (achievement_id, unlocked_at_utc, progress)" +
        " SELECT achievement_id, unlocked_at_utc, progress FROM achievements_v1;" +
        "DROP TABLE achievements_v1;" +
        AchievementNotificationTableSql +
        "INSERT INTO meta (key, value)" +
        " SELECT 'profile_initialized', '1' WHERE EXISTS (SELECT 1 FROM progress_state);";

    private static readonly string[] ProgressStateColumns =
    {
        "id", "last_selected_image_id", "last_selected_difficulty", "last_selected_cut_style",
    };

    private static readonly string[] SessionColumnsForSchema =
    {
        "id", "completed_at_utc", "image_id", "difficulty", "cut_style", "layout_divisions",
        "total_pieces", "cut_seed", "tray_seed", "elapsed_seconds", "moves_started",
        "incorrect_attempts", "hints_used", "score", "medal", "is_new_record",
    };

    private readonly SqliteConnection connection;

    public static string DefaultDatabasePath =>
        Path.Combine(Application.persistentDataPath, "puzzle_game.db");

    public GameDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Database path is required.", nameof(path));
#if !(UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
        throw new PlatformNotSupportedException(
            "The canonical SQLite runtime is currently available only on Windows x86_64.");
#else
        connection = new SqliteConnection(path);
        try
        {
            Migrate();
            VerifySchemaV2();
            VerifyIntegrity();
        }
        catch (Exception initializationException)
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception closeException)
            {
                throw new AggregateException(
                    "Game database initialization failed and its connection could not be closed.",
                    initializationException,
                    closeException);
            }
            throw;
        }
#endif
    }

    public void Dispose() => connection.Dispose();

    public string GetMeta(string key)
    {
        RequireText(key, nameof(key));
        using SqliteStatement statement = connection.Prepare(
            "SELECT value FROM meta WHERE key = ?;");
        statement.BindText(1, key);
        return statement.Step() ? statement.ColumnText(0) : null;
    }

    public void SetMeta(string key, string value)
    {
        RequireText(key, nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));
        using SqliteStatement statement = connection.Prepare(
            "INSERT INTO meta (key, value) VALUES (?, ?)" +
            " ON CONFLICT(key) DO UPDATE SET value = excluded.value;");
        statement.BindText(1, key).BindText(2, value).Step();
    }

    public bool IsProfileInitialized()
    {
        string value = GetMeta("profile_initialized");
        if (value == null) return false;
        if (value == "1") return true;
        throw new InvalidOperationException(
            $"SQLite profile initialization marker has invalid value '{value}'.");
    }

    public bool HasUserData()
    {
        using SqliteStatement statement = connection.Prepare(
            "SELECT" +
            " (SELECT COUNT(*) FROM progress_state) +" +
            " (SELECT COUNT(*) FROM unlocked_images) +" +
            " (SELECT COUNT(*) FROM completed_images) +" +
            " (SELECT COUNT(*) FROM best_results) +" +
            " (SELECT COUNT(*) FROM session_history) +" +
            " (SELECT COUNT(*) FROM achievements) +" +
            " (SELECT COUNT(*) FROM achievement_notifications);");
        if (!statement.Step())
            throw new InvalidOperationException("SQLite user-data query returned no row.");
        return statement.ColumnLong(0) > 0;
    }

    public PersistedProgressState LoadProgressState()
    {
        string lastImageId;
        int lastDifficulty;
        int lastCutStyle;
        using (SqliteStatement statement = connection.Prepare(
            "SELECT last_selected_image_id, last_selected_difficulty, last_selected_cut_style" +
            " FROM progress_state WHERE id = 1;"))
        {
            if (!statement.Step()) return null;
            lastImageId = statement.ColumnText(0);
            lastDifficulty = statement.ColumnInt(1);
            lastCutStyle = statement.ColumnInt(2);
        }

        var state = new PersistedProgressState
        {
            LastSelectedImageId = lastImageId,
            LastSelectedDifficulty = lastDifficulty,
            LastSelectedCutStyle = lastCutStyle,
            UnlockedImageIds = QueryTextColumn(
                "SELECT image_id FROM unlocked_images ORDER BY position;"),
            CompletedImageIds = QueryTextColumn(
                "SELECT image_id FROM completed_images ORDER BY image_id;"),
            BestResults = new List<PersistedBestResult>(),
        };

        using (SqliteStatement statement = connection.Prepare(
            "SELECT image_id, difficulty, best_score, best_seconds, best_medal, completions" +
            " FROM best_results ORDER BY image_id, difficulty;"))
        {
            while (statement.Step())
            {
                state.BestResults.Add(new PersistedBestResult
                {
                    ImageId = statement.ColumnText(0),
                    Difficulty = statement.ColumnInt(1),
                    BestScore = statement.ColumnInt(2),
                    BestSeconds = statement.ColumnFloat(3),
                    BestMedal = statement.ColumnInt(4),
                    Completions = statement.ColumnInt(5),
                });
            }
        }
        return state;
    }

    public void SaveProgressState(PersistedProgressState state)
    {
        ValidateProgressState(state);
        RunTransaction(() => SaveProgressStateCore(state));
    }

    public PuzzleCompletionDatabaseResult SaveCompletedSession(
        PersistedProgressState state,
        PuzzleSessionRecord record,
        IReadOnlyList<PuzzleAchievementProgressChange> achievementChanges)
    {
        ValidateProgressState(state);
        ValidateSessionRecord(record);
        if (achievementChanges == null)
            throw new ArgumentNullException(nameof(achievementChanges));
        ValidateAchievementChanges(achievementChanges);

        var unlockedIds = new List<string>();
        long sessionId = 0;
        DateTime completedAtUtc = DateTime.UtcNow;
        RunTransaction(() =>
        {
            SaveProgressStateCore(state);
            sessionId = InsertSessionRecordCore(record, completedAtUtc);
            for (int i = 0; i < achievementChanges.Count; i++)
                ApplyAchievementProgressCore(
                    achievementChanges[i],
                    completedAtUtc,
                    unlockedIds);
        });
        return new PuzzleCompletionDatabaseResult(sessionId, unlockedIds);
    }

    public IReadOnlyList<PuzzleSessionRecord> GetRecentSessions(int limit)
    {
        RequirePositiveLimit(limit);
        return QuerySessions(
            "SELECT " + SessionColumns +
            " FROM session_history ORDER BY completed_at_utc DESC, id DESC LIMIT ?;",
            statement => statement.BindInt(1, limit));
    }

    public IReadOnlyList<PuzzleSessionRecord> GetTopScoreSessions(int limit)
    {
        RequirePositiveLimit(limit);
        return QuerySessions(
            "SELECT " + SessionColumns +
            " FROM session_history ORDER BY score DESC, elapsed_seconds ASC, id ASC LIMIT ?;",
            statement => statement.BindInt(1, limit));
    }

    public IReadOnlyList<PuzzleSessionRecord> GetBestSessionsForImage(string imageId, int limit)
    {
        RequireText(imageId, nameof(imageId));
        RequirePositiveLimit(limit);
        return QuerySessions(
            "SELECT " + SessionColumns +
            " FROM session_history WHERE image_id = ?" +
            " ORDER BY score DESC, elapsed_seconds ASC, id ASC LIMIT ?;",
            statement => statement.BindText(1, imageId).BindInt(2, limit));
    }

    public int GetSessionCount()
    {
        using SqliteStatement statement = connection.Prepare(
            "SELECT COUNT(*) FROM session_history;");
        if (!statement.Step())
            throw new InvalidOperationException("Session count query returned no row.");
        return statement.ColumnInt(0);
    }

    public AchievementState GetAchievement(string achievementId)
    {
        RequireText(achievementId, nameof(achievementId));
        using SqliteStatement statement = connection.Prepare(
            "SELECT achievement_id, unlocked_at_utc, progress" +
            " FROM achievements WHERE achievement_id = ?;");
        statement.BindText(1, achievementId);
        return statement.Step() ? MapAchievement(statement) : null;
    }

    public IReadOnlyList<AchievementState> GetAchievements()
    {
        var achievements = new List<AchievementState>();
        using SqliteStatement statement = connection.Prepare(
            "SELECT achievement_id, unlocked_at_utc, progress" +
            " FROM achievements ORDER BY achievement_id;");
        while (statement.Step()) achievements.Add(MapAchievement(statement));
        return achievements;
    }

    public IReadOnlyList<AchievementNotificationRecord> GetPendingAchievementNotifications()
    {
        var notifications = new List<AchievementNotificationRecord>();
        using SqliteStatement statement = connection.Prepare(
            "SELECT id, achievement_id, created_at_utc, acknowledged_at_utc" +
            " FROM achievement_notifications WHERE acknowledged_at_utc IS NULL ORDER BY id;");
        while (statement.Step())
        {
            notifications.Add(new AchievementNotificationRecord
            {
                Id = statement.ColumnLong(0),
                AchievementId = statement.ColumnText(1),
                CreatedAtUtc = ParseUtc(statement.ColumnText(2)),
                AcknowledgedAtUtc = null,
            });
        }
        return notifications;
    }

    public void AcknowledgeAchievementNotification(long notificationId)
    {
        if (notificationId <= 0) throw new ArgumentOutOfRangeException(nameof(notificationId));
        RunTransaction(() =>
        {
            using SqliteStatement statement = connection.Prepare(
                "UPDATE achievement_notifications SET acknowledged_at_utc = ?" +
                " WHERE id = ? AND acknowledged_at_utc IS NULL;");
            statement
                .BindText(1, FormatUtc(DateTime.UtcNow))
                .BindLong(2, notificationId)
                .Step();
            if (connection.Changes != 1)
                throw new InvalidOperationException(
                    $"Achievement notification {notificationId} is missing or already acknowledged.");
        });
    }

    private const string SessionColumns =
        "id, completed_at_utc, image_id, difficulty, cut_style, layout_divisions," +
        " total_pieces, cut_seed, tray_seed, elapsed_seconds, moves_started," +
        " incorrect_attempts, hints_used, score, medal, is_new_record";

    private void SaveProgressStateCore(PersistedProgressState state)
    {
        EnsureProfileInitializationMarkerCore();
        using (SqliteStatement statement = connection.Prepare(
            "INSERT INTO progress_state" +
            " (id, last_selected_image_id, last_selected_difficulty, last_selected_cut_style)" +
            " VALUES (1, ?, ?, ?) ON CONFLICT(id) DO UPDATE SET" +
            " last_selected_image_id = excluded.last_selected_image_id," +
            " last_selected_difficulty = excluded.last_selected_difficulty," +
            " last_selected_cut_style = excluded.last_selected_cut_style;"))
        {
            statement
                .BindText(1, state.LastSelectedImageId)
                .BindInt(2, state.LastSelectedDifficulty)
                .BindInt(3, state.LastSelectedCutStyle)
                .Step();
        }

        connection.Execute("DELETE FROM unlocked_images;");
        using (SqliteStatement statement = connection.Prepare(
            "INSERT INTO unlocked_images (image_id, position) VALUES (?, ?);"))
        {
            for (int i = 0; i < state.UnlockedImageIds.Count; i++)
                statement.BindText(1, state.UnlockedImageIds[i]).BindInt(2, i).Step();
        }

        connection.Execute("DELETE FROM completed_images;");
        using (SqliteStatement statement = connection.Prepare(
            "INSERT INTO completed_images (image_id) VALUES (?);"))
        {
            for (int i = 0; i < state.CompletedImageIds.Count; i++)
                statement.BindText(1, state.CompletedImageIds[i]).Step();
        }

        connection.Execute("DELETE FROM best_results;");
        using (SqliteStatement statement = connection.Prepare(
            "INSERT INTO best_results" +
            " (image_id, difficulty, best_score, best_seconds, best_medal, completions)" +
            " VALUES (?, ?, ?, ?, ?, ?);"))
        {
            for (int i = 0; i < state.BestResults.Count; i++)
            {
                PersistedBestResult result = state.BestResults[i];
                statement
                    .BindText(1, result.ImageId)
                    .BindInt(2, result.Difficulty)
                    .BindInt(3, result.BestScore)
                    .BindFloat(4, result.BestSeconds)
                    .BindInt(5, result.BestMedal)
                    .BindInt(6, result.Completions)
                    .Step();
            }
        }
    }

    private long InsertSessionRecordCore(PuzzleSessionRecord record, DateTime completedAtUtc)
    {
        record.CompletedAtUtc = completedAtUtc;
        using SqliteStatement statement = connection.Prepare(
            "INSERT INTO session_history (" +
            " completed_at_utc, image_id, difficulty, cut_style, layout_divisions, total_pieces," +
            " cut_seed, tray_seed, elapsed_seconds, moves_started, incorrect_attempts," +
            " hints_used, score, medal, is_new_record)" +
            " VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);");
        statement
            .BindText(1, FormatUtc(completedAtUtc))
            .BindText(2, record.ImageId)
            .BindInt(3, record.Difficulty)
            .BindInt(4, record.CutStyle)
            .BindInt(5, record.LayoutDivisions)
            .BindInt(6, record.TotalPieces)
            .BindInt(7, record.CutSeed)
            .BindInt(8, record.TraySeed)
            .BindFloat(9, record.ElapsedSeconds)
            .BindInt(10, record.MovesStarted)
            .BindInt(11, record.IncorrectAttempts)
            .BindInt(12, record.HintsUsed)
            .BindInt(13, record.Score)
            .BindInt(14, record.Medal)
            .BindInt(15, record.IsNewRecord ? 1 : 0)
            .Step();
        return connection.LastInsertRowId;
    }

    private void ApplyAchievementProgressCore(
        PuzzleAchievementProgressChange change,
        DateTime completedAtUtc,
        List<string> unlockedIds)
    {
        AchievementState current = GetAchievement(change.AchievementId);
        int currentProgress = current?.Progress ?? 0;
        if (currentProgress != change.PreviousProgress)
            throw new InvalidOperationException(
                $"Achievement '{change.AchievementId}' changed during completion persistence.");
        if (current != null && current.IsUnlocked)
            throw new InvalidOperationException(
                $"Unlocked achievement '{change.AchievementId}' cannot receive more progress.");

        bool unlock = change.Progress == change.Target;
        string unlockedAtUtc = unlock ? FormatUtc(completedAtUtc) : null;
        using (SqliteStatement statement = connection.Prepare(
            "INSERT INTO achievements (achievement_id, unlocked_at_utc, progress)" +
            " VALUES (?, ?, ?) ON CONFLICT(achievement_id) DO UPDATE SET" +
            " unlocked_at_utc = excluded.unlocked_at_utc," +
            " progress = excluded.progress;"))
        {
            statement.BindText(1, change.AchievementId);
            if (unlockedAtUtc == null) statement.BindNull(2);
            else statement.BindText(2, unlockedAtUtc);
            statement.BindInt(3, change.Progress).Step();
        }

        if (!unlock) return;
        using (SqliteStatement statement = connection.Prepare(
            "INSERT INTO achievement_notifications" +
            " (achievement_id, created_at_utc, acknowledged_at_utc) VALUES (?, ?, NULL);"))
        {
            statement
                .BindText(1, change.AchievementId)
                .BindText(2, unlockedAtUtc)
                .Step();
        }
        unlockedIds.Add(change.AchievementId);
    }

    private void EnsureProfileInitializationMarkerCore()
    {
        string value = GetMeta("profile_initialized");
        if (value == "1") return;
        if (value != null)
            throw new InvalidOperationException(
                $"SQLite profile initialization marker has invalid value '{value}'.");
        using SqliteStatement statement = connection.Prepare(
            "INSERT INTO meta (key, value) VALUES ('profile_initialized', '1');");
        statement.Step();
    }

    private void RunTransaction(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        connection.BeginTransaction();
        try
        {
            action.Invoke();
            connection.Commit();
        }
        catch (Exception transactionException)
        {
            try
            {
                connection.Rollback();
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "SQLite transaction failed and could not be rolled back.",
                    transactionException,
                    rollbackException);
            }
            throw;
        }
    }

    private int ReadUserVersion()
    {
        using SqliteStatement statement = connection.Prepare("PRAGMA user_version;");
        if (!statement.Step())
            throw new InvalidOperationException("SQLite user_version returned no row.");
        return statement.ColumnInt(0);
    }

    private void WriteUserVersion(int version) =>
        connection.Execute($"PRAGMA user_version = {version};");

    private void Migrate()
    {
        int currentVersion = ReadUserVersion();
        if (currentVersion > SchemaVersion)
            throw new InvalidOperationException(
                $"Database schema version {currentVersion} is newer than supported version {SchemaVersion}.");

        switch (currentVersion)
        {
            case 0:
                RunTransaction(() =>
                {
                    connection.Execute(SchemaV2Sql);
                    WriteUserVersion(SchemaVersion);
                });
                break;
            case 1:
                VerifySchemaV1();
                VerifyVersionOneAchievementRows();
                RunTransaction(() =>
                {
                    connection.Execute(MigrationV1ToV2Sql);
                    WriteUserVersion(SchemaVersion);
                });
                break;
            case SchemaVersion:
                break;
            default:
                throw new InvalidOperationException(
                    $"Database schema version {currentVersion} has no canonical migration.");
        }
    }

    private void VerifySchemaV1()
    {
        RequireExactColumns("meta", new[] { "key", "value" });
        RequireExactColumns("progress_state", ProgressStateColumns);
        RequireExactColumns("unlocked_images", new[] { "image_id", "position" });
        RequireExactColumns("completed_images", new[] { "image_id" });
        RequireExactColumns(
            "best_results",
            new[]
            {
                "image_id", "difficulty", "best_score", "best_seconds", "best_medal", "completions",
            });
        RequireExactColumns("session_history", SessionColumnsForSchema);
        RequireExactColumns(
            "achievements",
            new[] { "achievement_id", "unlocked_at_utc", "progress", "target" });
    }

    private void VerifySchemaV2()
    {
        if (ReadUserVersion() != SchemaVersion)
            throw new InvalidOperationException("SQLite schema version was not committed.");
        RequireExactColumns("meta", new[] { "key", "value" });
        RequireExactColumns("progress_state", ProgressStateColumns);
        RequireExactColumns("unlocked_images", new[] { "image_id", "position" });
        RequireExactColumns("completed_images", new[] { "image_id" });
        RequireExactColumns(
            "best_results",
            new[]
            {
                "image_id", "difficulty", "best_score", "best_seconds", "best_medal", "completions",
            });
        RequireExactColumns("session_history", SessionColumnsForSchema);
        RequireExactColumns(
            "achievements",
            new[] { "achievement_id", "unlocked_at_utc", "progress" });
        RequireExactColumns(
            "achievement_notifications",
            new[] { "id", "achievement_id", "created_at_utc", "acknowledged_at_utc" });
    }

    private void VerifyVersionOneAchievementRows()
    {
        using SqliteStatement statement = connection.Prepare(
            "SELECT achievement_id, unlocked_at_utc, progress, target FROM achievements;");
        while (statement.Step())
        {
            string id = statement.ColumnText(0);
            bool unlocked = !statement.IsNull(1);
            int progress = statement.ColumnInt(2);
            int target = statement.ColumnInt(3);
            if (string.IsNullOrWhiteSpace(id) || target <= 0 || progress < 0 || progress > target ||
                (unlocked && progress != target) || (!unlocked && progress >= target))
                throw new InvalidOperationException(
                    $"Version 1 achievement '{id}' is inconsistent and cannot be migrated.");
        }
    }

    private void VerifyIntegrity()
    {
        using (SqliteStatement statement = connection.Prepare("PRAGMA quick_check;"))
        {
            if (!statement.Step() || statement.ColumnText(0) != "ok" || statement.Step())
                throw new InvalidOperationException("SQLite quick_check reported database corruption.");
        }
        using (SqliteStatement statement = connection.Prepare("PRAGMA foreign_key_check;"))
        {
            if (statement.Step())
                throw new InvalidOperationException("SQLite foreign_key_check reported invalid rows.");
        }
    }

    private void RequireExactColumns(string table, IReadOnlyList<string> expected)
    {
        var actual = new List<string>();
        using (SqliteStatement statement = connection.Prepare($"PRAGMA table_info('{table}');"))
        {
            while (statement.Step()) actual.Add(statement.ColumnText(1));
        }
        if (actual.Count != expected.Count)
            throw new InvalidOperationException(
                $"SQLite table '{table}' has {actual.Count} columns; expected {expected.Count}.");
        for (int i = 0; i < expected.Count; i++)
        {
            if (actual[i] != expected[i])
                throw new InvalidOperationException(
                    $"SQLite table '{table}' column {i} is '{actual[i]}'; expected '{expected[i]}'.");
        }
    }

    private List<string> QueryTextColumn(string sql)
    {
        var values = new List<string>();
        using SqliteStatement statement = connection.Prepare(sql);
        while (statement.Step()) values.Add(statement.ColumnText(0));
        return values;
    }

    private List<PuzzleSessionRecord> QuerySessions(
        string sql,
        Action<SqliteStatement> bindParameters)
    {
        if (bindParameters == null) throw new ArgumentNullException(nameof(bindParameters));
        var records = new List<PuzzleSessionRecord>();
        using SqliteStatement statement = connection.Prepare(sql);
        bindParameters.Invoke(statement);
        while (statement.Step()) records.Add(MapSessionRecord(statement));
        return records;
    }

    private static PuzzleSessionRecord MapSessionRecord(SqliteStatement statement) =>
        new PuzzleSessionRecord
        {
            Id = statement.ColumnLong(0),
            CompletedAtUtc = ParseUtc(statement.ColumnText(1)),
            ImageId = statement.ColumnText(2),
            Difficulty = statement.ColumnInt(3),
            CutStyle = statement.ColumnInt(4),
            LayoutDivisions = statement.ColumnInt(5),
            TotalPieces = statement.ColumnInt(6),
            CutSeed = statement.ColumnInt(7),
            TraySeed = statement.ColumnInt(8),
            ElapsedSeconds = statement.ColumnFloat(9),
            MovesStarted = statement.ColumnInt(10),
            IncorrectAttempts = statement.ColumnInt(11),
            HintsUsed = statement.ColumnInt(12),
            Score = statement.ColumnInt(13),
            Medal = statement.ColumnInt(14),
            IsNewRecord = statement.ColumnInt(15) != 0,
        };

    private static AchievementState MapAchievement(SqliteStatement statement)
    {
        string unlockedAtUtc = statement.IsNull(1) ? null : statement.ColumnText(1);
        return new AchievementState
        {
            AchievementId = statement.ColumnText(0),
            UnlockedAtUtc = unlockedAtUtc == null ? null : (DateTime?)ParseUtc(unlockedAtUtc),
            Progress = statement.ColumnInt(2),
        };
    }

    private static void ValidateProgressState(PersistedProgressState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        RequireText(state.LastSelectedImageId, nameof(state.LastSelectedImageId));
        if (!Enum.IsDefined(typeof(PuzzleDifficulty), state.LastSelectedDifficulty))
            throw new ArgumentOutOfRangeException(nameof(state.LastSelectedDifficulty));
        if (!Enum.IsDefined(typeof(PuzzleCutStyle), state.LastSelectedCutStyle))
            throw new ArgumentOutOfRangeException(nameof(state.LastSelectedCutStyle));
        if (state.UnlockedImageIds == null ||
            state.CompletedImageIds == null ||
            state.BestResults == null)
            throw new ArgumentException("Persisted state collections are missing.", nameof(state));

        ValidateUniqueIds(state.UnlockedImageIds, nameof(state.UnlockedImageIds));
        ValidateUniqueIds(state.CompletedImageIds, nameof(state.CompletedImageIds));
        var bestKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < state.BestResults.Count; i++)
        {
            PersistedBestResult result = state.BestResults[i] ??
                throw new ArgumentException($"Persisted best result {i} is missing.", nameof(state));
            RequireText(result.ImageId, nameof(result.ImageId));
            if (!Enum.IsDefined(typeof(PuzzleDifficulty), result.Difficulty) ||
                !Enum.IsDefined(typeof(PuzzleMedal), result.BestMedal) ||
                result.BestScore < 0 ||
                !float.IsFinite(result.BestSeconds) || result.BestSeconds < 0f ||
                result.Completions <= 0)
                throw new ArgumentException($"Persisted best result {i} is invalid.", nameof(state));
            string key = result.ImageId + "\n" + result.Difficulty;
            if (!bestKeys.Add(key))
                throw new ArgumentException($"Persisted best result {i} is duplicated.", nameof(state));
        }
    }

    private static void ValidateSessionRecord(PuzzleSessionRecord record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        RequireText(record.ImageId, nameof(record.ImageId));
        if (!Enum.IsDefined(typeof(PuzzleDifficulty), record.Difficulty))
            throw new ArgumentOutOfRangeException(nameof(record.Difficulty));
        if (!Enum.IsDefined(typeof(PuzzleCutStyle), record.CutStyle))
            throw new ArgumentOutOfRangeException(nameof(record.CutStyle));
        if (!Enum.IsDefined(typeof(PuzzleMedal), record.Medal))
            throw new ArgumentOutOfRangeException(nameof(record.Medal));
        if (record.LayoutDivisions <= 0 ||
            record.TotalPieces != checked(record.LayoutDivisions * record.LayoutDivisions))
            throw new ArgumentException(
                "Session piece count does not match its layout divisions.", nameof(record));
        if (record.CutSeed <= 0 || record.TraySeed <= 0)
            throw new ArgumentException("Session seeds must be positive.", nameof(record));
        if (!float.IsFinite(record.ElapsedSeconds) || record.ElapsedSeconds < 0f ||
            record.MovesStarted < 0 || record.IncorrectAttempts < 0 ||
            record.HintsUsed < 0 || record.Score < 0)
            throw new ArgumentException("Session statistics are out of range.", nameof(record));
    }

    private static void ValidateAchievementChanges(
        IReadOnlyList<PuzzleAchievementProgressChange> changes)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < changes.Count; i++)
        {
            PuzzleAchievementProgressChange change = changes[i] ??
                throw new ArgumentException($"Achievement change {i} is missing.", nameof(changes));
            if (!ids.Add(change.AchievementId))
                throw new ArgumentException(
                    $"Achievement change '{change.AchievementId}' is duplicated.", nameof(changes));
        }
    }

    private static void ValidateUniqueIds(IReadOnlyList<string> values, string parameterName)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
        {
            RequireText(values[i], parameterName);
            if (!ids.Add(values[i]))
                throw new ArgumentException($"Identifier '{values[i]}' is duplicated.", parameterName);
        }
    }

    private static void RequirePositiveLimit(int limit)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A non-empty value is required.", parameterName);
    }

    private static string FormatUtc(DateTime value) =>
        value.ToUniversalTime().ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    private static DateTime ParseUtc(string value)
    {
        DateTime parsed = DateTime.ParseExact(
            value,
            DateTimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);
        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }
}
