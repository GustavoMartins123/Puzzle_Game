using System;
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

public sealed class PuzzleProgressStore
{
    public const string DefaultKey = "Puzzle.Progress.v1";

    private readonly string key;

    public PuzzleProgressStore() : this(DefaultKey)
    {
    }

    public PuzzleProgressStore(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("PlayerPrefs progress key is required.", nameof(key));
        this.key = key;
    }

    public string Key => key;

    public PuzzleProgressData LoadOrCreate(PuzzleConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (PlayerPrefs.HasKey(key))
            return PuzzleProgressSerializer.Deserialize(PlayerPrefs.GetString(key), config);

        PuzzleProgressData progress = PuzzleProgressData.CreateNew(config);
        Save(progress, config);
        return progress;
    }

    public void Save(PuzzleProgressData progress, PuzzleConfig config)
    {
        string json = PuzzleProgressSerializer.Serialize(progress, config);
        PuzzleProgressSerializer.Deserialize(json, config);

        bool hadPreviousValue = PlayerPrefs.HasKey(key);
        string previousValue = hadPreviousValue ? PlayerPrefs.GetString(key) : null;
        try
        {
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
            string persisted = PlayerPrefs.GetString(key);
            if (!string.Equals(persisted, json, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"PlayerPrefs did not persist progress key '{key}' exactly.");
            PuzzleProgressSerializer.Deserialize(persisted, config);
        }
        catch (Exception saveException)
        {
            try
            {
                if (hadPreviousValue) PlayerPrefs.SetString(key, previousValue);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    $"Progress save and rollback both failed for PlayerPrefs key '{key}'.",
                    saveException,
                    rollbackException);
            }

            throw;
        }
    }
}
