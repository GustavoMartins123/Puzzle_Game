using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardBuilder : MonoBehaviour
{
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    [SerializeField] private PuzzlePiece piecePrefab;
    [SerializeField] private Slot slotPrefab;
    [SerializeField] private RectTransform tray;
    [SerializeField] private GridLayoutGroup grid;
    [SerializeField] private Image reference;

    [Header("Completion Wave")]
    [SerializeField] private float waveStep = 0.07f;

    [Header("Scatter")]
    [SerializeField] private Vector2 bandX = new Vector2(0.05f, 0.28f);
    [SerializeField] private Vector2 bandY = new Vector2(0.12f, 0.88f);

    private readonly List<Sprite> generated = new List<Sprite>();
    private readonly List<PuzzlePiece> pieces = new List<PuzzlePiece>();
    private readonly List<Slot> slots = new List<Slot>();
    private int divisionsUsed;

    public int Build(
        PuzzleSessionDefinition session,
        DragLayer dragLayer,
        Action<Slot> onSlotFilled)
    {
        if (dragLayer == null)
        {
            Debug.LogError("BoardBuilder: assign a DragLayer.", this);
            return 0;
        }

        if (session == null)
        {
            Debug.LogError("BoardBuilder: puzzle session is missing.", this);
            return 0;
        }

        if (!session.TryValidate(out string validationError))
        {
            Debug.LogError($"BoardBuilder: invalid puzzle session: {validationError}.", this);
            return 0;
        }

        if (!TryValidateReferences(out string referenceError))
        {
            Debug.LogError($"BoardBuilder: {referenceError}.", this);
            return 0;
        }

        if (pieces.Count != 0 || slots.Count != 0 || generated.Count != 0)
        {
            Debug.LogError("BoardBuilder: clear the current board before building another session.", this);
            return 0;
        }

        try
        {
            PuzzleLayout layout = session.Layout;
            PuzzleDifficultyProfile difficulty = session.Difficulty;
            Texture2D texture = session.Texture;
            PuzzleCutStyle cutStyle = session.CutStyle;
            ProceduralPuzzleGeometry[,] geometries = ProceduralPuzzleGenerator.Create(
                layout.divisions,
                cutStyle,
                difficulty.CutDepth,
                session.CutSeed);
            var scatterRandom = new System.Random(session.ScatterSeed);

            int divisions = layout.divisions;
            Vector2 cellSize = CalculateCellSize(texture, layout.cellSize);
            float cutPadding = cutStyle == PuzzleCutStyle.Square ? 0f : difficulty.CutDepth;
            Sprite sourceSprite = Track(Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                Center));

            reference.sprite = sourceSprite;
            reference.preserveAspect = true;
            Color referenceColor = reference.color;
            referenceColor.a = difficulty.ReferenceOpacity;
            reference.color = referenceColor;
            grid.constraintCount = divisions;
            grid.cellSize = cellSize;
            grid.spacing = Vector2.zero;
            RectTransform gridRect = (RectTransform)grid.transform;
            gridRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellSize.x * divisions);
            gridRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellSize.y * divisions);

            for (int y = 0; y < divisions; y++)
            {
                for (int x = 0; x < divisions; x++)
                {
                    int id = y * divisions + x;
                    ProceduralPuzzleGeometry geometry = geometries[x, divisions - 1 - y];

                    PuzzlePiece piece = Instantiate(piecePrefab, tray, false);
                    pieces.Add(piece);
                    piece.Setup(id, sourceSprite, geometry, cellSize, cutPadding, dragLayer, tray);
                    piece.ScatterTo(
                        RandomAnchor(scatterRandom),
                        RandomRange(scatterRandom, -difficulty.InitialTilt, difficulty.InitialTilt));

                    Slot slot = Instantiate(slotPrefab, grid.transform, false);
                    slots.Add(slot);
                    slot.Setup(id, geometry, cutPadding, dragLayer, onSlotFilled);
                }
            }

            divisionsUsed = divisions;
            return divisions * divisions;
        }
        catch (Exception exception)
        {
            ClearBoard();
            Debug.LogError($"BoardBuilder: puzzle generation failed: {exception.Message}", this);
            return 0;
        }
    }

    public void ClearBoard()
    {
        for (int i = 0; i < pieces.Count; i++)
            if (pieces[i] != null) Destroy(pieces[i].gameObject);
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null) Destroy(slots[i].gameObject);
        pieces.Clear();
        slots.Clear();
        ReleaseGeneratedSprites();
        divisionsUsed = 0;

        if (reference != null) reference.sprite = null;
    }

    public float PlayCompletionWave()
    {
        if (slots.Count == 0) return 0f;

        float longest = 0f;

        for (int i = 0; i < slots.Count; i++)
        {
            float delay = (i % divisionsUsed + i / divisionsUsed) * waveStep;
            SlotFeedback feedback = slots[i].Feedback;
            feedback.PlayWave(delay);
            longest = Mathf.Max(longest, delay + feedback.WaveDuration);
        }

        return longest;
    }

    private void OnDestroy()
    {
        ReleaseGeneratedSprites();
    }

    private Sprite Track(Sprite sprite)
    {
        generated.Add(sprite);
        return sprite;
    }

    private static Vector2 CalculateCellSize(Texture2D texture, float maximumCellSide)
    {
        if (texture.width <= 0 || texture.height <= 0)
            throw new InvalidOperationException("Puzzle image dimensions must be positive.");

        float aspect = texture.width / (float)texture.height;
        return aspect >= 1f
            ? new Vector2(maximumCellSide, maximumCellSide / aspect)
            : new Vector2(maximumCellSide * aspect, maximumCellSide);
    }

    private void ReleaseGeneratedSprites()
    {
        for (int i = 0; i < generated.Count; i++)
            if (generated[i] != null) Destroy(generated[i]);
        generated.Clear();
    }

    private bool TryValidateReferences(out string error)
    {
        if (piecePrefab == null) error = "piece prefab is missing";
        else if (slotPrefab == null) error = "slot prefab is missing";
        else if (tray == null) error = "piece tray is missing";
        else if (grid == null) error = "slot grid is missing";
        else if (reference == null) error = "reference image is missing";
        else
        {
            error = string.Empty;
            return true;
        }

        return false;
    }

    private Vector2 RandomAnchor(System.Random random)
    {
        float x = RandomRange(random, bandX.x, bandX.y);
        if (random.NextDouble() < 0.5d) x = 1f - x;
        return new Vector2(x, RandomRange(random, bandY.x, bandY.y));
    }

    private static float RandomRange(System.Random random, float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
}
