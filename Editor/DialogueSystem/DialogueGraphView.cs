using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

public class DialogueGraphView : GraphView
{
    private DialogueEditorWindow editorWindow;
    private DialogueData currentDialogueData;
    private Dictionary<string, DialogueGraphNode> nodesById = new Dictionary<string, DialogueGraphNode>();
    private readonly Vector2 defaultNodeSize = new Vector2(300, 250);
    private const string STYLE_SHEET_PATH = "Assets/Editor/DialogueSystem/DialogueEditorStyle.uss";



    //初始化画布 加载uss    订阅事件 设置平移缩放操作
    public DialogueGraphView(DialogueEditorWindow window)
    {
        editorWindow = window;

        // StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLE_SHEET_PATH);
        // if (styleSheet != null)
        // {
        //     styleSheets.Add(styleSheet);
        // }
        // else
        // {
        //     Debug.LogWarning($"DialogueEditorStyle.uss not found at {STYLE_SHEET_PATH}. Using default styles.");
        // }

        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());//拖拽画布
        this.AddManipulator(new SelectionDragger());//选择节点
        this.AddManipulator(new RectangleSelector());//矩形多选
        this.AddManipulator(new FreehandSelector());

        GridBackground grid = new GridBackground(); //特殊visualelement子类，一般背景
        Insert(0, grid);//最底层
        grid.StretchToParentSize();

        graphViewChanged += OnGraphViewChanged;
    }


    //数据转换UI
    public void PopulateView(DialogueData dialogueData)
    {
        currentDialogueData = dialogueData;
        
        ClearGraph();
        nodesById.Clear();

        // 1. 创建所有节点
        foreach (var nodeData in currentDialogueData.Nodes)
        {
            CreateNode(nodeData);
        }

        // 2. 连接所有节点
        foreach (var nodeData in currentDialogueData.Nodes)
        {
            if (nodesById.TryGetValue(nodeData.ID, out DialogueGraphNode parentNode))
            {
                for (int i = 0; i < nodeData.Choices.Count; i++)
                {
                    var choice = nodeData.Choices[i];
                    if (!string.IsNullOrEmpty(choice.TargetNodeID) && 
                        nodesById.TryGetValue(choice.TargetNodeID, out DialogueGraphNode childNode))
                    {
                        Port choicePort = parentNode.GetChoicePort(i);
                        if (choicePort != null)
                        {
                            Edge edge = choicePort.ConnectTo(childNode.InputPort);
                            AddElement(edge);
                        }
                    }
                }
            }
        }
    }
    
    private void ClearGraph()
    {
        DeleteElements(graphElements.ToList());
    }



    //加载现有节点
    private void CreateNode(DialogueNodeData nodeData)
    {
        DialogueGraphNode node = new DialogueGraphNode(nodeData, editorWindow);
        AddElement(node);
        nodesById[node.NodeData.ID] = node;
    }

    public DialogueGraphNode CreateNewDialogueNode(Vector2 position)
    {
        DialogueNodeData newNodeData = new DialogueNodeData
        {
            ID = Guid.NewGuid().ToString(),
            Speaker = "New Speaker",
            DialogueText = "Enter dialogue text here.",
            Position = position,
            Choices = new List<DialogueChoiceData>()
        };
        currentDialogueData.Nodes.Add(newNodeData);
        
        DialogueGraphNode newNode = new DialogueGraphNode(newNodeData, editorWindow);
        AddElement(newNode);
        nodesById[newNode.NodeData.ID] = newNode;

        return newNode;
    }

    // ====================================================================
    // GraphView 事件处理节点连接，增加
    // ====================================================================

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return ports.ToList().Where(endPort => 
            endPort.direction != startPort.direction &&
            endPort.node != startPort.node
        ).ToList();
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
    {
        // 处理连接删除
        if (graphViewChange.elementsToRemove != null)
        {
            foreach (GraphElement element in graphViewChange.elementsToRemove)
            {
                if (element is Edge edge)
                {
                    HandleEdgeRemoval(edge);
                }
                else if (element is DialogueGraphNode nodeToRemove)
                {
                    HandleNodeRemoval(nodeToRemove);
                }
            }
        }

        // 处理连接增加
        if (graphViewChange.edgesToCreate != null)
        {
            foreach (Edge edge in graphViewChange.edgesToCreate)
            {
                HandleEdgeCreation(edge);
            }
        }
        
        editorWindow.MarkDataDirty();
        return graphViewChange;
    }

    private void HandleEdgeRemoval(Edge edge)
    {
        DialogueGraphNode outputNode = edge.output.node as DialogueGraphNode;
        DialogueGraphNode inputNode = edge.input.node as DialogueGraphNode;

        if (outputNode != null && inputNode != null)
        {
            // 找到是哪个选择端口被断开连接
            int choiceIndex = outputNode.ChoicePorts.IndexOf(edge.output as Port);
            if (choiceIndex >= 0 && choiceIndex < outputNode.NodeData.Choices.Count)
            {
                // 清空该选择的目标节点ID
                outputNode.NodeData.Choices[choiceIndex].TargetNodeID = "";
            }
        }
    }

    private void HandleEdgeCreation(Edge edge)
    {
        DialogueGraphNode outputNode = edge.output.node as DialogueGraphNode;
        DialogueGraphNode inputNode = edge.input.node as DialogueGraphNode;

        if (outputNode != null && inputNode != null)
        {
            // 找到是哪个选择端口被连接
            int choiceIndex = outputNode.ChoicePorts.IndexOf(edge.output as Port);
            if (choiceIndex >= 0 && choiceIndex < outputNode.NodeData.Choices.Count)
            {
                // 更新该选择的目标节点ID
                outputNode.NodeData.Choices[choiceIndex].TargetNodeID = inputNode.NodeData.ID;
            }
        }
    }

    private void HandleNodeRemoval(DialogueGraphNode nodeToRemove)
    {
        currentDialogueData.Nodes.Remove(nodeToRemove.NodeData);
        nodesById.Remove(nodeToRemove.NodeData.ID);
        
        // 遍历所有其他节点，移除所有指向被删除节点的连接
        foreach(var node in currentDialogueData.Nodes)
        {
            foreach (var choice in node.Choices)
            {
                if (choice.TargetNodeID == nodeToRemove.NodeData.ID)
                {
                    choice.TargetNodeID = "";
                }
            }
        }
    }

    // ====================================================================
    // 上下文菜单
    // ====================================================================

    //右键菜单项定义
    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Create Node", (action) =>
        {
            Vector2 viewCenter = GetViewCenter();
            CreateNewDialogueNode(viewCenter);
            editorWindow.MarkDataDirty();
        }, DropdownMenuAction.Status.Normal);

        base.BuildContextualMenu(evt);
    }

    
    //计算画布中心
    private Vector2 GetViewCenter()
    {
        var contentTransform = contentViewContainer.transform;
        Vector3 contentPosition = contentTransform.position;
        Vector3 contentScale = contentTransform.scale;
        
        Vector2 graphViewSize = this.layout.size;
        
        Vector2 viewCenter = new Vector2(
            graphViewSize.x * 0.5f,
            graphViewSize.y * 0.5f
        );
        
        Vector2 contentLocalCenter = contentViewContainer.WorldToLocal(viewCenter);
        
        float scale = contentScale.x;
        Vector2 nodePosition = new Vector2(
            contentLocalCenter.x - (defaultNodeSize.x * 0.5f) / scale,
            contentLocalCenter.y - (defaultNodeSize.y * 0.5f) / scale
        );
        
        return nodePosition;
    }
}