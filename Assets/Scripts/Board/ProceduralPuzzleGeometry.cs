using System;
using System.Collections.Generic;
using UnityEngine;

public enum PuzzlePieceCategory
{
    Corner,
    Edge,
    Interior,
}

public sealed class ProceduralPuzzleGeometry
{
    private readonly Vector2[] vertices;
    private readonly Vector2[] textureUvs;
    private readonly ushort[] triangles;

    public ProceduralPuzzleGeometry(Vector2[] vertices, Vector2[] textureUvs, ushort[] triangles)
    {
        this.vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        this.textureUvs = textureUvs ?? throw new ArgumentNullException(nameof(textureUvs));
        this.triangles = triangles ?? throw new ArgumentNullException(nameof(triangles));

        if (vertices.Length < 3 || vertices.Length != textureUvs.Length)
            throw new ArgumentException("Puzzle geometry must have matching vertex and UV arrays.");
        if (triangles.Length < 3 || triangles.Length % 3 != 0)
            throw new ArgumentException("Puzzle geometry must contain complete triangles.");
    }

    public IReadOnlyList<Vector2> Vertices => vertices;

    public IReadOnlyList<Vector2> TextureUvs => textureUvs;

    public IReadOnlyList<ushort> Triangles => triangles;

    public bool Contains(Vector2 point)
    {
        for (int i = 0; i < triangles.Length; i += 3)
        {
            if (PointInTriangle(
                    point,
                    vertices[triangles[i]],
                    vertices[triangles[i + 1]],
                    vertices[triangles[i + 2]]))
                return true;
        }

        return false;
    }

    public PuzzlePieceCategory ClassifyByImageBoundary()
    {
        const float epsilon = 0.00001f;
        bool left = false;
        bool right = false;
        bool bottom = false;
        bool top = false;

        for (int i = 0; i < textureUvs.Length; i++)
        {
            Vector2 uv = textureUvs[i];
            if (Mathf.Abs(uv.x) <= epsilon) left = true;
            if (Mathf.Abs(uv.x - 1f) <= epsilon) right = true;
            if (Mathf.Abs(uv.y) <= epsilon) bottom = true;
            if (Mathf.Abs(uv.y - 1f) <= epsilon) top = true;
        }

        int boundaryCount =
            (left ? 1 : 0) +
            (right ? 1 : 0) +
            (bottom ? 1 : 0) +
            (top ? 1 : 0);
        return boundaryCount switch
        {
            0 => PuzzlePieceCategory.Interior,
            1 => PuzzlePieceCategory.Edge,
            2 => PuzzlePieceCategory.Corner,
            _ => throw new InvalidOperationException(
                $"Puzzle geometry touches {boundaryCount} image boundaries."),
        };
    }

    private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float ab = Cross(b - a, point - a);
        float bc = Cross(c - b, point - b);
        float ca = Cross(a - c, point - c);
        const float epsilon = 0.00001f;
        return ab >= -epsilon && bc >= -epsilon && ca >= -epsilon;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}

public static class ProceduralPuzzleGenerator
{
    private const float PointEpsilon = 0.00001f;

    public static ProceduralPuzzleGeometry[,] Create(
        int divisions,
        PuzzleCutStyle style,
        float depth,
        int seed)
    {
        if (divisions < 2) throw new ArgumentOutOfRangeException(nameof(divisions));
        if (!Enum.IsDefined(typeof(PuzzleCutStyle), style))
            throw new ArgumentOutOfRangeException(nameof(style));
        if (!float.IsFinite(depth) || depth < 0.08f || depth > 0.3f)
            throw new ArgumentOutOfRangeException(nameof(depth));

        var random = new System.Random(seed);
        EdgeProfile[,] vertical = CreateProfiles(divisions - 1, divisions, style, depth, random);
        EdgeProfile[,] horizontal = CreateProfiles(divisions, divisions - 1, style, depth, random);
        var result = new ProceduralPuzzleGeometry[divisions, divisions];
        float padding = style == PuzzleCutStyle.Square ? 0f : depth;

        for (int y = 0; y < divisions; y++)
        {
            for (int x = 0; x < divisions; x++)
                result[x, y] = CreatePiece(x, y, divisions, padding, vertical, horizontal);
        }

        return result;
    }

    private static EdgeProfile[,] CreateProfiles(
        int width,
        int height,
        PuzzleCutStyle requestedStyle,
        float depth,
        System.Random random)
    {
        var profiles = new EdgeProfile[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                PuzzleCutStyle style = requestedStyle == PuzzleCutStyle.FullyRandom
                    ? (PuzzleCutStyle)random.Next((int)PuzzleCutStyle.Round, (int)PuzzleCutStyle.FullyRandom)
                    : requestedStyle;
                float direction = random.Next(0, 2) == 0 ? -1f : 1f;
                float variation = 0.82f + (float)random.NextDouble() * 0.18f;
                profiles[x, y] = CreateProfile(style, direction * depth * variation, random);
            }
        }

