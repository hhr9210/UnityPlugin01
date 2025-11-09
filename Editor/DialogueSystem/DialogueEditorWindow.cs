using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UIElements; 
using System.Linq; 
using System.Text; 







//主界面
public class DialogueEditorWindow : EditorWindow
{
    // --- 配置 ---
    private const string RESOURCES_FOLDER = "Dialogues/";
    private const string FULL_RESOURCES_PATH = "Assets/Resources/" + RESOURCES_FOLDER;

    // --- 核心数据 ---
    private DialogueData currentDialogue;
    private string dialogueName = "NewDialogue";
    private bool isDirty = false;

    // --- GraphView 相关 ---
    private DialogueGraphView graphView;
    private VisualElement graphViewContainer;
    private Label dirtyLabel;

    // --- 列表相关 ---
    private ListView dialogueListView;
    private List<string> dialogueFileNames;

    [MenuItem("Tool/Dialogue Editor")]
    public static void ShowWindow()
    {
        DialogueEditorWindow window = GetWindow<DialogueEditorWindow>("Dialogue Editor");
        window.minSize = new Vector2(800, 600);
    }

    private void OnEnable()
    {
        if (rootVisualElement != null)
        {
            rootVisualElement.Clear();
        }

        EnsureResourcesDirectoryExists();

        // 1. 设置根元素为水平布局 (左侧列表 | 右侧工具栏+图表)
        rootVisualElement.style.flexDirection = FlexDirection.Row;

        // 2. 创建左侧列表面板 (左侧部分)
        VisualElement leftPanel = CreateLeftPanel();
        rootVisualElement.Add(leftPanel);

        // 3. 创建右侧垂直容器 (右侧部分)
        VisualElement rightColumnContainer = new VisualElement
        {
            style = {
                flexDirection = FlexDirection.Column, // 垂直堆叠：工具栏在顶部，图表在底部
                flexGrow = 1
            }
        };

        // 4. 创建顶部工具栏 (右侧的顶部)
        VisualElement toolbar = CreateToolbar();
        rightColumnContainer.Add(toolbar);

        // 5. 创建并添加 GraphView (右侧的底部)
        graphView = new DialogueGraphView(this);
        graphView.StretchToParentSize();

        graphViewContainer = new VisualElement
        {
            style = { flexGrow = 1 } // 关键：让图表独占右侧容器的剩余垂直空间
        };
        graphViewContainer.Add(graphView);
        rightColumnContainer.Add(graphViewContainer);

        rootVisualElement.Add(rightColumnContainer);

        NewDialogue();
        RefreshDialogueList();
    }

    private void OnDisable()
    {
        if (graphView != null)
        {
            graphViewContainer.Remove(graphView);
            graphView = null;
        }
    }

    /// <summary>
    /// 创建左侧面板，包含对话文件列表 
    /// </summary>

