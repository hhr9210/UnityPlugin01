using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueNodeData
{
    public string ID;               // 节点唯一ID
    public string Speaker;          // 说话者
    [TextArea(3, 5)]
    public string DialogueText;     // 对话内容
    


    public Vector2 Position;        
    public List<DialogueChoiceData> Choices = new List<DialogueChoiceData>(); 
}

[System.Serializable]
public class DialogueChoiceData
{
    public string ChoiceText;      // 玩家选择的文本
    public string TargetNodeID;    // 目标节点的ID
}

[System.Serializable]
public class DialogueData
{
    public string DialogueName; // 对话组名称 (例如：NPC_QuestStart)
    public List<DialogueNodeData> Nodes = new List<DialogueNodeData>(); // 所有节点数据
}