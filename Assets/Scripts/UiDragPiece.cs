using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UiDragPiece : MonoBehaviour
{
    public Image mouseImg;
    public int piecePreviousId;

    private void Update()
    {
        if(mouseImg.gameObject.activeSelf)
        {
            Vector2 mousePosition = GameManager.Instance.GetMousePosition();
            mouseImg.rectTransform.position = mousePosition;
        }
    }
}
