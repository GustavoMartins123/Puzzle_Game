using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class Slot : MonoBehaviour
{
    [SerializeField] private Image image;
    private int id;

    public void SetId(int id)
    {
        this.id = id;
    }

    public int GetId()
    {
        return id;
    }

    public Image GetImage()
    {
        return image;
    }
}
