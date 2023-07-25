using UnityEngine.UI;

public class PanelSizeManager
{
    private GridLayoutGroup gridLayout;
    private int constraintCount;
    public PanelSizeManager(GridLayoutGroup gridLayout,int constraintCount)
    {
        this.gridLayout = gridLayout;
        this.constraintCount = constraintCount; 
    }
}
