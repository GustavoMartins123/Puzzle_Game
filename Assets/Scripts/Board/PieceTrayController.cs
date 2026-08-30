using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum PieceTrayLayoutMode
{
    SidePanels,
    BottomPanel,
}

public readonly struct PieceTrayLayout
{
    public PieceTrayLayout(
        PieceTrayLayoutMode mode,
        Vector2 panelSize,
        int columns,
        int rows,
        int capacityPerPage,
        int pageCount,
        Vector2 cellSize,
        float pieceScale,
        Vector2 boardOffset)
    {
        Mode = mode;
        PanelSize = panelSize;
        Columns = columns;
        Rows = rows;
        CapacityPerPage = capacityPerPage;
        PageCount = pageCount;
        CellSize = cellSize;
        PieceScale = pieceScale;
        BoardOffset = boardOffset;
    }

    public PieceTrayLayoutMode Mode { get; }

    public Vector2 PanelSize { get; }

    public int Columns { get; }

    public int Rows { get; }

    public int CapacityPerPage { get; }

    public int PageCount { get; }

    public Vector2 CellSize { get; }

    public float PieceScale { get; }

    public Vector2 BoardOffset { get; }
}

[RequireComponent(typeof(RectTransform))]
public sealed class PieceTrayController : MonoBehaviour
{
    private const float NarrowAspectThreshold = 1.5f;
    private const float EdgeMargin = 20f;
    private const float CentralClearance = 40f;
    private const float PreferredSidePanelWidth = 360f;
    private const float MinimumSidePanelWidth = 300f;
    private const float BottomPanelHeight = 280f;
    private const float HeaderHeight = 44f;
    private const float FooterHeight = 58f;
    private const float PanelPadding = 12f;
    private const float CellGap = 10f;
    private const float CellPadding = 8f;
    private const float MinimumCellExtent = 140f;
    private const float MinimumPieceScale = 0.28f;

