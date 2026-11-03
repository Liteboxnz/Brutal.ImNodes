# Brutal.ImNodes
An imnodes port for Brutal.ImGui.

**No Association with Original Author or Brutal framework**

```csharp
ImNodes.CreateContext();

ImNodes.BeginNodeEditor();

ImNodes.BeginNode(1);
ImNodes.BeginNodeTitleBar();
ImGui.Text("Node");
ImNodes.EndNodeTitleBar();
ImNodes.BeginInputAttribute(2);
ImGui.Text("In");
ImNodes.EndInputAttribute();
ImNodes.BeginOutputAttribute(3);
ImGui.Text("Out");
ImNodes.EndOutputAttribute();
ImNodes.EndNode();

ImNodes.Link(4, 3, 2);
ImNodes.MiniMap();
ImNodes.EndNodeEditor();
```

Call `ImNodes.CreateContext()` once after the ImGui context exists. Node, pin, and link ids are integers and must stay unique inside one editor. Submit the same nodes every frame; positions survive in the editor context. `int.MinValue` is reserved.

`IsLinkCreated`, `IsLinkDropped`, `IsLinkStarted`, `IsLinkDestroyed`, and the hover queries are read after `EndNodeEditor`. A detached or snapped link is only removed when the application stops submitting it. `IsLinkDestroyed` reports that id.
