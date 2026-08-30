using System;
using System.Collections.Generic;
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
    public int Target;

    public bool IsUnlocked => UnlockedAtUtc != null;
}

public sealed class GameDatabase : IDisposable
{
    public const int SchemaVersion = 1;

    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

    private const string SchemaV1Sql =
        "CREATE TABLE IF NOT EXISTS meta (" +
        " key TEXT PRIMARY KEY NOT NULL," +
        " value TEXT NOT NULL);" +

        "CREATE TABLE IF NOT EXISTS progress_state (" +
        " id INTEGER PRIMARY KEY CHECK (id = 1)," +
        " last_selected_image_id TEXT NOT NULL," +
        " last_selected_difficulty INTEGER NOT NULL," +
        " last_selected_cut_style INTEGER NOT NULL);" +

        "CREATE TABLE IF NOT EXISTS unlocked_images (" +
        " image_id TEXT PRIMARY KEY NOT NULL," +
        " position INTEGER NOT NULL UNIQUE);" +

        "CREATE TABLE IF NOT EXISTS completed_images (" +
        " image_id TEXT PRIMARY KEY NOT NULL);" +

        "CREATE TABLE IF NOT EXISTS best_results (" +
        " image_id TEXT NOT NULL," +
        " difficulty INTEGER NOT NULL," +
        " best_score INTEGER NOT NULL," +
        " best_seconds REAL NOT NULL," +
        " best_medal INTEGER NOT NULL," +
        " completions INTEGER NOT NULL," +
        " PRIMARY KEY (image_id, difficulty));" +

        "CREATE TABLE IF NOT EXISTS session_history (" +
        " id INTEGER PRIMARY KEY AUTOINCREMENT," +
        " completed_at_utc TEXT NOT NULL," +
        " image_id TEXT NOT NULL," +
        " difficulty INTEGER NOT NULL," +
        " cut_style INTEGER NOT NULL," +
        " layout_divisions INTEGER NOT NULL," +
        " total_pieces INTEGER NOT NULL," +
        " cut_seed INTEGER NOT NULL," +
        " tray_seed INTEGER NOT NULL," +
        " elapsed_seconds REAL NOT NULL," +
        " moves_started INTEGER NOT NULL," +
        " incorrect_attempts INTEGER NOT NULL," +
        " hints_used INTEGER NOT NULL," +
        " score INTEGER NOT NULL," +
        " medal INTEGER NOT NULL," +
        " is_new_record INTEGER NOT NULL);" +

        "CREATE INDEX IF NOT EXISTS idx_session_history_completed_at" +
        " ON session_history (completed_at_utc);" +

        "CREATE INDEX IF NOT EXISTS idx_session_history_score" +
        " ON session_history (score);" +

        "CREATE INDEX IF NOT EXISTS idx_session_history_image" +
        " ON session_history (image_id);" +

        "CREATE TABLE IF NOT EXISTS achievements (" +
        " achievement_id TEXT PRIMARY KEY NOT NULL," +
        " unlocked_at_utc TEXT," +
        " progress INTEGER NOT NULL DEFAULT 0," +
        " target INTEGER NOT NULL DEFAULT 1);";

    private readonly SqliteConnection connection;

    public static string DefaultDatabasePath =>
        System.IO.Path.Combine(Application.persistentDataPath, "puzzle_game.db");