    private VisualElement CreateLeftPanel()
    {
        // 使用 Editor默认的深色背景
        VisualElement leftPanel = new VisualElement
        {
            style = {
                width = 300,
                backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f)),
                paddingTop = 15,
                paddingBottom = 10,
                paddingLeft = 5,
                paddingRight = 5,
                flexShrink = 0,
                flexDirection = FlexDirection.Column
            }
        };

        // --- 1. 标题区域 ---
        VisualElement titleContainer = new VisualElement
        {
            style = {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                paddingLeft = 10,
                marginBottom = 15,
                borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f)),
                borderBottomWidth = 2,
                height = 50
            }
        };

        // 标题文本
        Label title = new Label("DIALOGUE ASSETS")
        {
            style = {
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 20, 
                // 关键：移除蓝色强调色，使用默认颜色 (通常是白色或浅灰)
                color = StyleKeyword.Initial,
                flexGrow = 1
            }
        };
        titleContainer.Add(title);
        leftPanel.Add(titleContainer);

        // --- 2. 文件列表 (ListView) ---
        dialogueListView = new ListView
        {
            selectionType = SelectionType.Single,
            fixedItemHeight = 40,

            // makeItem: 创建列表项的 VisualElement
            makeItem = () =>
            {
                VisualElement itemContainer = new VisualElement
                {
                    style = {
                        flexDirection = FlexDirection.Column,
                        alignSelf = Align.Stretch,
                    }
                };

                VisualElement contentWrapper = new VisualElement
                {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        height = 38,
                        paddingLeft = 8,
                        paddingRight = 8,
                    }
                };

                // 关键：移除图标 Label
                // Label icon = new Label("📄") { style = { fontSize = 16, marginRight = 8 } };
                // contentWrapper.Add(icon);

                // 对话名称 Label
                Label label = new Label
                {
                    name = "dialogue-name-label",
                    style = {
                        fontSize = 16,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        flexGrow = 1,
                    }
                };
                contentWrapper.Add(label);

                itemContainer.Add(contentWrapper);

                // 分割线
                VisualElement separator = new VisualElement
                {
                    name = "separator",
                    style = {
                        backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 1f)),
                        height = 1,
                        alignSelf = Align.Stretch,
                    }
                };
                itemContainer.Add(separator);

                return itemContainer;
            },

            // bindItem: 绑定数据 
            bindItem = (element, i) =>
            {
                Label label = element.Q<Label>("dialogue-name-label");
                if (label != null)
                {
                    label.text = dialogueFileNames[i];
                }

                // 隐藏最后一个项目的分割线
                VisualElement separator = element.Q<VisualElement>("separator");
                if (separator != null)
                {
                    if (i == dialogueFileNames.Count - 1)
                    {
                        separator.style.display = DisplayStyle.None;
                    }
                    else
                    {
                        separator.style.display = DisplayStyle.Flex;
                    }
                }
            },
            itemsSource = dialogueFileNames
        };

        dialogueListView.style.paddingLeft = 0;
        dialogueListView.style.paddingRight = 0;
        dialogueListView.style.flexGrow = 1;
        leftPanel.Add(dialogueListView);

        // 注册选择变更回调 
        dialogueListView.onSelectionChange += (selection) =>
        {
            string selectedName = selection.FirstOrDefault() as string;
            if (!string.IsNullOrEmpty(selectedName))
            {
                dialogueName = selectedName;
                LoadDialogueFromFile(selectedName);
            }
        };

        // 刷新按钮区域已删除

        return leftPanel;
    }

    /// <summary>
    /// 创建工具栏 (右侧的上半部分) - 调整 Name Field 宽度匹配 List Panel
    /// </summary>
    private VisualElement CreateToolbar()
    {
        VisualElement toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.alignItems = Align.Center;

        toolbar.style.paddingLeft = 5;
        toolbar.style.paddingRight = 5;
        toolbar.style.paddingTop = 5;
        toolbar.style.paddingBottom = 5;
        toolbar.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        toolbar.style.flexShrink = 0;
        toolbar.style.flexGrow = 0;

        // Dialogue Name TextField - 宽度匹配左侧面板的宽度 (300px)
        TextField dialogueNameField = new TextField("Dialogue Name:")
        {
            value = dialogueName,
            style = {
                flexGrow = 0,
                width = 300, // 保持与左侧面板宽度一致
                height = 40,
                fontSize = 16,
                unityTextAlign = TextAnchor.MiddleLeft
            }
        };

        dialogueNameField.labelElement.style.fontSize = 16;
        dialogueNameField.labelElement.style.unityTextAlign = TextAnchor.MiddleLeft;

        dialogueNameField.RegisterValueChangedCallback(evt =>
        {
            dialogueName = evt.newValue;
            MarkDataDirty();
        });
        toolbar.Add(dialogueNameField);

        // Space - 用于将按钮推到右侧边缘
        toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

        // Buttons
        Button newBtn = new Button(NewDialogue)
        {
            text = "New",
            style = { width = 80, height = 40, fontSize = 16 }
        };
        Button saveBtn = new Button(SaveDialogue)
        {
            text = "Save",
            style = { width = 80, height = 40, fontSize = 16 }
        };
        Button deleteBtn = new Button(DeleteDialogue)
        {
            text = "Delete",
            style = { width = 80, height = 40, fontSize = 16 }
        };

        toolbar.Add(newBtn);
        toolbar.Add(saveBtn);
        toolbar.Add(deleteBtn);

        // Dirty Status Label
        dirtyLabel = new Label(isDirty ? "*" : "");
        dirtyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        dirtyLabel.style.color = Color.red;
        dirtyLabel.style.marginLeft = 5;
        dirtyLabel.style.marginRight = 5;
        dirtyLabel.style.fontSize = 20;
        dirtyLabel.style.alignSelf = Align.Center;
        toolbar.Add(dirtyLabel);

        return toolbar;
    }

    // --- 省略其余逻辑，它们保持不变 ---

    private void RefreshDialogueList()
    {
        EnsureResourcesDirectoryExists();

        string[] files = Directory.GetFiles(FULL_RESOURCES_PATH, "*.json");

        dialogueFileNames = files
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name)
            .ToList();

        if (dialogueListView != null)
        {
            dialogueListView.itemsSource = dialogueFileNames;
            dialogueListView.Rebuild();
        }
    }

    public void MarkDataDirty()
    {
        if (!isDirty)
        {
            isDirty = true;
            titleContent.text = GetWindow<DialogueEditorWindow>("Dialogue Editor").titleContent.text + "*";
            if (dirtyLabel != null) dirtyLabel.text = "*";
        }
    }

    private void ClearDirtyFlag()
    {
        isDirty = false;
        titleContent.text = titleContent.text.Replace("*", "");
        if (dirtyLabel != null) dirtyLabel.text = "";
    }

    private void EnsureResourcesDirectoryExists()
    {
        if (!AssetDatabase.IsValidFolder(FULL_RESOURCES_PATH.TrimEnd('/')))
        {
            Directory.CreateDirectory(FULL_RESOURCES_PATH);
            AssetDatabase.Refresh();
        }
    }

    private void UpdateToolbarNameField(string name)
    {
        VisualElement rightColumnContainer = rootVisualElement.Children().ElementAtOrDefault(1);
        if (rightColumnContainer != null)
        {
            VisualElement toolbar = rightColumnContainer.Children().FirstOrDefault();
            if (toolbar != null)
            {
                TextField nameField = toolbar.Q<TextField>();
                if (nameField != null) nameField.value = name;
            }
        }
    }

    private void NewDialogue()
    {
        if (isDirty && !EditorUtility.DisplayDialog("Unsaved Changes", "You have unsaved changes. Do you want to continue and discard them?", "Yes", "No"))
        {
            return;
        }

        dialogueName = "NewDialogue" + Random.Range(100, 999);
        currentDialogue = new DialogueData { DialogueName = dialogueName };

        if (graphView != null) graphView.PopulateView(currentDialogue);

        UpdateToolbarNameField(dialogueName);
        ClearDirtyFlag();
        if (dialogueListView != null) dialogueListView.ClearSelection();
    }

    private void SaveDialogue()
    {
        if (string.IsNullOrEmpty(dialogueName))
        {
            EditorUtility.DisplayDialog("Error", "Dialogue name cannot be empty!", "OK");
            return;
        }

        currentDialogue.DialogueName = dialogueName;
        string json = JsonUtility.ToJson(currentDialogue, true);

        string fullPath = FULL_RESOURCES_PATH + dialogueName + ".json";

        try
        {
            File.WriteAllText(fullPath, json, Encoding.UTF8);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Dialogue saved to:\n{fullPath}", "OK");
            ClearDirtyFlag();
            RefreshDialogueList();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save dialogue: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to save dialogue: {e.Message}", "OK");
        }
    }

    // 列表点击加载的实现
    private void LoadDialogueFromFile(string fileName)
    {
        bool proceed = true;
        if (isDirty)
        {
            // 弹出提示框
            proceed = EditorUtility.DisplayDialog("Unsaved Changes", "You have unsaved changes. Do you want to continue and discard them?", "Yes", "No");
        }

        if (!proceed)
        {
            // 如果用户选择 No，取消加载，并重新选择之前的项
            if (dialogueListView != null)
            {
                // 尝试重新选择之前的对话名称，防止列表选择与当前名称不一致
                if (dialogueListView.itemsSource.Contains(dialogueName))
                {
                    dialogueListView.selectedIndex = dialogueListView.itemsSource.IndexOf(dialogueName);
                }
                else
                {
                    dialogueListView.ClearSelection();
                }
            }
            return;
        }

        // --- 核心修改点：在加载前清除脏数据标记 ---
        // 这样做是为了防止在加载/PopulateView过程中，GraphView的内部机制（如位置调整）
        // 意外触发MarkDataDirty。我们相信用户已经确认要丢弃旧数据。
        ClearDirtyFlag();

        // 使用 Resources.Load 从 Assets/Resources 路径加载 TextAsset
        TextAsset jsonFile = Resources.Load<TextAsset>(RESOURCES_FOLDER + fileName);

        if (jsonFile == null)
        {
            EditorUtility.DisplayDialog("Error", $"Could not load **{fileName}** as TextAsset from Resources.\nPlease ensure it's directly in Assets/Resources/{RESOURCES_FOLDER}.", "OK");
            // 加载失败后，再次清除标志以防万一
            ClearDirtyFlag();
            return;
        }

        try
        {
            currentDialogue = JsonUtility.FromJson<DialogueData>(jsonFile.text);

            // 更新当前编辑器状态
            dialogueName = currentDialogue.DialogueName; // 使用文件内的名称确保一致

            // 刷新工具栏的对话名称显示
            UpdateToolbarNameField(dialogueName);

            if (graphView != null) graphView.PopulateView(currentDialogue); // 更新GraphView

            // --- 关键：加载完成后再次清除标志 ---
            // 确保 PopulateView 过程中可能引起的意外Dirty状态被重置。
            ClearDirtyFlag();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load dialogue: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to deserialize JSON: {e.Message}", "OK");
            ClearDirtyFlag(); // 失败后也清除标志
        }
    }
    private void LoadDialogue()
    {
        string selectedPath = EditorUtility.OpenFilePanel("Load Dialogue JSON", FULL_RESOURCES_PATH, "json");

        if (string.IsNullOrEmpty(selectedPath))
        {
            return;
        }

        string fileName = Path.GetFileNameWithoutExtension(selectedPath);

        LoadDialogueFromFile(fileName);

        if (dialogueListView != null && dialogueFileNames.Contains(fileName))
        {
            dialogueListView.selectedIndex = dialogueFileNames.IndexOf(fileName);
        }

    }


    private void DeleteDialogue()
    {
        if (string.IsNullOrEmpty(dialogueName))
        {
            EditorUtility.DisplayDialog("Error", "Dialogue name cannot be empty!", "OK");
            return;
        }

        string fullAssetPath = FULL_RESOURCES_PATH + dialogueName + ".json";

        if (File.Exists(fullAssetPath))
        {
            if (EditorUtility.DisplayDialog("Confirm Delete",
                                            $"Are you sure you want to delete **{dialogueName}.json** permanently?\nThis action cannot be undone.",
                                            "Delete", "Cancel"))
            {
                AssetDatabase.DeleteAsset(fullAssetPath);
                AssetDatabase.Refresh();

                NewDialogue();
                EditorUtility.DisplayDialog("Success", $"Dialogue **{dialogueName}.json** deleted.", "OK");

                RefreshDialogueList();
            }
        }
        else
        {
            EditorUtility.DisplayDialog("Error", $"Dialogue file **{dialogueName}.json** not found to delete.", "OK");
        }
    }
}