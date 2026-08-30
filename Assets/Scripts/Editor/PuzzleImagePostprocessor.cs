using UnityEditor;

public class PuzzleImagePostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!PuzzleImageLibrary.IsManaged(assetPath)) return;
        PuzzleImageLibrary.Apply((TextureImporter)assetImporter);
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!Touches(importedAssets) && !Touches(deletedAssets) &&
            !Touches(movedAssets) && !Touches(movedFromAssetPaths)) return;

        PuzzleImageLibrary.SynchronizeConfigs();
        PuzzleImageLibrary.ValidateConfigs();
    }

    private static bool Touches(string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
            if (PuzzleImageLibrary.IsManaged(paths[i])) return true;

        return false;
    }
}
