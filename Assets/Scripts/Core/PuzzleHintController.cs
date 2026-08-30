using System;
using System.Collections.Generic;

public sealed class PuzzleHintController
{
    private readonly List<PuzzlePiece> pieces = new List<PuzzlePiece>();
    private readonly Dictionary<int, Slot> slotsById = new Dictionary<int, Slot>();

    private PuzzleDifficultyProfile difficulty;
    private PieceTrayController tray;
    private PuzzleSessionMetrics metrics;
    private float cooldownRemaining;
    private bool configured;

    public float CooldownRemaining => configured ? cooldownRemaining : 0f;

    public bool HasAvailableTarget
    {
        get
        {
            if (!configured) return false;
            for (int i = 0; i < pieces.Count; i++)
                if (!pieces[i].Placed) return true;
            return false;
        }
    }

    public bool HasFiniteLimit =>
        configured && difficulty.HintAllowance == PuzzleHintAllowance.Limited;

    public int RemainingHints => HasFiniteLimit
        ? difficulty.MaximumHints - metrics.HintsUsed
        : 0;

    public void Configure(
        PuzzleDifficultyProfile difficulty,
        IReadOnlyList<PuzzlePiece> pieces,
        IReadOnlyList<Slot> slots,
        PieceTrayController tray,
        PuzzleSessionMetrics metrics)
    {
        if (difficulty == null) throw new ArgumentNullException(nameof(difficulty));
        if (pieces == null || pieces.Count == 0)
            throw new ArgumentException("At least one piece is required.", nameof(pieces));
        if (slots == null || slots.Count != pieces.Count)
            throw new ArgumentException("Slots must match the piece count.", nameof(slots));
        if (tray == null) throw new ArgumentNullException(nameof(tray));
        if (metrics == null || !metrics.IsActive)
            throw new ArgumentException("Active session metrics are required.", nameof(metrics));

        this.pieces.Clear();
        slotsById.Clear();
        for (int i = 0; i < pieces.Count; i++)
        {
            PuzzlePiece piece = pieces[i];
            Slot slot = slots[i];
            if (piece == null) throw new ArgumentException($"Piece {i} is missing.", nameof(pieces));
            if (slot == null) throw new ArgumentException($"Slot {i} is missing.", nameof(slots));
            if (piece.Id != slot.Id)
                throw new ArgumentException(
                    $"Piece {piece.Id} does not match slot {slot.Id} at index {i}.");
            if (!slotsById.TryAdd(slot.Id, slot))
                throw new ArgumentException($"Slot id {slot.Id} is duplicated.", nameof(slots));
            this.pieces.Add(piece);
        }

        this.difficulty = difficulty;
        this.tray = tray;
        this.metrics = metrics;
        cooldownRemaining = 0f;
        configured = true;
    }

    public void Reset()
    {
        pieces.Clear();
        slotsById.Clear();
        difficulty = null;
        tray = null;
        metrics = null;
        cooldownRemaining = 0f;
        configured = false;
    }

    public void Tick(float unscaledDeltaTime)
    {
        RequireConfigured();
        if (!float.IsFinite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
        cooldownRemaining = Math.Max(0f, cooldownRemaining - unscaledDeltaTime);
    }

    public bool TryUseHint(out string message)
    {
        RequireConfigured();

        if (!difficulty.CanUseHint(metrics.HintsUsed))
        {
            message = difficulty.HintAllowance == PuzzleHintAllowance.Disabled
                ? "Dicas desativadas neste perfil."
                : "Limite de dicas atingido.";
            return false;
        }

        if (cooldownRemaining > 0f)
        {
            message = $"Dica disponível em {Math.Ceiling(cooldownRemaining):0}s.";
            return false;
        }

        PuzzlePiece target = null;
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].Placed) continue;
            target = pieces[i];
            break;
        }

        if (target == null)
        {
            message = "Não há peças pendentes.";
            return false;
        }

        if (!slotsById.TryGetValue(target.Id, out Slot slot))
            throw new InvalidOperationException($"Slot {target.Id} is missing from the current session.");

        tray.RevealPiece(target);
        target.PlayHint();
        slot.PlayHint();
        metrics.RecordHint();
        cooldownRemaining = difficulty.HintCooldown;
        message = "Peça e região correta destacadas.";
        return true;
    }

    private void RequireConfigured()
    {
        if (!configured) throw new InvalidOperationException("Hint controller is not configured.");
    }
}
