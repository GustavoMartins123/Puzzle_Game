using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PuzzleConfig))]
public class PuzzleConfigEditor : Editor
{
    private SerializedProperty layouts;
    private SerializedProperty imagePaths;

    private void OnEnable()
    {
        layouts = serializedObject.FindProperty("layouts");
        imagePaths = serializedObject.FindProperty("imagePaths");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(layouts, true);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Image Library", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Folder", PuzzleImageLibrary.Folder);

        if (imagePaths.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                $"No images found. Drop square images into {PuzzleImageLibrary.Folder} — " +
                "import settings and this list are updated automatically.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox($"{imagePaths.arraySize} image(s) available.", MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(imagePaths, new GUIContent("Image Paths"), true);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Rescan Folder", GUILayout.Height(24f)))
        {
            int reimported = PuzzleImageLibrary.NormalizeImportSettings();
            PuzzleImageLibrary.SyncConfigs();
            Debug.Log($"Puzzle image library rescanned: {imagePaths.arraySize} image(s), " +
                      $"{reimported} reimported.", target);
        }
    }
}
