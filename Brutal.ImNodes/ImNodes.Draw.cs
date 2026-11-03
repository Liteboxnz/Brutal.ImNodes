using System.Diagnostics;
using System.Runtime.CompilerServices;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace Brutal.ImNodesAPI;

public static partial class ImNodes
{
    static floatRect Rect(float2 min, float2 max)
    {
        floatRect rect = default;
        rect.Min = min;
        rect.Max = max;
        return rect;
    }

    static float RectWidth(floatRect rect) => rect.Max.X - rect.Min.X;

    static float RectHeight(floatRect rect) => rect.Max.Y - rect.Min.Y;

    static float2 RectSize(floatRect rect) => new(RectWidth(rect), RectHeight(rect));

    static float2 RectCenter(floatRect rect) => new((rect.Min.X + rect.Max.X) * 0.5f, (rect.Min.Y + rect.Max.Y) * 0.5f);

    static bool Contains(floatRect rect, float2 point) =>
        point.X >= rect.Min.X && point.Y >= rect.Min.Y && point.X < rect.Max.X && point.Y < rect.Max.Y;

    static bool Overlaps(floatRect a, floatRect b) =>
        b.Min.Y < a.Max.Y && b.Max.Y > a.Min.Y && b.Min.X < a.Max.X && b.Max.X > a.Min.X;

    static bool IsInverted(floatRect rect) => rect.Min.X > rect.Max.X || rect.Min.Y > rect.Max.Y;

    static void Expand(ref floatRect rect, float2 amount)
    {
        rect.Min = new float2(rect.Min.X - amount.X, rect.Min.Y - amount.Y);
        rect.Max = new float2(rect.Max.X + amount.X, rect.Max.Y + amount.Y);
    }

    static void AddPoint(ref floatRect rect, float2 point)
    {
        rect.Min = new float2(MathF.Min(rect.Min.X, point.X), MathF.Min(rect.Min.Y, point.Y));
        rect.Max = new float2(MathF.Max(rect.Max.X, point.X), MathF.Max(rect.Max.Y, point.Y));
    }

