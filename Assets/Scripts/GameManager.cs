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
    [SerializeField] GameObject pieceObjectReference, slot;
    public GameObject fatherOfPieces;
    [SerializeField] Image[] panelObjects;
    [SerializeField] Image panelGrid;
    [Space(2)]
    [Header("Input")]
    [SerializeField] InputManager inputManager;
    
    [SerializeField] UiDragPiece dragPiece;

    [Space(3)]
    [Header("Sprite")]
    public Piece[] scriptablePiece;
    [SerializeField] Piece piecePart;
    [SerializeField] Sprite panelGridImage;

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
        piecePart = scriptablePiece[Random.Range(0, scriptablePiece.Length)];
        GameObject panel = Instantiate(panelObjects[(int)piecePart.type].gameObject, fatherOfPieces.transform);
        panel.transform.SetAsFirstSibling();
        panelGrid = panel.GetComponent<Image>();
        pieceManager.InitializePiece(piecePart.piecePart.Length - 1, dragPiece);
        panelGridImage = piecePart.panelGridImage;
        panelGrid.sprite = panelGridImage;

        for (int x = 0; x < piecePart.piecePart.Length - 1; x++)
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
        Vector3 pos = new Vector2(Random.Range(-Screen.width * 0.4f, Screen.width * 0.4f), Random.Range(-Screen.height * 0.4f, Screen.height * 0.4f));
        Quaternion rotation = Quaternion.Euler(0, 0, Random.Range(-360f, 360f));
        pieceClass.transform.localPosition = pos;
        pieceClass.transform.localRotation = rotation;
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

}