    private static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.085f, 0.96f);
    private static readonly Color CellColor = new Color(0.075f, 0.105f, 0.15f, 0.96f);
    private static readonly Color SelectedCellColor = new Color(0.12f, 0.38f, 0.38f, 1f);
    private static readonly Color EmptyCellColor = new Color(0.045f, 0.065f, 0.09f, 0.7f);
    private static readonly Color AccentColor = new Color(0.25f, 0.92f, 0.78f, 1f);

    private readonly Dictionary<PuzzlePiece, TrayCell> cellsByPiece =
        new Dictionary<PuzzlePiece, TrayCell>();
    private readonly List<PageView> pages = new List<PageView>();
    private readonly List<GameObject> runtimeRoots = new List<GameObject>();

    private RectTransform root;
    private RectTransform board;
    private RectTransform reference;
    private PuzzlePiece activeDrag;
    private Text pageLabel;
    private Button previousButton;
    private Button nextButton;
    private Font font;
    private PieceTrayLayout layout;
    private int currentPage;
    private bool configured;

    public RectTransform Root
    {
        get
        {
            if (root == null) root = (RectTransform)transform;
            return root;
        }
    }

    public bool IsConfigured => configured;

    public int PageCount => configured ? layout.PageCount : 0;

    public int CurrentPage => configured ? currentPage : -1;

    public PieceTrayLayout Layout => configured
        ? layout
        : throw new InvalidOperationException("Piece tray is not configured.");

    private void Awake() => root = (RectTransform)transform;

    public static bool TryCalculateLayout(
        Vector2 viewportSize,
        Vector2 boardSize,
        int pieceCount,
        Vector2 pieceVisualSize,
        float maximumTilt,
        out PieceTrayLayout result,
        out string error)
    {
        result = default;
        if (!IsPositiveFinite(viewportSize))
        {
            error = "viewport size must be positive and finite";
            return false;
        }

        if (!IsPositiveFinite(boardSize))
        {
            error = "board size must be positive and finite";
            return false;
        }

        if (pieceCount <= 0)
        {
            error = "at least one piece is required";
            return false;
        }

        if (!IsPositiveFinite(pieceVisualSize))
        {
            error = "piece size must be positive and finite";
            return false;
        }

        if (!float.IsFinite(maximumTilt) || maximumTilt < 0f || maximumTilt > 30f)
        {
            error = "maximum tilt must be between 0 and 30 degrees";
            return false;
        }

        PieceTrayLayoutMode mode = viewportSize.x / viewportSize.y >= NarrowAspectThreshold
            ? PieceTrayLayoutMode.SidePanels
            : PieceTrayLayoutMode.BottomPanel;

        Vector2 panelSize;
        Vector2 cellSize;
        Vector2 boardOffset;
        int columns;
        int rows;
        int capacityPerPage;

        if (mode == PieceTrayLayoutMode.SidePanels)
        {
            float maximumPanelWidth =
                (viewportSize.x - boardSize.x - CentralClearance * 2f - EdgeMargin * 2f) * 0.5f;
            float panelWidth = Mathf.Min(PreferredSidePanelWidth, maximumPanelWidth);
            if (panelWidth < MinimumSidePanelWidth)
            {
                error = $"side tray width {panelWidth:F1} is below the required " +
                        $"{MinimumSidePanelWidth:F1}";
                return false;
            }

            float panelHeight = viewportSize.y - EdgeMargin * 2f;
            float contentHeight = panelHeight - HeaderHeight - FooterHeight;
            columns = pieceCount <= 8 ? 1 : 2;
            rows = columns == 1 ? Mathf.CeilToInt(pieceCount * 0.5f) : pieceCount <= 12 ? 3 : 4;
            rows = Mathf.Clamp(rows, 1, 4);
            capacityPerPage = columns * rows * 2;
            panelSize = new Vector2(panelWidth, panelHeight);
            cellSize = new Vector2(
                (panelWidth - PanelPadding * 2f - CellGap * (columns - 1)) / columns,
                (contentHeight - CellGap * (rows - 1)) / rows);
            boardOffset = Vector2.zero;
        }
        else
        {
            float availableBoardHeight =
                viewportSize.y - EdgeMargin * 2f - BottomPanelHeight - CentralClearance;
            if (boardSize.y > availableBoardHeight)
            {
                error = $"board height {boardSize.y:F1} exceeds the narrow viewport allowance " +
                        $"{availableBoardHeight:F1}";
                return false;
            }

            float panelWidth = viewportSize.x - EdgeMargin * 2f;
            float contentWidth = panelWidth - PanelPadding * 2f;
            int maximumColumns = Mathf.FloorToInt(
                (contentWidth + CellGap) / (MinimumCellExtent + CellGap));
            if (maximumColumns <= 0)
            {
                error = "bottom tray cannot fit one cell";
                return false;
            }

            columns = Mathf.Min(pieceCount, maximumColumns);
            rows = 1;
            capacityPerPage = columns;
            panelSize = new Vector2(panelWidth, BottomPanelHeight);
            cellSize = new Vector2(
                (contentWidth - CellGap * (columns - 1)) / columns,
                BottomPanelHeight - HeaderHeight - FooterHeight);
            boardOffset = new Vector2(0f, (BottomPanelHeight + CentralClearance) * 0.5f);
        }

        if (!IsPositiveFinite(cellSize))
        {
            error = "calculated tray cells are invalid";
            return false;
        }

        float radians = maximumTilt * Mathf.Deg2Rad;
        float cosine = Mathf.Abs(Mathf.Cos(radians));
        float sine = Mathf.Abs(Mathf.Sin(radians));
        Vector2 rotatedBounds = new Vector2(
            pieceVisualSize.x * cosine + pieceVisualSize.y * sine,
            pieceVisualSize.x * sine + pieceVisualSize.y * cosine);
        Vector2 availablePieceSize = cellSize - Vector2.one * (CellPadding * 2f);
        float pieceScale = Mathf.Min(
            1f,
            availablePieceSize.x / rotatedBounds.x,
            availablePieceSize.y / rotatedBounds.y);
        if (!float.IsFinite(pieceScale) || pieceScale < MinimumPieceScale)
        {
            error = $"piece display scale {pieceScale:F3} is below the readable minimum " +
                    $"{MinimumPieceScale:F2}";
            return false;
        }

        int pageCount = Mathf.CeilToInt(pieceCount / (float)capacityPerPage);
        result = new PieceTrayLayout(
            mode,
            panelSize,
            columns,
            rows,
            capacityPerPage,
            pageCount,
            cellSize,
            pieceScale,
            boardOffset);
        error = string.Empty;
        return true;
    }

    public bool TryConfigure(
        IReadOnlyList<PuzzlePiece> pieces,
        Vector2 pieceVisualSize,
        float maximumTilt,
        int traySeed,
        RectTransform board,
        RectTransform reference,
        out string error)
    {
        if (configured || runtimeRoots.Count != 0 || cellsByPiece.Count != 0)
        {
            error = "clear the existing tray before configuring another session";
            return false;
        }

        if (pieces == null || pieces.Count == 0)
        {
            error = "at least one puzzle piece is required";
            return false;
        }

        if (traySeed <= 0)
        {
            error = "tray seed must be positive";
            return false;
        }

        if (board == null || reference == null)
        {
            error = "board and reference transforms are required";
            return false;
        }

        var uniquePieces = new HashSet<PuzzlePiece>();
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] == null)
            {
                error = $"piece {i} is missing";
                return false;
            }

            if (!uniquePieces.Add(pieces[i]))
            {
                error = $"piece {pieces[i].name} is duplicated";
                return false;
            }
        }

        Canvas.ForceUpdateCanvases();
        Vector2 viewportSize = Root.rect.size;
        Vector2 boardSize = Vector2.Max(board.rect.size, reference.rect.size);
        if (!TryCalculateLayout(
                viewportSize,
                boardSize,
                pieces.Count,
                pieceVisualSize,
                maximumTilt,
                out PieceTrayLayout calculatedLayout,
                out error))
            return false;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            error = "Unity built-in font 'LegacyRuntime.ttf' is unavailable";
            return false;
        }

        this.board = board;
        this.reference = reference;
        layout = calculatedLayout;
        board.anchoredPosition = layout.BoardOffset;
        reference.anchoredPosition = layout.BoardOffset;

        var orderedPieces = new List<PuzzlePiece>(pieces.Count);
        for (int i = 0; i < pieces.Count; i++) orderedPieces.Add(pieces[i]);
        var random = new System.Random(traySeed);
        for (int i = orderedPieces.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (orderedPieces[i], orderedPieces[swapIndex]) =
                (orderedPieces[swapIndex], orderedPieces[i]);
        }

        if (layout.Mode == PieceTrayLayoutMode.SidePanels)
            BuildSidePanels(orderedPieces, maximumTilt, random);
        else
            BuildBottomPanel(orderedPieces, maximumTilt, random);

        BuildNavigation();
        configured = true;
        currentPage = 0;
        ApplyPage(0);
        return true;
    }

    public void BeginDrag(PuzzlePiece piece)
    {
        if (!configured) throw new InvalidOperationException("Piece tray is not configured.");
        if (piece == null) throw new ArgumentNullException(nameof(piece));
        if (activeDrag != null)
            throw new InvalidOperationException("Another piece is already reserved for dragging.");
        if (!cellsByPiece.TryGetValue(piece, out TrayCell cell))
            throw new InvalidOperationException($"Piece {piece.name} has no tray cell.");
        if (cell.PageIndex != currentPage)
            throw new InvalidOperationException(
                $"Piece {piece.name} belongs to inactive page {cell.PageIndex + 1}.");

        activeDrag = piece;
        cell.Background.color = SelectedCellColor;
        RefreshNavigation();
    }

    public void ReturnToCell(PuzzlePiece piece)
    {
        if (!configured) throw new InvalidOperationException("Piece tray is not configured.");
        if (piece == null) throw new ArgumentNullException(nameof(piece));
        if (!cellsByPiece.TryGetValue(piece, out TrayCell cell))
            throw new InvalidOperationException($"Piece {piece.name} has no reserved tray cell.");
        if (cell.PageIndex != currentPage)
            throw new InvalidOperationException(
                $"Cannot return {piece.name} to inactive page {cell.PageIndex + 1}.");
        if (activeDrag != null && activeDrag != piece)
            throw new InvalidOperationException("The reserved drag belongs to another piece.");

        piece.AttachToTrayCell(cell.RectTransform, layout.PieceScale, cell.TiltDegrees);
        cell.Background.color = CellColor;
        activeDrag = null;
        RefreshNavigation();
    }

    public void CommitPlacement(PuzzlePiece piece)
    {
        if (!configured) throw new InvalidOperationException("Piece tray is not configured.");
        if (piece == null) throw new ArgumentNullException(nameof(piece));
        if (!cellsByPiece.TryGetValue(piece, out TrayCell cell))
            throw new InvalidOperationException($"Piece {piece.name} has no reserved tray cell.");
        if (activeDrag != piece)
            throw new InvalidOperationException($"Piece {piece.name} is not the active drag.");

        cell.Background.color = EmptyCellColor;
        cellsByPiece.Remove(piece);
        activeDrag = null;
        RefreshNavigation();
    }

    public void RevealPiece(PuzzlePiece piece)
    {
        if (!configured) throw new InvalidOperationException("Piece tray is not configured.");
        if (piece == null) throw new ArgumentNullException(nameof(piece));
        if (activeDrag != null)
            throw new InvalidOperationException("Cannot reveal a hint while dragging a piece.");
        if (!cellsByPiece.TryGetValue(piece, out TrayCell cell))
            throw new InvalidOperationException($"Piece {piece.name} is not pending in the tray.");

        if (cell.PageIndex != currentPage) ApplyPage(cell.PageIndex);
    }

    public void Clear()
    {
        activeDrag = null;
        cellsByPiece.Clear();
        pages.Clear();

        if (board != null) board.anchoredPosition = Vector2.zero;
        if (reference != null) reference.anchoredPosition = Vector2.zero;
        board = null;
        reference = null;

        for (int i = 0; i < runtimeRoots.Count; i++)
            if (runtimeRoots[i] != null) Destroy(runtimeRoots[i]);
        runtimeRoots.Clear();

        pageLabel = null;
        previousButton = null;
        nextButton = null;
        currentPage = 0;
        configured = false;
    }

    private void BuildSidePanels(
        IReadOnlyList<PuzzlePiece> orderedPieces,
        float maximumTilt,
        System.Random random)
    {
        RectTransform leftPanel = CreatePanel(
            "LeftTray",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(EdgeMargin, 0f),
            layout.PanelSize);
        RectTransform rightPanel = CreatePanel(
            "RightTray",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-EdgeMargin, 0f),
            layout.PanelSize);
        CreateHeader(leftPanel, "PEÇAS");
        CreateHeader(rightPanel, "PEÇAS");

        int capacityPerPanel = layout.Columns * layout.Rows;
        for (int pageIndex = 0; pageIndex < layout.PageCount; pageIndex++)
        {
            RectTransform leftPage = CreatePageRoot(leftPanel, $"Page_{pageIndex + 1}");
            RectTransform rightPage = CreatePageRoot(rightPanel, $"Page_{pageIndex + 1}");
            pages.Add(new PageView(leftPage.gameObject, rightPage.gameObject));

            int pageStart = pageIndex * layout.CapacityPerPage;
            int pageEnd = Mathf.Min(pageStart + layout.CapacityPerPage, orderedPieces.Count);
            for (int pieceIndex = pageStart; pieceIndex < pageEnd; pieceIndex++)
            {
                int withinPage = pieceIndex - pageStart;
                bool useRightPanel = withinPage >= capacityPerPanel;
                int withinPanel = useRightPanel ? withinPage - capacityPerPanel : withinPage;
                RectTransform pageRoot = useRightPanel ? rightPage : leftPage;
                CreateCell(
                    pageRoot,
                    orderedPieces[pieceIndex],
                    pageIndex,
                    withinPanel,
                    maximumTilt,
                    random);
            }
        }
    }

    private void BuildBottomPanel(
        IReadOnlyList<PuzzlePiece> orderedPieces,
        float maximumTilt,
        System.Random random)
    {
        RectTransform panel = CreatePanel(
            "BottomTray",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, EdgeMargin),
            layout.PanelSize);
        CreateHeader(panel, "PEÇAS");

        for (int pageIndex = 0; pageIndex < layout.PageCount; pageIndex++)
        {
            RectTransform pageRoot = CreatePageRoot(panel, $"Page_{pageIndex + 1}");
            pages.Add(new PageView(pageRoot.gameObject));

            int pageStart = pageIndex * layout.CapacityPerPage;
            int pageEnd = Mathf.Min(pageStart + layout.CapacityPerPage, orderedPieces.Count);
            for (int pieceIndex = pageStart; pieceIndex < pageEnd; pieceIndex++)
            {
                CreateCell(
                    pageRoot,
                    orderedPieces[pieceIndex],
                    pageIndex,
                    pieceIndex - pageStart,
                    maximumTilt,
                    random);
            }
        }
    }

    private RectTransform CreatePanel(
        string name,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        GameObject panelObject = CreateUiObject(name, Root);
        runtimeRoots.Add(panelObject);
        RectTransform panel = (RectTransform)panelObject.transform;
        SetRect(panel, position, size, anchor, pivot);
        Image image = panelObject.AddComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = false;
        return panel;
    }

    private void CreateHeader(RectTransform panel, string label)
    {
        RectTransform header = CreateRect("Header", panel);
        SetRect(
            header,
            Vector2.zero,
            new Vector2(0f, HeaderHeight),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
        header.anchorMax = Vector2.one;
        header.offsetMin = new Vector2(PanelPadding, -HeaderHeight);
        header.offsetMax = new Vector2(-PanelPadding, 0f);

        Text text = header.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = label;
        text.fontSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.alignByGeometry = true;
        text.raycastTarget = false;
    }

    private RectTransform CreatePageRoot(RectTransform panel, string name)
    {
        RectTransform page = CreateRect(name, panel);
        Stretch(page);
        page.offsetMin = new Vector2(PanelPadding, FooterHeight);
        page.offsetMax = new Vector2(-PanelPadding, -HeaderHeight);
        return page;
    }

    private void CreateCell(
        RectTransform pageRoot,
        PuzzlePiece piece,
        int pageIndex,
        int cellIndex,
        float maximumTilt,
        System.Random random)
    {
        int column = cellIndex % layout.Columns;
        int row = cellIndex / layout.Columns;
        if (row >= layout.Rows)
            throw new InvalidOperationException(
                $"Cell {cellIndex} exceeds the calculated tray page capacity.");

        RectTransform cellRect = CreateRect($"Cell_{piece.Id:D2}", pageRoot);
        SetRect(
            cellRect,
            new Vector2(
                column * (layout.CellSize.x + CellGap),
                -row * (layout.CellSize.y + CellGap)),
            layout.CellSize,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
        Image background = cellRect.gameObject.AddComponent<Image>();
        background.color = CellColor;
        background.raycastTarget = false;
        Outline outline = cellRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.3f);
        outline.effectDistance = Vector2.one;
        outline.useGraphicAlpha = true;

        float tilt = Mathf.Lerp(
            -maximumTilt,
            maximumTilt,
            (float)random.NextDouble());
        var cell = new TrayCell(cellRect, background, pageIndex, tilt);
        cellsByPiece.Add(piece, cell);
        piece.AssignTray(this);
        piece.AttachToTrayCell(cellRect, layout.PieceScale, tilt);
    }

    private void BuildNavigation()
    {
        if (layout.PageCount <= 1) return;

        RectTransform navigation = CreateRect("TrayNavigation", Root);
        runtimeRoots.Add(navigation.gameObject);
        SetRect(
            navigation,
            new Vector2(0f, EdgeMargin + 6f),
            new Vector2(300f, 46f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f));
        Image background = navigation.gameObject.AddComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = true;

        previousButton = CreateNavigationButton(navigation, "Previous", "‹", false);
        nextButton = CreateNavigationButton(navigation, "Next", "›", true);

        RectTransform labelRect = CreateRect("PageLabel", navigation);
        SetRect(
            labelRect,
            Vector2.zero,
            new Vector2(174f, 40f),
            Vector2.one * 0.5f,
            Vector2.one * 0.5f);
        pageLabel = labelRect.gameObject.AddComponent<Text>();
        pageLabel.font = font;
        pageLabel.fontSize = 17;
        pageLabel.fontStyle = FontStyle.Bold;
        pageLabel.color = Color.white;
        pageLabel.alignment = TextAnchor.MiddleCenter;
        pageLabel.alignByGeometry = true;
        pageLabel.raycastTarget = false;
    }

    private Button CreateNavigationButton(
        RectTransform parent,
        string name,
        string label,
        bool alignRight)
    {
        RectTransform buttonRect = CreateRect(name, parent);
        SetRect(
            buttonRect,
            new Vector2(alignRight ? -4f : 4f, 3f),
            new Vector2(54f, 40f),
            new Vector2(alignRight ? 1f : 0f, 0f),
            new Vector2(alignRight ? 1f : 0f, 0f));
        Image image = buttonRect.gameObject.AddComponent<Image>();
        image.color = AccentColor;
        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => ChangePage(alignRight ? 1 : -1));

        RectTransform labelRect = CreateRect("Label", buttonRect);
        Stretch(labelRect);
        Text text = labelRect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = label;
        text.fontSize = 28;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(0.02f, 0.05f, 0.06f, 1f);
        text.alignment = TextAnchor.MiddleCenter;
        text.alignByGeometry = true;
        text.raycastTarget = false;
        return button;
    }

    private void ChangePage(int direction)
    {
        if (!configured) throw new InvalidOperationException("Piece tray is not configured.");
        if (activeDrag != null)
            throw new InvalidOperationException("Cannot change tray pages while dragging a piece.");

        int targetPage = currentPage + direction;
        if (targetPage < 0 || targetPage >= layout.PageCount)
            throw new ArgumentOutOfRangeException(nameof(direction), "Requested tray page is invalid.");
        ApplyPage(targetPage);
    }

    private void ApplyPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));

        currentPage = pageIndex;
        for (int i = 0; i < pages.Count; i++) pages[i].SetActive(i == pageIndex);
        RefreshNavigation();
    }

    private void RefreshNavigation()
    {
        if (pageLabel == null || previousButton == null || nextButton == null) return;
        pageLabel.text = $"PÁGINA {currentPage + 1}/{layout.PageCount}";
        previousButton.interactable = activeDrag == null && currentPage > 0;
        nextButton.interactable = activeDrag == null && currentPage < layout.PageCount - 1;
    }

    private static bool IsPositiveFinite(Vector2 value) =>
        float.IsFinite(value.x) && float.IsFinite(value.y) && value.x > 0f && value.y > 0f;

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = parent.gameObject.layer;
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static RectTransform CreateRect(string name, Transform parent) =>
        (RectTransform)CreateUiObject(name, parent).transform;

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size,
        Vector2 anchor,
        Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private sealed class TrayCell
    {
        public TrayCell(
            RectTransform rectTransform,
            Image background,
            int pageIndex,
            float tiltDegrees)
        {
            RectTransform = rectTransform;
            Background = background;
            PageIndex = pageIndex;
            TiltDegrees = tiltDegrees;
        }

        public RectTransform RectTransform { get; }

        public Image Background { get; }

        public int PageIndex { get; }

        public float TiltDegrees { get; }
    }

    private sealed class PageView
    {
        private readonly GameObject[] roots;

        public PageView(params GameObject[] roots) =>
            this.roots = roots ?? throw new ArgumentNullException(nameof(roots));

        public void SetActive(bool value)
        {
            for (int i = 0; i < roots.Length; i++) roots[i].SetActive(value);
        }
    }
}
