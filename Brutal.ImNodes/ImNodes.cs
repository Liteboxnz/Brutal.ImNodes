// C# port for the Brutal framework (imnodes).
// https://github.com/Nelarius/imnodes
//
// The MIT License (MIT)
// Copyright (c) 2019 Johann Muszynski
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Text;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace Brutal.ImNodesAPI;

public enum ImNodesCol
{
    NodeBackground = 0,
    NodeBackgroundHovered,
    NodeBackgroundSelected,
    NodeOutline,
    TitleBar,
    TitleBarHovered,
    TitleBarSelected,
    Link,
    LinkHovered,
    LinkSelected,
    Pin,
    PinHovered,
    BoxSelector,
    BoxSelectorOutline,
    GridBackground,
    GridLine,
    GridLinePrimary,
    MiniMapBackground,
    MiniMapBackgroundHovered,
    MiniMapOutline,
    MiniMapOutlineHovered,
    MiniMapNodeBackground,
    MiniMapNodeBackgroundHovered,
    MiniMapNodeBackgroundSelected,
    MiniMapNodeOutline,
    MiniMapLink,
    MiniMapLinkSelected,
    MiniMapCanvas,
    MiniMapCanvasOutline,
    Count,
}

public enum ImNodesStyleVar
{
    GridSpacing = 0,
    NodeCornerRounding,
    NodePadding,
    NodeBorderThickness,
    LinkThickness,
    LinkLineSegmentsPerLength,
    LinkHoverDistance,
    PinCircleRadius,
    PinQuadSideLength,
    PinTriangleSideLength,
    PinLineThickness,
    PinHoverRadius,
    PinOffset,
    MiniMapPadding,
    MiniMapOffset,
    Count,
}

[Flags]
public enum ImNodesStyleFlags
{
    None = 0,
    NodeOutline = 1 << 0,
    GridLines = 1 << 2,
    GridLinesPrimary = 1 << 3,
    GridSnapping = 1 << 4,
}

public enum ImNodesPinShape
{
    Circle,
    CircleFilled,
    Triangle,
    TriangleFilled,
    Quad,
    QuadFilled,
}

/// <summary>Controls how attribute pins behave. Combine values with a bitwise or.</summary>
[Flags]
public enum ImNodesAttributeFlags
{
    None = 0,
    /// <summary>
    /// Detach a link by dragging it off a pin. The link stays until the application sees
    /// <see cref="ImNodes.IsLinkDestroyed"/> and stops submitting it.
    /// </summary>
    EnableLinkDetachWithDragClick = 1 << 0,
    /// <summary>
    /// Snapping a dragged link onto a pin reports <see cref="ImNodes.IsLinkCreated"/> immediately.
    /// </summary>
    EnableLinkCreationOnSnap = 1 << 1,
}

public enum ImNodesMiniMapLocation
{
    BottomLeft,
    BottomRight,
    TopLeft,
    TopRight,
}

public delegate void ImNodesMiniMapNodeHoverCallback(int nodeId, object? userData);

public sealed class ImNodesIo
{
    public sealed class ModifierButton
    {
        /// <summary>
        /// Polled while the editor handles input. Return the live modifier state.
        /// Leave null to disable the feature. <see cref="ImNodesIo.MultipleSelectModifier"/> uses Ctrl when this is null.
        /// </summary>
        public Func<bool>? Modifier;
    }

    public ModifierButton EmulateThreeButtonMouse { get; } = new();
    public ModifierButton LinkDetachWithModifierClick { get; } = new();
    public ModifierButton MultipleSelectModifier { get; } = new();

    /// <summary>Mouse button that pans the canvas. Middle button by default.</summary>
    public ImGuiMouseButton AltMouseButton = ImGuiMouseButton.Middle;

    /// <summary>Pan speed, in pixels per second, while dragging outside the editor.</summary>
    public float AutoPanningSpeed = 1000f;
}

