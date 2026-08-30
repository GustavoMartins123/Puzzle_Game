using System;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Procedural Puzzle Preview")]
public sealed class ProceduralPuzzlePreviewGraphic : MaskableGraphic
{
    private ProceduralPuzzleGeometry geometry;

    public void Configure(ProceduralPuzzleGeometry previewGeometry, Color previewColor)
    {
        geometry = previewGeometry ?? throw new ArgumentNullException(nameof(previewGeometry));
        color = previewColor;
        raycastTarget = false;
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
            Vector2 position = new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, normalized.x),
                Mathf.Lerp(rect.yMin, rect.yMax, normalized.y));
            vertexHelper.AddVert(position, vertexColor, Vector2.zero);
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