        return profiles;
    }

    private static EdgeProfile CreateProfile(PuzzleCutStyle style, float amplitude, System.Random random)
    {
        switch (style)
        {
            case PuzzleCutStyle.Square:
                return EdgeProfile.Straight;
            case PuzzleCutStyle.Round:
                return SampleBell(amplitude, 0.34f, 0.66f, 10, 0.5f);
            case PuzzleCutStyle.Ellipse:
                return SampleBell(amplitude, 0.24f, 0.76f, 12, 0.78f);
            case PuzzleCutStyle.Rectangle:
                return FromPoints(
                    new Vector2(0f, 0f), new Vector2(0.32f, 0f),
                    new Vector2(0.32f, amplitude), new Vector2(0.68f, amplitude),
                    new Vector2(0.68f, 0f), new Vector2(1f, 0f));
            case PuzzleCutStyle.Hexagon:
                return FromPoints(
                    new Vector2(0f, 0f), new Vector2(0.28f, 0f),
                    new Vector2(0.4f, amplitude), new Vector2(0.6f, amplitude),
                    new Vector2(0.72f, 0f), new Vector2(1f, 0f));
            case PuzzleCutStyle.Triangle:
                return FromPoints(
                    new Vector2(0f, 0f), new Vector2(0.28f, 0f),
                    new Vector2(0.5f, amplitude), new Vector2(0.72f, 0f),
                    new Vector2(1f, 0f));
            case PuzzleCutStyle.Diamond:
                return FromPoints(
                    new Vector2(0f, 0f), new Vector2(0.3f, 0f),
                    new Vector2(0.38f, -amplitude * 0.28f), new Vector2(0.5f, amplitude),
                    new Vector2(0.62f, -amplitude * 0.28f), new Vector2(0.7f, 0f),
                    new Vector2(1f, 0f));
            case PuzzleCutStyle.Wave:
                return SampleWave(amplitude, 16);
            case PuzzleCutStyle.Zigzag:
                return FromPoints(
                    new Vector2(0f, 0f), new Vector2(0.24f, 0f),
                    new Vector2(0.34f, amplitude * 0.55f), new Vector2(0.43f, -amplitude * 0.35f),
                    new Vector2(0.52f, amplitude), new Vector2(0.61f, -amplitude * 0.3f),
                    new Vector2(0.72f, amplitude * 0.45f), new Vector2(0.78f, 0f),
                    new Vector2(1f, 0f));
            case PuzzleCutStyle.DoubleLobe:
                return SampleDoubleLobe(amplitude, 18);
            case PuzzleCutStyle.Organic:
                return SampleOrganic(amplitude, random, 18);
            case PuzzleCutStyle.Procedural:
                return SampleProcedural(amplitude, random, 20);
            case PuzzleCutStyle.FullyRandom:
                throw new InvalidOperationException("FullyRandom must be resolved per shared edge.");
            default:
                throw new ArgumentOutOfRangeException(nameof(style));
        }
    }

    private static EdgeProfile SampleBell(
        float amplitude,
        float start,
        float end,
        int samples,
        float exponent)
    {
        var points = new Vector2[samples + 4];
        points[0] = Vector2.zero;
        points[1] = new Vector2(start, 0f);
        for (int i = 0; i <= samples; i++)
        {
            float u = i / (float)samples;
            float sine = i == 0 || i == samples ? 0f : Mathf.Max(0f, Mathf.Sin(Mathf.PI * u));
            float displacement = Mathf.Pow(sine, exponent) * amplitude;
            points[i + 2] = new Vector2(Mathf.Lerp(start, end, u), displacement);
        }
        points[points.Length - 1] = Vector2.right;
        return new EdgeProfile(points);
    }

    private static EdgeProfile SampleWave(float amplitude, int samples)
    {
        var points = new Vector2[samples + 1];
        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            float envelope = CentralEnvelope(t);
            float displacement = Mathf.Sin((t - 0.22f) / 0.56f * Mathf.PI * 2f) * envelope * amplitude;
            points[i] = new Vector2(t, displacement);
        }
        return new EdgeProfile(points);
    }

    private static EdgeProfile SampleDoubleLobe(float amplitude, int samples)
    {
        var points = new Vector2[samples + 1];
        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            float u = Mathf.InverseLerp(0.22f, 0.78f, t);
            float lobes = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f));
            points[i] = new Vector2(t, lobes * CentralEnvelope(t) * amplitude);
        }
        return new EdgeProfile(points);
    }

    private static EdgeProfile SampleOrganic(float amplitude, System.Random random, int samples)
    {
        float skew = Mathf.Lerp(-0.12f, 0.12f, (float)random.NextDouble());
        float secondary = Mathf.Lerp(-0.32f, 0.38f, (float)random.NextDouble());
        var points = new Vector2[samples + 1];

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            float u = Mathf.Clamp01((t - 0.22f - skew) / 0.56f);
            float primary = Mathf.Sin(Mathf.PI * u);
            float detail = Mathf.Sin(Mathf.PI * u * 2f) * secondary;
            points[i] = new Vector2(t, (primary + detail) * CentralEnvelope(t) * amplitude);
        }

        return new EdgeProfile(points);
    }

    private static EdgeProfile SampleProcedural(float amplitude, System.Random random, int samples)
    {
        float phaseA = (float)random.NextDouble() * Mathf.PI * 2f;
        float phaseB = (float)random.NextDouble() * Mathf.PI * 2f;
        float harmonicA = Mathf.Lerp(0.16f, 0.38f, (float)random.NextDouble());
        float harmonicB = Mathf.Lerp(0.08f, 0.22f, (float)random.NextDouble());
        var raw = new float[samples + 1];
        float peak = 0f;

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            float envelope = CentralEnvelope(t);
            float noise = 0.7f +
                          Mathf.Sin(t * Mathf.PI * 4f + phaseA) * harmonicA +
                          Mathf.Sin(t * Mathf.PI * 7f + phaseB) * harmonicB;
            raw[i] = envelope * Mathf.Max(0.16f, noise);
            peak = Mathf.Max(peak, raw[i]);
        }

        var points = new Vector2[samples + 1];
        for (int i = 0; i <= samples; i++)
            points[i] = new Vector2(i / (float)samples, raw[i] / peak * amplitude);

        return new EdgeProfile(points);
    }

    private static float CentralEnvelope(float t)
    {
        if (t <= 0.22f || t >= 0.78f) return 0f;
        float u = Mathf.InverseLerp(0.22f, 0.78f, t);
        return Mathf.Sin(Mathf.PI * u);
    }

    private static EdgeProfile FromPoints(params Vector2[] points) => new EdgeProfile(points);

    private static ProceduralPuzzleGeometry CreatePiece(
        int x,
        int y,
        int divisions,
        float padding,
        EdgeProfile[,] vertical,
        EdgeProfile[,] horizontal)
    {
        EdgeProfile bottom = y == 0 ? EdgeProfile.Straight : horizontal[x, y - 1];
        EdgeProfile right = x == divisions - 1 ? EdgeProfile.Straight : vertical[x, y];
        EdgeProfile top = y == divisions - 1 ? EdgeProfile.Straight : horizontal[x, y];
        EdgeProfile left = x == 0 ? EdgeProfile.Straight : vertical[x - 1, y];
        var global = new List<Vector2>(80);

        AppendHorizontal(global, bottom, x, y, false);
        AppendVertical(global, right, x + 1, y, false);
        AppendHorizontal(global, top, x, y + 1, true);
        AppendVertical(global, left, x, y, true);
        RemoveDuplicateClosure(global);
        RemoveCollinearPoints(global);

        float expandedSize = 1f + padding * 2f;
        var local = new Vector2[global.Count];
        var uvs = new Vector2[global.Count];
        for (int i = 0; i < global.Count; i++)
        {
            Vector2 point = global[i];
            local[i] = new Vector2(
                (point.x - x + padding) / expandedSize,
                (point.y - y + padding) / expandedSize);
            uvs[i] = point / divisions;
        }

        return new ProceduralPuzzleGeometry(local, uvs, Triangulate(local));
    }

    private static void AppendHorizontal(
        List<Vector2> output,
        EdgeProfile profile,
        float originX,
        float originY,
        bool reverse)
    {
        Append(output, profile.Points, reverse, point =>
            new Vector2(originX + point.x, originY + point.y));
    }

    private static void AppendVertical(
        List<Vector2> output,
        EdgeProfile profile,
        float originX,
        float originY,
        bool reverse)
    {
        Append(output, profile.Points, reverse, point =>
            new Vector2(originX + point.y, originY + point.x));
    }

    private static void Append(
        List<Vector2> output,
        IReadOnlyList<Vector2> source,
        bool reverse,
        Func<Vector2, Vector2> transform)
    {
        int start = reverse ? source.Count - 1 : 0;
        int end = reverse ? -1 : source.Count;
        int step = reverse ? -1 : 1;

        for (int i = start; i != end; i += step)
        {
            Vector2 point = transform(source[i]);
            if (output.Count > 0 && (output[output.Count - 1] - point).sqrMagnitude <= PointEpsilon)
                continue;
            output.Add(point);
        }
    }

    private static void RemoveDuplicateClosure(List<Vector2> points)
    {
        if (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude <= PointEpsilon)
            points.RemoveAt(points.Count - 1);
    }

    private static void RemoveCollinearPoints(List<Vector2> points)
    {
        bool removed;
        do
        {
            removed = false;
            for (int i = 0; i < points.Count && points.Count > 3; i++)
            {
                Vector2 previous = points[(i - 1 + points.Count) % points.Count];
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Count];
                if (Mathf.Abs(Cross(current - previous, next - current)) > PointEpsilon) continue;
                if (Vector2.Dot(current - previous, next - current) < 0f) continue;

                points.RemoveAt(i);
                removed = true;
                break;
            }
        } while (removed);
    }

    private static ushort[] Triangulate(IReadOnlyList<Vector2> polygon)
    {
        if (polygon.Count > ushort.MaxValue)
            throw new InvalidOperationException("Procedural piece has too many vertices.");

        var remaining = new List<int>(polygon.Count);
        for (int i = 0; i < polygon.Count; i++) remaining.Add(i);
        if (SignedArea(polygon) < 0f) remaining.Reverse();

        var triangles = new List<ushort>((polygon.Count - 2) * 3);
        int guard = polygon.Count * polygon.Count;
        while (remaining.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int previous = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int current = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];
                if (!IsEar(previous, current, next, remaining, polygon)) continue;

                triangles.Add((ushort)previous);
                triangles.Add((ushort)current);
                triangles.Add((ushort)next);
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped)
            {
                if (RemoveCollinearIndex(remaining, polygon)) continue;
                throw new InvalidOperationException("Generated cut produced a non-triangulable piece.");
            }
        }

        if (remaining.Count != 3)
            throw new InvalidOperationException("Generated cut triangulation did not complete.");

        int a = remaining[0];
        int b = remaining[1];
        int c = remaining[2];
        float finalCross = Cross(polygon[b] - polygon[a], polygon[c] - polygon[a]);
        if (finalCross < -PointEpsilon)
            throw new InvalidOperationException("Generated cut ended with a reversed triangle.");
        if (Mathf.Abs(finalCross) > PointEpsilon)
        {
            triangles.Add((ushort)a);
            triangles.Add((ushort)b);
            triangles.Add((ushort)c);
        }
        return triangles.ToArray();
    }

    private static bool RemoveCollinearIndex(List<int> indices, IReadOnlyList<Vector2> points)
    {
        for (int i = 0; i < indices.Count; i++)
        {
            Vector2 previous = points[indices[(i - 1 + indices.Count) % indices.Count]];
            Vector2 current = points[indices[i]];
            Vector2 next = points[indices[(i + 1) % indices.Count]];
            if (Mathf.Abs(Cross(current - previous, next - current)) > PointEpsilon) continue;
            if (Vector2.Dot(current - previous, next - current) < 0f) continue;
            indices.RemoveAt(i);
            return true;
        }

        return false;
    }

    private static bool IsEar(
        int previous,
        int current,
        int next,
        IReadOnlyList<int> polygonIndices,
        IReadOnlyList<Vector2> points)
    {
        Vector2 a = points[previous];
        Vector2 b = points[current];
        Vector2 c = points[next];
        if (Cross(b - a, c - b) <= PointEpsilon) return false;

        for (int i = 0; i < polygonIndices.Count; i++)
        {
            int candidate = polygonIndices[i];
            if (candidate == previous || candidate == current || candidate == next) continue;
            if (PointInTriangle(points[candidate], a, b, c)) return false;
        }

        return true;
    }

    private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float ab = Cross(b - a, point - a);
        float bc = Cross(c - b, point - b);
        float ca = Cross(a - c, point - c);
        return ab >= -PointEpsilon && bc >= -PointEpsilon && ca >= -PointEpsilon;
    }

    private static float SignedArea(IReadOnlyList<Vector2> points)
    {
        float area = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];
            area += a.x * b.y - b.x * a.y;
        }
        return area * 0.5f;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private sealed class EdgeProfile
    {
        public static readonly EdgeProfile Straight =
            new EdgeProfile(new[] { Vector2.zero, Vector2.right });

        public EdgeProfile(Vector2[] points)
        {
            Points = points ?? throw new ArgumentNullException(nameof(points));
            if (points.Length < 2 || points[0] != Vector2.zero || points[points.Length - 1] != Vector2.right)
                throw new ArgumentException("Edge profiles must run from (0,0) to (1,0).");
        }

        public IReadOnlyList<Vector2> Points { get; }
    }
}
