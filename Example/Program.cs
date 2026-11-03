/*
 * Node editor hosted in a GLFW window with the Brutal ImGui OpenGL backend.
 * Press Delete to remove selected links. Press Escape to close.
 */

using Brutal;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Backends;
using Brutal.ImNodesAPI;
using Brutal.Numerics;
using Brutal.OpenGlApi;

if (!Glfw.Init())
{
    var errorCode = Glfw.GetError(out var errorMessage);
    throw new InvalidOperationException($"Failed to initialize GLFW: {errorCode}, {errorMessage}");
}

var imgui = ImGui.CreateContext();
var nodes = ImNodes.CreateContext();

try
{
    Run();
}
finally
{
    ImNodes.DestroyContext(nodes);
    ImGui.DestroyContext(imgui);
    Glfw.Terminate();
}

static void Run()
{
    Glfw.WindowHint(GlfwWindowHint.ContextVersionMajor, 4);
    Glfw.WindowHint(GlfwWindowHint.ContextVersionMinor, 1);
    Glfw.WindowHint(GlfwWindowHint.OpenGlProfile, Glfw.Constants.GLFW_OPENGL_CORE_PROFILE);

    if (Platform.IsMacOS)
        Glfw.WindowHint(GlfwWindowHint.OpenGlForwardCompat, 1);

    using var window = Glfw.CreateWindow(new GlfwWindow.CreateInfo
    {
        Title = "Brutal.ImNodes",
        Size = new int2(1100, 700),
    });

    window.MakeContextCurrent();
    Glfw.SwapInterval(1);

    unsafe
    {
        if (!Glad.Initialize(Glfw.PInvoke.GetProcAddress))
            throw new InvalidOperationException("Failed to load OpenGL functions with GLAD");
    }

    window.OnKey += (handle, key, _, _, _) =>
    {
        if (key == GlfwKey.Escape)
            handle.ShouldClose = true;
    };

    ImGui.GetIO().IniFilename = null;

    using var glfwBackend = new GlfwBackend(window, GlfwBackend.ClientApi.OpenGL);
    using var openGlBackend = new OpenGlBackend();

    var graph = new Graph();
    OpenGl.ClearColor(float4.Rgb(0.12f, 0.12f, 0.14f));

    while (!window.ShouldClose)
    {
        Glfw.PollEvents();

        glfwBackend.NewFrame();
        openGlBackend.NewFrame();
        ImGui.NewFrame();
        ImString.SharedStorage.Reset();

        graph.Draw();

        ImGui.Render();
        OpenGl.Clear(GlClearBit.Color);
        openGlBackend.RenderDrawData(ImGui.GetDrawData());
        window.SwapBuffers();
    }
}

sealed class Graph
{
    readonly List<Node> _nodes =
    [
        new(1, "Source", 2, 3, 0.25f),
        new(4, "Sink", 5, 6, 0.75f),
    ];

    readonly List<Link> _links = [new(7, 3, 5)];
    int _nextLinkId = 8;
    bool _placed;

    public void Draw()
    {
        ImGui.SetNextWindowSize(new float2(980, 620), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Node editor"u8))
        {
            ImGui.Text("Drag a pin to connect nodes. Delete removes the selected link."u8);
            ImNodes.BeginNodeEditor();

            if (!_placed)
            {
                ImNodes.SetNodeGridSpacePos(1, new float2(80, 120));
                ImNodes.SetNodeGridSpacePos(4, new float2(420, 180));
                _placed = true;
            }

            foreach (var node in _nodes)
                DrawNode(node);

            foreach (var link in _links)
                ImNodes.Link(link.Id, link.StartPin, link.EndPin);

            ImNodes.MiniMap(0.2f, ImNodesMiniMapLocation.BottomRight);
            ImNodes.EndNodeEditor();
            ApplyLinkChanges();
        }

        ImGui.End();
    }

    static void DrawNode(Node node)
    {
        ImNodes.BeginNode(node.Id);
        ImNodes.BeginNodeTitleBar();
        ImGui.Text(node.Title);
        ImNodes.EndNodeTitleBar();

        ImNodes.BeginInputAttribute(node.InputPin);
        ImGui.Text("In"u8);
        ImNodes.EndInputAttribute();

        ImNodes.BeginOutputAttribute(node.OutputPin);
        ImGui.Text("Out"u8);
        ImNodes.EndOutputAttribute();

        ImNodes.BeginStaticAttribute(node.Id + 100);
        ImGui.SetNextItemWidth(140f);
        ImGui.SliderFloat("Value"u8, ref node.Value, 0f, 1f);
        ImNodes.EndStaticAttribute();
        ImNodes.EndNode();
    }

    void ApplyLinkChanges()
    {
        if (ImNodes.IsLinkCreated(out int startPin, out int endPin))
            _links.Add(new Link(_nextLinkId++, startPin, endPin));

        if (ImNodes.IsLinkDestroyed(out int destroyedId))
            _links.RemoveAll(link => link.Id == destroyedId);

        if (ImGui.IsKeyPressed(ImGuiKey.Delete) && ImNodes.NumSelectedLinks() > 0)
        {
            Span<int> selected = stackalloc int[ImNodes.NumSelectedLinks()];
            ImNodes.GetSelectedLinks(selected);
            foreach (int id in selected)
                _links.RemoveAll(link => link.Id == id);
            ImNodes.ClearLinkSelection();
        }
    }

    sealed class Node(int id, string title, int inputPin, int outputPin, float value)
    {
        public int Id { get; } = id;
        public string Title { get; } = title;
        public int InputPin { get; } = inputPin;
        public int OutputPin { get; } = outputPin;
        public float Value = value;
    }

    readonly record struct Link(int Id, int StartPin, int EndPin);
}
