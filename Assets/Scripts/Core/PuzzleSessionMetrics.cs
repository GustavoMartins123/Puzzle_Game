using System;

public sealed class PuzzleSessionMetrics
{
    public bool IsActive { get; private set; }

    public bool IsComplete => IsActive && PlacedPieces == TotalPieces;

    public float ElapsedSeconds { get; private set; }

    public int MovesStarted { get; private set; }

    public int CorrectPlacements { get; private set; }

    public int IncorrectAttempts { get; private set; }

    public int HintsUsed { get; private set; }

    public int PlacedPieces { get; private set; }

    public int TotalPieces { get; private set; }

    public void Begin(int totalPieces)
    {
        if (totalPieces <= 0) throw new ArgumentOutOfRangeException(nameof(totalPieces));

        ElapsedSeconds = 0f;
        MovesStarted = 0;
        CorrectPlacements = 0;
        IncorrectAttempts = 0;
        HintsUsed = 0;
        PlacedPieces = 0;
        TotalPieces = totalPieces;
        IsActive = true;
    }

    public void Reset()
    {
        IsActive = false;
        ElapsedSeconds = 0f;
        MovesStarted = 0;
        CorrectPlacements = 0;
        IncorrectAttempts = 0;
        HintsUsed = 0;
        PlacedPieces = 0;
        TotalPieces = 0;
    }

    public void Tick(float unscaledDeltaTime)
    {
        RequireActive();
        if (!float.IsFinite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
        if (!IsComplete) ElapsedSeconds += unscaledDeltaTime;
    }

    public void RecordMoveStarted()
    {
        RequireActive();
        if (IsComplete) throw new InvalidOperationException("The puzzle is already complete.");
        MovesStarted++;
    }

    public void RecordIncorrectAttempt()
    {
        RequireActive();
        if (IsComplete) throw new InvalidOperationException("The puzzle is already complete.");
        IncorrectAttempts++;
    }

    public void RecordCorrectPlacement()
    {
        RequireActive();
        if (IsComplete) throw new InvalidOperationException("The puzzle is already complete.");

        CorrectPlacements++;
        PlacedPieces++;
        if (PlacedPieces > TotalPieces)
            throw new InvalidOperationException("Placed piece count exceeded the session total.");
    }

    public void RecordHint()
    {
        RequireActive();
        if (IsComplete) throw new InvalidOperationException("The puzzle is already complete.");
        HintsUsed++;
    }

    private void RequireActive()
    {
        if (!IsActive) throw new InvalidOperationException("Session metrics are not active.");
    }
}