    static floatRect ItemRect() => Rect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());

    static float2 Add(float2 a, float2 b) => new(a.X + b.X, a.Y + b.Y);

    static float2 Sub(float2 a, float2 b) => new(a.X - b.X, a.Y - b.Y);

    static float2 Mul(float2 v, float s) => new(v.X * s, v.Y * s);

    static float2 Div(float2 v, float s) => new(v.X / s, v.Y / s);

    static float LengthSqr(float2 v) => v.X * v.X + v.Y * v.Y;

    static float InvLength(float2 v, float fail)
    {
        float d = LengthSqr(v);
        return d > 0f ? 1f / MathF.Sqrt(d) : fail;
    }

    static float FMod(float x, float y) => x - y * MathF.Truncate(x / y);

    static float2 Floor(float2 v) => new(MathF.Floor(v.X), MathF.Floor(v.Y));

    static float2 Lerp(float2 a, float2 b, float2 t) => new(a.X + (b.X - a.X) * t.X, a.Y + (b.Y - a.Y) * t.Y);

    static void Swap(ref float a, ref float b) => (a, b) = (b, a);

    static float2 EvalBezier(float t, float2 p0, float2 p1, float2 p2, float2 p3)
    {
        float u = 1f - t;
        float b0 = u * u * u;
        float b1 = 3f * u * u * t;
        float b2 = 3f * u * t * t;
        float b3 = t * t * t;
        return new float2(
            b0 * p0.X + b1 * p1.X + b2 * p2.X + b3 * p3.X,
            b0 * p0.Y + b1 * p1.Y + b2 * p2.Y + b3 * p3.Y);
    }

    static float2 ClosestPointOnSegment(float2 a, float2 b, float2 p)
    {
        var ap = Sub(p, a);
        var ab = Sub(b, a);
        float dot = ap.X * ab.X + ap.Y * ab.Y;
        if (dot < 0f)
            return a;
        float abLenSqr = LengthSqr(ab);
        if (dot > abLenSqr)
            return b;
        return Add(a, Mul(ab, dot / abLenSqr));
    }

    static float2 ClosestOnBezier(int numSegments, float2 p, CubicBezier curve)
    {
        Debug.Assert(numSegments > 0);
        var previous = curve.P0;
        var closest = previous;
        float closestDist = float.MaxValue;
        float step = 1f / numSegments;
        for (int i = 1; i <= numSegments; i++)
        {
            var current = EvalBezier(step * i, curve.P0, curve.P1, curve.P2, curve.P3);
            var point = ClosestPointOnSegment(previous, current, p);
            float dist = LengthSqr(Sub(p, point));
            if (dist < closestDist)
            {
                closest = point;
                closestDist = dist;
            }

            previous = current;
        }

        return closest;
    }

    static float DistanceToBezier(float2 pos, CubicBezier curve, int numSegments)
    {
        var delta = Sub(ClosestOnBezier(numSegments, pos, curve), pos);
        return MathF.Sqrt(LengthSqr(delta));
    }

    static floatRect BezierBounds(CubicBezier curve)
    {
        var min = new float2(MathF.Min(curve.P0.X, curve.P3.X), MathF.Min(curve.P0.Y, curve.P3.Y));
        var max = new float2(MathF.Max(curve.P0.X, curve.P3.X), MathF.Max(curve.P0.Y, curve.P3.Y));
        var rect = Rect(min, max);
        AddPoint(ref rect, curve.P1);
        AddPoint(ref rect, curve.P2);
        float hover = Current.Style.LinkHoverDistance;
        Expand(ref rect, new float2(hover, hover));
        return rect;
    }

    static CubicBezier MakeBezier(float2 start, float2 end, ImNodesAttributeType startType, float segmentsPerLength)
    {
        if (startType == ImNodesAttributeType.Input)
            (start, end) = (end, start);

        float length = MathF.Sqrt(LengthSqr(Sub(end, start)));
        var offset = new float2(0.25f * length, 0f);
        return new CubicBezier
        {
            P0 = start,
            P1 = Add(start, offset),
            P2 = Sub(end, offset),
            P3 = end,
            NumSegments = Math.Max((int)(length * segmentsPerLength), 1),
        };
    }

    static float ImplicitLine(float2 p1, float2 p2, float2 p) =>
        (p2.Y - p1.Y) * p.X + (p1.X - p2.X) * p.Y + (p2.X * p1.Y - p1.X * p2.Y);

    static int Sign(float value) => (value > 0f ? 1 : 0) - (value < 0f ? 1 : 0);

    static bool RectOverlapsSegment(floatRect rect, float2 p1, float2 p2)
    {
        if (Contains(rect, p1) || Contains(rect, p2))
            return true;

        var flipped = rect;
        if (flipped.Min.X > flipped.Max.X)
            Swap(ref flipped.Min.X, ref flipped.Max.X);
        if (flipped.Min.Y > flipped.Max.Y)
            Swap(ref flipped.Min.Y, ref flipped.Max.Y);

        if ((p1.X < flipped.Min.X && p2.X < flipped.Min.X) ||
            (p1.X > flipped.Max.X && p2.X > flipped.Max.X) ||
            (p1.Y < flipped.Min.Y && p2.Y < flipped.Min.Y) ||
            (p1.Y > flipped.Max.Y && p2.Y > flipped.Max.Y))
            return false;

        int[] signs =
        [
            Sign(ImplicitLine(p1, p2, flipped.Min)),
            Sign(ImplicitLine(p1, p2, new float2(flipped.Max.X, flipped.Min.Y))),
            Sign(ImplicitLine(p1, p2, new float2(flipped.Min.X, flipped.Max.Y))),
            Sign(ImplicitLine(p1, p2, flipped.Max)),
        ];
        int sum = 0;
        int sumAbs = 0;
        foreach (int sign in signs)
        {
            sum += sign;
            sumAbs += Math.Abs(sign);
        }

        return Math.Abs(sum) != sumAbs;
    }

    static bool RectOverlapsBezier(floatRect rect, CubicBezier curve)
    {
        var current = EvalBezier(0f, curve.P0, curve.P1, curve.P2, curve.P3);
        float dt = 1f / curve.NumSegments;
        for (int s = 0; s < curve.NumSegments; s++)
        {
            var next = EvalBezier((s + 1) * dt, curve.P0, curve.P1, curve.P2, curve.P3);
            if (RectOverlapsSegment(rect, current, next))
                return true;
            current = next;
        }

        return false;
    }

    static bool RectOverlapsLink(floatRect rect, float2 start, float2 end, ImNodesAttributeType startType)
    {
        var bounds = Rect(start, end);
        if (bounds.Min.X > bounds.Max.X)
            Swap(ref bounds.Min.X, ref bounds.Max.X);
        if (bounds.Min.Y > bounds.Max.Y)
            Swap(ref bounds.Min.Y, ref bounds.Max.Y);
        if (!Overlaps(rect, bounds))
            return false;
        if (Contains(rect, start) || Contains(rect, end))
            return true;
        var curve = MakeBezier(start, end, startType, Current.Style.LinkLineSegmentsPerLength);
        return RectOverlapsBezier(rect, curve);
    }

    static float2 ScreenToGrid(ImNodesEditorContext editor, float2 v) =>
        Sub(Sub(v, Current.CanvasOriginScreenSpace), editor.Panning);

    static floatRect ScreenToGrid(ImNodesEditorContext editor, floatRect rect) =>
        Rect(ScreenToGrid(editor, rect.Min), ScreenToGrid(editor, rect.Max));

    static float2 GridToScreen(ImNodesEditorContext editor, float2 v) =>
        Add(Add(v, Current.CanvasOriginScreenSpace), editor.Panning);

    static float2 GridToEditor(ImNodesEditorContext editor, float2 v) => Add(v, editor.Panning);

    static float2 EditorToGrid(ImNodesEditorContext editor, float2 v) => Sub(v, editor.Panning);

    static float2 EditorToScreen(float2 v) => Add(Current.CanvasOriginScreenSpace, v);

    static float2 MiniMapToGrid(ImNodesEditorContext editor, float2 v) =>
        Add(Div(Sub(v, editor.MiniMapContentScreenSpace.Min), editor.MiniMapScaling), editor.GridContentBounds.Min);

    static float2 ScreenToMiniMap(ImNodesEditorContext editor, float2 v) =>
        Add(Mul(Sub(ScreenToGrid(editor, v), editor.GridContentBounds.Min), editor.MiniMapScaling), editor.MiniMapContentScreenSpace.Min);

    static floatRect ScreenToMiniMap(ImNodesEditorContext editor, floatRect rect) =>
        Rect(ScreenToMiniMap(editor, rect.Min), ScreenToMiniMap(editor, rect.Max));

    static unsafe void GrowChannels(ImDrawListPtr drawList, int numChannels)
    {
        ref var splitter = ref drawList._Splitter;
        if (splitter._Count == 1)
        {
            var splitterPtr = new ImDrawListSplitterPtr((ImDrawListSplitter*)Unsafe.AsPointer(ref splitter));
            splitterPtr.Split(drawList, numChannels + 1);
            return;
        }

        ref var channels = ref splitter._Channels;
        int oldCapacity = channels.Count;
        int oldCount = splitter._Count;
        int requested = oldCount + numChannels;
        if (oldCapacity < requested)
            channels.Count = requested;
        splitter._Count = requested;

        ref var clipStack = ref drawList._ClipRectStack;
        ref var textureStack = ref drawList._TextureStack;
        var clip = clipStack[clipStack.Count - 1];
        var texture = textureStack[textureStack.Count - 1];
        for (int i = oldCount; i < requested; i++)
        {
            var channel = channels[i];
            if (i < oldCapacity)
            {
                channel._CmdBuffer.Count = 0;
                channel._IdxBuffer.Count = 0;
            }

            channel._CmdBuffer.Add(new ImDrawCmd { ClipRect = clip, TexRef = texture });
            channels[i] = channel;
        }
    }

    static void SwapChannels(ImDrawListPtr drawList, int lhsIdx, int rhsIdx)
    {
        if (lhsIdx == rhsIdx)
            return;
        ref var splitter = ref drawList._Splitter;
        ref var channels = ref splitter._Channels;
        var lhs = channels[lhsIdx];
        var rhs = channels[rhsIdx];
        (lhs._CmdBuffer, rhs._CmdBuffer) = (rhs._CmdBuffer, lhs._CmdBuffer);
        (lhs._IdxBuffer, rhs._IdxBuffer) = (rhs._IdxBuffer, lhs._IdxBuffer);
        channels[lhsIdx] = lhs;
        channels[rhsIdx] = rhs;

        if (splitter._Current == lhsIdx)
            splitter._Current = rhsIdx;
        else if (splitter._Current == rhsIdx)
            splitter._Current = lhsIdx;
    }

    static void DrawListSet(ImDrawListPtr drawList)
    {
        var g = Current;
        g.CanvasDrawList = drawList;
        g.NodeIdxToSubmissionIdx.Clear();
        g.NodeIdxSubmissionOrder.Clear();
    }

    static void AddNodeChannel(int nodeIdx)
    {
        var g = Current;
        g.NodeIdxToSubmissionIdx[nodeIdx] = g.NodeIdxSubmissionOrder.Count;
        g.NodeIdxSubmissionOrder.Add(nodeIdx);
        GrowChannels(g.CanvasDrawList, 2);
    }

    static int BackgroundChannel(int submissionIdx) => 1 + 2 * submissionIdx;

    static int ForegroundChannel(int submissionIdx) => BackgroundChannel(submissionIdx) + 1;

    static void ActivateClickChannel()
    {
        var drawList = Current.CanvasDrawList;
        drawList.ChannelsSetCurrent(drawList._Splitter._Count - 1);
    }

    static void ActivateCurrentNodeForeground()
    {
        int channel = ForegroundChannel(Current.NodeIdxSubmissionOrder.Count - 1);
        Current.CanvasDrawList.ChannelsSetCurrent(channel);
    }

    static void ActivateNodeBackground(int nodeIdx)
    {
        if (!Current.NodeIdxToSubmissionIdx.TryGetValue(nodeIdx, out int submissionIdx))
            throw new InvalidOperationException("Node draw channels do not match BeginNode submissions. Call EditorContextMoveToNode, SetNodeGridSpacePos, SetNodeScreenSpacePos, and SetNodeDraggable before EndNode.");
        Current.CanvasDrawList.ChannelsSetCurrent(BackgroundChannel(submissionIdx));
    }

    static void SortChannelsByDepth(List<int> depthOrder)
    {
        var g = Current;
        if (g.NodeIdxToSubmissionIdx.Count < 2)
            return;
        if (depthOrder.Count != g.NodeIdxSubmissionOrder.Count)
            throw new InvalidOperationException("Node submission order does not match the depth stack. Set node positions before EndNode.");

        int start = depthOrder.Count - 1;
        while (depthOrder[start] == g.NodeIdxSubmissionOrder[start])
        {
            if (--start == 0)
                return;
        }

        for (int depthIdx = start; depthIdx > 0; depthIdx--)
        {
            int nodeIdx = depthOrder[depthIdx];
            int submissionIdx = g.NodeIdxSubmissionOrder.IndexOf(nodeIdx);
            if (submissionIdx < 0)
                throw new InvalidOperationException("A depth-stack node has no draw channel.");
            if (submissionIdx == depthIdx)
                continue;

            for (int j = submissionIdx; j < depthIdx; j++)
            {
                SwapChannels(g.CanvasDrawList, BackgroundChannel(j), BackgroundChannel(j + 1));
                SwapChannels(g.CanvasDrawList, ForegroundChannel(j), ForegroundChannel(j + 1));
                (g.NodeIdxSubmissionOrder[j], g.NodeIdxSubmissionOrder[j + 1]) =
                    (g.NodeIdxSubmissionOrder[j + 1], g.NodeIdxSubmissionOrder[j]);
            }
        }
    }

    static float2 PinPosition(floatRect nodeRect, floatRect attributeRect, ImNodesAttributeType type)
    {
        float x = type == ImNodesAttributeType.Input
            ? nodeRect.Min.X - Current.Style.PinOffset
            : nodeRect.Max.X + Current.Style.PinOffset;
        return new float2(x, 0.5f * (attributeRect.Min.Y + attributeRect.Max.Y));
    }

    static float2 PinPosition(ImNodesEditorContext editor, PinData pin)
    {
        var parent = editor.Nodes.Pool[pin.ParentNodeIdx]!.Rect;
        return PinPosition(parent, pin.AttributeRect, pin.Type);
    }

    static bool MouseInCanvas()
    {
        bool hoveredOrFocused = ImGui.IsWindowHovered(ImGuiHoveredFlags.None) || ImGui.IsWindowFocused(ImGuiFocusedFlags.None);
        return hoveredOrFocused && Contains(Current.CanvasRectScreenSpace, ImGui.GetMousePos());
    }

    static void BeginNodeSelection(ImNodesEditorContext editor, int nodeIdx)
    {
        if (editor.ClickInteraction.Type != ImNodesClickInteractionType.None)
            return;

        var g = Current;
        editor.ClickInteraction.Type = ImNodesClickInteractionType.Node;
        if (!editor.SelectedNodeIndices.Contains(nodeIdx))
        {
            editor.SelectedLinkIndices.Clear();
            if (!g.MultipleSelectModifier)
                editor.SelectedNodeIndices.Clear();
            editor.SelectedNodeIndices.Add(nodeIdx);

            var depth = editor.NodeDepthOrder;
            int found = depth.IndexOf(nodeIdx);
            Debug.Assert(found >= 0);
            if (found >= 0)
            {
                depth.RemoveAt(found);
                depth.Add(nodeIdx);
            }
        }
        else if (g.MultipleSelectModifier)
        {
            int found = editor.SelectedNodeIndices.IndexOf(nodeIdx);
            if (found >= 0)
                editor.SelectedNodeIndices.RemoveAt(found);
            editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
        }

        var origin = editor.Nodes.Pool[nodeIdx]!.Origin;
        editor.PrimaryNodeOffset = Sub(Add(Add(origin, g.CanvasOriginScreenSpace), editor.Panning), g.MousePos);
        editor.SelectedNodeOffsets.Clear();
        foreach (int selected in editor.SelectedNodeIndices)
            editor.SelectedNodeOffsets.Add(Sub(editor.Nodes.Pool[selected]!.Origin, origin));
    }

    static void BeginLinkSelection(ImNodesEditorContext editor, int linkIdx)
    {
        editor.ClickInteraction.Type = ImNodesClickInteractionType.Link;
        editor.SelectedNodeIndices.Clear();
        editor.SelectedLinkIndices.Clear();
        editor.SelectedLinkIndices.Add(linkIdx);
    }

    static void BeginLinkDetach(ImNodesEditorContext editor, int linkIdx, int detachPinIdx)
    {
        var link = editor.Links.Pool[linkIdx]!;
        var state = editor.ClickInteraction;
        state.Type = ImNodesClickInteractionType.LinkCreation;
        state.LinkCreation.EndPinIdx.Reset();
        state.LinkCreation.StartPinIdx = detachPinIdx == link.StartPinIdx ? link.EndPinIdx : link.StartPinIdx;
        Current.DeletedLinkIdx = new OptionalIndex(linkIdx);
    }

    static void BeginLinkCreation(ImNodesEditorContext editor, int hoveredPinIdx)
    {
        editor.ClickInteraction.Type = ImNodesClickInteractionType.LinkCreation;
        editor.ClickInteraction.LinkCreation.StartPinIdx = hoveredPinIdx;
        editor.ClickInteraction.LinkCreation.EndPinIdx.Reset();
        editor.ClickInteraction.LinkCreation.Type = ImNodesLinkCreationType.Standard;
        Current.UiState |= (int)ImNodesUiState.LinkStarted;
    }

    static void BeginLinkInteraction(ImNodesEditorContext editor, int linkIdx, OptionalIndex pinIdx)
    {
        bool modifier = Current.Io.LinkDetachWithModifierClick.Modifier?.Invoke() ?? false;
        if (modifier)
        {
            var link = editor.Links.Pool[linkIdx]!;
            var start = editor.Pins.Pool[link.StartPinIdx]!;
            var end = editor.Pins.Pool[link.EndPinIdx]!;
            float distStart = LengthSqr(Sub(start.Pos, Current.MousePos));
            float distEnd = LengthSqr(Sub(end.Pos, Current.MousePos));
            int closest = distStart < distEnd ? link.StartPinIdx : link.EndPinIdx;
            BeginLinkDetach(editor, linkIdx, closest);
            editor.ClickInteraction.LinkCreation.Type = ImNodesLinkCreationType.FromDetach;
            return;
        }

        if (pinIdx.HasValue)
        {
            int flags = editor.Pins.Pool[pinIdx.Value]!.Flags;
            if ((flags & (int)ImNodesAttributeFlags.EnableLinkDetachWithDragClick) != 0)
            {
                BeginLinkDetach(editor, linkIdx, pinIdx.Value);
                editor.ClickInteraction.LinkCreation.Type = ImNodesLinkCreationType.FromDetach;
            }
            else
            {
                BeginLinkCreation(editor, pinIdx.Value);
            }
        }
        else
        {
            BeginLinkSelection(editor, linkIdx);
        }
    }

    static void BeginCanvasInteraction(ImNodesEditorContext editor)
    {
        var g = Current;
        bool anyHovered = g.HoveredNodeIdx.HasValue || g.HoveredLinkIdx.HasValue || g.HoveredPinIdx.HasValue || ImGui.IsAnyItemHovered();
        if (editor.ClickInteraction.Type != ImNodesClickInteractionType.None || anyHovered || !MouseInCanvas() || !ImGui.IsWindowHovered(ImGuiHoveredFlags.None))
            return;

        if (g.AltMouseClicked)
        {
            editor.ClickInteraction.Type = ImNodesClickInteractionType.Panning;
        }
        else if (g.LeftMouseClicked)
        {
            editor.ClickInteraction.Type = ImNodesClickInteractionType.BoxSelection;
            editor.ClickInteraction.BoxSelectorRect.Min = ScreenToGrid(editor, g.MousePos);
        }
    }

    static void BoxSelectorUpdateSelection(ImNodesEditorContext editor, floatRect box)
    {
        if (box.Min.X > box.Max.X)
            Swap(ref box.Min.X, ref box.Max.X);
        if (box.Min.Y > box.Max.Y)
            Swap(ref box.Min.Y, ref box.Max.Y);

        editor.SelectedNodeIndices.Clear();
        for (int nodeIdx = 0; nodeIdx < editor.Nodes.Pool.Count; nodeIdx++)
        {
            if (editor.Nodes.InUse[nodeIdx] && Overlaps(box, editor.Nodes.Pool[nodeIdx]!.Rect))
                editor.SelectedNodeIndices.Add(nodeIdx);
        }

        editor.SelectedLinkIndices.Clear();
        for (int linkIdx = 0; linkIdx < editor.Links.Pool.Count; linkIdx++)
        {
            if (!editor.Links.InUse[linkIdx])
                continue;
            var link = editor.Links.Pool[linkIdx]!;
            var startPin = editor.Pins.Pool[link.StartPinIdx]!;
            var endPin = editor.Pins.Pool[link.EndPinIdx]!;
            var start = PinPosition(editor.Nodes.Pool[startPin.ParentNodeIdx]!.Rect, startPin.AttributeRect, startPin.Type);
            var end = PinPosition(editor.Nodes.Pool[endPin.ParentNodeIdx]!.Rect, endPin.AttributeRect, endPin.Type);
            if (RectOverlapsLink(box, start, end, startPin.Type))
                editor.SelectedLinkIndices.Add(linkIdx);
        }
    }

    static float2 SnapOriginToGrid(float2 origin)
    {
        if ((Current.Style.Flags & ImNodesStyleFlags.GridSnapping) == 0)
            return origin;

        float spacing = Current.Style.GridSpacing;
        float half = spacing * 0.5f;
        float modX = FMod(MathF.Abs(origin.X) + half, spacing) - half;
        float modY = FMod(MathF.Abs(origin.Y) + half, spacing) - half;
        origin.X += origin.X < 0f ? modX : -modX;
        origin.Y += origin.Y < 0f ? modY : -modY;
        return origin;
    }

    static float MouseDragMaxSqr()
    {
        var distances = ImGui.GetIO().MouseDragMaxDistanceSqr;
        return distances[0];
    }

    static void TranslateSelectedNodes(ImNodesEditorContext editor)
    {
        var g = Current;
        if (!g.LeftMouseDragging)
            return;

        bool shouldTranslate = (g.Style.Flags & ImNodesStyleFlags.GridSnapping) == 0 || MouseDragMaxSqr() > 5f;
        var origin = SnapOriginToGrid(Add(Sub(Sub(g.MousePos, g.CanvasOriginScreenSpace), editor.Panning), editor.PrimaryNodeOffset));
        for (int i = 0; i < editor.SelectedNodeIndices.Count; i++)
        {
            var node = editor.Nodes.Pool[editor.SelectedNodeIndices[i]]!;
            if (node.Draggable && shouldTranslate)
                node.Origin = Add(Add(origin, editor.SelectedNodeOffsets[i]), editor.AutoPanningDelta);
        }
    }

    static bool SameLink(LinkData lhs, LinkData rhs)
    {
        int lhsStart = lhs.StartPinIdx;
        int lhsEnd = lhs.EndPinIdx;
        int rhsStart = rhs.StartPinIdx;
        int rhsEnd = rhs.EndPinIdx;
        if (lhsStart > lhsEnd)
            (lhsStart, lhsEnd) = (lhsEnd, lhsStart);
        if (rhsStart > rhsEnd)
            (rhsStart, rhsEnd) = (rhsEnd, rhsStart);
        return lhsStart == rhsStart && lhsEnd == rhsEnd;
    }

    static OptionalIndex FindDuplicateLink(ImNodesEditorContext editor, int startPinIdx, int endPinIdx)
    {
        var test = new LinkData(0) { StartPinIdx = startPinIdx, EndPinIdx = endPinIdx };
        for (int linkIdx = 0; linkIdx < editor.Links.Pool.Count; linkIdx++)
        {
            if (editor.Links.InUse[linkIdx] && editor.Links.Pool[linkIdx] is { } link && SameLink(test, link))
                return new OptionalIndex(linkIdx);
        }

        return new OptionalIndex();
    }

    static bool ShouldLinkSnapToPin(ImNodesEditorContext editor, PinData startPin, int hoveredPinIdx, OptionalIndex duplicate)
    {
        var endPin = editor.Pins.Pool[hoveredPinIdx]!;
        if (startPin.ParentNodeIdx == endPin.ParentNodeIdx || startPin.Type == endPin.Type)
            return false;
        if (duplicate.HasValue && duplicate != Current.SnapLinkIdx)
            return false;
        return true;
    }

    static void ClickInteractionUpdate(ImNodesEditorContext editor)
    {
        var g = Current;
        switch (editor.ClickInteraction.Type)
        {
            case ImNodesClickInteractionType.BoxSelection:
            {
                editor.ClickInteraction.BoxSelectorRect.Max = ScreenToGrid(editor, g.MousePos);
                var box = editor.ClickInteraction.BoxSelectorRect;
                box.Min = GridToScreen(editor, box.Min);
                box.Max = GridToScreen(editor, box.Max);
                BoxSelectorUpdateSelection(editor, box);
                FillRect(g.CanvasDrawList, box.Min, box.Max, g.Style.Colors[(int)ImNodesCol.BoxSelector]);
                StrokeRect(g.CanvasDrawList, box.Min, box.Max, g.Style.Colors[(int)ImNodesCol.BoxSelectorOutline]);

                if (g.LeftMouseReleased)
                {
                    var depth = editor.NodeDepthOrder;
                    var selected = editor.SelectedNodeIndices;
                    if (selected.Count > 0 && selected.Count < depth.Count)
                    {
                        int moved = 0;
                        for (int i = 0; i < depth.Count - selected.Count; i++)
                        {
                            for (int nodeIdx = depth[i]; selected.Contains(nodeIdx); nodeIdx = depth[i])
                            {
                                depth.RemoveAt(i);
                                depth.Add(nodeIdx);
                                moved++;
                            }

                            if (moved == selected.Count)
                                break;
                        }
                    }

                    editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                }

                break;
            }
            case ImNodesClickInteractionType.Node:
                TranslateSelectedNodes(editor);
                if (g.LeftMouseReleased)
                    editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                break;
            case ImNodesClickInteractionType.Link:
                if (g.LeftMouseReleased)
                    editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                break;
            case ImNodesClickInteractionType.LinkCreation:
            {
                var startPin = editor.Pins.Pool[editor.ClickInteraction.LinkCreation.StartPinIdx]!;
                var duplicate = g.HoveredPinIdx.HasValue
                    ? FindDuplicateLink(editor, editor.ClickInteraction.LinkCreation.StartPinIdx, g.HoveredPinIdx.Value)
                    : new OptionalIndex();
                bool shouldSnap = g.HoveredPinIdx.HasValue &&
                                  ShouldLinkSnapToPin(editor, startPin, g.HoveredPinIdx.Value, duplicate);
                bool snappingChanged = editor.ClickInteraction.LinkCreation.EndPinIdx.HasValue &&
                                       g.HoveredPinIdx != editor.ClickInteraction.LinkCreation.EndPinIdx;
                if (snappingChanged && g.SnapLinkIdx.HasValue)
                    BeginLinkDetach(editor, g.SnapLinkIdx.Value, editor.ClickInteraction.LinkCreation.EndPinIdx.Value);

                var startPos = PinPosition(editor, startPin);
                var endPos = shouldSnap
                    ? PinPosition(editor, editor.Pins.Pool[g.HoveredPinIdx.Value]!)
                    : g.MousePos;
                var curve = MakeBezier(startPos, endPos, startPin.Type, g.Style.LinkLineSegmentsPerLength);
                Bezier(g.CanvasDrawList, curve, g.Style.Colors[(int)ImNodesCol.Link], g.Style.LinkThickness);

                bool createOnSnap = g.HoveredPinIdx.HasValue &&
                                    (editor.Pins.Pool[g.HoveredPinIdx.Value]!.Flags & (int)ImNodesAttributeFlags.EnableLinkCreationOnSnap) != 0;
                if (!shouldSnap)
                    editor.ClickInteraction.LinkCreation.EndPinIdx.Reset();

                bool create = shouldSnap && (g.LeftMouseReleased || createOnSnap);
                if (create && !duplicate.HasValue)
                {
                    if (!g.LeftMouseReleased && editor.ClickInteraction.LinkCreation.EndPinIdx == g.HoveredPinIdx)
                        break;
                    g.UiState |= (int)ImNodesUiState.LinkCreated;
                    editor.ClickInteraction.LinkCreation.EndPinIdx = new OptionalIndex(g.HoveredPinIdx.Value);
                }

                if (g.LeftMouseReleased)
                {
                    editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                    if (!create)
                        g.UiState |= (int)ImNodesUiState.LinkDropped;
                }

                break;
            }
            case ImNodesClickInteractionType.Panning:
                if (g.AltMouseDragging)
                {
                    var delta = ImGui.GetIO().MouseDelta;
                    editor.Panning = Add(editor.Panning, delta);
                }
                else
                {
                    editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                }

                break;
            case ImNodesClickInteractionType.ImGuiItem:
                if (g.LeftMouseReleased)
                    editor.ClickInteraction.Type = ImNodesClickInteractionType.None;
                break;
            case ImNodesClickInteractionType.None:
                break;
        }
    }

    static void ResolveOccludedPins(ImNodesEditorContext editor, List<int> occluded)
    {
        occluded.Clear();
        var depth = editor.NodeDepthOrder;
        if (depth.Count < 2)
            return;

        for (int depthIdx = 0; depthIdx < depth.Count - 1; depthIdx++)
        {
            var below = editor.Nodes.Pool[depth[depthIdx]]!;
            for (int next = depthIdx + 1; next < depth.Count; next++)
            {
                var above = editor.Nodes.Pool[depth[next]]!.Rect;
                foreach (int pinIdx in below.PinIndices)
                {
                    if (Contains(above, editor.Pins.Pool[pinIdx]!.Pos))
                        occluded.Add(pinIdx);
                }
            }
        }
    }

    static OptionalIndex ResolveHoveredPin(ObjectPool<PinData> pins, List<int> occluded)
    {
        float smallest = float.MaxValue;
        var found = new OptionalIndex();
        float radiusSqr = Current.Style.PinHoverRadius * Current.Style.PinHoverRadius;
        for (int i = 0; i < pins.Pool.Count; i++)
        {
            if (!pins.InUse[i] || occluded.Contains(i) || pins.Pool[i] is not { } pin)
                continue;
            float distance = LengthSqr(Sub(pin.Pos, Current.MousePos));
            if (distance < radiusSqr && distance < smallest)
            {
                smallest = distance;
                found = new OptionalIndex(i);
            }
        }

        return found;
    }

    static OptionalIndex ResolveHoveredNode(List<int> depth)
    {
        var overlapping = Current.NodeIndicesOverlappingWithMouse;
        if (overlapping.Count == 0)
            return new OptionalIndex();
        if (overlapping.Count == 1)
            return new OptionalIndex(overlapping[0]);

        int largest = -1;
        int top = -1;
        foreach (int nodeIdx in overlapping)
        {
            for (int depthIdx = 0; depthIdx < depth.Count; depthIdx++)
            {
                if (depth[depthIdx] == nodeIdx && depthIdx > largest)
                {
                    largest = depthIdx;
                    top = nodeIdx;
                }
            }
        }

        return new OptionalIndex(top);
    }

    static OptionalIndex ResolveHoveredLink(ObjectPool<LinkData> links, ObjectPool<PinData> pins)
    {
        float smallest = float.MaxValue;
        var found = new OptionalIndex();
        var g = Current;
        for (int i = 0; i < links.Pool.Count; i++)
        {
            if (!links.InUse[i] || links.Pool[i] is not { } link)
                continue;
            if (g.HoveredPinIdx.HasValue)
            {
                if (g.HoveredPinIdx == link.StartPinIdx || g.HoveredPinIdx == link.EndPinIdx)
                    return new OptionalIndex(i);
                continue;
            }

            var start = pins.Pool[link.StartPinIdx]!;
            var end = pins.Pool[link.EndPinIdx]!;
            var curve = MakeBezier(start.Pos, end.Pos, start.Type, g.Style.LinkLineSegmentsPerLength);
            if (!Contains(BezierBounds(curve), g.MousePos))
                continue;
            float distance = DistanceToBezier(g.MousePos, curve, curve.NumSegments);
            if (distance < g.Style.LinkHoverDistance && distance < smallest)
            {
                smallest = distance;
                found = new OptionalIndex(i);
            }
        }

        return found;
    }

    static float2 TitleBarOrigin(NodeData node) => Add(node.Origin, node.LayoutStyle.Padding);

    static float2 ContentOrigin(NodeData node)
    {
        float title = RectHeight(node.TitleBarContentRect) + 2f * node.LayoutStyle.Padding.Y;
        return Add(Add(node.Origin, new float2(0f, title)), node.LayoutStyle.Padding);
    }

    static floatRect TitleRect(NodeData node)
    {
        var expanded = node.TitleBarContentRect;
        Expand(ref expanded, node.LayoutStyle.Padding);
        return Rect(
            expanded.Min,
            Add(expanded.Min, new float2(RectWidth(node.Rect), RectHeight(expanded))));
    }

    static void DrawGrid(ImNodesEditorContext editor, float2 canvasSize)
    {
        var g = Current;
        uint line = g.Style.Colors[(int)ImNodesCol.GridLine];
        uint primary = g.Style.Colors[(int)ImNodesCol.GridLinePrimary];
        bool drawPrimary = (g.Style.Flags & ImNodesStyleFlags.GridLinesPrimary) != 0;
        float spacing = g.Style.GridSpacing;
        for (float x = FMod(editor.Panning.X, spacing); x < canvasSize.X; x += spacing)
        {
            uint color = editor.Panning.X - x == 0f && drawPrimary ? primary : line;
            Line(g.CanvasDrawList, EditorToScreen(new float2(x, 0f)), EditorToScreen(new float2(x, canvasSize.Y)), color);
        }

        for (float y = FMod(editor.Panning.Y, spacing); y < canvasSize.Y; y += spacing)
        {
            uint color = editor.Panning.Y - y == 0f && drawPrimary ? primary : line;
            Line(g.CanvasDrawList, EditorToScreen(new float2(0f, y)), EditorToScreen(new float2(canvasSize.X, y)), color);
        }
    }

    static void DrawPinShape(float2 pos, PinData pin, uint color)
    {
        const int segments = 8;
        var g = Current;
        var drawList = g.CanvasDrawList;
        switch (pin.Shape)
        {
            case ImNodesPinShape.Circle:
                Circle(drawList, pos, g.Style.PinCircleRadius, color, segments, g.Style.PinLineThickness);
                break;
            case ImNodesPinShape.CircleFilled:
                CircleFilled(drawList, pos, g.Style.PinCircleRadius, color, segments);
                break;
            case ImNodesPinShape.Quad:
            {
                float half = 0.5f * g.Style.PinQuadSideLength;
                var topLeft = Add(pos, new float2(-half, half));
                var bottomLeft = Add(pos, new float2(-half, -half));
                var bottomRight = Add(pos, new float2(half, -half));
                var topRight = Add(pos, new float2(half, half));
                Quad(drawList, topLeft, bottomLeft, bottomRight, topRight, color, g.Style.PinLineThickness);
                break;
            }
            case ImNodesPinShape.QuadFilled:
            {
                float half = 0.5f * g.Style.PinQuadSideLength;
                QuadFilled(
                    drawList,
                    Add(pos, new float2(-half, half)),
                    Add(pos, new float2(-half, -half)),
                    Add(pos, new float2(half, -half)),
                    Add(pos, new float2(half, half)),
                    color);
                break;
            }
            case ImNodesPinShape.Triangle:
            {
                var offset = TriangleOffsets(g.Style.PinTriangleSideLength);
                Triangle(drawList, Add(pos, offset.TopLeft), Add(pos, offset.BottomLeft), Add(pos, offset.Right), color, 2f * g.Style.PinLineThickness);
                break;
            }
            case ImNodesPinShape.TriangleFilled:
            {
                var offset = TriangleOffsets(g.Style.PinTriangleSideLength);
                TriangleFilled(drawList, Add(pos, offset.TopLeft), Add(pos, offset.BottomLeft), Add(pos, offset.Right), color);
                break;
            }
        }
    }

    readonly struct TriangleOffset
    {
        public readonly float2 TopLeft;
        public readonly float2 BottomLeft;
        public readonly float2 Right;

        public TriangleOffset(float2 topLeft, float2 bottomLeft, float2 right)
        {
            TopLeft = topLeft;
            BottomLeft = bottomLeft;
            Right = right;
        }
    }

    static TriangleOffset TriangleOffsets(float side)
    {
        float sqrt3 = MathF.Sqrt(3f);
        float left = -0.1666666666667f * sqrt3 * side;
        float right = 0.333333333333f * sqrt3 * side;
        float vertical = 0.5f * side;
        return new TriangleOffset(new float2(left, vertical), new float2(left, -vertical), new float2(right, 0f));
    }

    static void DrawPin(ImNodesEditorContext editor, int pinIdx)
    {
        var pin = editor.Pins.Pool[pinIdx]!;
        pin.Pos = PinPosition(editor.Nodes.Pool[pin.ParentNodeIdx]!.Rect, pin.AttributeRect, pin.Type);
        uint color = Current.HoveredPinIdx == pinIdx ? pin.ColorStyle.Hovered : pin.ColorStyle.Background;
        DrawPinShape(pin.Pos, pin, color);
    }

    static void DrawNode(ImNodesEditorContext editor, int nodeIdx)
    {
        var g = Current;
        var node = editor.Nodes.Pool[nodeIdx]!;
        var cursor = Add(node.Origin, editor.Panning);
        ImGui.SetCursorPos(cursor);

        bool hovered = g.HoveredNodeIdx == nodeIdx && editor.ClickInteraction.Type != ImNodesClickInteractionType.BoxSelection;
        uint background = node.ColorStyle.Background;
        uint title = node.ColorStyle.Titlebar;
        if (editor.SelectedNodeIndices.Contains(nodeIdx))
        {
            background = node.ColorStyle.BackgroundSelected;
            title = node.ColorStyle.TitlebarSelected;
        }
        else if (hovered)
        {
            background = node.ColorStyle.BackgroundHovered;
            title = node.ColorStyle.TitlebarHovered;
        }

        FillRect(g.CanvasDrawList, node.Rect.Min, node.Rect.Max, background, node.LayoutStyle.CornerRounding);
        if (RectHeight(node.TitleBarContentRect) > 0f)
        {
            var titleRect = TitleRect(node);
            FillRect(g.CanvasDrawList, titleRect.Min, titleRect.Max, title, node.LayoutStyle.CornerRounding, ImDrawFlags.RoundCornersTop);
        }

        if ((g.Style.Flags & ImNodesStyleFlags.NodeOutline) != 0)
        {
            StrokeRect(
                g.CanvasDrawList,
                node.Rect.Min,
                node.Rect.Max,
                node.ColorStyle.Outline,
                node.LayoutStyle.CornerRounding,
                ImDrawFlags.RoundCornersAll,
                node.LayoutStyle.BorderThickness);
        }

        foreach (int pinIdx in node.PinIndices)
            DrawPin(editor, pinIdx);
        if (hovered)
            g.HoveredNodeIdx = new OptionalIndex(nodeIdx);
    }

    static void DrawLink(ImNodesEditorContext editor, int linkIdx)
    {
        var g = Current;
        if (g.DeletedLinkIdx == linkIdx)
            return;
        var link = editor.Links.Pool[linkIdx]!;
        var start = editor.Pins.Pool[link.StartPinIdx]!;
        var end = editor.Pins.Pool[link.EndPinIdx]!;
        var curve = MakeBezier(start.Pos, end.Pos, start.Type, g.Style.LinkLineSegmentsPerLength);
        bool hovered = g.HoveredLinkIdx == linkIdx && editor.ClickInteraction.Type != ImNodesClickInteractionType.BoxSelection;
        if (hovered)
            g.HoveredLinkIdx = new OptionalIndex(linkIdx);

        uint color = link.ColorStyle.Base;
        if (editor.SelectedLinkIndices.Contains(linkIdx))
            color = link.ColorStyle.Selected;
        else if (hovered)
            color = link.ColorStyle.Hovered;
        Bezier(g.CanvasDrawList, curve, color, g.Style.LinkThickness);
    }

    static bool IsMiniMapActive()
    {
        var editor = Editor;
        return editor.MiniMapEnabled && editor.MiniMapSizeFraction > 0f;
    }

    static bool IsMiniMapHovered()
    {
        var editor = Editor;
        return IsMiniMapActive() && ImGui.IsMouseHoveringRect(editor.MiniMapRectScreenSpace.Min, editor.MiniMapRectScreenSpace.Max, true);
    }

    static void CalcMiniMapLayout()
    {
        var g = Current;
        var editor = Editor;
        var offset = g.Style.MiniMapOffset;
        var border = g.Style.MiniMapPadding;
        var editorRect = g.CanvasRectScreenSpace;
        var maxSize = Floor(Sub(Mul(RectSize(editorRect), editor.MiniMapSizeFraction), Mul(border, 2f)));
        float maxAspect = maxSize.X / maxSize.Y;
        var gridSize = IsInverted(editor.GridContentBounds) ? maxSize : Floor(RectSize(editor.GridContentBounds));
        float gridAspect = gridSize.X / gridSize.Y;
        var miniSize = Floor(gridAspect > maxAspect
            ? new float2(maxSize.X, maxSize.X / gridAspect)
            : new float2(maxSize.Y * gridAspect, maxSize.Y));
        float scaling = miniSize.X / gridSize.X;

        float2 align = editor.MiniMapLocation switch
        {
            ImNodesMiniMapLocation.BottomRight => new float2(1f, 1f),
            ImNodesMiniMapLocation.BottomLeft => new float2(0f, 1f),
            ImNodesMiniMapLocation.TopRight => new float2(1f, 0f),
            _ => new float2(0f, 0f),
        };
        var topLeft = Add(Add(editorRect.Min, offset), border);
        var bottomRight = Sub(Sub(Sub(editorRect.Max, offset), border), miniSize);
        var miniPos = Floor(Lerp(topLeft, bottomRight, align));
        editor.MiniMapRectScreenSpace = Rect(Sub(miniPos, border), Add(Add(miniPos, miniSize), border));
        editor.MiniMapContentScreenSpace = Rect(miniPos, Add(miniPos, miniSize));
        editor.MiniMapScaling = scaling;
    }

    static void MiniMapUpdate()
    {
        var g = Current;
        var editor = Editor;
        uint background = IsMiniMapHovered()
            ? g.Style.Colors[(int)ImNodesCol.MiniMapBackgroundHovered]
            : g.Style.Colors[(int)ImNodesCol.MiniMapBackground];

        var childPos = editor.MiniMapRectScreenSpace.Min;
        ImGui.SetCursorScreenPos(childPos);
        float2? childSize = RectSize(editor.MiniMapRectScreenSpace);
        ImGui.BeginChild("minimap"u8, in childSize, ImGuiChildFlags.None, ImGuiWindowFlags.NoBackground);

        var miniRect = editor.MiniMapRectScreenSpace;
        FillRect(g.CanvasDrawList, miniRect.Min, miniRect.Max, background);
        StrokeRect(g.CanvasDrawList, miniRect.Min, miniRect.Max, g.Style.Colors[(int)ImNodesCol.MiniMapOutline]);
        var clipMin = miniRect.Min;
        var clipMax = miniRect.Max;
        g.CanvasDrawList.PushClipRect(in clipMin, in clipMax, true);

        for (int linkIdx = 0; linkIdx < editor.Links.Pool.Count; linkIdx++)
        {
            if (editor.Links.InUse[linkIdx])
                MiniMapDrawLink(editor, linkIdx);
        }

        for (int nodeIdx = 0; nodeIdx < editor.Nodes.Pool.Count; nodeIdx++)
        {
            if (editor.Nodes.InUse[nodeIdx])
                MiniMapDrawNode(editor, nodeIdx);
        }

        var canvas = ScreenToMiniMap(editor, g.CanvasRectScreenSpace);
        FillRect(g.CanvasDrawList, canvas.Min, canvas.Max, g.Style.Colors[(int)ImNodesCol.MiniMapCanvas]);
        StrokeRect(g.CanvasDrawList, canvas.Min, canvas.Max, g.Style.Colors[(int)ImNodesCol.MiniMapCanvasOutline]);
        g.CanvasDrawList.PopClipRect();

        bool hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.None);
        ImGui.EndChild();

        bool center = hovered &&
                      ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
                      editor.ClickInteraction.Type == ImNodesClickInteractionType.None &&
                      g.NodeIdxSubmissionOrder.Count > 0;
        if (center)
        {
            var target = MiniMapToGrid(editor, ImGui.GetMousePos());
            var centerPoint = Mul(RectSize(g.CanvasRectScreenSpace), 0.5f);
            editor.Panning = Floor(Sub(centerPoint, target));
        }

        editor.MiniMapNodeHoveringCallback = null;
        editor.MiniMapNodeHoveringCallbackUserData = null;
    }

    static void MiniMapDrawNode(ImNodesEditorContext editor, int nodeIdx)
    {
        var g = Current;
        var node = editor.Nodes.Pool[nodeIdx]!;
        var rect = ScreenToMiniMap(editor, node.Rect);
        float rounding = MathF.Floor(node.LayoutStyle.CornerRounding * editor.MiniMapScaling);
        uint background;
        if (editor.ClickInteraction.Type == ImNodesClickInteractionType.None &&
            ImGui.IsMouseHoveringRect(rect.Min, rect.Max, true))
        {
            background = g.Style.Colors[(int)ImNodesCol.MiniMapNodeBackgroundHovered];
            editor.MiniMapNodeHoveringCallback?.Invoke(node.Id, editor.MiniMapNodeHoveringCallbackUserData);
        }
        else if (editor.SelectedNodeIndices.Contains(nodeIdx))
        {
            background = g.Style.Colors[(int)ImNodesCol.MiniMapNodeBackgroundSelected];
        }
        else
        {
            background = g.Style.Colors[(int)ImNodesCol.MiniMapNodeBackground];
        }

        FillRect(g.CanvasDrawList, rect.Min, rect.Max, background, rounding);
        StrokeRect(g.CanvasDrawList, rect.Min, rect.Max, g.Style.Colors[(int)ImNodesCol.MiniMapNodeOutline], rounding);
    }

    static void MiniMapDrawLink(ImNodesEditorContext editor, int linkIdx)
    {
        var g = Current;
        if (g.DeletedLinkIdx == linkIdx)
            return;
        var link = editor.Links.Pool[linkIdx]!;
        var start = editor.Pins.Pool[link.StartPinIdx]!;
        var end = editor.Pins.Pool[link.EndPinIdx]!;
        var curve = MakeBezier(
            ScreenToMiniMap(editor, start.Pos),
            ScreenToMiniMap(editor, end.Pos),
            start.Type,
            g.Style.LinkLineSegmentsPerLength / editor.MiniMapScaling);
        uint color = editor.SelectedLinkIndices.Contains(linkIdx)
            ? g.Style.Colors[(int)ImNodesCol.MiniMapLinkSelected]
            : g.Style.Colors[(int)ImNodesCol.MiniMapLink];
        Bezier(g.CanvasDrawList, curve, color, g.Style.LinkThickness * editor.MiniMapScaling);
    }

    static void Line(ImDrawListPtr drawList, float2 a, float2 b, uint color, float thickness = 1f) =>
        drawList.AddLine(in a, in b, (ImColor8)color, thickness);

    static void FillRect(ImDrawListPtr drawList, float2 min, float2 max, uint color, float rounding = 0f, ImDrawFlags flags = ImDrawFlags.None) =>
        drawList.AddRectFilled(in min, in max, (ImColor8)color, rounding, flags);

    static void StrokeRect(ImDrawListPtr drawList, float2 min, float2 max, uint color, float rounding = 0f, ImDrawFlags flags = ImDrawFlags.None, float thickness = 1f) =>
        drawList.AddRect(in min, in max, (ImColor8)color, rounding, flags, thickness);

    static void Circle(ImDrawListPtr drawList, float2 center, float radius, uint color, int segments, float thickness) =>
        drawList.AddCircle(in center, radius, (ImColor8)color, segments, thickness);

    static void CircleFilled(ImDrawListPtr drawList, float2 center, float radius, uint color, int segments) =>
        drawList.AddCircleFilled(in center, radius, (ImColor8)color, segments);

    static void Quad(ImDrawListPtr drawList, float2 a, float2 b, float2 c, float2 d, uint color, float thickness) =>
        drawList.AddQuad(in a, in b, in c, in d, (ImColor8)color, thickness);

    static void QuadFilled(ImDrawListPtr drawList, float2 a, float2 b, float2 c, float2 d, uint color) =>
        drawList.AddQuadFilled(in a, in b, in c, in d, (ImColor8)color);

    static void Triangle(ImDrawListPtr drawList, float2 a, float2 b, float2 c, uint color, float thickness) =>
        drawList.AddTriangle(in a, in b, in c, (ImColor8)color, thickness);

    static void TriangleFilled(ImDrawListPtr drawList, float2 a, float2 b, float2 c, uint color) =>
        drawList.AddTriangleFilled(in a, in b, in c, (ImColor8)color);

    static void Bezier(ImDrawListPtr drawList, CubicBezier curve, uint color, float thickness)
    {
        var p0 = curve.P0;
        var p1 = curve.P1;
        var p2 = curve.P2;
        var p3 = curve.P3;
        drawList.AddBezierCubic(in p0, in p1, in p2, in p3, (ImColor8)color, thickness, curve.NumSegments);
    }
}
