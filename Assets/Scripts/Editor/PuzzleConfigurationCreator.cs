using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Unity Editor utility to create puzzle configuration presets.
/// Accessible via Tools → Puzzle Game → Create Configuration Presets
/// </summary>
public class PuzzleConfigurationCreator : EditorWindow
{
    [MenuItem("Tools/Puzzle Game/Create Configuration Presets")]
    public static void CreateAllPresets()
    {
        string folderPath = "Assets/Resources/Configurations";
        
        // Create folder if it doesn't exist
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Configurations");
        }

        // Create Easy preset
        CreateAndSavePreset(folderPath, "Easy_2x2", 2, 300);
        
        // Create Medium preset
        CreateAndSavePreset(folderPath, "Medium_3x3", 3, 200);
        
        // Create Hard preset
        CreateAndSavePreset(folderPath, "Hard_4x4", 4, 150);
        
        // Create Expert preset
        CreateAndSavePreset(folderPath, "Expert_5x5", 5, 120);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("✅ Created all puzzle configuration presets in " + folderPath);
        EditorUtility.DisplayDialog("Success", 
            "Created 4 configuration presets:\n\n" +
            "• Easy_2x2 (300px pieces)\n" +
            "• Medium_3x3 (200px pieces)\n" +
            "• Hard_4x4 (150px pieces)\n" +
            "• Expert_5x5 (120px pieces)\n\n" +
            "Find them in " + folderPath, 
            "OK");
    }

    private static void CreateAndSavePreset(string folderPath, string fileName, int gridSize, int cellSize)
    {
        PuzzleConfiguration config = ScriptableObject.CreateInstance<PuzzleConfiguration>();
        config.difficultyName = fileName.Split('_')[0];
        config.gridSize = gridSize;
        config.cellSize = cellSize;
        config.gridSpacing = 1;
        config.imageResourcePath = "Sprites/Fish";
        
        string assetPath = Path.Combine(folderPath, fileName + ".asset");
        AssetDatabase.CreateAsset(config, assetPath);
        
        Debug.Log($"Created preset: {fileName} ({gridSize}x{gridSize}, {cellSize}px)");
    }

    [MenuItem("Tools/Puzzle Game/Open Configuration Folder")]
    public static void OpenConfigurationFolder()
    {
        string folderPath = "Assets/Resources/Configurations";
        
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
        else
        {
            EditorUtility.DisplayDialog("Folder Not Found", 
                "Configuration folder doesn't exist yet.\n\n" +
                "Use 'Tools → Puzzle Game → Create Configuration Presets' to create it.", 
                "OK");
        }
    }
}