public sealed class ImNodesStyle
{
    public float GridSpacing = 24f;
    public float NodeCornerRounding = 4f;
    public float2 NodePadding = new(8f, 8f);
    public float NodeBorderThickness = 1f;
    public float LinkThickness = 3f;
    public float LinkLineSegmentsPerLength = 0.1f;
    public float LinkHoverDistance = 10f;
    public float PinCircleRadius = 4f;
    public float PinQuadSideLength = 7f;
    public float PinTriangleSideLength = 9.5f;
    public float PinLineThickness = 1f;
    public float PinHoverRadius = 10f;
    public float PinOffset = 0f;
    public float2 MiniMapPadding = new(8f, 8f);
    public float2 MiniMapOffset = new(4f, 4f);
    public ImNodesStyleFlags Flags = ImNodesStyleFlags.NodeOutline | ImNodesStyleFlags.GridLines;
    public uint[] Colors = new uint[(int)ImNodesCol.Count];
}

/// <summary>One node workspace, created with a single Begin/EndNodeEditor pair.</summary>
public sealed class ImNodesEditorContext
{
    internal ImNodesEditorContext()
    {
    }

    internal readonly ObjectPool<NodeData> Nodes = new();
    internal readonly ObjectPool<PinData> Pins = new();
    internal readonly ObjectPool<LinkData> Links = new();
    internal readonly List<int> NodeDepthOrder = new();
    internal float2 Panning;
    internal float2 AutoPanningDelta;
    internal floatRect GridContentBounds;
    internal readonly List<int> SelectedNodeIndices = new();
    internal readonly List<int> SelectedLinkIndices = new();
    internal readonly List<float2> SelectedNodeOffsets = new();
    internal float2 PrimaryNodeOffset;
    internal ClickInteractionState ClickInteraction = new();
    internal bool MiniMapEnabled;
    internal ImNodesMiniMapLocation MiniMapLocation;
    internal float MiniMapSizeFraction;
    internal ImNodesMiniMapNodeHoverCallback? MiniMapNodeHoveringCallback;
    internal object? MiniMapNodeHoveringCallbackUserData;
    internal floatRect MiniMapRectScreenSpace;
    internal floatRect MiniMapContentScreenSpace;
    internal float MiniMapScaling;
}

public sealed class ImNodesContext
{
    internal ImNodesContext()
    {
    }

    internal ImNodesEditorContext? DefaultEditorCtx;
    internal ImNodesEditorContext? EditorCtx;
    internal ImDrawListPtr CanvasDrawList;
    internal readonly Dictionary<int, int> NodeIdxToSubmissionIdx = new();
    internal readonly List<int> NodeIdxSubmissionOrder = new();
    internal readonly List<int> NodeIndicesOverlappingWithMouse = new();
    internal readonly List<int> OccludedPinIndices = new();
    internal float2 CanvasOriginScreenSpace;
    internal floatRect CanvasRectScreenSpace;
    internal ImNodesScope CurrentScope = ImNodesScope.None;
    internal readonly ImNodesIo Io = new();
    internal readonly ImNodesStyle Style = new();
    internal readonly List<ColorElement> ColorModifierStack = new();
    internal readonly List<StyleVarElement> StyleModifierStack = new();
    internal int CurrentAttributeFlags;
    internal readonly List<int> AttributeFlagStack = new();
    internal int CurrentNodeIdx;
    internal int CurrentPinIdx;
    internal int CurrentAttributeId;
    internal OptionalIndex HoveredNodeIdx;
    internal OptionalIndex HoveredLinkIdx;
    internal OptionalIndex HoveredPinIdx;
    internal OptionalIndex DeletedLinkIdx;
    internal OptionalIndex SnapLinkIdx;
    internal int UiState;
    internal int ActiveAttributeId;
    internal bool ActiveAttribute;
    internal float2 MousePos;
    internal bool LeftMouseClicked;
    internal bool LeftMouseReleased;
    internal bool AltMouseClicked;
    internal bool LeftMouseDragging;
    internal bool AltMouseDragging;
    internal float AltMouseScrollDelta;
    internal bool MultipleSelectModifier;
}

/// <summary>Node editor for Brutal.ImGui. Behavior follows Nelarius/imnodes.</summary>
public static partial class ImNodes
{
    /// <summary>Points this library at an existing ImGui context.</summary>
    public static void SetImGuiContext(ImGuiContextPtr ctx) => ImGui.SetCurrentContext(ctx);

    public static ImNodesContext CreateContext()
    {
        var ctx = new ImNodesContext();
        if (GImNodes == null)
            SetCurrentContext(ctx);
        Initialize(ctx);
        return ctx;
    }

