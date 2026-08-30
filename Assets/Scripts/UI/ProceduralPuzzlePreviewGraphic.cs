using System;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Procedural Puzzle Preview")]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ProceduralPuzzlePreviewGraphic : MaskableGraphic
{
    private const float ContentScale = 0.9f;

    private ProceduralPuzzleGeometry geometry;
    private Texture2D previewTexture;

    public override Texture mainTexture => previewTexture;

    public void Configure(ProceduralPuzzleGeometry previewGeometry, Texture2D texture)
    {
        geometry = previewGeometry ?? throw new ArgumentNullException(nameof(previewGeometry));
        previewTexture = texture != null ? texture : throw new ArgumentNullException(nameof(texture));
        color = Color.white;
        raycastTarget = false;
        canvasRenderer.SetAlpha(1f);
        SetMaterialDirty();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (geometry == null) return;

        Rect rect = GetPixelAdjustedRect();
        float side = Mathf.Min(rect.width, rect.height) * ContentScale;
        Vector2 size = Vector2.one * side;
        Vector2 center = rect.center;
        Color32 vertexColor = color;
        for (int i = 0; i < geometry.Vertices.Count; i++)
        {
            Vector2 normalized = geometry.Vertices[i];
            Vector2 textureUv = geometry.TextureUvs[i];
            Vector2 position = center + Vector2.Scale(normalized - Vector2.one * 0.5f, size);
            vertexHelper.AddVert(position, vertexColor, textureUv);
        }

        for (int i = 0; i < geometry.Triangles.Count; i += 3)
        {
            vertexHelper.AddTriangle(
                geometry.Triangles[i],
                geometry.Triangles[i + 1],
                geometry.Triangles[i + 2]);
        }
    }
}
