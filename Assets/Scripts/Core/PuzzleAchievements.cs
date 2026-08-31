using System;
using System.Collections.Generic;

public enum PuzzleAchievementMetric
{
    TotalCompletions,
    UniqueImagesCompleted,
    PerfectCompletions,
    HintlessCompletions,
    DifficultyCompletions,
    CutStyleCompletions,
    HighestScore,
    FastCompletions,
}

[Serializable]
public sealed class PuzzleAchievementDefinition
{
    [UnityEngine.SerializeField] private string id;
    [UnityEngine.SerializeField] private string title;
    [UnityEngine.SerializeField] private string description;
    [UnityEngine.SerializeField] private PuzzleAchievementMetric metric;
    [UnityEngine.SerializeField, UnityEngine.Min(1)] private int target = 1;
    [UnityEngine.SerializeField] private PuzzleDifficulty difficulty;
    [UnityEngine.SerializeField] private PuzzleCutStyle cutStyle;
    [UnityEngine.SerializeField, UnityEngine.Min(0f)] private float maximumSeconds;
    [UnityEngine.SerializeField] private bool hidden;

    public string Id => id;

    public string Title => title;

    public string Description => description;

    public PuzzleAchievementMetric Metric => metric;

    public int Target => target;

    public PuzzleDifficulty Difficulty => difficulty;

    public PuzzleCutStyle CutStyle => cutStyle;

    public float MaximumSeconds => maximumSeconds;

