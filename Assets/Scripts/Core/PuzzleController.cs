using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [SerializeField] private List<PieceClass> pieces = new List<PieceClass>();
    [SerializeField] private List<Slot> slots = new List<Slot>();
    [SerializeField] private GameObject slot;
    [SerializeField] private GameObject fatherOfPieces;
    [SerializeField] private GameObject panelGrid;
    [SerializeField] private UiDragPiece dragPiece;
    [Space(3)]
    [Header("Sprite")]
    [SerializeField] private Piece piecePart;

    [Header("PanelWin")]
    [SerializeField] private GameObject panelWin;

    [Space(2)]
    [Header("Input")]
    [SerializeField] private InputManager inputManager;
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
    }

    private void Start()
    {
        inputManager.OnPieceChanged += PieceManager_OnPieceChanged;
        PlacePiecesAndSlots();
    }
    private void PieceManager_OnPieceChanged(object sender, bool e)
    {
        dragPiece.GetMouseImg().gameObject.SetActive(e);
        bool allTrue = true;
        foreach (var item in pieces)
        {
            if (!item.GetPieceInSlot())
            {
                allTrue = false;
                break;
            }
        }

        if (allTrue)
        {
            panelWin.SetActive(true);
        }
    }

    private void PlacePiecesAndSlots()
    {
        for (int x = 0; x < piecePart.GetSprites().Count; x++)
        {
            PieceClass pieceClass = piecePart.GetSprites()[x].GetComponent<PieceClass>();
            pieces.Add(pieceClass);
            pieceClass.SetId(x);
            pieceClass.SetSprite(piecePart.GetSprites()[x].sprite);
            PositionAndRotation(pieceClass);
            GameObject gameSlot = Instantiate(this.slot, panelGrid.transform);
            Slot slot = gameSlot.GetComponent<Slot>();
            slot.GetImage().rectTransform.sizeDelta = piecePart.GetSprites()[x].rectTransform.sizeDelta;
            slot.SetId(x);
            slots.Add(slot);
        }
        dragPiece.GetMouseImg().transform.SetAsLastSibling();
    }

    

    private void PositionAndRotation(PieceClass pieceClass)
    {
        Vector3 pos = new Vector2(Random.Range(-Screen.currentResolution.width * 0.4f, Screen.currentResolution.width * 0.4f), Random.Range(-Screen.currentResolution.height * 0.4f, Screen.currentResolution.height * 0.4f));
        Quaternion rotation = Quaternion.Euler(0, 0, Random.Range(-360f, 360f));
        pieceClass.transform.localPosition = pos;
        pieceClass.transform.localRotation = rotation;
    }
    
    public Transform GetFatherOfPiecesTransform()
    {
        return fatherOfPieces.transform;
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
    
    public void InvokeInputManager(object caller, bool pieceChanged)
    {
        inputManager.OnPieceChanged?.Invoke(caller, pieceChanged);
    }

}