    public static void DestroyContext(ImNodesContext? ctx = null)
    {
        ctx ??= GImNodes ?? throw new InvalidOperationException("No ImNodes context exists.");
        Shutdown(ctx);
        if (ReferenceEquals(GImNodes, ctx))
            SetCurrentContext(null);
    }

    public static ImNodesContext? GetCurrentContext() => GImNodes;

    public static void SetCurrentContext(ImNodesContext? ctx) => GImNodes = ctx;

    public static ImNodesEditorContext EditorContextCreate() => new();

    public static void EditorContextFree(ImNodesEditorContext editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (GImNodes == null)
            return;
        if (ReferenceEquals(GImNodes.DefaultEditorCtx, editor))
            GImNodes.DefaultEditorCtx = null;
        if (ReferenceEquals(GImNodes.EditorCtx, editor))
            GImNodes.EditorCtx = GImNodes.DefaultEditorCtx;
    }

    public static void EditorContextSet(ImNodesEditorContext? editor) => Current.EditorCtx = editor;

    public static float2 EditorContextGetPanning() => Editor.Panning;

    public static void EditorContextResetPanning(float2 pos) => Editor.Panning = pos;

    public static void EditorContextMoveToNode(int nodeId)
    {
        var editor = Editor;
        var node = editor.Nodes.FindOrCreate(nodeId, static id => new NodeData(id), editor.NodeDepthOrder);
        editor.Panning = new float2(-node.Origin.X, -node.Origin.Y);
    }

    public static ImNodesIo GetIO() => Current.Io;

    public static ImNodesStyle GetStyle() => Current.Style;

    public static void StyleColorsDark(ImNodesStyle? dest = null) => ApplyColorPreset(dest, DarkColors);

    public static void StyleColorsClassic(ImNodesStyle? dest = null) => ApplyColorPreset(dest, ClassicColors);

    public static void StyleColorsLight(ImNodesStyle? dest = null) => ApplyColorPreset(dest, LightColors);

    public static string GetStyleColorName(ImNodesCol idx) => idx switch
    {
        ImNodesCol.NodeBackground => "NodeBackground",
        ImNodesCol.NodeBackgroundHovered => "NodeBackgroundHovered",
        ImNodesCol.NodeBackgroundSelected => "NodeBackgroundSelected",
        ImNodesCol.NodeOutline => "NodeOutline",
        ImNodesCol.TitleBar => "TitleBar",
        ImNodesCol.TitleBarHovered => "TitleBarHovered",
        ImNodesCol.TitleBarSelected => "TitleBarSelected",
        ImNodesCol.Link => "Link",
        ImNodesCol.LinkHovered => "LinkHovered",
        ImNodesCol.LinkSelected => "LinkSelected",
        ImNodesCol.Pin => "Pin",
        ImNodesCol.PinHovered => "PinHovered",
        ImNodesCol.BoxSelector => "BoxSelector",
        ImNodesCol.BoxSelectorOutline => "BoxSelectorOutline",
        ImNodesCol.GridBackground => "GridBackground",
        ImNodesCol.GridLine => "GridLine",
        ImNodesCol.GridLinePrimary => "GridLinePrimary",
        ImNodesCol.MiniMapBackground => "MiniMapBackground",
        ImNodesCol.MiniMapBackgroundHovered => "MiniMapBackgroundHovered",
        ImNodesCol.MiniMapOutline => "MiniMapOutline",
        ImNodesCol.MiniMapOutlineHovered => "MiniMapOutlineHovered",
        ImNodesCol.MiniMapNodeBackground => "MiniMapNodeBackground",
        ImNodesCol.MiniMapNodeBackgroundHovered => "MiniMapNodeBackgroundHovered",
        ImNodesCol.MiniMapNodeBackgroundSelected => "MiniMapNodeBackgroundSelected",
        ImNodesCol.MiniMapNodeOutline => "MiniMapNodeOutline",
        ImNodesCol.MiniMapLink => "MiniMapLink",
        ImNodesCol.MiniMapLinkSelected => "MiniMapLinkSelected",
        ImNodesCol.MiniMapCanvas => "MiniMapCanvas",
        ImNodesCol.MiniMapCanvasOutline => "MiniMapCanvasOutline",
        _ => "Unknown",
    };

    public static void BeginNodeEditor() => BeginNodeEditorImpl();

    public static void EndNodeEditor() => EndNodeEditorImpl();

