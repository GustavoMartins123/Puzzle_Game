using System;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Procedural Puzzle Preview")]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ProceduralPuzzlePreviewGraphic : MaskableGraphic
{
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
        Color32 vertexColor = color;
        for (int i = 0; i < geometry.Vertices.Count; i++)
        {
            Vector2 normalized = geometry.Vertices[i];
            Vector2 textureUv = geometry.TextureUvs[i];
            Vector2 position = new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, normalized.x),
                Mathf.Lerp(rect.yMin, rect.yMax, normalized.y));
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
