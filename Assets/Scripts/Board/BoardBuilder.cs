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

    [Header("Scatter")]
    [SerializeField] private Vector2 bandX = new Vector2(0.05f, 0.28f);
    [SerializeField] private Vector2 bandY = new Vector2(0.12f, 0.88f);
    [SerializeField] private float tilt = 12f;

    private readonly List<Sprite> generated = new List<Sprite>();

    public int Build(PuzzleConfig config, DragLayer dragLayer, Action<Slot> onSlotFilled)
    {
        if (config == null || !config.HasContent)
        {
            Debug.LogError($"BoardBuilder: assign a PuzzleConfig and put square images in {ImagesFolder}.", this);
            return 0;
        }

        Texture2D texture = config.PickImage();
        if (texture == null)
        {
            Debug.LogError($"BoardBuilder: the image library is out of date. Rescan {ImagesFolder}.", this);
            return 0;
        }

        PuzzleConfig.Layout layout = config.PickLayout();
        int divisions = layout.divisions;

        reference.sprite = Track(Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Center));
        grid.constraintCount = divisions;
        grid.cellSize = new Vector2(layout.cellSize, layout.cellSize);

        int pieceWidth = texture.width / divisions;
        int pieceHeight = texture.height / divisions;

        for (int y = 0; y < divisions; y++)
        {
            for (int x = 0; x < divisions; x++)
            {
                int id = y * divisions + x;
                Rect area = new Rect(x * pieceWidth, (divisions - 1 - y) * pieceHeight, pieceWidth, pieceHeight);

                PuzzlePiece piece = Instantiate(piecePrefab, tray, false);
                piece.Setup(id, Track(Sprite.Create(texture, area, Center)), layout.cellSize, dragLayer, tray);
                piece.ScatterTo(RandomAnchor(), UnityEngine.Random.Range(-tilt, tilt));

                Slot slot = Instantiate(slotPrefab, grid.transform, false);
                slot.Setup(id, dragLayer, onSlotFilled);
            }
        }

        return divisions * divisions;
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

    private Vector2 RandomAnchor()
    {
        float x = UnityEngine.Random.Range(bandX.x, bandX.y);
        if (UnityEngine.Random.value < 0.5f) x = 1f - x;
        return new Vector2(x, UnityEngine.Random.Range(bandY.x, bandY.y));
    }
}