    public bool Hidden => hidden;

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "achievement id is empty";
            return false;
        }
        for (int i = 0; i < id.Length; i++)
        {
            char character = id[i];
            if ((character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9') ||
                character == '_')
                continue;
            error = $"achievement id '{id}' must use lowercase ASCII letters, digits, or underscores";
            return false;
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            error = $"achievement '{id}' title is empty";
            return false;
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            error = $"achievement '{id}' description is empty";
            return false;
        }
        if (!Enum.IsDefined(typeof(PuzzleAchievementMetric), metric))
        {
            error = $"achievement '{id}' metric is invalid";
            return false;
        }
        if (target <= 0)
        {
            error = $"achievement '{id}' target must be positive";
            return false;
        }
        if (metric == PuzzleAchievementMetric.DifficultyCompletions &&
            !Enum.IsDefined(typeof(PuzzleDifficulty), difficulty))
        {
            error = $"achievement '{id}' difficulty is invalid";
            return false;
        }
        if (metric == PuzzleAchievementMetric.CutStyleCompletions &&
            !Enum.IsDefined(typeof(PuzzleCutStyle), cutStyle))
        {
            error = $"achievement '{id}' cut style is invalid";
            return false;
        }
        if (metric == PuzzleAchievementMetric.FastCompletions)
        {
            if (!float.IsFinite(maximumSeconds) || maximumSeconds <= 0f)
            {
                error = $"achievement '{id}' maximum time must be finite and positive";
                return false;
            }
        }
        else if (!float.IsFinite(maximumSeconds) || maximumSeconds != 0f)
        {
            error = $"achievement '{id}' defines a maximum time for a metric that does not use it";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public sealed class PuzzleAchievementProgressChange
{
    public PuzzleAchievementProgressChange(
        string achievementId,
        int previousProgress,
        int progress,
        int target)
    {
        if (string.IsNullOrWhiteSpace(achievementId))
            throw new ArgumentException("Achievement id is required.", nameof(achievementId));
        if (previousProgress < 0)
            throw new ArgumentOutOfRangeException(nameof(previousProgress));
        if (progress <= previousProgress)
            throw new ArgumentOutOfRangeException(
                nameof(progress),
                "Achievement progress changes must be strictly monotonic.");
        if (target <= 0 || progress > target)
            throw new ArgumentOutOfRangeException(nameof(target));

        AchievementId = achievementId;
        PreviousProgress = previousProgress;
        Progress = progress;
        Target = target;
    }

    public string AchievementId { get; }

    public int PreviousProgress { get; }

    public int Progress { get; }

    public int Target { get; }
}

public sealed class PuzzleAchievementCatalogEntry
{
    public PuzzleAchievementCatalogEntry(
        PuzzleAchievementDefinition definition,
        int progress,
        DateTime? unlockedAtUtc)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (progress < 0 || progress > definition.Target)
            throw new ArgumentOutOfRangeException(nameof(progress));
        Progress = progress;
        UnlockedAtUtc = unlockedAtUtc;
    }

    public PuzzleAchievementDefinition Definition { get; }

    public int Progress { get; }

    public DateTime? UnlockedAtUtc { get; }

    public bool IsUnlocked => UnlockedAtUtc != null;
}

public sealed class PuzzleAchievementNotification
{
    public PuzzleAchievementNotification(
        long notificationId,
        PuzzleAchievementDefinition definition,
        DateTime createdAtUtc)
    {
        if (notificationId <= 0) throw new ArgumentOutOfRangeException(nameof(notificationId));
        NotificationId = notificationId;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        CreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Utc
            ? createdAtUtc
            : throw new ArgumentException("Notification time must be UTC.", nameof(createdAtUtc));
    }

    public long NotificationId { get; }

    public PuzzleAchievementDefinition Definition { get; }

    public DateTime CreatedAtUtc { get; }
}

public static class PuzzleAchievementEvaluator
{
    public static IReadOnlyList<PuzzleAchievementProgressChange> EvaluateCompletion(
        IReadOnlyList<PuzzleAchievementDefinition> definitions,
        IReadOnlyDictionary<string, AchievementState> states,
        PuzzleProgressData progress,
        PuzzleSessionDefinition session,
        PuzzleSessionMetrics metrics,
        PuzzleScoreBreakdown score)
    {
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));
        if (states == null) throw new ArgumentNullException(nameof(states));
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (metrics == null || !metrics.IsActive || !metrics.IsComplete)
            throw new ArgumentException("Completed session metrics are required.", nameof(metrics));
        if (score == null) throw new ArgumentNullException(nameof(score));

        var changes = new List<PuzzleAchievementProgressChange>();
        for (int i = 0; i < definitions.Count; i++)
        {
            PuzzleAchievementDefinition definition = definitions[i] ??
                throw new InvalidOperationException($"Achievement definition {i} is missing.");
            if (!definition.TryValidate(out string error))
                throw new InvalidOperationException($"Invalid achievement definition: {error}.");

            states.TryGetValue(definition.Id, out AchievementState state);
            int current = state?.Progress ?? 0;
            if (current < 0 || current > definition.Target)
                throw new InvalidOperationException(
                    $"Persisted achievement '{definition.Id}' has invalid progress {current}.");
            if (state != null && state.IsUnlocked)
            {
                if (current != definition.Target)
                    throw new InvalidOperationException(
                        $"Unlocked achievement '{definition.Id}' is not at its target.");
                continue;
            }

            int evaluated = Evaluate(definition, current, progress, session, metrics, score);
            evaluated = Math.Min(definition.Target, evaluated);
            if (evaluated > current)
                changes.Add(new PuzzleAchievementProgressChange(
                    definition.Id,
                    current,
                    evaluated,
                    definition.Target));
        }
        return changes;
    }

    private static int Evaluate(
        PuzzleAchievementDefinition definition,
        int current,
        PuzzleProgressData progress,
        PuzzleSessionDefinition session,
        PuzzleSessionMetrics metrics,
        PuzzleScoreBreakdown score)
    {
        return definition.Metric switch
        {
            PuzzleAchievementMetric.TotalCompletions => checked(current + 1),
            PuzzleAchievementMetric.UniqueImagesCompleted => progress.UniqueCompletedImages,
            PuzzleAchievementMetric.PerfectCompletions =>
                metrics.IncorrectAttempts == 0 && metrics.HintsUsed == 0
                    ? checked(current + 1)
                    : current,
            PuzzleAchievementMetric.HintlessCompletions =>
                metrics.HintsUsed == 0 ? checked(current + 1) : current,
            PuzzleAchievementMetric.DifficultyCompletions =>
                session.Difficulty.Difficulty == definition.Difficulty
                    ? checked(current + 1)
                    : current,
            PuzzleAchievementMetric.CutStyleCompletions =>
                session.CutStyle == definition.CutStyle ? checked(current + 1) : current,
            PuzzleAchievementMetric.HighestScore => Math.Max(current, score.Total),
            PuzzleAchievementMetric.FastCompletions =>
                metrics.ElapsedSeconds <= definition.MaximumSeconds
                    ? checked(current + 1)
                    : current,
            _ => throw new ArgumentOutOfRangeException(nameof(definition)),
        };
    }
}
