using System;

public sealed class PuzzleScoreBreakdown
{
    public PuzzleScoreBreakdown(
        int baseScore,
        int complexityBonus,
        int rotationBonus,
        int referenceBonus,
        int timePenalty,
        int errorPenalty,
        int hintPenalty)
    {
        if (baseScore <= 0) throw new ArgumentOutOfRangeException(nameof(baseScore));
        if (complexityBonus < 0) throw new ArgumentOutOfRangeException(nameof(complexityBonus));
        if (rotationBonus < 0) throw new ArgumentOutOfRangeException(nameof(rotationBonus));
        if (referenceBonus < 0) throw new ArgumentOutOfRangeException(nameof(referenceBonus));
        if (timePenalty < 0) throw new ArgumentOutOfRangeException(nameof(timePenalty));
        if (errorPenalty < 0) throw new ArgumentOutOfRangeException(nameof(errorPenalty));
        if (hintPenalty < 0) throw new ArgumentOutOfRangeException(nameof(hintPenalty));

        BaseScore = baseScore;
        ComplexityBonus = complexityBonus;
        RotationBonus = rotationBonus;
        ReferenceBonus = referenceBonus;
        TimePenalty = timePenalty;
        ErrorPenalty = errorPenalty;
        HintPenalty = hintPenalty;
        Total = checked(
            baseScore + complexityBonus + rotationBonus + referenceBonus -
            timePenalty - errorPenalty - hintPenalty);
    }

    public int BaseScore { get; }

    public int ComplexityBonus { get; }

    public int RotationBonus { get; }

    public int ReferenceBonus { get; }

    public int TimePenalty { get; }

    public int ErrorPenalty { get; }

    public int HintPenalty { get; }

    public int Total { get; }

    public string BuildDetails() =>
        $"PONTUAÇÃO  {Total:N0}\n\n" +
        $"Base pelas peças             +{BaseScore:N0}\n" +
        $"Complexidade do formato        +{ComplexityBonus:N0}\n" +
        $"Desafio de rotação           +{RotationBonus:N0}\n" +
        $"Referência reduzida           +{ReferenceBonus:N0}\n" +
        $"Tempo                         -{TimePenalty:N0}\n" +
        $"Tentativas incorretas         -{ErrorPenalty:N0}\n" +
        $"Dicas utilizadas              -{HintPenalty:N0}";
}

public static class PuzzleScoreCalculator
{
    private const int BasePerPiece = 1000;
    private const int RotationBonusPerPiece = 250;
    private const int MaximumReferenceBonusPerPiece = 200;
    private const int TimePenaltyPerSecond = 4;
    private const int ErrorPenalty = 200;
    private const int HintPenalty = 500;

    public static PuzzleScoreBreakdown Calculate(
        PuzzleSessionDefinition session,
        PuzzleSessionMetrics metrics)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (metrics == null || !metrics.IsActive || !metrics.IsComplete)
            throw new ArgumentException("Completed session metrics are required.", nameof(metrics));

        int baseScore = checked(metrics.TotalPieces * BasePerPiece);
        int complexityPercent = ComplexityPercent(session.CutStyle);
        int complexityBonus = checked(baseScore * (complexityPercent - 100) / 100);
        int rotationBonus = session.Difficulty.RotationEnabled
            ? checked(metrics.TotalPieces * RotationBonusPerPiece)
            : 0;
        int referenceBonus = checked((int)Math.Round(
            metrics.TotalPieces *
            (1d - session.Difficulty.ReferenceOpacity) *
            MaximumReferenceBonusPerPiece,
            MidpointRounding.AwayFromZero));
        int timePenalty = checked(
            (int)Math.Ceiling(metrics.ElapsedSeconds) * TimePenaltyPerSecond);
        int errorPenalty = checked(metrics.IncorrectAttempts * ErrorPenalty);
        int hintPenalty = checked(metrics.HintsUsed * HintPenalty);

        return new PuzzleScoreBreakdown(
            baseScore,
            complexityBonus,
            rotationBonus,
            referenceBonus,
            timePenalty,
            errorPenalty,
            hintPenalty);
    }

    public static int ComplexityPercent(PuzzleCutStyle style) => style switch
    {
        PuzzleCutStyle.Square => 100,
        PuzzleCutStyle.Round => 110,
        PuzzleCutStyle.Ellipse => 112,
        PuzzleCutStyle.Rectangle => 115,
        PuzzleCutStyle.Hexagon => 118,
        PuzzleCutStyle.Triangle => 120,
        PuzzleCutStyle.Diamond => 122,
        PuzzleCutStyle.Wave => 125,
        PuzzleCutStyle.Zigzag => 130,
        PuzzleCutStyle.DoubleLobe => 132,
        PuzzleCutStyle.Organic => 138,
        PuzzleCutStyle.Procedural => 145,
        PuzzleCutStyle.FullyRandom => 155,
        _ => throw new ArgumentOutOfRangeException(nameof(style)),
    };
}
