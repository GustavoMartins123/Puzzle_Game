using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PuzzleConfig))]
public class PuzzleConfigEditor : Editor
{
    private SerializedProperty layouts;
    private SerializedProperty cutStyle;
    private SerializedProperty cutDepth;
    private SerializedProperty useFixedCutSeed;
    private SerializedProperty cutSeed;
    private SerializedProperty imagePaths;

    private void OnEnable()
    {
        layouts = serializedObject.FindProperty("layouts");
        cutStyle = serializedObject.FindProperty("cutStyle");
        cutDepth = serializedObject.FindProperty("cutDepth");
        useFixedCutSeed = serializedObject.FindProperty("useFixedCutSeed");
        cutSeed = serializedObject.FindProperty("cutSeed");
        imagePaths = serializedObject.FindProperty("imagePaths");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(layouts, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Procedural Pieces", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cutStyle, new GUIContent("Cut Style"));
        EditorGUILayout.PropertyField(cutDepth, new GUIContent("Cut Depth"));
        EditorGUILayout.PropertyField(useFixedCutSeed, new GUIContent("Use Fixed Seed"));
        if (useFixedCutSeed.boolValue)
            EditorGUILayout.PropertyField(cutSeed, new GUIContent("Seed"));

        EditorGUILayout.HelpBox(
            "Fully Random chooses a different procedural edge family for every shared cut. " +
            "Adjacent pieces always use the exact same complementary edge.",
            MessageType.None);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Image Library", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Folder", PuzzleImageLibrary.Folder);

        if (imagePaths.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                $"No images found. Drop images into {PuzzleImageLibrary.Folder} — " +
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
