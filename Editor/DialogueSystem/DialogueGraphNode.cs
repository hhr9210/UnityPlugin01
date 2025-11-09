using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class DialogueGraphNode : Node
{
    public DialogueNodeData NodeData { get; private set; }
    private DialogueEditorWindow editorWindow;

    public Port InputPort { get; private set; }
    public List<Port> ChoicePorts { get; private set; } = new List<Port>(); // 每个选择对应的输出端口

    private TextField speakerField;
    private TextField dialogueTextField;
    private VisualElement choicesContainer;

    public DialogueGraphNode(DialogueNodeData data, DialogueEditorWindow window)
    {
        NodeData = data;
        editorWindow = window;
        
        title = "Dialogue Node"; 
        SetPosition(new Rect(NodeData.Position, new Vector2(300, 250))); // 稍微增加默认尺寸以容纳更多端口
        
        RegisterCallback<MouseUpEvent>(OnNodeMoved);

        CreateInputPort();
        DrawContent();
    }

    private void OnNodeMoved(MouseUpEvent evt)
    {
        NodeData.Position = GetPosition().position;
        editorWindow.MarkDataDirty();
    }





    private void CreateInputPort()  //端口创建
    {
        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "Input";
        inputContainer.Add(InputPort);
    }

    private void CreateChoicePort(int choiceIndex)   
    {
        // 为特定选择创建输出端口
        Port choicePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        choicePort.portName = $"Choice {choiceIndex + 1}";
        outputContainer.Add(choicePort);
        ChoicePorts.Add(choicePort);
    }

    private void RemoveChoicePort(int choiceIndex)
    {
        if (choiceIndex < ChoicePorts.Count)
        {
            Port portToRemove = ChoicePorts[choiceIndex];
            outputContainer.Remove(portToRemove);
            ChoicePorts.RemoveAt(choiceIndex);
            
            // 更新剩余端口的名称
            for (int i = choiceIndex; i < ChoicePorts.Count; i++)
            {
                ChoicePorts[i].portName = $"Choice {i + 1}";
            }
        }
    }

    private void DrawContent()
    {
        // --- 说话者字段 ---
        speakerField = new TextField("Speaker:") {
            value = NodeData.Speaker
        };
        speakerField.RegisterValueChangedCallback(evt => {
            NodeData.Speaker = evt.newValue;
            editorWindow.MarkDataDirty();
        });
        contentContainer.Add(speakerField);

        // --- 对话文本区域 ---
        dialogueTextField = new TextField("Dialogue Text:") {
            value = NodeData.DialogueText,
            multiline = true
        };
        dialogueTextField.style.minHeight = 60;
        dialogueTextField.RegisterValueChangedCallback(evt => {
            NodeData.DialogueText = evt.newValue;
            editorWindow.MarkDataDirty();
        });
        contentContainer.Add(dialogueTextField);
        
        // --- 分隔线 ---
        contentContainer.Add(new VisualElement { style = { height = 1, backgroundColor = new Color(0.3f, 0.3f, 0.3f) } });

        // --- 选择容器 ---
        choicesContainer = new VisualElement();
        choicesContainer.style.flexDirection = FlexDirection.Column;
        contentContainer.Add(choicesContainer);
        
        RefreshChoicesUI();

        // --- 添加选择按钮 ---
        Button addChoiceButton = new Button(() => AddNewChoice());
        addChoiceButton.text = "Add Choice";
        contentContainer.Add(addChoiceButton);
    }

    private void AddNewChoice()
    {
        DialogueChoiceData newChoice = new DialogueChoiceData {
            ChoiceText = "New Choice",
            TargetNodeID = "" // 初始为空
        };
        NodeData.Choices.Add(newChoice);
        
        // 为新选择创建端口
        CreateChoicePort(NodeData.Choices.Count - 1);
        
        RefreshChoicesUI();
        editorWindow.MarkDataDirty();
    }

    public void RefreshChoicesUI()
    {
        choicesContainer.Clear();
        
        // 确保端口数量与选择数量匹配
        while (ChoicePorts.Count > NodeData.Choices.Count)
        {
            RemoveChoicePort(ChoicePorts.Count - 1);
        }
        
        while (ChoicePorts.Count < NodeData.Choices.Count)
        {
            CreateChoicePort(ChoicePorts.Count);
        }

        for (int i = 0; i < NodeData.Choices.Count; i++)
        {
            DialogueChoiceData choice = NodeData.Choices[i];
            
            VisualElement choiceRow = new VisualElement();
            choiceRow.style.flexDirection = FlexDirection.Row;
            choiceRow.style.alignItems = Align.Center;
            choiceRow.style.marginTop = 2;

            TextField choiceTextField = new TextField() {
                value = choice.ChoiceText,
                style = { flexGrow = 1 }
            };
            choiceTextField.RegisterValueChangedCallback(evt => {
                choice.ChoiceText = evt.newValue;
                editorWindow.MarkDataDirty();
            });
            choiceRow.Add(choiceTextField);

            // 删除选择按钮
            Button removeChoiceButton = new Button(() => RemoveChoice(choice));
            removeChoiceButton.text = "X";
            removeChoiceButton.style.width = 20;
            removeChoiceButton.style.marginRight = 2;
            choiceRow.Add(removeChoiceButton);

            choicesContainer.Add(choiceRow);
        }
    }

    private void RemoveChoice(DialogueChoiceData choiceToRemove)
    {
        int index = NodeData.Choices.IndexOf(choiceToRemove);
        NodeData.Choices.Remove(choiceToRemove);
        
        // 移除对应的端口
        if (index >= 0 && index < ChoicePorts.Count)
        {
            RemoveChoicePort(index);
        }
        
        RefreshChoicesUI();
        editorWindow.MarkDataDirty();
    }

    // 获取特定选择的输出端口
    public Port GetChoicePort(int choiceIndex)
    {
        if (choiceIndex >= 0 && choiceIndex < ChoicePorts.Count)
        {
            return ChoicePorts[choiceIndex];
        }
        return null;
    }
}