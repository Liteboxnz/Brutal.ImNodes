using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.Pointers;

namespace Brutal.ImNodesAPI;

enum ImNodesScope
{
    None = 1,
    Editor = 1 << 1,
    Node = 1 << 2,
    Attribute = 1 << 3,
}

enum ImNodesAttributeType
{
    None,
    Input,
    Output,
}

[Flags]
enum ImNodesUiState
{
    None = 0,
    LinkStarted = 1 << 0,
    LinkDropped = 1 << 1,
    LinkCreated = 1 << 2,
}

enum ImNodesClickInteractionType
{
    Node,
    Link,
    LinkCreation,
    Panning,
    BoxSelection,
    ImGuiItem,
    None,
}

enum ImNodesLinkCreationType
{
    Standard,
    FromDetach,
}

struct OptionalIndex
{
    public const int Invalid = -1;
    public int Index;

    public OptionalIndex() => Index = Invalid;

    public OptionalIndex(int value) => Index = value;

    public readonly bool HasValue => Index != Invalid;

    public readonly int Value => Index;

    public void Reset() => Index = Invalid;

    public static bool operator ==(OptionalIndex lhs, OptionalIndex rhs) => lhs.Index == rhs.Index;

    public static bool operator !=(OptionalIndex lhs, OptionalIndex rhs) => lhs.Index != rhs.Index;

    public static bool operator ==(OptionalIndex lhs, int rhs) => lhs.Index == rhs;

    public static bool operator !=(OptionalIndex lhs, int rhs) => lhs.Index != rhs;

    public readonly override bool Equals(object? obj) => obj is OptionalIndex other && this == other;

    public readonly override int GetHashCode() => Index;
}

readonly struct ColorElement
{
    public readonly uint Color;
    public readonly ImNodesCol Item;

    public ColorElement(uint color, ImNodesCol item)
    {
        Color = color;
        Item = item;
    }
}

readonly struct StyleVarElement
{
    public readonly ImNodesStyleVar Item;
    public readonly float X;
    public readonly float Y;

    public StyleVarElement(ImNodesStyleVar item, float x, float y)
    {
        Item = item;
        X = x;
        Y = y;
    }
}

struct NodeColorStyle
{
    public uint Background;
    public uint BackgroundHovered;
    public uint BackgroundSelected;
    public uint Outline;
    public uint Titlebar;
    public uint TitlebarHovered;
    public uint TitlebarSelected;
}

struct NodeLayoutStyle
{
    public float CornerRounding;
    public float2 Padding;
    public float BorderThickness;
}

struct PinColorStyle
{
    public uint Background;
    public uint Hovered;
}

struct LinkColorStyle
{
    public uint Base;
    public uint Hovered;
    public uint Selected;
}

sealed class NodeData
{
    public int Id;
    public float2 Origin;
    public floatRect TitleBarContentRect;
    public floatRect Rect;
    public NodeColorStyle ColorStyle;
    public NodeLayoutStyle LayoutStyle;
    public readonly List<int> PinIndices = new();
    public bool Draggable = true;

    public NodeData(int id) => Id = id;
}

sealed class PinData
{
    public int Id;
    public int ParentNodeIdx;
    public floatRect AttributeRect;
    public ImNodesAttributeType Type;
    public ImNodesPinShape Shape = ImNodesPinShape.CircleFilled;
    public float2 Pos;
    public int Flags;
    public PinColorStyle ColorStyle;

    public PinData(int id) => Id = id;
}

sealed class LinkData
{
    public int Id;
    public int StartPinIdx;
    public int EndPinIdx;
    public LinkColorStyle ColorStyle;

    public LinkData(int id) => Id = id;
}

sealed class LinkCreationState
{
    public int StartPinIdx;
    public OptionalIndex EndPinIdx;
    public ImNodesLinkCreationType Type;
}

sealed class ClickInteractionState
{
    public ImNodesClickInteractionType Type = ImNodesClickInteractionType.None;
    public readonly LinkCreationState LinkCreation = new();
    public floatRect BoxSelectorRect;
}

struct CubicBezier
{
    public float2 P0;
    public float2 P1;
    public float2 P2;
    public float2 P3;
    public int NumSegments;
}

sealed class ObjectPool<T> where T : class
{
    public readonly List<T?> Pool = new();
    public readonly List<bool> InUse = new();
    public readonly List<int> FreeList = new();
    public readonly Dictionary<uint, int> IdMap = new();

    public int Find(int id) => IdMap.TryGetValue((uint)id, out int index) ? index : -1;

    public T FindOrCreate(int id, Func<int, T> factory, List<int>? depthOrder = null)
    {
        int index = FindOrCreateIndex(id, factory, depthOrder);
        return Pool[index]!;
    }

