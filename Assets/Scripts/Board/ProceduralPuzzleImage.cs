using System;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

[AddComponentMenu("UI/Procedural Puzzle Image")]
public sealed class ProceduralPuzzleImage : Image
{
    private ProceduralPuzzleGeometry geometry;
    private Vector2 meshScale = Vector2.one;
    private bool useTextureUvs;

    public void Configure(
        Sprite source,
        ProceduralPuzzleGeometry pieceGeometry,
        bool mapOriginalImage,
        Vector2 scale)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        geometry = pieceGeometry ?? throw new ArgumentNullException(nameof(pieceGeometry));
        if (!float.IsFinite(scale.x) || !float.IsFinite(scale.y) || scale.x <= 0f || scale.y <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale));

        sprite = source;
        useTextureUvs = mapOriginalImage;
        meshScale = scale;
        type = Type.Simple;
        preserveAspect = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (geometry == null) return;

        Rect rect = GetPixelAdjustedRect();
        Vector2 size = Vector2.Scale(rect.size, meshScale);
        Vector2 center = rect.center;
        Vector4 outerUv = DataUtility.GetOuterUV(sprite);
        Vector2 solidUv = new Vector2(
            (outerUv.x + outerUv.z) * 0.5f,
            (outerUv.y + outerUv.w) * 0.5f);
        Color32 vertexColor = color;

        for (int i = 0; i < geometry.Vertices.Count; i++)
        {
            Vector2 normalized = geometry.Vertices[i];
            Vector2 position = center + Vector2.Scale(normalized - Vector2.one * 0.5f, size);
            Vector2 uv = solidUv;
            if (useTextureUvs)
            {
                Vector2 sourceUv = geometry.TextureUvs[i];
                uv = new Vector2(
                    Mathf.Lerp(outerUv.x, outerUv.z, sourceUv.x),
                    Mathf.Lerp(outerUv.y, outerUv.w, sourceUv.y));
            }

            vertexHelper.AddVert(position, vertexColor, uv);
        }

        for (int i = 0; i < geometry.Triangles.Count; i += 3)
        {
            vertexHelper.AddTriangle(
                geometry.Triangles[i],
                geometry.Triangles[i + 1],
                geometry.Triangles[i + 2]);
        }
    }

    public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (geometry == null) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, eventCamera, out Vector2 localPoint))
            return false;

        Rect rect = rectTransform.rect;
        Vector2 size = Vector2.Scale(rect.size, meshScale);
        if (size.x <= 0f || size.y <= 0f) return false;

        Vector2 normalized = new Vector2(
            (localPoint.x - rect.center.x) / size.x + 0.5f,
            (localPoint.y - rect.center.y) / size.y + 0.5f);
        return geometry.Contains(normalized);
    }
}
