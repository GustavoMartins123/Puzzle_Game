using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Piece: MonoBehaviour
{
    [SerializeField] private PieceClass pieceObjectReference;
    [SerializeField] private GameObject fatherOfPieces;
    [SerializeField] private Image BackGround;
    [SerializeField] GridLayoutGroup gridLayoutGroup;
    private List<Image> sprites = new List<Image>();
    private Texture2D sourceTexture;

    [SerializeField]
    int[] numImages =
    {
        4,9,16,25
    };

    [SerializeField]
    int[] numColumsRows =
    {
        2,3,4,5
    };

    [SerializeField]
    int[] numCellSize =
    {
        300,200,150,120
    };

    private void Awake()
    {
        InstantiateSprites(numImages[Random.Range(0, numImages.Length)]);
        LoadAndSliceTexture();
    }

    private void InstantiateSprites(int num)
    {
        for (int i = 0; i < num; i++)
        {
            GameObject gameObject = Instantiate(pieceObjectReference.gameObject, fatherOfPieces.transform);
            sprites.Add(gameObject.GetComponent<Image>());
        }
    }

    private Sprite GetSpriteCut(int x, int y, int columns, int rows)
    {
        int spriteWidth = sourceTexture.width / rows;
        int spriteHeight = sourceTexture.height / columns;

        Texture2D slicedTexture = new Texture2D(spriteWidth, spriteHeight);

        Color[] pixels = sourceTexture.GetPixels(x * spriteWidth, (rows - 1 - y) * spriteHeight, spriteWidth, spriteHeight);
        slicedTexture.SetPixels(pixels);
        slicedTexture.Apply();

        Sprite slicedSprite = Sprite.Create(slicedTexture, new Rect(0, 0, spriteWidth, spriteHeight), Vector2.zero);
        return slicedSprite;
    }

    public void LoadAndSliceTexture()
    {
        string folderPath = "Sprites/Fish";
        Texture2D[] textures = Resources.LoadAll<Texture2D>(folderPath);

        if (textures.Length > 0)
        {
            int randomIndex = Random.Range(0, textures.Length);
            Texture2D randomImage = textures[randomIndex];
            BackGround.sprite = Sprite.Create(randomImage, new Rect(0, 0, randomImage.width, randomImage.height), Vector2.zero);

            if (randomImage != null)
            {
                sourceTexture = randomImage;
                int columns = 0;
                for (int i = 0; i < numImages.Length; i++)
                {
                    if (numImages[i] == fatherOfPieces.transform.childCount)
                    {
                        columns = numColumsRows[i];
                        gridLayoutGroup.constraintCount = numColumsRows[i];
                        gridLayoutGroup.cellSize = new Vector2(numCellSize[i], numCellSize[i]);
                    }
                }
                foreach (var item in sprites)
                {
                    item.rectTransform.sizeDelta = gridLayoutGroup.cellSize;
                }


                int rows = columns;

                int spriteIndex = 0;

                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        sprites[spriteIndex].sprite = GetSpriteCut(x, y, columns, rows);
                        spriteIndex++;
                    }
                }
            }
            else
            {
                Debug.Log("Failed to load image");
            }
        }
        else
        {
            Debug.Log("No Images Found in the Specified Folder");
        }
    }

    public List<Image> GetSprites()
    {
        return sprites;
    }
}