    public static void MiniMap(
        float minimapSizeFraction = 0.2f,
        ImNodesMiniMapLocation location = ImNodesMiniMapLocation.TopLeft,
        ImNodesMiniMapNodeHoverCallback? nodeHoveringCallback = null,
        object? nodeHoveringCallbackData = null)
    {
        if (minimapSizeFraction <= 0f || minimapSizeFraction > 1f)
            throw new ArgumentOutOfRangeException(nameof(minimapSizeFraction), "Mini-map size fraction must be in the range (0, 1].");
        ExpectScope(ImNodesScope.Editor, nameof(MiniMap));

        var editor = Editor;
        editor.MiniMapEnabled = true;
        editor.MiniMapSizeFraction = minimapSizeFraction;
        editor.MiniMapLocation = location;
        editor.MiniMapNodeHoveringCallback = nodeHoveringCallback;
        editor.MiniMapNodeHoveringCallbackUserData = nodeHoveringCallbackData;
    }

    public static void PushColorStyle(ImNodesCol item, uint color)
    {
        var style = Current.Style;
        Current.ColorModifierStack.Add(new ColorElement(style.Colors[(int)item], item));
        style.Colors[(int)item] = color;
    }

    public static void PopColorStyle()
    {
        var stack = Current.ColorModifierStack;
        if (stack.Count == 0)
            throw new InvalidOperationException("ImNodes.PopColorStyle called without a matching PushColorStyle.");
        var elem = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        Current.Style.Colors[(int)elem.Item] = elem.Color;
    }

    public static void PushStyleVar(ImNodesStyleVar styleItem, float value)
    {
        if (!TryGetStyleFloat(styleItem, out float current))
            throw new ArgumentException($"ImNodes style variable {styleItem} is not a float.", nameof(styleItem));
        Current.StyleModifierStack.Add(new StyleVarElement(styleItem, current, 0f));
        SetStyleFloat(styleItem, value);
    }

    public static void PushStyleVar(ImNodesStyleVar styleItem, float2 value)
    {
        if (!TryGetStyleVec(styleItem, out float2 current))
            throw new ArgumentException($"ImNodes style variable {styleItem} is not a float2.", nameof(styleItem));
        Current.StyleModifierStack.Add(new StyleVarElement(styleItem, current.X, current.Y));
        SetStyleVec(styleItem, value);
    }

    public static void PopStyleVar(int count = 1)
    {
        while (count > 0)
        {
            var stack = Current.StyleModifierStack;
            if (stack.Count == 0)
                throw new InvalidOperationException("ImNodes.PopStyleVar called without a matching PushStyleVar.");
            var backup = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            if (IsVecStyle(backup.Item))
                SetStyleVec(backup.Item, new float2(backup.X, backup.Y));
            else
                SetStyleFloat(backup.Item, backup.X);
            count--;
        }
    }

    public static void BeginNode(int id) => BeginNodeImpl(id);

    public static void EndNode() => EndNodeImpl();

    public static float2 GetNodeDimensions(int nodeId)
    {
        var editor = Editor;
        int nodeIdx = editor.Nodes.Find(nodeId);
        if (nodeIdx < 0)
            throw new ArgumentException($"Unknown node id {nodeId}.", nameof(nodeId));
        return RectSize(editor.Nodes.Pool[nodeIdx]!.Rect);
    }

    public static void BeginNodeTitleBar()
    {
        ExpectScope(ImNodesScope.Node, nameof(BeginNodeTitleBar));
        ImGui.BeginGroup();
    }

    public static void EndNodeTitleBar() => EndNodeTitleBarImpl();

    public static void BeginInputAttribute(int id, ImNodesPinShape shape = ImNodesPinShape.CircleFilled) =>
        BeginPinAttribute(id, ImNodesAttributeType.Input, shape);

    public static void EndInputAttribute() => EndPinAttribute();

    public static void BeginOutputAttribute(int id, ImNodesPinShape shape = ImNodesPinShape.CircleFilled) =>
        BeginPinAttribute(id, ImNodesAttributeType.Output, shape);

    public static void EndOutputAttribute() => EndPinAttribute();

    public static void BeginStaticAttribute(int id)
    {
        ExpectScope(ImNodesScope.Node, nameof(BeginStaticAttribute));
        Current.CurrentScope = ImNodesScope.Attribute;
        Current.CurrentAttributeId = id;
        ImGui.BeginGroup();
        ImGui.PushID(id);
    }

