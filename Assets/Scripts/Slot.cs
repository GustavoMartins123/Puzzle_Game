using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Represents a slot where puzzle pieces can be placed.
/// Each slot has a unique ID that matches with a specific piece.
/// </summary>
[RequireComponent(typeof(Image))]
public class Slot : MonoBehaviour
{
    [SerializeField] private Image image;
    private int id;

    /// <summary>
    /// Sets the unique identifier for this slot
    /// </summary>
    public void SetId(int id)
    {
        this.id = id;
    }

    /// <summary>
    /// Gets the unique identifier for this slot
    /// </summary>
    public int GetId()
    {
        return id;
    }

    /// <summary>
    /// Gets the image component of this slot
    /// </summary>
    public Image GetImage()
    {
        return image;
    }
}