    public int FindOrCreateIndex(int id, Func<int, T> factory, List<int>? depthOrder = null)
    {
        int index = Find(id);
        if (index == -1)
        {
            if (FreeList.Count == 0)
            {
                index = Pool.Count;
                Pool.Add(factory(id));
                InUse.Add(false);
            }
            else
            {
                index = FreeList[^1];
                FreeList.RemoveAt(FreeList.Count - 1);
                Pool[index] = factory(id);
            }

            IdMap[(uint)id] = index;
            depthOrder?.Add(index);
        }

        InUse[index] = true;
        return index;
    }

    public void ResetUsed()
    {
        for (int i = 0; i < InUse.Count; i++)
            InUse[i] = false;
    }

    public void Update(Func<T, int> idOf, Action<int>? onUnused = null, Action<T>? onUsed = null)
    {
        for (int i = 0; i < InUse.Count; i++)
        {
            if (Pool[i] is not { } item)
                continue;

            if (InUse[i])
            {
                onUsed?.Invoke(item);
                continue;
            }

            int id = idOf(item);
            if (IdMap.TryGetValue((uint)id, out int mapped) && mapped == i)
            {
                onUnused?.Invoke(i);
                IdMap.Remove((uint)id);
                FreeList.Add(i);
                Pool[i] = null;
            }
        }
    }
}

public static partial class ImNodes
{
    static ImNodesContext? GImNodes;

    static ImNodesContext Current => GImNodes ?? throw new InvalidOperationException("Call ImNodes.CreateContext() before using the node editor.");

    static ImNodesEditorContext Editor => Current.EditorCtx ?? throw new InvalidOperationException("No ImNodes editor context is set. Call ImNodes.CreateContext().");

    static readonly uint[] DarkColors = BuildColors(dark: true);
    static readonly uint[] ClassicColors = BuildColors(classic: true);
    static readonly uint[] LightColors = BuildColors(light: true);

    static void Initialize(ImNodesContext context)
    {
        context.CanvasOriginScreenSpace = default;
        context.CanvasRectScreenSpace = Rect(default, default);
        context.CurrentScope = ImNodesScope.None;
        context.CurrentPinIdx = int.MaxValue;
        context.CurrentNodeIdx = int.MaxValue;
        context.DefaultEditorCtx = new ImNodesEditorContext();
        context.EditorCtx = context.DefaultEditorCtx;
        context.CurrentAttributeFlags = (int)ImNodesAttributeFlags.None;
        context.AttributeFlagStack.Add(GImNodes!.CurrentAttributeFlags);
        DarkColors.CopyTo(context.Style.Colors, 0);
    }

    static void Shutdown(ImNodesContext ctx)
    {
        if (ReferenceEquals(ctx.EditorCtx, ctx.DefaultEditorCtx))
            ctx.EditorCtx = null;
        ctx.DefaultEditorCtx = null;
    }

    static void ExpectScope(ImNodesScope scope, string member)
    {
        if (Current.CurrentScope != scope)
            throw new InvalidOperationException($"ImNodes.{member} called in the wrong scope.");
    }

    static void BeginNodeEditorImpl()
    {
        ExpectScope(ImNodesScope.None, nameof(BeginNodeEditor));
        var g = Current;
        g.CurrentScope = ImNodesScope.Editor;

        var editor = Editor;
        editor.AutoPanningDelta = default;
        editor.GridContentBounds = Rect(new float2(float.MaxValue, float.MaxValue), new float2(float.MinValue, float.MinValue));
        editor.MiniMapEnabled = false;
        editor.Nodes.ResetUsed();
        editor.Pins.ResetUsed();
        editor.Links.ResetUsed();

        g.HoveredNodeIdx.Reset();
        g.HoveredLinkIdx.Reset();
        g.HoveredPinIdx.Reset();
        g.DeletedLinkIdx.Reset();
        g.SnapLinkIdx.Reset();
        g.NodeIndicesOverlappingWithMouse.Clear();
        g.UiState = (int)ImNodesUiState.None;

        var io = ImGui.GetIO();
        g.MousePos = io.MousePos;
        g.LeftMouseClicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left, false);
        g.LeftMouseReleased = ImGui.IsMouseReleased(ImGuiMouseButton.Left);
        g.LeftMouseDragging = ImGui.IsMouseDragging(ImGuiMouseButton.Left, 0f);
        bool emulate = g.Io.EmulateThreeButtonMouse.Modifier?.Invoke() ?? false;
        g.AltMouseClicked = (emulate && g.LeftMouseClicked) || ImGui.IsMouseClicked(g.Io.AltMouseButton, false);
        g.AltMouseDragging = (emulate && g.LeftMouseDragging) || ImGui.IsMouseDragging(g.Io.AltMouseButton, 0f);
        g.AltMouseScrollDelta = io.MouseWheel;
        g.MultipleSelectModifier = g.Io.MultipleSelectModifier.Modifier?.Invoke() ?? io.KeyCtrl;
        g.ActiveAttribute = false;