    public static void EndStaticAttribute()
    {
        ExpectScope(ImNodesScope.Attribute, nameof(EndStaticAttribute));
        Current.CurrentScope = ImNodesScope.Node;
        ImGui.PopID();
        ImGui.EndGroup();
        NoteActiveAttribute();
    }

    public static void PushAttributeFlag(ImNodesAttributeFlags flag)
    {
        Current.CurrentAttributeFlags |= (int)flag;
        Current.AttributeFlagStack.Add(Current.CurrentAttributeFlags);
    }

    public static void PopAttributeFlag()
    {
        var stack = Current.AttributeFlagStack;
        if (stack.Count <= 1)
            throw new InvalidOperationException("ImNodes.PopAttributeFlag called without a matching PushAttributeFlag.");
        stack.RemoveAt(stack.Count - 1);
        Current.CurrentAttributeFlags = stack[^1];
    }

    public static void Link(int id, int startAttributeId, int endAttributeId) => LinkImpl(id, startAttributeId, endAttributeId);

    public static void SetNodeDraggable(int nodeId, bool draggable)
    {
        var editor = Editor;
        var node = editor.Nodes.FindOrCreate(nodeId, static id => new NodeData(id), editor.NodeDepthOrder);
        node.Draggable = draggable;
    }

    public static void SetNodeScreenSpacePos(int nodeId, float2 screenSpacePos)
    {
        var editor = Editor;
        var node = editor.Nodes.FindOrCreate(nodeId, static id => new NodeData(id), editor.NodeDepthOrder);
        node.Origin = ScreenToGrid(editor, screenSpacePos);
    }

    public static void SetNodeEditorSpacePos(int nodeId, float2 editorSpacePos)
    {
        var editor = Editor;
        var node = editor.Nodes.FindOrCreate(nodeId, static id => new NodeData(id), editor.NodeDepthOrder);
        node.Origin = EditorToGrid(editor, editorSpacePos);
    }

    public static void SetNodeGridSpacePos(int nodeId, float2 gridPos)
    {
        var editor = Editor;
        var node = editor.Nodes.FindOrCreate(nodeId, static id => new NodeData(id), editor.NodeDepthOrder);
        node.Origin = gridPos;
    }

    public static float2 GetNodeScreenSpacePos(int nodeId) => GridToScreen(Editor, ExistingNode(nodeId).Origin);

    public static float2 GetNodeEditorSpacePos(int nodeId) => GridToEditor(Editor, ExistingNode(nodeId).Origin);

    public static float2 GetNodeGridSpacePos(int nodeId) => ExistingNode(nodeId).Origin;

    public static void SnapNodeToGrid(int nodeId)
    {
        var editor = Editor;
        var node = editor.Nodes.FindOrCreate(nodeId, static id => new NodeData(id), editor.NodeDepthOrder);
        node.Origin = SnapOriginToGrid(node.Origin);
    }

    public static bool IsEditorHovered() => MouseInCanvas();

    public static bool IsNodeHovered(out int nodeId) => TryHoveredId(Current.HoveredNodeIdx, Editor.Nodes, out nodeId, ImNodesScope.None);

    public static bool IsLinkHovered(out int linkId) => TryHoveredId(Current.HoveredLinkIdx, Editor.Links, out linkId, ImNodesScope.None);

    public static bool IsPinHovered(out int attributeId) => TryHoveredId(Current.HoveredPinIdx, Editor.Pins, out attributeId, ImNodesScope.None);

    public static int NumSelectedNodes()
    {
        ExpectScope(ImNodesScope.None, nameof(NumSelectedNodes));
        return Editor.SelectedNodeIndices.Count;
    }

    public static int NumSelectedLinks()
    {
        ExpectScope(ImNodesScope.None, nameof(NumSelectedLinks));
        return Editor.SelectedLinkIndices.Count;
    }

    public static void GetSelectedNodes(Span<int> nodeIds) => CopySelectedIds(Editor.SelectedNodeIndices, Editor.Nodes, nodeIds);

    public static void GetSelectedLinks(Span<int> linkIds) => CopySelectedIds(Editor.SelectedLinkIndices, Editor.Links, linkIds);

    public static void ClearNodeSelection() => Editor.SelectedNodeIndices.Clear();

    public static void ClearNodeSelection(int nodeId) => ClearObjectSelection(Editor.Nodes, Editor.SelectedNodeIndices, nodeId);

