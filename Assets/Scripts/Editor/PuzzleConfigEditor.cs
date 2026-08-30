using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PuzzleConfig))]
public class PuzzleConfigEditor : Editor
{
    private SerializedProperty difficultyProfiles;
    private SerializedProperty defaultDifficulty;
    private SerializedProperty useFixedCutSeed;
    private SerializedProperty cutSeed;
    private SerializedProperty imagePaths;

    private void OnEnable()
    {
        difficultyProfiles = serializedObject.FindProperty("difficultyProfiles");
        defaultDifficulty = serializedObject.FindProperty("defaultDifficulty");
        useFixedCutSeed = serializedObject.FindProperty("useFixedCutSeed");
        cutSeed = serializedObject.FindProperty("cutSeed");
        imagePaths = serializedObject.FindProperty("imagePaths");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Difficulty", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(difficultyProfiles, new GUIContent("Profiles"), true);
        EditorGUILayout.PropertyField(defaultDifficulty, new GUIContent("Default"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Session Seeds", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useFixedCutSeed, new GUIContent("Use Fixed Seed"));
        if (useFixedCutSeed.boolValue)
            EditorGUILayout.PropertyField(cutSeed, new GUIContent("Seed"));

        EditorGUILayout.HelpBox(
            "Layouts, cut depth, reference opacity, initial tilt and allowed formats are " +
            "defined by the selected difficulty profile. Fully Random is restricted to Expert.",
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
