using UnityEngine;

public class Slot : MonoBehaviour
{
    private int id;

    public void SetId(int id)
    {
        this.id = id;
    }

    public int GetId()
    {
        return id;
    }
}
