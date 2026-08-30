using UnityEngine;

[CreateAssetMenu(fileName = "PuzzleConfig", menuName = "Puzzle/Puzzle Config")]
public class PuzzleConfig : ScriptableObject
{
    [System.Serializable]
    public struct Layout
    {
        public int divisions;
        public float cellSize;
    }

    [SerializeField] private Layout[] layouts =
    {
        new Layout { divisions = 2, cellSize = 300f },
        new Layout { divisions = 3, cellSize = 200f },
        new Layout { divisions = 4, cellSize = 150f },
        new Layout { divisions = 5, cellSize = 120f },
    };

    [SerializeField] private string[] imagePaths;

    public bool HasContent => layouts.Length > 0 && imagePaths != null && imagePaths.Length > 0;

    public int ImageCount => imagePaths == null ? 0 : imagePaths.Length;

    public Layout PickLayout() => layouts[Random.Range(0, layouts.Length)];

    public Texture2D PickImage() => Resources.Load<Texture2D>(imagePaths[Random.Range(0, imagePaths.Length)]);
}
