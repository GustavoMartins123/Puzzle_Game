using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PuzzleConfig))]
public class PuzzleConfigEditor : Editor
{
    private SerializedProperty difficultyProfiles;
    private SerializedProperty defaultDifficulty;
    private SerializedProperty useFixedCutSeed;
    private SerializedProperty cutSeed;
    private SerializedProperty defaultImageId;
    private SerializedProperty importCollectionId;
    private SerializedProperty collections;
    private SerializedProperty cutStyleUnlocks;
    private SerializedProperty achievements;

    private void OnEnable()
    {
        difficultyProfiles = serializedObject.FindProperty("difficultyProfiles");
        defaultDifficulty = serializedObject.FindProperty("defaultDifficulty");
        useFixedCutSeed = serializedObject.FindProperty("useFixedCutSeed");
        cutSeed = serializedObject.FindProperty("cutSeed");
        defaultImageId = serializedObject.FindProperty("defaultImageId");
        importCollectionId = serializedObject.FindProperty("importCollectionId");
        collections = serializedObject.FindProperty("collections");
        cutStyleUnlocks = serializedObject.FindProperty("cutStyleUnlocks");
        achievements = serializedObject.FindProperty("achievements");
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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Progression", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(defaultImageId, new GUIContent("Default Image Id"));
        EditorGUILayout.PropertyField(
            importCollectionId,
            new GUIContent("New Images Collection"));
        EditorGUILayout.PropertyField(collections, new GUIContent("Collections"), true);
        EditorGUILayout.PropertyField(cutStyleUnlocks, new GUIContent("Cut Style Unlocks"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Achievements", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(achievements, new GUIContent("Definitions"), true);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Image Library", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Folder", PuzzleImageLibrary.Folder);
        PuzzleConfig config = (PuzzleConfig)target;
        EditorGUILayout.HelpBox(
            $"{config.ImageCount} configured image(s). Every file in the folder must belong " +
            "to exactly one explicit collection.",
            MessageType.Info);

        if (GUILayout.Button("Rescan Folder", GUILayout.Height(24f)))
        {
            int reimported = PuzzleImageLibrary.NormalizeImportSettings();
            int added = PuzzleImageLibrary.SynchronizeConfig(config);
            if (!PuzzleImageLibrary.TryValidateAssignments(config, out string error))
                Debug.LogError($"Puzzle image library validation failed: {error}.", config);
            else
                Debug.Log(
                    $"Puzzle image library validated: {config.ImageCount} image(s), " +
                    $"{added} added and {reimported} reimported.",
                    config);
        }
    }
}
