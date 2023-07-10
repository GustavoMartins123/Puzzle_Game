using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public List<PieceClass> pieces = new List<PieceClass>();
    public List<Slot> slots = new List<Slot>();
    public PieceManager pieceManager;
    public int numPieces;
    [SerializeField] SpriteManager spriteManager;
    [SerializeField] GameObject pieceObjectReference, slot;
    public GameObject fatherOfPieces;
    [SerializeField] Image panelGrid;
    [Space(2)]
    [Header("Input")]
    InputManager inputManager;
    [SerializeField] UiDragPiece dragPiece;

    [Space(3)]
    [Header("Sprite")]
    [SerializeField] Piece[] scriptablePiece;
    [SerializeField] Piece piecePart;
    [SerializeField] Sprite panelGridImage;

    [SerializeField] Texture2D sourceTexture;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        inputManager = new InputManager();
        inputManager.InitializeInput();
        pieceManager.InitializePiece(numPieces, dragPiece);
        piecePart = scriptablePiece[Random.Range(0, scriptablePiece.Length)];
        panelGridImage = piecePart.panelGridImage;
        panelGrid.sprite = panelGridImage;
        for (int x = 0; x < numPieces; x++)
        {
            GameObject piece = Instantiate(pieceObjectReference);
            PieceClass pieceClass = piece.GetComponent<PieceClass>();
            pieces.Add(pieceClass);
            piece.transform.SetParent(fatherOfPieces.transform);
            pieceClass.SetId(x);
            pieceClass.SetSprite(piecePart.piecePart[x]);
            PositionAndRotation(pieceClass);

            GameObject gameSlot = Instantiate(this.slot, panelGrid.transform);
            Slot slot = gameSlot.GetComponent<Slot>();
            slot.id = x;
            slots.Add(slot);
        }
    }

    private void Start()
    {
        pieceManager.OnPieceChanged += PieceManager_OnPieceChanged;
    }

    private void PieceManager_OnPieceChanged(object sender, bool e)
    {
        dragPiece.mouseImg.gameObject.SetActive(e);
        bool allTrue = true;
        foreach (var item in pieces)
        {
            if (!item.pieceInSlot)
            {
                allTrue = false;
                break;
            }
        }

        if (allTrue)
        {
            Debug.Log("Win");
        }
    }

    void PositionAndRotation(PieceClass pieceClass)
    {
        Vector3 pos = new Vector3(Random.Range(-700f, 700f), Random.Range(-350f, 350f), 0);
        Quaternion rotation = Quaternion.Euler(0, 0, Random.Range(-360f, 360f));
        pieceClass.transform.localPosition = pos;
        pieceClass.transform.localRotation = rotation;
    }
    public Vector2 GetMousePosition()
    {
        Vector2 pos = Mouse.current.position.ReadValue();
        return pos;
    }

    public Slot GetClosestSlotToPiece(PieceClass piece)
    {
        Slot closestSlot = null;
        float closestDistance = 100f;

        foreach (Slot slot in slots)
        {
            float distance = Vector3.Distance(piece.transform.position, slot.transform.position);
            if (distance < closestDistance)
            {
                closestSlot = slot;
                closestDistance = distance;
            }
        }

        return closestSlot;
    }
    void DivideImage(int numParts)
    {
        int width = globalTexture.width;
        int height = globalTexture.height;

        int pieceWidth = width / numParts;
        int pieceHeight = height / numParts;
        Texture2D[] piecePart = new Texture2D[numParts * numParts];

        int index = 0;

        for (int y = 0; y < numParts; y++)
        {
            for (int x = 0; x < numParts; x++)
            {
                Texture2D piece = new Texture2D(pieceWidth, pieceHeight);
            }
        }
    }
    //Working on this
    private Sprite CutSprite(int x, int y, int spriteWidth, int spriteHeight)
    {
        int rows = sourceTexture.height / spriteHeight;
        int columns = sourceTexture.width / spriteWidth;

        x = columns - 1 - x;
        y = rows - 1 - y;

        int startX = 0;
        int startY = y * spriteHeight;

        Texture2D slicedTexture = new Texture2D(spriteWidth, spriteHeight);
        Color[] pixels = sourceTexture.GetPixels(startX, startY, spriteWidth, spriteHeight);
        slicedTexture.SetPixels(pixels);
        slicedTexture.Apply();

        Sprite slicedSprite = Sprite.Create(slicedTexture, new Rect(0, 0, spriteWidth, spriteHeight), Vector2.zero);

        return slicedSprite;
    }

}
