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
