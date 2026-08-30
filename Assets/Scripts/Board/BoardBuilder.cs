using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardBuilder : MonoBehaviour
{
    private const string ImagesFolder = "Assets/Resources/PuzzleImages";

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
    [SerializeField] private float tilt = 12f;

    private readonly List<Sprite> generated = new List<Sprite>();
    private readonly List<Slot> slots = new List<Slot>();
    private int divisionsUsed;

    public int Build(
        PuzzleConfig config,
        PuzzleCutStyle cutStyle,
        Texture2D texture,
        DragLayer dragLayer,
        Action<Slot> onSlotFilled)
    {
        if (config == null)
        {
            Debug.LogError("BoardBuilder: assign a PuzzleConfig.", this);
            return 0;
        }

        if (dragLayer == null)
        {
            Debug.LogError("BoardBuilder: assign a DragLayer.", this);
            return 0;
        }

        if (texture == null)
        {
            Debug.LogError("BoardBuilder: puzzle texture is missing.", this);
            return 0;
        }

        if (!config.TryValidate(out string validationError))
        {
            Debug.LogError($"BoardBuilder: invalid configuration: {validationError}.", config);
            return 0;
        }

        if (!Enum.IsDefined(typeof(PuzzleCutStyle), cutStyle))
        {
            Debug.LogError($"BoardBuilder: invalid cut style value {(int)cutStyle}.", this);
            return 0;
        }

        PuzzleConfig.Layout layout;
        ProceduralPuzzleGeometry[,] geometries;
        try
        {
            layout = config.PickLayout();
            geometries = ProceduralPuzzleGenerator.Create(
                layout.divisions,
                cutStyle,
                config.CutDepth,
                config.CreateCutSeed());
        }
        catch (Exception exception)
        {
            Debug.LogError($"BoardBuilder: puzzle generation failed: {exception.Message}", this);
            return 0;
        }

        int divisions = layout.divisions;
        Vector2 cellSize = CalculateCellSize(texture, layout.cellSize);
        float cutPadding = cutStyle == PuzzleCutStyle.Square ? 0f : config.CutDepth;
        Sprite sourceSprite = Track(Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            Center));

        reference.sprite = sourceSprite;
        reference.preserveAspect = true;
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
                piece.Setup(id, sourceSprite, geometry, cellSize, cutPadding, dragLayer, tray);
                piece.ScatterTo(RandomAnchor(), UnityEngine.Random.Range(-tilt, tilt));

                Slot slot = Instantiate(slotPrefab, grid.transform, false);
                slot.Setup(id, geometry, cutPadding, dragLayer, onSlotFilled);
                slots.Add(slot);
            }
        }

        divisionsUsed = divisions;
        return divisions * divisions;
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
        for (int i = 0; i < generated.Count; i++) Destroy(generated[i]);
        generated.Clear();
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

    private Vector2 RandomAnchor()
    {
        float x = UnityEngine.Random.Range(bandX.x, bandX.y);
        if (UnityEngine.Random.value < 0.5f) x = 1f - x;
        return new Vector2(x, UnityEngine.Random.Range(bandY.x, bandY.y));
    }
}