    public static void ClearLinkSelection() => Editor.SelectedLinkIndices.Clear();

    public static void ClearLinkSelection(int linkId) => ClearObjectSelection(Editor.Links, Editor.SelectedLinkIndices, linkId);

    public static void SelectNode(int nodeId) => SelectObject(Editor.Nodes, Editor.SelectedNodeIndices, nodeId);

    public static void SelectLink(int linkId) => SelectObject(Editor.Links, Editor.SelectedLinkIndices, linkId);

    public static bool IsNodeSelected(int nodeId) => IsObjectSelected(Editor.Nodes, Editor.SelectedNodeIndices, nodeId);

    public static bool IsLinkSelected(int linkId) => IsObjectSelected(Editor.Links, Editor.SelectedLinkIndices, linkId);

    public static bool IsAttributeActive()
    {
        if ((Current.CurrentScope & ImNodesScope.Node) == 0)
            throw new InvalidOperationException("ImNodes.IsAttributeActive must be called from inside a node.");
        return Current.ActiveAttribute && Current.ActiveAttributeId == Current.CurrentAttributeId;
    }

    public static bool IsAnyAttributeActive() => IsAnyAttributeActive(out _);

    public static bool IsAnyAttributeActive(out int attributeId)
    {
        if ((Current.CurrentScope & (ImNodesScope.Node | ImNodesScope.Attribute)) != 0)
            throw new InvalidOperationException("ImNodes.IsAnyAttributeActive must be called outside a node.");
        attributeId = Current.ActiveAttribute ? Current.ActiveAttributeId : 0;
        return Current.ActiveAttribute;
    }

    public static bool IsLinkStarted(out int startedAtAttributeId)
    {
        ExpectScope(ImNodesScope.None, nameof(IsLinkStarted));
        bool started = (Current.UiState & (int)ImNodesUiState.LinkStarted) != 0;
        startedAtAttributeId = started ? Editor.Pins.Pool[Editor.ClickInteraction.LinkCreation.StartPinIdx]!.Id : 0;
        return started;
    }

    public static bool IsLinkDropped(bool includingDetachedLinks = true) =>
        IsLinkDropped(out _, includingDetachedLinks);

    public static bool IsLinkDropped(out int startedAtAttributeId, bool includingDetachedLinks = true)
    {
        ExpectScope(ImNodesScope.None, nameof(IsLinkDropped));
        var editor = Editor;
        bool dropped = (Current.UiState & (int)ImNodesUiState.LinkDropped) != 0 &&
                       (includingDetachedLinks || editor.ClickInteraction.LinkCreation.Type != ImNodesLinkCreationType.FromDetach);
        startedAtAttributeId = dropped ? editor.Pins.Pool[editor.ClickInteraction.LinkCreation.StartPinIdx]!.Id : 0;
        return dropped;
    }

    public static bool IsLinkCreated(out int startedAtAttributeId, out int endedAtAttributeId) =>
        IsLinkCreated(out startedAtAttributeId, out endedAtAttributeId, out _);

    public static bool IsLinkCreated(out int startedAtAttributeId, out int endedAtAttributeId, out bool createdFromSnap)
    {
        ExpectScope(ImNodesScope.None, nameof(IsLinkCreated));
        startedAtAttributeId = 0;
        endedAtAttributeId = 0;
        createdFromSnap = false;
        if ((Current.UiState & (int)ImNodesUiState.LinkCreated) == 0)
            return false;

        var editor = Editor;
        var start = editor.Pins.Pool[editor.ClickInteraction.LinkCreation.StartPinIdx]!;
        var end = editor.Pins.Pool[editor.ClickInteraction.LinkCreation.EndPinIdx.Value]!;
        if (start.Type == ImNodesAttributeType.Output)
        {
            startedAtAttributeId = start.Id;
            endedAtAttributeId = end.Id;
        }
        else
        {
            startedAtAttributeId = end.Id;
            endedAtAttributeId = start.Id;
        }

        createdFromSnap = editor.ClickInteraction.Type == ImNodesClickInteractionType.LinkCreation;
        return true;
    }

    public static bool IsLinkCreated(out int startedAtNodeId, out int startedAtAttributeId, out int endedAtNodeId, out int endedAtAttributeId) =>
        IsLinkCreated(out startedAtNodeId, out startedAtAttributeId, out endedAtNodeId, out endedAtAttributeId, out _);

