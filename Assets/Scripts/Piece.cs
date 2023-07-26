using System.Linq;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName ="Item", menuName ="New Item")]
public class Piece : ScriptableObject
{
    public enum PieceType
    {
        threeX3 = 0,
        fourX3 = 1,
        fiveX3 = 2,
    }
    public PieceType type;
    public Sprite panelGridImage;
    public Sprite[] piecePart;


#if UNITY_EDITOR
    [CustomEditor(typeof(Piece))]
    public class PieceEditor : Editor
    {
        private bool generateSpritesFlag = false;

        private void OnEnable()
        {
            EditorApplication.update += Update;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Update;
        }

        private void Update()
        {
            if (generateSpritesFlag)
            {
                generateSpritesFlag = false;
                GenerateSprites();
            }
        }
        public override void OnInspectorGUI()
        {
            Piece piece = (Piece)target;

            if (!generateSpritesFlag)
            {
                base.OnInspectorGUI();
            }

            if (GUILayout.Button("Generate Sprites"))
            {
                generateSpritesFlag = true;
            }

        }
        private void GenerateSprites()
        {
            Piece piece = (Piece)target;
            string folderPath = "Assets/Sprites/Fish/";
            switch (piece.type)
            {
                case PieceType.threeX3:
                    piece.piecePart = new Sprite[10];
                    folderPath += "3x3";
                    break;
                case PieceType.fourX3:
                    piece.piecePart = new Sprite[13];
                    folderPath += "4x3";
                    break;
                case PieceType.fiveX3:
                    piece.piecePart = new Sprite[16];
                    folderPath += "5x3";
                    break;
            }
            GetSprites(piece, folderPath);
        }
        private static void GetSprites(Piece piece, string folderPath)
        {
            string[] imagePaths = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

            if (imagePaths.Length > 0)
            {
                int randomIndex = Random.Range(0, imagePaths.Length);
                string randomImagePath = AssetDatabase.GUIDToAssetPath(imagePaths[randomIndex]);

                Texture2D randomImage = AssetDatabase.LoadAssetAtPath<Texture2D>(randomImagePath);
                if (randomImage != null)
                {
                    Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(randomImagePath).OfType<Sprite>().ToArray();

                    if (sprites.Length >= piece.piecePart.Length)
                    {
                        for (int i = 0; i < piece.piecePart.Length; i++)
                        {
                            piece.piecePart[i] = sprites[i];
                        }
                        piece.panelGridImage = sprites[sprites.Length - 1];
                    }
                    else
                    {
                        Debug.Log("Not enough sprites in the selected image");
                    }
                }
                else
                {
                    Debug.Log("Failed to Load Random Image");
                }
            }
            else
            {
                Debug.Log("No Images Found in the Specified Folder");
            }
            EditorUtility.SetDirty(piece);
            AssetDatabase.SaveAssets();
        }
    }
#endif
}