    public GameDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Database path is required.", nameof(path));
        connection = new SqliteConnection(path);
        Migrate();
    }

    public void Dispose()
    {
        connection.Dispose();
    }

    public string GetMeta(string key)
    {
        RequireMetaKey(key);
        using SqliteStatement statement = connection.Prepare(
            "SELECT value FROM meta WHERE key = ?;");
        statement.BindText(1, key);
        return statement.Step() ? statement.ColumnText(0) : null;
    }

    public void SetMeta(string key, string value)
    {
        RequireMetaKey(key);
        if (value == null) throw new ArgumentNullException(nameof(value));
        using SqliteStatement statement = connection.Prepare(
            "INSERT INTO meta (key, value) VALUES (?, ?)" +
            " ON CONFLICT(key) DO UPDATE SET value = excluded.value;");
        statement.BindText(1, key);
        statement.BindText(2, value);
        statement.Step();
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
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (string.IsNullOrWhiteSpace(state.LastSelectedImageId))
            throw new ArgumentException("Persisted state requires a selected image.", nameof(state));
        if (state.UnlockedImageIds == null ||
            state.CompletedImageIds == null ||
            state.BestResults == null)
            throw new ArgumentException("Persisted state collections are missing.", nameof(state));

        connection.BeginTransaction();
        try
        {
            using (SqliteStatement statement = connection.Prepare(
                "INSERT INTO progress_state (id, last_selected_image_id, last_selected_difficulty, last_selected_cut_style)" +
                " VALUES (1, ?, ?, ?)" +
                " ON CONFLICT(id) DO UPDATE SET" +
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
                {
                    statement
                        .BindText(1, state.UnlockedImageIds[i])
                        .BindInt(2, i)
                        .Step();
                }
            }

            connection.Execute("DELETE FROM completed_images;");
            using (SqliteStatement statement = connection.Prepare(
                "INSERT INTO completed_images (image_id) VALUES (?);"))
            {
                for (int i = 0; i < state.CompletedImageIds.Count; i++)
                {
                    statement
                        .BindText(1, state.CompletedImageIds[i])
                        .Step();
                }
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
                    if (result == null)
                        throw new ArgumentException($"Persisted best result {i} is missing.", nameof(state));
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

            connection.Commit();
        }
        catch
        {
            connection.Rollback();
            throw;
        }
    }

    public long InsertSessionRecord(PuzzleSessionRecord record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        ValidateSessionRecord(record);

        record.CompletedAtUtc = DateTime.UtcNow;
        using SqliteStatement statement = connection.Prepare(
            "INSERT INTO session_history (" +
            " completed_at_utc, image_id, difficulty, cut_style, layout_divisions, total_pieces," +
            " cut_seed, tray_seed, elapsed_seconds, moves_started, incorrect_attempts," +
            " hints_used, score, medal, is_new_record)" +
            " VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);");
        statement
            .BindText(1, FormatUtc(record.CompletedAtUtc))
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

    public IReadOnlyList<PuzzleSessionRecord> GetRecentSessions(int limit)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        return QuerySessions(
            "SELECT " + SessionColumns +
            " FROM session_history ORDER BY completed_at_utc DESC, id DESC LIMIT ?;",
            statement => statement.BindInt(1, limit));
    }

    public IReadOnlyList<PuzzleSessionRecord> GetTopScoreSessions(int limit)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        return QuerySessions(
            "SELECT " + SessionColumns +
            " FROM session_history ORDER BY score DESC, elapsed_seconds ASC, id ASC LIMIT ?;",
            statement => statement.BindInt(1, limit));
    }

    public IReadOnlyList<PuzzleSessionRecord> GetBestSessionsForImage(
        string imageId,
        int limit)
    {
        if (string.IsNullOrWhiteSpace(imageId))
            throw new ArgumentException("Image id is required.", nameof(imageId));
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        return QuerySessions(
            "SELECT " + SessionColumns +
            " FROM session_history WHERE image_id = ?" +
            " ORDER BY score DESC, elapsed_seconds ASC, id ASC LIMIT ?;",
            statement =>
            {
                statement.BindText(1, imageId);
                statement.BindInt(2, limit);
            });
    }

    public int GetSessionCount()
    {
        using SqliteStatement statement = connection.Prepare(
            "SELECT COUNT(*) FROM session_history;");
        statement.Step();
        return statement.ColumnInt(0);
    }

    public bool TryUnlockAchievement(string achievementId)
    {
        return SetAchievementProgress(achievementId, 1, 1);
    }

    public bool SetAchievementProgress(string achievementId, int target, int progress)
    {
        RequireAchievementId(achievementId);
        if (target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
        if (progress < 0) throw new ArgumentOutOfRangeException(nameof(progress));

        connection.BeginTransaction();
        try
        {
            AchievementState current = GetAchievement(achievementId);
            bool alreadyUnlocked = current != null && current.IsUnlocked;
            bool newlyUnlocked = !alreadyUnlocked && progress >= target;
            string unlockedAtUtc = alreadyUnlocked
                ? FormatUtc(current.UnlockedAtUtc.Value)
                : newlyUnlocked ? FormatUtc(DateTime.UtcNow) : null;

            using (SqliteStatement statement = connection.Prepare(
                "INSERT INTO achievements (achievement_id, unlocked_at_utc, progress, target)" +
                " VALUES (?, ?, ?, ?)" +
                " ON CONFLICT(achievement_id) DO UPDATE SET" +
                " unlocked_at_utc = excluded.unlocked_at_utc," +
                " progress = excluded.progress," +
                " target = excluded.target;"))
            {
                statement.BindText(1, achievementId);
                if (unlockedAtUtc == null) statement.BindNull(2);
                else statement.BindText(2, unlockedAtUtc);
                statement
                    .BindInt(3, progress)
                    .BindInt(4, target)
                    .Step();
            }

            connection.Commit();
            return newlyUnlocked;
        }
        catch
        {
            connection.Rollback();
            throw;
        }
    }

    public AchievementState GetAchievement(string achievementId)
    {
        RequireAchievementId(achievementId);
        using SqliteStatement statement = connection.Prepare(
            "SELECT achievement_id, unlocked_at_utc, progress, target" +
            " FROM achievements WHERE achievement_id = ?;");
        statement.BindText(1, achievementId);
        return statement.Step() ? MapAchievement(statement) : null;
    }

    public IReadOnlyList<AchievementState> GetAchievements()
    {
        var achievements = new List<AchievementState>();
        using SqliteStatement statement = connection.Prepare(
            "SELECT achievement_id, unlocked_at_utc, progress, target" +
            " FROM achievements ORDER BY achievement_id;");
        while (statement.Step())
            achievements.Add(MapAchievement(statement));
        return achievements;
    }

    public IReadOnlyList<AchievementState> GetUnlockedAchievements()
    {
        var achievements = new List<AchievementState>();
        using SqliteStatement statement = connection.Prepare(
            "SELECT achievement_id, unlocked_at_utc, progress, target" +
            " FROM achievements WHERE unlocked_at_utc IS NOT NULL" +
            " ORDER BY unlocked_at_utc;");
        while (statement.Step())
            achievements.Add(MapAchievement(statement));
        return achievements;
    }

    private const string SessionColumns =
        "id, completed_at_utc, image_id, difficulty, cut_style, layout_divisions," +
        " total_pieces, cut_seed, tray_seed, elapsed_seconds, moves_started," +
        " incorrect_attempts, hints_used, score, medal, is_new_record";

    private int ReadUserVersion()
    {
        using SqliteStatement statement = connection.Prepare("PRAGMA user_version;");
        statement.Step();
        return statement.ColumnInt(0);
    }

    private void WriteUserVersion(int version)
    {
        connection.Execute($"PRAGMA user_version = {version};");
    }

    private void Migrate()
    {
        int currentVersion = ReadUserVersion();
        if (currentVersion > SchemaVersion)
            throw new InvalidOperationException(
                $"Database schema version {currentVersion} is newer than the supported version {SchemaVersion}.");
        if (currentVersion == SchemaVersion) return;

        connection.BeginTransaction();
        try
        {
            connection.Execute(SchemaV1Sql);
            WriteUserVersion(SchemaVersion);
            connection.Commit();
        }
        catch
        {
            connection.Rollback();
            throw;
        }
    }

    private List<string> QueryTextColumn(string sql)
    {
        var values = new List<string>();
        using SqliteStatement statement = connection.Prepare(sql);
        while (statement.Step())
            values.Add(statement.ColumnText(0));
        return values;
    }

    private List<PuzzleSessionRecord> QuerySessions(
        string sql,
        Action<SqliteStatement> bindParameters)
    {
        var records = new List<PuzzleSessionRecord>();
        using SqliteStatement statement = connection.Prepare(sql);
        bindParameters?.Invoke(statement);
        while (statement.Step())
            records.Add(MapSessionRecord(statement));
        return records;
    }

    private PuzzleSessionRecord MapSessionRecord(SqliteStatement statement)
    {
        return new PuzzleSessionRecord
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
    }

    private AchievementState MapAchievement(SqliteStatement statement)
    {
        string unlockedAtUtc = statement.IsNull(1) ? null : statement.ColumnText(1);
        return new AchievementState
        {
            AchievementId = statement.ColumnText(0),
            UnlockedAtUtc = unlockedAtUtc == null ? null : (DateTime?)ParseUtc(unlockedAtUtc),
            Progress = statement.ColumnInt(2),
            Target = statement.ColumnInt(3),
        };
    }

    private static void ValidateSessionRecord(PuzzleSessionRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.ImageId))
            throw new ArgumentException("Session record requires an image id.", nameof(record));
        if (!float.IsFinite(record.ElapsedSeconds) || record.ElapsedSeconds < 0f)
            throw new ArgumentException(
                "Session record requires a finite non-negative elapsed time.",
                nameof(record));
        if (record.TotalPieces <= 0)
            throw new ArgumentException(
                "Session record requires a positive piece count.",
                nameof(record));
        if (record.LayoutDivisions <= 0)
            throw new ArgumentException(
                "Session record requires a positive layout division.",
                nameof(record));
        if (record.CutSeed <= 0 || record.TraySeed <= 0)
            throw new ArgumentException("Session record seeds must be positive.", nameof(record));
        if (record.MovesStarted < 0 ||
            record.IncorrectAttempts < 0 ||
            record.HintsUsed < 0 ||
            record.Score <= 0)
            throw new ArgumentException(
                "Session record statistics are out of range.",
                nameof(record));
    }

    private static void RequireMetaKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Meta key is required.", nameof(key));
    }

    private static void RequireAchievementId(string achievementId)
    {
        if (string.IsNullOrWhiteSpace(achievementId))
            throw new ArgumentException("Achievement id is required.", nameof(achievementId));
    }

    private static string FormatUtc(DateTime value)
    {
        return value.ToUniversalTime().ToString(DateTimeFormat);
    }

    private static DateTime ParseUtc(string value)
    {
        DateTime parsed = DateTime.ParseExact(
            value,
            DateTimeFormat,
            System.Globalization.CultureInfo.InvariantCulture);
        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }
}