    public static bool IsLinkCreated(
        out int startedAtNodeId,
        out int startedAtAttributeId,
        out int endedAtNodeId,
        out int endedAtAttributeId,
        out bool createdFromSnap)
    {
        ExpectScope(ImNodesScope.None, nameof(IsLinkCreated));
        startedAtNodeId = 0;
        startedAtAttributeId = 0;
        endedAtNodeId = 0;
        endedAtAttributeId = 0;
        createdFromSnap = false;
        if ((Current.UiState & (int)ImNodesUiState.LinkCreated) == 0)
            return false;

        var editor = Editor;
        var startPin = editor.Pins.Pool[editor.ClickInteraction.LinkCreation.StartPinIdx]!;
        var endPin = editor.Pins.Pool[editor.ClickInteraction.LinkCreation.EndPinIdx.Value]!;
        var startNode = editor.Nodes.Pool[startPin.ParentNodeIdx]!;
        var endNode = editor.Nodes.Pool[endPin.ParentNodeIdx]!;
        if (startPin.Type == ImNodesAttributeType.Output)
        {
            startedAtAttributeId = startPin.Id;
            startedAtNodeId = startNode.Id;
            endedAtAttributeId = endPin.Id;
            endedAtNodeId = endNode.Id;
        }
        else
        {
            startedAtAttributeId = endPin.Id;
            startedAtNodeId = endNode.Id;
            endedAtAttributeId = startPin.Id;
            endedAtNodeId = startNode.Id;
        }

        createdFromSnap = editor.ClickInteraction.Type == ImNodesClickInteractionType.LinkCreation;
        return true;
    }

    public static bool IsLinkDestroyed(out int linkId)
    {
        ExpectScope(ImNodesScope.None, nameof(IsLinkDestroyed));
        bool destroyed = Current.DeletedLinkIdx.HasValue;
        linkId = destroyed ? Editor.Links.Pool[Current.DeletedLinkIdx.Value]!.Id : 0;
        return destroyed;
    }

    public static string SaveCurrentEditorStateToIniString() => SaveEditorStateToIniString(Editor);

    public static string SaveEditorStateToIniString(ImNodesEditorContext editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var builder = new StringBuilder();
        builder.Append("[editor]\npanning=");
        builder.Append((int)editor.Panning.X);
        builder.Append(',');
        builder.Append((int)editor.Panning.Y);
        builder.Append('\n');

        for (int i = 0; i < editor.Nodes.Pool.Count; i++)
        {
            if (!editor.Nodes.InUse[i] || editor.Nodes.Pool[i] is not { } node)
                continue;
            builder.Append("\n[node.");
            builder.Append(node.Id);
            builder.Append("]\norigin=");
            builder.Append((int)node.Origin.X);
            builder.Append(',');
            builder.Append((int)node.Origin.Y);
            builder.Append('\n');
        }

        return builder.ToString();
    }

    public static void LoadCurrentEditorStateFromIniString(string data) => LoadEditorStateFromIniString(Editor, data);

    public static void LoadEditorStateFromIniString(ImNodesEditorContext editor, string data)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
            return;

        var g = Current;
        Action<string>? handler = null;
        foreach (var raw in data.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            var line = raw;
            if (line.Length == 0 || line[0] == ';')
                continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                line = line[..^1];
                var section = line[1..];
                if (section.StartsWith("node", StringComparison.Ordinal))
                    handler = parsed => NodeLine(editor, g, parsed);
                else if (section == "editor")
                    handler = parsed => EditorLine(editor, parsed);
            }

            handler?.Invoke(line);
        }
    }

    public static void SaveCurrentEditorStateToIniFile(string fileName) => SaveEditorStateToIniFile(Editor, fileName);

    public static void SaveEditorStateToIniFile(ImNodesEditorContext editor, string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        File.WriteAllText(fileName, SaveEditorStateToIniString(editor));
    }

    public static void LoadCurrentEditorStateFromIniFile(string fileName) => LoadEditorStateFromIniFile(Editor, fileName);

    public static void LoadEditorStateFromIniFile(ImNodesEditorContext editor, string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        if (!File.Exists(fileName))
            return;
        LoadEditorStateFromIniString(editor, File.ReadAllText(fileName));
    }
}
