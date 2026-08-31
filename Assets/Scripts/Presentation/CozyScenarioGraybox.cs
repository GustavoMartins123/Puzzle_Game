using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CozyScenarioGraybox : MonoBehaviour
{
    private const float MinimumAspect = 4f / 3f;
    private const float MaximumAspect = 2.4f;
    private const float BoardHalfExtent = 3.72f;
    private const float WorkMatHalfExtent = 4.72f;

    [Header("Rendering")]
    [SerializeField] private Shader scenarioShader;
    [SerializeField, Min(4.5f)] private float orthographicSize = 5.625f;

    [Header("Palette")]
    [SerializeField] private Color backgroundColor = new Color(0.043f, 0.067f, 0.094f, 1f);
    [SerializeField] private Color walnutColor = new Color(0.169f, 0.129f, 0.11f, 1f);
    [SerializeField] private Color feltColor = new Color(0.071f, 0.16f, 0.153f, 1f);
    [SerializeField] private Color frameColor = new Color(0.255f, 0.176f, 0.122f, 1f);
    [SerializeField] private Color brassColor = new Color(0.64f, 0.46f, 0.2f, 1f);
    [SerializeField] private Color ivoryColor = new Color(0.72f, 0.68f, 0.6f, 1f);
    [SerializeField] private Color leafColor = new Color(0.18f, 0.32f, 0.22f, 1f);

    private readonly List<Material> generatedMaterials = new List<Material>();
    private Camera scenarioCamera;
    private CameraState originalCameraState;
    private LightingState originalLightingState;
    private GameObject scenarioRoot;
    private Transform tableSurface;
    private Transform topLeftAnchor;
    private Transform topRightAnchor;
    private Transform bottomLeftAnchor;
    private Transform bottomRightAnchor;
    private float appliedAspect;

    public bool IsBuilt => scenarioRoot != null;

    public Transform ScenarioRoot => scenarioRoot != null
        ? scenarioRoot.transform
        : throw new InvalidOperationException("Cozy scenario graybox is not built.");

    public Shader ScenarioShader => scenarioShader != null
        ? scenarioShader
        : throw new InvalidOperationException("Cozy scenario shader is not assigned.");

    public float CurrentAspect => IsBuilt
        ? appliedAspect
        : throw new InvalidOperationException("Cozy scenario graybox is not built.");

    public float VisibleHalfWidth => IsBuilt
        ? orthographicSize * appliedAspect
        : throw new InvalidOperationException("Cozy scenario graybox is not built.");

    public float PlayAreaHalfExtent => BoardHalfExtent;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        Build(ResolveScreenAspect());
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (!IsBuilt)
            throw new InvalidOperationException(
                "Cozy scenario graybox was not built before its runtime update.");

        float aspect = ResolveScreenAspect();
        if (!Mathf.Approximately(aspect, appliedAspect)) ApplyAspect(aspect);
    }

    private void OnDisable()
    {
        TearDown();
    }

    public void BuildForValidation(float aspect)
    {
        if (Application.isPlaying)
            throw new InvalidOperationException(
                "Editor validation build cannot run while the game is playing.");
        Build(aspect);
    }

    public void ApplyAspectForValidation(float aspect)
    {
        if (Application.isPlaying)
            throw new InvalidOperationException(
                "Editor validation layout cannot run while the game is playing.");
        ApplyAspect(aspect);
    }

    public void TearDownForValidation()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException(
                "Editor validation teardown cannot run while the game is playing.");
        TearDown();
    }

    private void Build(float aspect)
    {
        if (IsBuilt)
            throw new InvalidOperationException("Cozy scenario graybox is already built.");
        ValidateAspect(aspect);
        if (scenarioShader == null)
            throw new InvalidOperationException("Cozy scenario shader is not assigned.");
        if (!scenarioShader.isSupported)
            throw new InvalidOperationException(
                $"Cozy scenario shader '{scenarioShader.name}' is not supported.");

        scenarioCamera = GetComponent<Camera>();
        if (scenarioCamera == null)
            throw new InvalidOperationException("Cozy scenario requires a Camera component.");

        originalCameraState = CameraState.Capture(scenarioCamera);
        originalLightingState = LightingState.Capture();
        try
        {
            ConfigureCamera();
            CreateScenarioGraph();
            ApplyAspect(aspect);
        }
        catch
        {
            TearDown();
            throw;
        }
    }

    private void ConfigureCamera()
    {
        scenarioCamera.orthographic = true;
        scenarioCamera.orthographicSize = orthographicSize;
        scenarioCamera.clearFlags = CameraClearFlags.SolidColor;
        scenarioCamera.backgroundColor = backgroundColor;
        scenarioCamera.nearClipPlane = 0.1f;
        scenarioCamera.farClipPlane = 40f;
        scenarioCamera.transform.SetPositionAndRotation(
            new Vector3(0f, 12f, 0f),
            Quaternion.Euler(90f, 0f, 0f));

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.145f, 0.12f, 1f);
        RenderSettings.ambientIntensity = 0.7f;
        RenderSettings.reflectionIntensity = 0.45f;
    }

    private void CreateScenarioGraph()
    {
        scenarioRoot = new GameObject("ScenarioRoot");
        scenarioRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Material walnut = CreateMaterial("WalnutGraybox", walnutColor, 0f, 0.22f);
        Material felt = CreateMaterial("FeltGraybox", feltColor, 0f, 0.08f);
        Material workMat = CreateMaterial(
            "WorkMatGraybox",
            Color.Lerp(frameColor, feltColor, 0.58f),
            0f,
            0.06f);
        Material frame = CreateMaterial("FrameGraybox", frameColor, 0.12f, 0.24f);
        Material brass = CreateMaterial("BrassGraybox", brassColor, 0.68f, 0.42f);
        Material ivory = CreateMaterial("IvoryGraybox", ivoryColor, 0f, 0.16f);
        Material leaf = CreateMaterial("LeafGraybox", leafColor, 0f, 0.12f);
        Material ceramic = CreateMaterial(
            "CeramicGraybox",
            new Color(0.16f, 0.22f, 0.26f, 1f),
            0f,
            0.36f);

        Transform tableModule = CreateRoot("TableModule", scenarioRoot.transform);
        tableSurface = CreatePrimitive(
            PrimitiveType.Cube,
            "Surface",
            tableModule,
            walnut,
            new Vector3(0f, -0.52f, 0f),
            new Vector3(1f, 0.62f, 11.8f));

        Transform boardRecess = CreateRoot("BoardRecess", tableModule);
        CreatePrimitive(
            PrimitiveType.Cube,
            "WorkMatShadow",
            boardRecess,
            frame,
            new Vector3(0f, -0.24f, 0f),
            new Vector3(WorkMatHalfExtent * 2f + 0.24f, 0.1f, WorkMatHalfExtent * 2f + 0.24f));
        CreatePrimitive(
            PrimitiveType.Cube,
            "WorkMatSurface",
            boardRecess,
            workMat,
            new Vector3(0f, -0.18f, 0f),
            new Vector3(WorkMatHalfExtent * 2f, 0.1f, WorkMatHalfExtent * 2f));
        CreatePrimitive(
            PrimitiveType.Cube,
            "FeltSurface",
            boardRecess,
            felt,
            new Vector3(0f, -0.15f, 0f),
            new Vector3(BoardHalfExtent * 2f, 0.14f, BoardHalfExtent * 2f));

        Transform decorativeFrame = CreateRoot("DecorativeFrame", tableModule);
        const float rail = 0.16f;
        float outer = WorkMatHalfExtent + rail;
        float length = outer * 2f;
        CreatePrimitive(
            PrimitiveType.Cube,
            "FrameTop",
            decorativeFrame,
            frame,
            new Vector3(0f, -0.04f, outer),
            new Vector3(length, 0.16f, rail));
        CreatePrimitive(
            PrimitiveType.Cube,
            "FrameBottom",
            decorativeFrame,
            frame,
            new Vector3(0f, -0.04f, -outer),
            new Vector3(length, 0.16f, rail));
        CreatePrimitive(
            PrimitiveType.Cube,
            "FrameLeft",
            decorativeFrame,
            frame,
            new Vector3(-outer, -0.04f, 0f),
            new Vector3(rail, 0.16f, length));
        CreatePrimitive(
            PrimitiveType.Cube,
            "FrameRight",
            decorativeFrame,
            frame,
            new Vector3(outer, -0.04f, 0f),
            new Vector3(rail, 0.16f, length));

        Transform innerFrame = CreateRoot("BoardFrame", tableModule);
        const float innerRail = 0.1f;
        float innerOuter = BoardHalfExtent + innerRail;
        float innerLength = innerOuter * 2f;
        CreatePrimitive(
            PrimitiveType.Cube,
            "FrameTop",
            innerFrame,
            frame,
            new Vector3(0f, -0.08f, innerOuter),
            new Vector3(innerLength, 0.1f, innerRail));
        CreatePrimitive(
            PrimitiveType.Cube,
            "FrameBottom",
            innerFrame,
            frame,
            new Vector3(0f, -0.08f, -innerOuter),
            new Vector3(innerLength, 0.1f, innerRail));
        CreatePrimitive(
            PrimitiveType.Cube,
            "FrameLeft",
            innerFrame,
            frame,
            new Vector3(-innerOuter, -0.08f, 0f),
            new Vector3(innerRail, 0.1f, innerLength));
        CreatePrimitive(
            PrimitiveType.Cube,
            "FrameRight",
            innerFrame,
            frame,
            new Vector3(innerOuter, -0.08f, 0f),
            new Vector3(innerRail, 0.1f, innerLength));

        Transform anchors = CreateRoot("DecorAnchors", scenarioRoot.transform);
        topLeftAnchor = CreateRoot("TopLeft", anchors);
        topRightAnchor = CreateRoot("TopRight", anchors);
        bottomLeftAnchor = CreateRoot("BottomLeft", anchors);
        bottomRightAnchor = CreateRoot("BottomRight", anchors);
        CreateLamp(topLeftAnchor, brass, frame);
        CreatePlant(topRightAnchor, ceramic, leaf);
        CreatePuzzleBox(bottomLeftAnchor, ivory, brass);
        CreateMug(bottomRightAnchor, ceramic, ivory);

        CreateLighting(CreateRoot("LightingRig", scenarioRoot.transform));
        CreateRoot("AmbientVfx", scenarioRoot.transform);
        CreateRoot("AudioRig", scenarioRoot.transform);
    }

    private void CreateLamp(Transform parent, Material brass, Material darkMetal)
    {
        CreatePrimitive(
            PrimitiveType.Cylinder,
            "LampBase",
            parent,
            brass,
            new Vector3(0f, -0.02f, 0f),
            new Vector3(1.05f, 0.09f, 1.05f));
        Transform arm = CreatePrimitive(
            PrimitiveType.Cube,
            "LampArm",
            parent,
            darkMetal,
            new Vector3(0.38f, 0.24f, -0.18f),
            new Vector3(0.16f, 0.16f, 1.35f));
        arm.localRotation = Quaternion.Euler(0f, -34f, 0f);
        CreatePrimitive(
            PrimitiveType.Sphere,
            "LampShade",
            parent,
            brass,
            new Vector3(0.78f, 0.28f, -0.74f),
            new Vector3(0.72f, 0.28f, 0.72f));
    }

    private void CreatePlant(Transform parent, Material pot, Material leaf)
    {
        CreatePrimitive(
            PrimitiveType.Cylinder,
            "PlantPot",
            parent,
            pot,
            new Vector3(0f, 0f, 0f),
            new Vector3(0.9f, 0.16f, 0.9f));
        Vector3[] positions =
        {
            new Vector3(-0.34f, 0.22f, 0.08f),
            new Vector3(0.28f, 0.25f, 0.2f),
            new Vector3(0.08f, 0.24f, -0.34f),
            new Vector3(-0.22f, 0.2f, -0.3f),
        };
        for (int i = 0; i < positions.Length; i++)
        {
            Transform value = CreatePrimitive(
                PrimitiveType.Sphere,
                $"Leaf{i + 1}",
                parent,
                leaf,
                positions[i],
                new Vector3(0.72f, 0.24f, 0.42f));
            value.localRotation = Quaternion.Euler(0f, i * 47f, 0f);
        }
    }

    private void CreatePuzzleBox(Transform parent, Material ivory, Material brass)
    {
        CreatePrimitive(
            PrimitiveType.Cube,
            "PuzzleBox",
            parent,
            ivory,
            Vector3.zero,
            new Vector3(1.75f, 0.22f, 1.12f));
        CreatePrimitive(
            PrimitiveType.Cube,
            "BoxBand",
            parent,
            brass,
            new Vector3(0f, 0.13f, 0f),
            new Vector3(0.24f, 0.04f, 1.14f));
    }

    private void CreateMug(Transform parent, Material ceramic, Material ivory)
    {
        CreatePrimitive(
            PrimitiveType.Cylinder,
            "MugBody",
            parent,
            ceramic,
            Vector3.zero,
            new Vector3(0.92f, 0.2f, 0.92f));
        CreatePrimitive(
            PrimitiveType.Cylinder,
            "MugInterior",
            parent,
            ivory,
            new Vector3(0f, 0.21f, 0f),
            new Vector3(0.68f, 0.015f, 0.68f));
        CreatePrimitive(
            PrimitiveType.Cube,
            "MugHandle",
            parent,
            ceramic,
            new Vector3(0.6f, 0.02f, 0f),
            new Vector3(0.5f, 0.12f, 0.22f));
    }

    private void CreateLighting(Transform parent)
    {
        Light key = CreateLight("KeyLight", parent, LightType.Directional);
        key.transform.localRotation = Quaternion.Euler(48f, -32f, 0f);
        key.color = new Color(1f, 0.82f, 0.64f, 1f);
        key.intensity = 0.78f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.24f;

        Light practical = CreateLight("LampGlow", parent, LightType.Point);
        practical.transform.localPosition = new Vector3(-4.2f, 4f, 3.8f);
        practical.color = new Color(1f, 0.62f, 0.3f, 1f);
        practical.intensity = 0.82f;
        practical.range = 9f;
        practical.shadows = LightShadows.None;
    }

    private void ApplyAspect(float aspect)
    {
        if (!IsBuilt)
            throw new InvalidOperationException(
                "Cozy scenario must be built before applying an aspect ratio.");
        ValidateAspect(aspect);

        appliedAspect = aspect;
        float halfWidth = orthographicSize * aspect;
        tableSurface.localScale = new Vector3(halfWidth * 2f + 1f, 0.62f, 11.8f);

        float anchorX = Mathf.Min(halfWidth - 1.05f, WorkMatHalfExtent + 0.45f);
        float anchorZ = orthographicSize * 0.8f;
        topLeftAnchor.localPosition = new Vector3(-anchorX, 0f, anchorZ);
        topRightAnchor.localPosition = new Vector3(anchorX, 0f, anchorZ);
        bottomLeftAnchor.localPosition = new Vector3(-anchorX, 0f, -anchorZ);
        bottomRightAnchor.localPosition = new Vector3(anchorX, 0f, -anchorZ);
    }

    private Material CreateMaterial(
        string materialName,
        Color color,
        float metallic,
        float smoothness)
    {
        if (!float.IsFinite(metallic) || metallic < 0f || metallic > 1f)
            throw new ArgumentOutOfRangeException(nameof(metallic));
        if (!float.IsFinite(smoothness) || smoothness < 0f || smoothness > 1f)
            throw new ArgumentOutOfRangeException(nameof(smoothness));

        var material = new Material(ScenarioShader)
        {
            name = materialName,
            color = color,
            hideFlags = HideFlags.DontSave,
        };
        if (!material.HasProperty("_Metallic") || !material.HasProperty("_Glossiness"))
        {
            DestroyValue(material);
            throw new InvalidOperationException(
                $"Scenario shader '{ScenarioShader.name}' lacks Standard material properties.");
        }
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        generatedMaterials.Add(material);
        return material;
    }

    private static Transform CreateRoot(string rootName, Transform parent)
    {
        var root = new GameObject(rootName);
        root.transform.SetParent(parent, false);
        return root.transform;
    }

    private static Transform CreatePrimitive(
        PrimitiveType type,
        string primitiveName,
        Transform parent,
        Material material,
        Vector3 localPosition,
        Vector3 localScale)
    {
        if (material == null) throw new ArgumentNullException(nameof(material));
        var value = GameObject.CreatePrimitive(type);
        value.name = primitiveName;
        value.transform.SetParent(parent, false);
        value.transform.localPosition = localPosition;
        value.transform.localRotation = Quaternion.identity;
        value.transform.localScale = localScale;

        Collider collider = value.GetComponent<Collider>();
        if (collider == null)
            throw new InvalidOperationException($"Primitive '{primitiveName}' has no collider to remove.");
        collider.enabled = false;
        DestroyValue(collider);

        Renderer renderer = value.GetComponent<Renderer>();
        if (renderer == null)
            throw new InvalidOperationException($"Primitive '{primitiveName}' has no renderer.");
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return value.transform;
    }

    private static Light CreateLight(string lightName, Transform parent, LightType type)
    {
        var lightObject = new GameObject(lightName, typeof(Light));
        lightObject.transform.SetParent(parent, false);
        Light value = lightObject.GetComponent<Light>();
        value.type = type;
        return value;
    }

    private void TearDown()
    {
        if (scenarioRoot != null)
        {
            DestroyValue(scenarioRoot);
            scenarioRoot = null;
        }

        for (int i = generatedMaterials.Count - 1; i >= 0; i--)
            if (generatedMaterials[i] != null) DestroyValue(generatedMaterials[i]);
        generatedMaterials.Clear();

        if (scenarioCamera != null && originalCameraState != null)
            originalCameraState.Restore(scenarioCamera);
        if (originalLightingState != null)
            originalLightingState.Restore();
        scenarioCamera = null;
        originalCameraState = null;
        originalLightingState = null;
        tableSurface = null;
        topLeftAnchor = null;
        topRightAnchor = null;
        bottomLeftAnchor = null;
        bottomRightAnchor = null;
        appliedAspect = 0f;
    }

    private static float ResolveScreenAspect()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            throw new InvalidOperationException(
                $"Invalid screen size {Screen.width}x{Screen.height} for cozy scenario.");
        float aspect = (float)Screen.width / Screen.height;
        ValidateAspect(aspect);
        return aspect;
    }

    private static void ValidateAspect(float aspect)
    {
        if (!float.IsFinite(aspect) || aspect < MinimumAspect || aspect > MaximumAspect)
            throw new ArgumentOutOfRangeException(
                nameof(aspect),
                aspect,
                $"Cozy scenario supports aspect ratios from {MinimumAspect:0.0} to " +
                $"{MaximumAspect:0.0}.");
    }

    private static void DestroyValue(UnityEngine.Object value)
    {
        if (value == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(value);
            return;
        }
#endif
        Destroy(value);
    }

    private sealed class CameraState
    {
        private readonly bool orthographic;
        private readonly float orthographicSize;
        private readonly CameraClearFlags clearFlags;
        private readonly Color backgroundColor;
        private readonly float nearClipPlane;
        private readonly float farClipPlane;
        private readonly Vector3 position;
        private readonly Quaternion rotation;

        private CameraState(Camera camera)
        {
            orthographic = camera.orthographic;
            orthographicSize = camera.orthographicSize;
            clearFlags = camera.clearFlags;
            backgroundColor = camera.backgroundColor;
            nearClipPlane = camera.nearClipPlane;
            farClipPlane = camera.farClipPlane;
            position = camera.transform.position;
            rotation = camera.transform.rotation;
        }

        public static CameraState Capture(Camera camera) => camera != null
            ? new CameraState(camera)
            : throw new ArgumentNullException(nameof(camera));

        public void Restore(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            camera.orthographic = orthographic;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = clearFlags;
            camera.backgroundColor = backgroundColor;
            camera.nearClipPlane = nearClipPlane;
            camera.farClipPlane = farClipPlane;
            camera.transform.SetPositionAndRotation(position, rotation);
        }
    }

    private sealed class LightingState
    {
        private readonly AmbientMode ambientMode;
        private readonly Color ambientLight;
        private readonly float ambientIntensity;
        private readonly float reflectionIntensity;

        private LightingState()
        {
            ambientMode = RenderSettings.ambientMode;
            ambientLight = RenderSettings.ambientLight;
            ambientIntensity = RenderSettings.ambientIntensity;
            reflectionIntensity = RenderSettings.reflectionIntensity;
        }

        public static LightingState Capture() => new LightingState();

        public void Restore()
        {
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientLight;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.reflectionIntensity = reflectionIntensity;
        }
    }
}