        ImGui.BeginGroup();
        var framePadding = new float2(1f, 1f);
        var windowPadding = new float2(0f, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, framePadding);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, windowPadding);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, (ImColor8)g.Style.Colors[(int)ImNodesCol.GridBackground]);

        float2? childSize = new float2(0f, 0f);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollWithMouse;
        ImGui.BeginChild("scrolling_region"u8, in childSize, ImGuiChildFlags.Borders, flags);
        g.CanvasOriginScreenSpace = ImGui.GetCursorScreenPos();
        DrawListSet(ImGui.GetWindowDrawList());

        var canvasSize = ImGui.GetWindowSize();
        g.CanvasRectScreenSpace = Rect(EditorToScreen(default), EditorToScreen(canvasSize));
        if ((g.Style.Flags & ImNodesStyleFlags.GridLines) != 0)
            DrawGrid(editor, canvasSize);
    }

    static void EndNodeEditorImpl()
    {
        ExpectScope(ImNodesScope.Editor, nameof(EndNodeEditor));
        var g = Current;
        g.CurrentScope = ImNodesScope.None;
        var editor = Editor;

        if (IsInverted(editor.GridContentBounds))
            editor.GridContentBounds = ScreenToGrid(editor, g.CanvasRectScreenSpace);

        if (g.LeftMouseClicked && ImGui.IsAnyItemActive())
            editor.ClickInteraction.Type = ImNodesClickInteractionType.ImGuiItem;

        if ((editor.ClickInteraction.Type == ImNodesClickInteractionType.None ||
             editor.ClickInteraction.Type == ImNodesClickInteractionType.LinkCreation) &&
            MouseInCanvas() && !IsMiniMapHovered())
        {
            ResolveOccludedPins(editor, g.OccludedPinIndices);
            g.HoveredPinIdx = ResolveHoveredPin(editor.Pins, g.OccludedPinIndices);
            if (!g.HoveredPinIdx.HasValue)
                g.HoveredNodeIdx = ResolveHoveredNode(editor.NodeDepthOrder);
            if (!g.HoveredNodeIdx.HasValue)
                g.HoveredLinkIdx = ResolveHoveredLink(editor.Links, editor.Pins);
        }

        for (int nodeIdx = 0; nodeIdx < editor.Nodes.Pool.Count; nodeIdx++)
        {
            if (editor.Nodes.InUse[nodeIdx])
            {
                ActivateNodeBackground(nodeIdx);
                DrawNode(editor, nodeIdx);
            }
        }

        g.CanvasDrawList.ChannelsSetCurrent(0);
        for (int linkIdx = 0; linkIdx < editor.Links.Pool.Count; linkIdx++)
        {
            if (editor.Links.InUse[linkIdx])
                DrawLink(editor, linkIdx);
        }

        GrowChannels(g.CanvasDrawList, 1);
        ActivateClickChannel();

        if (IsMiniMapActive())
        {
            CalcMiniMapLayout();
            MiniMapUpdate();
        }

        if (!IsMiniMapHovered())
        {
            if (g.LeftMouseClicked && g.HoveredLinkIdx.HasValue)
                BeginLinkInteraction(editor, g.HoveredLinkIdx.Value, g.HoveredPinIdx);
            else if (g.LeftMouseClicked && g.HoveredPinIdx.HasValue)
                BeginLinkCreation(editor, g.HoveredPinIdx.Value);
            else if (g.LeftMouseClicked && g.HoveredNodeIdx.HasValue)
                BeginNodeSelection(editor, g.HoveredNodeIdx.Value);
            else if (g.LeftMouseClicked || g.LeftMouseReleased || g.AltMouseClicked || g.AltMouseScrollDelta != 0f)
                BeginCanvasInteraction(editor);

            bool shouldAutoPan =
                editor.ClickInteraction.Type == ImNodesClickInteractionType.BoxSelection ||
                editor.ClickInteraction.Type == ImNodesClickInteractionType.LinkCreation ||
                editor.ClickInteraction.Type == ImNodesClickInteractionType.Node;
            if (shouldAutoPan && !MouseInCanvas())
            {
                var mouse = ImGui.GetMousePos();
                var center = RectCenter(g.CanvasRectScreenSpace);
                var direction = new float2(center.X - mouse.X, center.Y - mouse.Y);
                float inv = InvLength(direction, 0f);
                direction = new float2(direction.X * inv, direction.Y * inv);
                editor.AutoPanningDelta = new float2(
                    direction.X * ImGui.GetIO().DeltaTime * g.Io.AutoPanningSpeed,
                    direction.Y * ImGui.GetIO().DeltaTime * g.Io.AutoPanningSpeed);
                editor.Panning = new float2(editor.Panning.X + editor.AutoPanningDelta.X, editor.Panning.Y + editor.AutoPanningDelta.Y);
            }
        }

        ClickInteractionUpdate(editor);

        editor.Nodes.Update(static node => node.Id, RemoveNodeFromDepth, static node => node.PinIndices.Clear());
        editor.Pins.Update(static pin => pin.Id);
        SortChannelsByDepth(editor.NodeDepthOrder);
        editor.Links.Update(static link => link.Id);

        g.CanvasDrawList.ChannelsMerge();
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
        ImGui.PopStyleVar();
        ImGui.EndGroup();
    }

    static void RemoveNodeFromDepth(int nodeIdx)
    {
        var depth = Editor.NodeDepthOrder;
        int found = depth.IndexOf(nodeIdx);
        Debug.Assert(found >= 0);
        if (found >= 0)
            depth.RemoveAt(found);
    }

    static void BeginNodeImpl(int id)
    {
        ExpectScope(ImNodesScope.Editor, nameof(BeginNode));
        var g = Current;
        g.CurrentScope = ImNodesScope.Node;
        var editor = Editor;
        int nodeIdx = editor.Nodes.FindOrCreateIndex(id, static nodeId => new NodeData(nodeId), editor.NodeDepthOrder);
        g.CurrentNodeIdx = nodeIdx;
        var node = editor.Nodes.Pool[nodeIdx]!;
        ref var colors = ref g.Style.Colors;
        node.ColorStyle.Background = colors[(int)ImNodesCol.NodeBackground];
        node.ColorStyle.BackgroundHovered = colors[(int)ImNodesCol.NodeBackgroundHovered];
        node.ColorStyle.BackgroundSelected = colors[(int)ImNodesCol.NodeBackgroundSelected];
        node.ColorStyle.Outline = colors[(int)ImNodesCol.NodeOutline];
        node.ColorStyle.Titlebar = colors[(int)ImNodesCol.TitleBar];
        node.ColorStyle.TitlebarHovered = colors[(int)ImNodesCol.TitleBarHovered];
        node.ColorStyle.TitlebarSelected = colors[(int)ImNodesCol.TitleBarSelected];
        node.LayoutStyle.CornerRounding = g.Style.NodeCornerRounding;
        node.LayoutStyle.Padding = g.Style.NodePadding;
        node.LayoutStyle.BorderThickness = g.Style.NodeBorderThickness;

        var cursor = GridToEditor(editor, TitleBarOrigin(node));
        ImGui.SetCursorPos(cursor);
        AddNodeChannel(nodeIdx);
        ActivateCurrentNodeForeground();
        ImGui.PushID(node.Id);
        ImGui.BeginGroup();
    }

    static void EndNodeImpl()
    {
        ExpectScope(ImNodesScope.Node, nameof(EndNode));
        var g = Current;
        g.CurrentScope = ImNodesScope.Editor;
        var editor = Editor;
        ImGui.EndGroup();
        ImGui.PopID();

        var node = editor.Nodes.Pool[g.CurrentNodeIdx]!;
        node.Rect = ItemRect();
        Expand(ref node.Rect, node.LayoutStyle.Padding);
        AddPoint(ref editor.GridContentBounds, node.Origin);
        AddPoint(ref editor.GridContentBounds, new float2(node.Origin.X + RectWidth(node.Rect), node.Origin.Y + RectHeight(node.Rect)));
        if (Contains(node.Rect, g.MousePos))
            g.NodeIndicesOverlappingWithMouse.Add(g.CurrentNodeIdx);
    }

    static void EndNodeTitleBarImpl()
    {
        ExpectScope(ImNodesScope.Node, nameof(EndNodeTitleBar));
        ImGui.EndGroup();
        var editor = Editor;
        var node = editor.Nodes.Pool[Current.CurrentNodeIdx]!;
        node.TitleBarContentRect = ItemRect();
        var titleRect = TitleRect(node);
        ImGui.Internal.ItemAdd(in titleRect, ImGui.GetID("title_bar"u8), default, default);
        var cursor = GridToEditor(editor, ContentOrigin(node));
        ImGui.SetCursorPos(cursor);
    }

    static void BeginPinAttribute(int id, ImNodesAttributeType type, ImNodesPinShape shape)
    {
        ExpectScope(ImNodesScope.Node, type == ImNodesAttributeType.Input ? nameof(BeginInputAttribute) : nameof(BeginOutputAttribute));
        var g = Current;
        g.CurrentScope = ImNodesScope.Attribute;
        ImGui.BeginGroup();
        ImGui.PushID(id);
        var editor = Editor;
        g.CurrentAttributeId = id;
        int pinIdx = editor.Pins.FindOrCreateIndex(id, static pinId => new PinData(pinId));
        g.CurrentPinIdx = pinIdx;
        var pin = editor.Pins.Pool[pinIdx]!;
        pin.Id = id;
        pin.ParentNodeIdx = g.CurrentNodeIdx;
        pin.Type = type;
        pin.Shape = shape;
        pin.Flags = g.CurrentAttributeFlags;
        pin.ColorStyle.Background = g.Style.Colors[(int)ImNodesCol.Pin];
        pin.ColorStyle.Hovered = g.Style.Colors[(int)ImNodesCol.PinHovered];
    }

    static void EndPinAttribute()
    {
        ExpectScope(ImNodesScope.Attribute, nameof(EndInputAttribute));
        var g = Current;
        g.CurrentScope = ImNodesScope.Node;
        ImGui.PopID();
        ImGui.EndGroup();
        NoteActiveAttribute();
        var editor = Editor;
        var pin = editor.Pins.Pool[g.CurrentPinIdx]!;
        var node = editor.Nodes.Pool[g.CurrentNodeIdx]!;
        pin.AttributeRect = ItemRect();
        node.PinIndices.Add(g.CurrentPinIdx);
    }

    static void NoteActiveAttribute()
    {
        if (!ImGui.IsItemActive())
            return;
        Current.ActiveAttribute = true;
        Current.ActiveAttributeId = Current.CurrentAttributeId;
    }

    static void LinkImpl(int id, int startAttributeId, int endAttributeId)
    {
        ExpectScope(ImNodesScope.Editor, nameof(Link));
        var g = Current;
        var editor = Editor;
        int linkIdx = editor.Links.FindOrCreateIndex(id, static linkId => new LinkData(linkId));
        var link = editor.Links.Pool[linkIdx]!;
        link.Id = id;
        link.StartPinIdx = editor.Pins.FindOrCreateIndex(startAttributeId, static pinId => new PinData(pinId));
        link.EndPinIdx = editor.Pins.FindOrCreateIndex(endAttributeId, static pinId => new PinData(pinId));
        link.ColorStyle.Base = g.Style.Colors[(int)ImNodesCol.Link];
        link.ColorStyle.Hovered = g.Style.Colors[(int)ImNodesCol.LinkHovered];
        link.ColorStyle.Selected = g.Style.Colors[(int)ImNodesCol.LinkSelected];

        var creation = editor.ClickInteraction.LinkCreation;
        bool forward = editor.ClickInteraction.Type == ImNodesClickInteractionType.LinkCreation &&
                       (editor.Pins.Pool[link.EndPinIdx]!.Flags & (int)ImNodesAttributeFlags.EnableLinkCreationOnSnap) != 0 &&
                       creation.StartPinIdx == link.StartPinIdx &&
                       creation.EndPinIdx == link.EndPinIdx;
        bool reversed = creation.StartPinIdx == link.EndPinIdx && creation.EndPinIdx == link.StartPinIdx;
        if (forward || reversed)
            g.SnapLinkIdx = new OptionalIndex(linkIdx);
    }

    static NodeData ExistingNode(int nodeId)
    {
        int index = Editor.Nodes.Find(nodeId);
        if (index < 0 || Editor.Nodes.Pool[index] is not { } node)
            throw new ArgumentException($"Unknown node id {nodeId}.", nameof(nodeId));
        return node;
    }

    static bool TryHoveredId<T>(OptionalIndex index, ObjectPool<T> pool, out int id, ImNodesScope scope) where T : class
    {
        ExpectScope(scope, "hover query");
        if (!index.HasValue)
        {
            id = 0;
            return false;
        }

        id = IdOf(pool.Pool[index.Value]!);
        return true;
    }

    static int IdOf<T>(T item) => item switch
    {
        NodeData node => node.Id,
        PinData pin => pin.Id,
        LinkData link => link.Id,
        _ => throw new InvalidOperationException(),
    };

    static void CopySelectedIds<T>(List<int> selected, ObjectPool<T> pool, Span<int> ids) where T : class
    {
        if (ids.Length < selected.Count)
            throw new ArgumentException("The destination is smaller than the selection.", nameof(ids));
        for (int i = 0; i < selected.Count; i++)
            ids[i] = IdOf(pool.Pool[selected[i]]!);
    }

    static void SelectObject<T>(ObjectPool<T> pool, List<int> selected, int id) where T : class
    {
        int index = pool.Find(id);
        if (index < 0)
            throw new ArgumentException($"Unknown id {id}.");
        if (selected.Contains(index))
            throw new InvalidOperationException($"Id {id} is already selected.");
        selected.Add(index);
    }

    static void ClearObjectSelection<T>(ObjectPool<T> pool, List<int> selected, int id) where T : class
    {
        int index = pool.Find(id);
        if (index < 0)
            throw new ArgumentException($"Unknown id {id}.");
        int found = selected.IndexOf(index);
        if (found < 0)
            throw new InvalidOperationException($"Id {id} is not selected.");
        selected[found] = selected[^1];
        selected.RemoveAt(selected.Count - 1);
    }

    static bool IsObjectSelected<T>(ObjectPool<T> pool, List<int> selected, int id) where T : class
    {
        int index = pool.Find(id);
        return index >= 0 && selected.Contains(index);
    }

    static void NodeLine(ImNodesEditorContext editor, ImNodesContext g, string line)
    {
        if (line.StartsWith("[node.", StringComparison.Ordinal) &&
            int.TryParse(line.AsSpan("[node.".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
        {
            editor.Nodes.FindOrCreate(id, static nodeId => new NodeData(nodeId), editor.NodeDepthOrder);
            g.CurrentNodeIdx = editor.Nodes.Find(id);
        }
        else if (TryParsePair(line, "origin=", out float x, out float y))
        {
            editor.Nodes.Pool[g.CurrentNodeIdx]!.Origin = SnapOriginToGrid(new float2(x, y));
        }
    }

    static void EditorLine(ImNodesEditorContext editor, string line)
    {
        if (TryParsePair(line, "panning=", out float x, out float y))
            editor.Panning = new float2(x, y);
    }

    static bool TryParsePair(string line, string prefix, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var parts = line.AsSpan(prefix.Length);
        int comma = parts.IndexOf(',');
        return comma > 0 &&
               float.TryParse(parts[..comma], NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
               float.TryParse(parts[(comma + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
    }

    static bool TryGetStyleFloat(ImNodesStyleVar item, out float value)
    {
        var style = Current.Style;
        switch (item)
        {
            case ImNodesStyleVar.GridSpacing: value = style.GridSpacing; return true;
            case ImNodesStyleVar.NodeCornerRounding: value = style.NodeCornerRounding; return true;
            case ImNodesStyleVar.NodeBorderThickness: value = style.NodeBorderThickness; return true;
            case ImNodesStyleVar.LinkThickness: value = style.LinkThickness; return true;
            case ImNodesStyleVar.LinkLineSegmentsPerLength: value = style.LinkLineSegmentsPerLength; return true;
            case ImNodesStyleVar.LinkHoverDistance: value = style.LinkHoverDistance; return true;
            case ImNodesStyleVar.PinCircleRadius: value = style.PinCircleRadius; return true;
            case ImNodesStyleVar.PinQuadSideLength: value = style.PinQuadSideLength; return true;
            case ImNodesStyleVar.PinTriangleSideLength: value = style.PinTriangleSideLength; return true;
            case ImNodesStyleVar.PinLineThickness: value = style.PinLineThickness; return true;
            case ImNodesStyleVar.PinHoverRadius: value = style.PinHoverRadius; return true;
            case ImNodesStyleVar.PinOffset: value = style.PinOffset; return true;
            default: value = 0f; return false;
        }
    }

    static bool TryGetStyleVec(ImNodesStyleVar item, out float2 value)
    {
        var style = Current.Style;
        switch (item)
        {
            case ImNodesStyleVar.NodePadding: value = style.NodePadding; return true;
            case ImNodesStyleVar.MiniMapPadding: value = style.MiniMapPadding; return true;
            case ImNodesStyleVar.MiniMapOffset: value = style.MiniMapOffset; return true;
            default: value = default; return false;
        }
    }

    static void SetStyleFloat(ImNodesStyleVar item, float value)
    {
        var style = Current.Style;
        switch (item)
        {
            case ImNodesStyleVar.GridSpacing: style.GridSpacing = value; break;
            case ImNodesStyleVar.NodeCornerRounding: style.NodeCornerRounding = value; break;
            case ImNodesStyleVar.NodeBorderThickness: style.NodeBorderThickness = value; break;
            case ImNodesStyleVar.LinkThickness: style.LinkThickness = value; break;
            case ImNodesStyleVar.LinkLineSegmentsPerLength: style.LinkLineSegmentsPerLength = value; break;
            case ImNodesStyleVar.LinkHoverDistance: style.LinkHoverDistance = value; break;
            case ImNodesStyleVar.PinCircleRadius: style.PinCircleRadius = value; break;
            case ImNodesStyleVar.PinQuadSideLength: style.PinQuadSideLength = value; break;
            case ImNodesStyleVar.PinTriangleSideLength: style.PinTriangleSideLength = value; break;
            case ImNodesStyleVar.PinLineThickness: style.PinLineThickness = value; break;
            case ImNodesStyleVar.PinHoverRadius: style.PinHoverRadius = value; break;
            case ImNodesStyleVar.PinOffset: style.PinOffset = value; break;
            default: throw new ArgumentException($"ImNodes style variable {item} is not a float.", nameof(item));
        }
    }

    static void SetStyleVec(ImNodesStyleVar item, float2 value)
    {
        var style = Current.Style;
        switch (item)
        {
            case ImNodesStyleVar.NodePadding: style.NodePadding = value; break;
            case ImNodesStyleVar.MiniMapPadding: style.MiniMapPadding = value; break;
            case ImNodesStyleVar.MiniMapOffset: style.MiniMapOffset = value; break;
            default: throw new ArgumentException($"ImNodes style variable {item} is not a float2.", nameof(item));
        }
    }

    static bool IsVecStyle(ImNodesStyleVar item) =>
        item is ImNodesStyleVar.NodePadding or ImNodesStyleVar.MiniMapPadding or ImNodesStyleVar.MiniMapOffset;

    static void ApplyColorPreset(ImNodesStyle? dest, uint[] colors)
    {
        dest ??= Current.Style;
        colors.CopyTo(dest.Colors, 0);
    }

    static uint[] BuildColors(bool dark = false, bool classic = false, bool light = false)
    {
        var colors = new uint[(int)ImNodesCol.Count];
        if (dark)
        {
            colors[(int)ImNodesCol.NodeBackground] = Rgba(50, 50, 50, 255);
            colors[(int)ImNodesCol.NodeBackgroundHovered] = Rgba(75, 75, 75, 255);
            colors[(int)ImNodesCol.NodeBackgroundSelected] = Rgba(75, 75, 75, 255);
            colors[(int)ImNodesCol.NodeOutline] = Rgba(100, 100, 100, 255);
            colors[(int)ImNodesCol.TitleBar] = Rgba(41, 74, 122, 255);
            colors[(int)ImNodesCol.TitleBarHovered] = Rgba(66, 150, 250, 255);
            colors[(int)ImNodesCol.TitleBarSelected] = Rgba(66, 150, 250, 255);
            colors[(int)ImNodesCol.Link] = Rgba(61, 133, 224, 200);
            colors[(int)ImNodesCol.LinkHovered] = Rgba(66, 150, 250, 255);
            colors[(int)ImNodesCol.LinkSelected] = Rgba(66, 150, 250, 255);
            colors[(int)ImNodesCol.Pin] = Rgba(53, 150, 250, 180);
            colors[(int)ImNodesCol.PinHovered] = Rgba(53, 150, 250, 255);
            colors[(int)ImNodesCol.BoxSelector] = Rgba(61, 133, 224, 30);
            colors[(int)ImNodesCol.BoxSelectorOutline] = Rgba(61, 133, 224, 150);
            colors[(int)ImNodesCol.GridBackground] = Rgba(40, 40, 50, 200);
            colors[(int)ImNodesCol.GridLine] = Rgba(200, 200, 200, 40);
            colors[(int)ImNodesCol.GridLinePrimary] = Rgba(240, 240, 240, 60);
            colors[(int)ImNodesCol.MiniMapBackground] = Rgba(25, 25, 25, 150);
            colors[(int)ImNodesCol.MiniMapBackgroundHovered] = Rgba(25, 25, 25, 200);
            colors[(int)ImNodesCol.MiniMapOutline] = Rgba(150, 150, 150, 100);
            colors[(int)ImNodesCol.MiniMapOutlineHovered] = Rgba(150, 150, 150, 200);
            colors[(int)ImNodesCol.MiniMapNodeBackground] = Rgba(200, 200, 200, 100);
            colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered] = Rgba(200, 200, 200, 255);
            colors[(int)ImNodesCol.MiniMapNodeBackgroundSelected] = colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered];
            colors[(int)ImNodesCol.MiniMapNodeOutline] = Rgba(200, 200, 200, 100);
            colors[(int)ImNodesCol.MiniMapLink] = colors[(int)ImNodesCol.Link];
            colors[(int)ImNodesCol.MiniMapLinkSelected] = colors[(int)ImNodesCol.LinkSelected];
            colors[(int)ImNodesCol.MiniMapCanvas] = Rgba(200, 200, 200, 25);
            colors[(int)ImNodesCol.MiniMapCanvasOutline] = Rgba(200, 200, 200, 200);
        }
        else if (classic)
        {
            colors[(int)ImNodesCol.NodeBackground] = Rgba(50, 50, 50, 255);
            colors[(int)ImNodesCol.NodeBackgroundHovered] = Rgba(75, 75, 75, 255);
            colors[(int)ImNodesCol.NodeBackgroundSelected] = Rgba(75, 75, 75, 255);
            colors[(int)ImNodesCol.NodeOutline] = Rgba(100, 100, 100, 255);
            colors[(int)ImNodesCol.TitleBar] = Rgba(69, 69, 138, 255);
            colors[(int)ImNodesCol.TitleBarHovered] = Rgba(82, 82, 161, 255);
            colors[(int)ImNodesCol.TitleBarSelected] = Rgba(82, 82, 161, 255);
            colors[(int)ImNodesCol.Link] = Rgba(255, 255, 255, 100);
            colors[(int)ImNodesCol.LinkHovered] = Rgba(105, 99, 204, 153);
            colors[(int)ImNodesCol.LinkSelected] = Rgba(105, 99, 204, 153);
            colors[(int)ImNodesCol.Pin] = Rgba(89, 102, 156, 170);
            colors[(int)ImNodesCol.PinHovered] = Rgba(102, 122, 179, 200);
            colors[(int)ImNodesCol.BoxSelector] = Rgba(82, 82, 161, 100);
            colors[(int)ImNodesCol.BoxSelectorOutline] = Rgba(82, 82, 161, 255);
            colors[(int)ImNodesCol.GridBackground] = Rgba(40, 40, 50, 200);
            colors[(int)ImNodesCol.GridLine] = Rgba(200, 200, 200, 40);
            colors[(int)ImNodesCol.GridLinePrimary] = Rgba(240, 240, 240, 60);
            colors[(int)ImNodesCol.MiniMapBackground] = Rgba(25, 25, 25, 100);
            colors[(int)ImNodesCol.MiniMapBackgroundHovered] = Rgba(25, 25, 25, 200);
            colors[(int)ImNodesCol.MiniMapOutline] = Rgba(150, 150, 150, 100);
            colors[(int)ImNodesCol.MiniMapOutlineHovered] = Rgba(150, 150, 150, 200);
            colors[(int)ImNodesCol.MiniMapNodeBackground] = Rgba(200, 200, 200, 100);
            colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered] = Rgba(200, 200, 200, 255);
            colors[(int)ImNodesCol.MiniMapNodeBackgroundSelected] = Rgba(200, 200, 240, 255);
            colors[(int)ImNodesCol.MiniMapNodeOutline] = Rgba(200, 200, 200, 100);
            colors[(int)ImNodesCol.MiniMapLink] = colors[(int)ImNodesCol.Link];
            colors[(int)ImNodesCol.MiniMapLinkSelected] = colors[(int)ImNodesCol.LinkSelected];
            colors[(int)ImNodesCol.MiniMapCanvas] = Rgba(200, 200, 200, 25);
            colors[(int)ImNodesCol.MiniMapCanvasOutline] = Rgba(200, 200, 200, 200);
        }
        else if (light)
        {
            colors[(int)ImNodesCol.NodeBackground] = Rgba(240, 240, 240, 255);
            colors[(int)ImNodesCol.NodeBackgroundHovered] = Rgba(240, 240, 240, 255);
            colors[(int)ImNodesCol.NodeBackgroundSelected] = Rgba(240, 240, 240, 255);
            colors[(int)ImNodesCol.NodeOutline] = Rgba(100, 100, 100, 255);
            colors[(int)ImNodesCol.TitleBar] = Rgba(248, 248, 248, 255);
            colors[(int)ImNodesCol.TitleBarHovered] = Rgba(209, 209, 209, 255);
            colors[(int)ImNodesCol.TitleBarSelected] = Rgba(209, 209, 209, 255);
            colors[(int)ImNodesCol.Link] = Rgba(66, 150, 250, 100);
            colors[(int)ImNodesCol.LinkHovered] = Rgba(66, 150, 250, 242);
            colors[(int)ImNodesCol.LinkSelected] = Rgba(66, 150, 250, 242);
            colors[(int)ImNodesCol.Pin] = Rgba(66, 150, 250, 160);
            colors[(int)ImNodesCol.PinHovered] = Rgba(66, 150, 250, 255);
            colors[(int)ImNodesCol.BoxSelector] = Rgba(90, 170, 250, 30);
            colors[(int)ImNodesCol.BoxSelectorOutline] = Rgba(90, 170, 250, 150);
            colors[(int)ImNodesCol.GridBackground] = Rgba(225, 225, 225, 255);
            colors[(int)ImNodesCol.GridLine] = Rgba(180, 180, 180, 100);
            colors[(int)ImNodesCol.GridLinePrimary] = Rgba(120, 120, 120, 100);
            colors[(int)ImNodesCol.MiniMapBackground] = Rgba(25, 25, 25, 100);
            colors[(int)ImNodesCol.MiniMapBackgroundHovered] = Rgba(25, 25, 25, 200);
            colors[(int)ImNodesCol.MiniMapOutline] = Rgba(150, 150, 150, 100);
            colors[(int)ImNodesCol.MiniMapOutlineHovered] = Rgba(150, 150, 150, 200);
            colors[(int)ImNodesCol.MiniMapNodeBackground] = Rgba(200, 200, 200, 100);
            colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered] = Rgba(200, 200, 200, 255);
            colors[(int)ImNodesCol.MiniMapNodeBackgroundSelected] = Rgba(200, 200, 240, 255);
            colors[(int)ImNodesCol.MiniMapNodeOutline] = Rgba(200, 200, 200, 100);
            colors[(int)ImNodesCol.MiniMapLink] = colors[(int)ImNodesCol.Link];
            colors[(int)ImNodesCol.MiniMapLinkSelected] = colors[(int)ImNodesCol.LinkSelected];
            colors[(int)ImNodesCol.MiniMapCanvas] = Rgba(200, 200, 200, 25);
            colors[(int)ImNodesCol.MiniMapCanvasOutline] = Rgba(200, 200, 200, 200);
        }

        return colors;
    }

    static uint Rgba(byte r, byte g, byte b, byte a) => new ImColor8(r, g, b, a);
}
