using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;

public class ItemEditorWindow : EditorWindow
{
    private ListView inventoryList;
    private ListView itemList;
    private Inventory[] allInventories;
    private Item[] allItems;
    private IMGUIContainer itemDetails;
    private IMGUIContainer itemPreviewer;
    private IMGUIContainer inventoryDetails;

    private Vector3 previewCameraRotation = new Vector3(45, 0, 0);
    private float previewCameraDistance = 3.0f;
    private Vector3 previewPosition = Vector3.zero;

    private const string INVENTORY_PATH = "Assets/Resources/Inventory/";
    private const string ITEM_PATH = "Assets/Resources/Item/";

    private static readonly Color DeepDarkGray = new Color(0.15f, 0.15f, 0.15f);
    private static readonly Color DarkGray = new Color(0.2f, 0.2f, 0.2f);
    private static readonly Color MediumDarkGray = new Color(0.25f, 0.25f, 0.25f);

    private Camera previewCamera;
    private RenderTexture previewRenderTexture;
    private bool isRenderingPreview = false;
    private bool isDragging = false;

    private GameObject currentPreviewInstance;
    private Light previewLight;

    private enum ViewMode { Inventory, Item }
    private ViewMode currentViewMode = ViewMode.Inventory;

    [MenuItem("Tool/Item Editor")]
    public static void ShowWindow()
    {
        ItemEditorWindow wnd = GetWindow<ItemEditorWindow>();
        wnd.titleContent = new GUIContent("Item & Inventory Editor");
        wnd.minSize = new Vector2(1000, 600);
    }

    public void CreateGUI()
    {
        LoadAllData();

        VisualElement root = rootVisualElement;
        root.style.backgroundColor = DeepDarkGray;

        // 主分割视图：左侧列表 + 右侧内容
        var mainSplitView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);
        root.Add(mainSplitView);

        // --- 左侧容器 (列表) ---
        var leftContainer = new VisualElement
        {
            style = {
                width = 300,
                backgroundColor = MediumDarkGray
            }
        };
        mainSplitView.Add(leftContainer);

        // 模式切换按钮
        var modeContainer = new VisualElement
        {
            style = {
                flexDirection = FlexDirection.Row,
                height = 30,
                marginBottom = 10
            }
        };
        leftContainer.Add(modeContainer);

        var inventoryModeBtn = new Button(() => SwitchViewMode(ViewMode.Inventory)) { text = "Inventory" };
        var itemModeBtn = new Button(() => SwitchViewMode(ViewMode.Item)) { text = "Items" };

        ApplyModeButtonStyle(inventoryModeBtn, currentViewMode == ViewMode.Inventory);
        ApplyModeButtonStyle(itemModeBtn, currentViewMode == ViewMode.Item);

        inventoryModeBtn.style.flexGrow = 1;
        itemModeBtn.style.flexGrow = 1;

        modeContainer.Add(inventoryModeBtn);
        modeContainer.Add(new VisualElement() { style = { width = 2 } });
        modeContainer.Add(itemModeBtn);

        // 列表容器
        var listContainer = new VisualElement { style = { flexGrow = 1 } };
        leftContainer.Add(listContainer);

        // Inventory 列表
        inventoryList = new ListView(allInventories)
        {
            fixedItemHeight = 35,
            makeItem = () => CreateListItem(),
            bindItem = (element, index) => BindListItem(element, allInventories[index].inventoryName),
            selectionType = SelectionType.Single,
            style = { flexGrow = 1, display = currentViewMode == ViewMode.Inventory ? DisplayStyle.Flex : DisplayStyle.None }
        };
        listContainer.Add(inventoryList);

        // Item 列表
        itemList = new ListView(allItems)
        {
            fixedItemHeight = 35,
            makeItem = () => CreateListItem(),
            bindItem = (element, index) => BindListItem(element, allItems[index].name),
            selectionType = SelectionType.Single,
            style = { flexGrow = 1, display = currentViewMode == ViewMode.Item ? DisplayStyle.Flex : DisplayStyle.None }
        };
        listContainer.Add(itemList);

        // --- 右侧内容分隔视图 ---
        var contentSplitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal)
        {
            style = { flexGrow = 1 }
        };
        mainSplitView.Add(contentSplitView);

        // --- 详情容器 ---
        var detailsContainer = new VisualElement
        {
            style = {
                width = Length.Percent(40),
                backgroundColor = MediumDarkGray
            }
        };
        contentSplitView.Add(detailsContainer);

        // Inventory 详情
        inventoryDetails = new IMGUIContainer(DrawInventoryDetails)
        {
            style = { flexGrow = 1, display = currentViewMode == ViewMode.Inventory ? DisplayStyle.Flex : DisplayStyle.None }
        };
        detailsContainer.Add(inventoryDetails);

        // Item 详情
        itemDetails = new IMGUIContainer(DrawItemDetails)
        {
            style = {
                flexGrow = 1,
                backgroundColor = MediumDarkGray,
                display = currentViewMode == ViewMode.Item ? DisplayStyle.Flex : DisplayStyle.None
            }
        };
        detailsContainer.Add(itemDetails);

        // --- 预览容器 ---
        itemPreviewer = new IMGUIContainer(DrawItemPreviewer)
        {
            style = {
                width = Length.Percent(60),
                backgroundColor = DarkGray,
                display = currentViewMode == ViewMode.Item ? DisplayStyle.Flex : DisplayStyle.None
            }
        };
        contentSplitView.Add(itemPreviewer);

        // --- 按钮容器 ---
        var buttonContainer = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                height = 30,
                borderTopColor = new Color(0.35f, 0.35f, 0.35f),
                borderTopWidth = 1,
                paddingTop = 2,
                paddingBottom = 2,
                paddingLeft = 5,
                paddingRight = 5
            }
        };
        leftContainer.Add(buttonContainer);

        var addButton = new Button(AddItemOrInventory) { text = " Add" };
        var deleteButton = new Button(DeleteItemOrInventory) { text = " Delete" };
        var saveButton = new Button(SaveAllChanges) { text = " Save" };
        var exportButton = new Button(ExportToJson) { text = " Export" };

        ApplyButtonStyle(addButton, Color.green * 0.7f);
        ApplyButtonStyle(deleteButton, Color.red * 0.7f);
        ApplyButtonStyle(saveButton, Color.blue * 0.7f);
        ApplyButtonStyle(exportButton, Color.yellow * 0.7f);

        addButton.style.flexGrow = 1;
        deleteButton.style.flexGrow = 1;
        saveButton.style.flexGrow = 1;
        exportButton.style.flexGrow = 1;

        buttonContainer.Add(addButton);
        buttonContainer.Add(new VisualElement() { style = { width = 2 } });
        buttonContainer.Add(deleteButton);
        buttonContainer.Add(new VisualElement() { style = { width = 2 } });
        buttonContainer.Add(saveButton);
        buttonContainer.Add(new VisualElement() { style = { width = 2 } });
        buttonContainer.Add(exportButton);

        // 事件处理
        inventoryList.selectionChanged += (selectedItems) =>
        {
            var selectedInventory = inventoryList.selectedItem as Inventory;
            deleteButton.SetEnabled(selectedInventory != null);
            inventoryDetails.MarkDirtyRepaint();
        };

        itemList.selectionChanged += (selectedItems) =>
        {
            var selectedItem = itemList.selectedItem as Item;
            deleteButton.SetEnabled(selectedItem != null);

            if (selectedItem != null)
            {
                ResetCameraToDefault();
                SetupPreviewInstance(selectedItem);
            }
            else
            {
                SetupPreviewInstance(null);
                ResetCameraToDefault();
            }

            itemDetails.MarkDirtyRepaint();
            itemPreviewer.MarkDirtyRepaint();
        };

        deleteButton.SetEnabled(false);

        // 初始选择第一个物品
        if (allItems.Length > 0 && currentViewMode == ViewMode.Item)
        {
            itemList.SetSelection(0);
        }
        else if (allInventories.Length > 0 && currentViewMode == ViewMode.Inventory)
        {
            inventoryList.SetSelection(0);
        }
    }

    private VisualElement CreateListItem()
    {
        var label = new Label();
        label.style.borderBottomWidth = 1;
        label.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f);
        label.style.paddingLeft = 10;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.color = Color.white;
        return label;
    }

    private void BindListItem(VisualElement element, string text)
    {
        (element as Label).text = text;
    }

    private void ApplyModeButtonStyle(Button button, bool isActive)
    {
        button.style.backgroundColor = isActive ? new Color(0.3f, 0.5f, 0.8f) : new Color(0.3f, 0.3f, 0.3f);
        button.style.color = Color.white;
        button.style.borderTopLeftRadius = 3;
        button.style.borderTopRightRadius = 3;
        button.style.borderBottomLeftRadius = 3;
        button.style.borderBottomRightRadius = 3;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
    }

    private void SwitchViewMode(ViewMode newMode)
    {
        currentViewMode = newMode;

        // 更新列表显示
        inventoryList.style.display = newMode == ViewMode.Inventory ? DisplayStyle.Flex : DisplayStyle.None;
        itemList.style.display = newMode == ViewMode.Item ? DisplayStyle.Flex : DisplayStyle.None;

        // 更新详情显示
        inventoryDetails.style.display = newMode == ViewMode.Inventory ? DisplayStyle.Flex : DisplayStyle.None;
        itemDetails.style.display = newMode == ViewMode.Item ? DisplayStyle.Flex : DisplayStyle.None;
        itemPreviewer.style.display = newMode == ViewMode.Item ? DisplayStyle.Flex : DisplayStyle.None;

        // 刷新按钮状态
        rootVisualElement.Query<Button>().ForEach(button =>
        {
            if (button.text.Contains("Inventory") || button.text.Contains("Items"))
            {
                ApplyModeButtonStyle(button,
                    (newMode == ViewMode.Inventory && button.text.Contains("Inventory")) ||
                    (newMode == ViewMode.Item && button.text.Contains("Items")));
            }
        });

        // 清除选择状态
        if (newMode == ViewMode.Inventory)
        {
            itemList.ClearSelection();
            if (allInventories.Length > 0)
            {
                inventoryList.SetSelection(0);
            }
        }
        else
        {
            inventoryList.ClearSelection();
            if (allItems.Length > 0)
            {
                itemList.SetSelection(0);
            }
        }
    }

    // Inventory 详情绘制
    // 优化后的 Inventory 详情绘制方法
    private void DrawInventoryDetails()
    {
        var selectedInventory = inventoryList.selectedItem as Inventory;

        if (selectedInventory == null)
        {
            DrawNoSelectionMessage("No inventory selected");
            return;
        }

        var serializedObject = new SerializedObject(selectedInventory);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(15);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.Space(5);

        var scrollPos = EditorGUILayout.BeginScrollView(Vector2.zero);

        // --- 样式定义 ---
        Color labelColor = Color.white;
        Color highlightLabelColor = Color.cyan;
        Color inputBgColor = new Color(0.8f, 0.8f, 0.8f);
        Color inputTextColor = new Color(0.15f, 0.15f, 0.15f);
        float fieldLeftPadding = 12f;
        float labelWidth = 150f;

        GUIStyle customHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 28
        };

        GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 24
        };

        var labelStyle = EditorStyles.label;
        var textFieldStyle = EditorStyles.textField;

        Color originalLabelColor = labelStyle.normal.textColor;
        Color originalTextColor = textFieldStyle.normal.textColor;
        Texture2D originalBg = textFieldStyle.normal.background;

        // 临时修改样式
        labelStyle.focused.textColor = highlightLabelColor;
        labelStyle.hover.textColor = highlightLabelColor;
        labelStyle.normal.textColor = labelColor;

        Texture2D lightBg = new Texture2D(1, 1);
        lightBg.SetPixel(0, 0, inputBgColor);
        lightBg.Apply();

        textFieldStyle.normal.background = lightBg;
        textFieldStyle.focused.background = lightBg;
        textFieldStyle.active.background = lightBg;
        textFieldStyle.normal.textColor = inputTextColor;
        textFieldStyle.focused.textColor = inputTextColor;
        textFieldStyle.active.textColor = inputTextColor;

        EditorStyles.objectField.normal.textColor = inputTextColor;
        EditorStyles.objectField.focused.textColor = inputTextColor;
        EditorStyles.objectField.active.textColor = inputTextColor;

        // --- 库存属性部分 ---
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Inventory Properties", customHeaderStyle);
        EditorGUILayout.Space(8);

        var nameProperty = serializedObject.FindProperty("inventoryName");

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(fieldLeftPadding);
        EditorGUILayout.BeginVertical();

        EditorGUIUtility.labelWidth = labelWidth;
        EditorGUILayout.PropertyField(nameProperty, new GUIContent("Inventory Name"));

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        // --- 物品列表部分 ---
        EditorGUILayout.LabelField("Items in Inventory", customHeaderStyle);
        EditorGUILayout.Space(8);

        var itemsProperty = serializedObject.FindProperty("items");

        // 统计信息
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(fieldLeftPadding);


        GUIStyle totalItemsStyle = new GUIStyle(EditorStyles.miniLabel);
        totalItemsStyle.normal.textColor = new Color(1f, 1f, 1.0f); // 浅蓝色
        totalItemsStyle.fontSize = 12;

        EditorGUILayout.LabelField($"Total Items: {itemsProperty.arraySize}", totalItemsStyle);
        EditorGUILayout.EndHorizontal();


        EditorGUILayout.Space(8);

        // 添加物品按钮 - 美化版本
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(fieldLeftPadding);
        if (GUILayout.Button("＋ Add Item to Inventory", GUILayout.Height(30)))
        {
            ShowAddItemToInventoryPopup(selectedInventory);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(12);

        // 显示物品列表
        if (itemsProperty.arraySize == 0)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(fieldLeftPadding);
            EditorGUILayout.HelpBox("This inventory is empty. Click 'Add Item to Inventory' to add items.", MessageType.Info);
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            for (int i = 0; i < itemsProperty.arraySize; i++)
            {
                var itemElement = itemsProperty.GetArrayElementAtIndex(i);
                DrawInventoryItemElement(itemElement, i, selectedInventory);
            }
        }

        EditorGUILayout.EndScrollView();

        // --- 恢复默认样式 ---
        labelStyle.focused.textColor = originalLabelColor;
        labelStyle.hover.textColor = originalLabelColor;
        labelStyle.normal.textColor = originalLabelColor;

        textFieldStyle.normal.textColor = originalTextColor;
        textFieldStyle.focused.textColor = originalTextColor;
        textFieldStyle.active.textColor = originalTextColor;
        textFieldStyle.normal.background = originalBg;
        textFieldStyle.focused.background = originalBg;
        textFieldStyle.active.background = originalBg;

        EditorStyles.objectField.normal.textColor = originalTextColor;
        EditorStyles.objectField.focused.textColor = originalTextColor;
        EditorStyles.objectField.active.textColor = originalTextColor;

        EditorGUILayout.EndVertical();
        GUILayout.Space(15);
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();

            // 更新库存名称
            if (selectedInventory.name != selectedInventory.inventoryName)
            {
                selectedInventory.name = selectedInventory.inventoryName;
                AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(selectedInventory), selectedInventory.inventoryName);
                ReloadAndRefreshLists(selectedInventory, null);
            }
        }
    }


private void DrawInventoryItemElement(SerializedProperty itemElement, int index, Inventory inventory)
{
    var itemProperty = itemElement.FindPropertyRelative("item");
    var quantityProperty = itemElement.FindPropertyRelative("quantity");
    var item = itemProperty.objectReferenceValue as Item;

    if (item == null)
    {
        DrawInvalidItemElement(itemElement, index, inventory);
        return;
    }

    // 主容器
    EditorGUILayout.BeginVertical("box");
    {
        EditorGUILayout.BeginHorizontal();
        {
            // 左侧：图标
            DrawItemIcon(item);
            
            // 右侧：信息区域
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            {
                // 第一行：名称、类型、稀有度、价钱
                EditorGUILayout.BeginHorizontal();
                {
                    // 物品名称
                    GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel);
                    nameStyle.normal.textColor = GetRarityColor(item.rarity);
                    EditorGUILayout.LabelField(item.itemName, nameStyle, GUILayout.Width(100));
                    
                    // 类型标签
                    DrawCompactTag(item.category.ToString(), GetCategoryColor(item.category.ToString()), 60);
                    
                    // 稀有度标签
                    DrawCompactTag(item.rarity.ToString(), GetRarityColor(item.rarity), 60);
                    
                    // 价钱
                    if (item.price > 0)
                    {
                        GUIStyle priceStyle = new GUIStyle(EditorStyles.miniLabel);
                        priceStyle.normal.textColor = Color.yellow;
                        EditorGUILayout.LabelField($"${item.price}", priceStyle, GUILayout.Width(40));
                    }
                    
                    GUILayout.FlexibleSpace();
                    
                    // 移除按钮
                    DrawRemoveButton(itemProperty, index, inventory, item);
                }
                EditorGUILayout.EndHorizontal();

                // 第二行：增益属性 - 全宽度
                if (item.equipCatergory != Item.EquipCatergory.None)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        DrawEquipmentStats(item);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // 第三行：描述和数量控制
                EditorGUILayout.BeginHorizontal();
                {
                    // 描述
                    if (!string.IsNullOrEmpty(item.description))
                    {
                        GUIStyle descStyle = new GUIStyle(EditorStyles.miniLabel);
                        descStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                        string shortDesc = item.description.Length > 80 ? 
                            item.description.Substring(0, 77) + "..." : item.description;
                        EditorGUILayout.LabelField(shortDesc, descStyle, GUILayout.ExpandWidth(true));
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                    }
                    
                    // 数量控制
                    DrawQuantityControl(quantityProperty);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();
    }
    EditorGUILayout.EndVertical();
    
    EditorGUILayout.Space(3);
}   
private void DrawCompactTag(string text, Color color, int width = 50)
{
    GUIStyle tagStyle = new GUIStyle(EditorStyles.miniLabel);
    tagStyle.normal.textColor = Color.white;
    tagStyle.alignment = TextAnchor.MiddleCenter;
    tagStyle.padding = new RectOffset(2, 2, 0, 0);
    tagStyle.fontSize = 8;
    
    Rect tagRect = GUILayoutUtility.GetRect(width, 14);
    EditorGUI.DrawRect(tagRect, color * 0.3f);
    
    Handles.color = color * 0.8f;
    Handles.DrawPolyLine(
        new Vector3(tagRect.x, tagRect.y, 0),
        new Vector3(tagRect.xMax, tagRect.y, 0),
        new Vector3(tagRect.xMax, tagRect.yMax, 0),
        new Vector3(tagRect.x, tagRect.yMax, 0),
        new Vector3(tagRect.x, tagRect.y, 0)
    );
    
    GUI.Label(tagRect, text, tagStyle);
}

private void DrawEquipmentStats(Item item)
{
    List<string> stats = new List<string>();
    
    if (item.bonusAttack > 0) stats.Add($"ATK +{item.bonusAttack}");
    if (item.bonusDefend > 0) stats.Add($"DEF +{item.bonusDefend}");
    if (item.bonusSpeed > 0) stats.Add($"SPD +{item.bonusSpeed}");
    if (item.bonusIntelligence > 0) stats.Add($"INT +{item.bonusIntelligence}");
    
    if (stats.Count > 0)
    {
        GUIStyle statsStyle = new GUIStyle(EditorStyles.miniLabel);
        statsStyle.normal.textColor = new Color(0.8f, 1f, 0.8f);
        // 使用全宽度显示装备属性
        EditorGUILayout.LabelField(string.Join("    ", stats), statsStyle, GUILayout.ExpandWidth(true));
    }
}


private void DrawQuantityControl(SerializedProperty quantityProperty)
{
    EditorGUILayout.BeginHorizontal(GUILayout.Width(90));
    {
        EditorGUILayout.LabelField("Qty:", GUILayout.Width(25));
        
        // 减少按钮
        if (GUILayout.Button("-", GUILayout.Width(18)))
        {
            quantityProperty.intValue = Mathf.Max(1, quantityProperty.intValue - 1);
        }
        
        // 数量显示
        GUIStyle quantityStyle = new GUIStyle(EditorStyles.textField);
        quantityStyle.alignment = TextAnchor.MiddleCenter;
        quantityStyle.fontSize = 10;
        quantityProperty.intValue = EditorGUILayout.IntField(
            quantityProperty.intValue, 
            quantityStyle, 
            GUILayout.Width(25)
        );
        quantityProperty.intValue = Mathf.Max(1, quantityProperty.intValue);
        
        // 增加按钮
        if (GUILayout.Button("+", GUILayout.Width(18)))
        {
            quantityProperty.intValue++;
        }
    }
    EditorGUILayout.EndHorizontal();
}

private void DrawRemoveButton(SerializedProperty itemProperty, int index, Inventory inventory, Item item)
{
    GUIStyle removeStyle = new GUIStyle(GUI.skin.button);
    removeStyle.normal.textColor = Color.red;
    removeStyle.fontSize = 8;
    removeStyle.padding = new RectOffset(4, 4, 1, 1);
    
    if (GUILayout.Button("X", removeStyle, GUILayout.Width(25), GUILayout.Height(16)))
    {
        if (EditorUtility.DisplayDialog(
            "Confirm Removal",
            $"Remove {item.itemName}?",
            "Remove", "Cancel"))
        {
            inventory.items.RemoveAt(index);
            EditorUtility.SetDirty(inventory);
            inventoryDetails.MarkDirtyRepaint();
        }
    }
}

    // 绘制物品图标
private void DrawItemIcon(Item item)
{
    EditorGUILayout.BeginVertical(GUILayout.Width(50));
    {
        Rect iconRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));
        
        // 图标背景
        EditorGUI.DrawRect(iconRect, new Color(0.3f, 0.3f, 0.3f));
        
        if (item.icon != null && item.icon.texture != null)
        {
            // 计算内边距，让图标在容器中居中显示
            Rect iconContentRect = new Rect(iconRect.x + 2, iconRect.y + 2, iconRect.width - 4, iconRect.height - 4);
            GUI.DrawTexture(iconContentRect, item.icon.texture, ScaleMode.ScaleToFit);
        }
        else
        {
            // 无图标时的占位符
            GUIStyle placeholderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            placeholderStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            placeholderStyle.fontSize = 8;
            GUI.Label(iconRect, "No\nIcon", placeholderStyle);
        }
        
        // 图标边框
        Handles.color = new Color(0.5f, 0.5f, 0.5f);
        Handles.DrawPolyLine(
            new Vector3(iconRect.x, iconRect.y, 0),
            new Vector3(iconRect.xMax, iconRect.y, 0),
            new Vector3(iconRect.xMax, iconRect.yMax, 0),
            new Vector3(iconRect.x, iconRect.yMax, 0),
            new Vector3(iconRect.x, iconRect.y, 0)
        );
    }
    EditorGUILayout.EndVertical();
}

// 绘制主要信息
private void DrawItemMainInfo(Item item)
{
    // 第一行：名称和稀有度
    EditorGUILayout.BeginHorizontal();
    {
        // 物品名称
        GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel);
        nameStyle.normal.textColor = GetRarityColor(item.rarity);
        EditorGUILayout.LabelField(item.itemName, nameStyle, GUILayout.ExpandWidth(true));

        // 稀有度标签
        DrawRarityBadge(item.rarity.ToString());
    }
    EditorGUILayout.EndHorizontal();

    // 第二行：类别和属性
    EditorGUILayout.BeginHorizontal();
    {
        // 类别标签
        DrawCategoryBadge(item.category.ToString());
        
        // 属性信息
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
        {
            if (item.price > 0)
            {
                GUIStyle priceStyle = new GUIStyle(EditorStyles.miniLabel);
                priceStyle.normal.textColor = Color.yellow;
                EditorGUILayout.LabelField($"${item.price}", priceStyle, GUILayout.Width(50));
            }

            if (item.weight > 0)
            {
                GUIStyle weightStyle = new GUIStyle(EditorStyles.miniLabel);
                weightStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
                EditorGUILayout.LabelField($"{item.weight}kg", weightStyle, GUILayout.Width(40));
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    EditorGUILayout.EndHorizontal();

    // 第三行：装备属性（如果有）
    if (item.equipCatergory != Item.EquipCatergory.None)
    {
        DrawEquipmentAttributes(item);
    }
}

// 绘制稀有度徽章
private void DrawRarityBadge(string rarity)
{
    Color color = GetRarityColor(rarity);
    DrawBadge(rarity, color, 60);
}

// 绘制类别徽章
private void DrawCategoryBadge(string category)
{
    Color color = GetCategoryColor(category);
    DrawBadge(category, color, 70);
}

// 绘制徽章
private void DrawBadge(string text, Color color, int width)
{
    GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel);
    badgeStyle.normal.textColor = Color.white;
    badgeStyle.alignment = TextAnchor.MiddleCenter;
    badgeStyle.padding = new RectOffset(4, 4, 1, 1);
    
    Rect badgeRect = GUILayoutUtility.GetRect(width, 16);
    EditorGUI.DrawRect(badgeRect, color * 0.3f);
    
    Handles.color = color * 0.8f;
    Handles.DrawPolyLine(
        new Vector3(badgeRect.x, badgeRect.y, 0),
        new Vector3(badgeRect.xMax, badgeRect.y, 0),
        new Vector3(badgeRect.xMax, badgeRect.yMax, 0),
        new Vector3(badgeRect.x, badgeRect.yMax, 0),
        new Vector3(badgeRect.x, badgeRect.y, 0)
    );
    
    GUI.Label(badgeRect, text, badgeStyle);
}

// 绘制装备属性
private void DrawEquipmentAttributes(Item item)
{
    EditorGUILayout.BeginHorizontal();
    {
        List<string> attributes = new List<string>();
        
        if (item.bonusAttack > 0) attributes.Add($"ATK +{item.bonusAttack}");
        if (item.bonusDefend > 0) attributes.Add($"DEF +{item.bonusDefend}");
        if (item.bonusSpeed > 0) attributes.Add($"SPD +{item.bonusSpeed}");
        if (item.bonusIntelligence > 0) attributes.Add($"INT +{item.bonusIntelligence}");
        
        if (attributes.Count > 0)
        {
            GUIStyle attrStyle = new GUIStyle(EditorStyles.miniLabel);
            attrStyle.normal.textColor = new Color(0.8f, 1f, 0.8f);
            EditorGUILayout.LabelField(string.Join(" | ", attributes), attrStyle);
        }
    }
    EditorGUILayout.EndHorizontal();
}

// 绘制操作控制
private void DrawItemControls(SerializedProperty quantityProperty, SerializedProperty itemProperty, int index, Inventory inventory, Item item)
{
    // 数量控制
    EditorGUILayout.BeginVertical();
    {
        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
        labelStyle.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.LabelField("Quantity", labelStyle);
        
        EditorGUILayout.BeginHorizontal();
        {
            // 减少按钮
            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                quantityProperty.intValue = Mathf.Max(1, quantityProperty.intValue - 1);
            }
            
            // 数量显示
            GUIStyle quantityStyle = new GUIStyle(EditorStyles.textField);
            quantityStyle.alignment = TextAnchor.MiddleCenter;
            quantityProperty.intValue = EditorGUILayout.IntField(
                quantityProperty.intValue, 
                quantityStyle, 
                GUILayout.Width(30)
            );
            quantityProperty.intValue = Mathf.Max(1, quantityProperty.intValue);
            
            // 增加按钮
            if (GUILayout.Button("+", GUILayout.Width(20)))
            {
                quantityProperty.intValue++;
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    EditorGUILayout.EndVertical();

    // 移除按钮
    GUIStyle removeStyle = new GUIStyle(GUI.skin.button);
    removeStyle.normal.textColor = Color.red;
    removeStyle.fontSize = 10;
    
    if (GUILayout.Button("Remove", removeStyle, GUILayout.Height(24)))
    {
        if (EditorUtility.DisplayDialog(
            "Confirm Removal",
            $"Remove {item.itemName} from inventory?",
            "Remove", "Cancel"))
        {
            inventory.items.RemoveAt(index);
            EditorUtility.SetDirty(inventory);
            inventoryDetails.MarkDirtyRepaint();
        }
    }
}


// 绘制物品标题和基本信息
private void DrawItemHeader(Item item)
{
    EditorGUILayout.BeginHorizontal();
    {
        // 物品名称
        GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel);
        nameStyle.normal.textColor = GetRarityColor(item.rarity);
        nameStyle.fontSize = 12;
        EditorGUILayout.LabelField(item.itemName, nameStyle, GUILayout.ExpandWidth(true));
        
        // 价格（如果有）
        if (item.price > 0)
        {
            GUIStyle priceStyle = new GUIStyle(EditorStyles.miniLabel);
            priceStyle.normal.textColor = Color.yellow;
            priceStyle.alignment = TextAnchor.MiddleRight;
            EditorGUILayout.LabelField($"${item.price:F1}", priceStyle, GUILayout.Width(50));
        }
    }
    EditorGUILayout.EndHorizontal();
}

// 绘制物品属性
private void DrawItemStats(Item item)
{
    EditorGUILayout.BeginHorizontal();
    {
        // 类别标签
        DrawCategoryTag(item.category.ToString());
        
        // 稀有度标签
        DrawRarityTag(item.rarity.ToString());
        
        // 重量（如果有）
        if (item.weight > 0)
        {
            GUIStyle weightStyle = new GUIStyle(EditorStyles.miniLabel);
            weightStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            EditorGUILayout.LabelField($"{item.weight}kg", weightStyle, GUILayout.Width(40));
        }
    }
    EditorGUILayout.EndHorizontal();
}

// 绘制类别标签
private void DrawCategoryTag(string category)
{
    Color categoryColor = GetCategoryColor(category);
    DrawTag(category, categoryColor, 70);
}

// 绘制稀有度标签
private void DrawRarityTag(string rarity)
{
    Color rarityColor = GetRarityColor(rarity);
    DrawTag(rarity, rarityColor, 60);
}

// 绘制通用标签
private void DrawTag(string text, Color color, int width = 60)
{
    GUIStyle tagStyle = new GUIStyle(EditorStyles.miniLabel);
    tagStyle.normal.textColor = Color.white;
    tagStyle.alignment = TextAnchor.MiddleCenter;
    tagStyle.padding = new RectOffset(4, 4, 1, 1);
    
    // 绘制标签背景
    Rect tagRect = GUILayoutUtility.GetRect(width, 16);
    EditorGUI.DrawRect(tagRect, color * 0.3f);
    
    // 绘制边框
    Handles.color = color * 0.8f;
    Handles.DrawPolyLine(
        new Vector3(tagRect.x, tagRect.y, 0),
        new Vector3(tagRect.xMax, tagRect.y, 0),
        new Vector3(tagRect.xMax, tagRect.yMax, 0),
        new Vector3(tagRect.x, tagRect.yMax, 0),
        new Vector3(tagRect.x, tagRect.y, 0)
    );
    
    // 绘制文字
    GUI.Label(tagRect, text, tagStyle);
}

// 绘制数量控制
private void DrawQuantityControls(SerializedProperty quantityProperty)
{
    EditorGUILayout.BeginVertical(GUILayout.Width(70));
    {
        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
        labelStyle.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.LabelField("Quantity", labelStyle);
        
        EditorGUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                quantityProperty.intValue = Mathf.Max(1, quantityProperty.intValue - 1);
            }
            
            GUIStyle quantityStyle = new GUIStyle(EditorStyles.textField);
            quantityStyle.alignment = TextAnchor.MiddleCenter;
            quantityProperty.intValue = EditorGUILayout.IntField(
                quantityProperty.intValue, 
                quantityStyle, 
                GUILayout.Width(30)
            );
            quantityProperty.intValue = Mathf.Max(1, quantityProperty.intValue);
            
            if (GUILayout.Button("+", GUILayout.Width(20)))
            {
                quantityProperty.intValue++;
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    EditorGUILayout.EndVertical();
}



// 绘制物品描述
private void DrawItemDescription(Item item)
{
    if (!string.IsNullOrEmpty(item.description))
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(52); // 与图标对齐
        
        GUIStyle descStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
        descStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        descStyle.padding = new RectOffset(0, 8, 0, 0);
        
        string displayDescription = item.description;
        if (displayDescription.Length > 120)
        {
            displayDescription = displayDescription.Substring(0, 117) + "...";
        }
        
        EditorGUILayout.LabelField(displayDescription, descStyle);
        EditorGUILayout.EndHorizontal();
    }
}

// 绘制无效物品元素
private void DrawInvalidItemElement(SerializedProperty itemElement, int index, Inventory inventory)
{
    EditorGUILayout.BeginVertical("box");
    {
        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.HelpBox("Missing Item Reference", MessageType.Error);
            
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                inventory.items.RemoveAt(index);
                EditorUtility.SetDirty(inventory);
                inventoryDetails.MarkDirtyRepaint();
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    EditorGUILayout.EndVertical();
    
    EditorGUILayout.Space(5);
}

// 获取稀有度颜色
private Color GetRarityColor(Item.Rarity rarity)
{
    switch (rarity)
    {
        case Item.Rarity.Common: return Color.gray;
        case Item.Rarity.Uncommon: return Color.yellow;
        case Item.Rarity.Rare: return Color.blue;
        case Item.Rarity.Epic: return new Color(0.5f, 0f, 0.5f); // 紫色
        case Item.Rarity.Legendary: return new Color(1f, 0.5f, 0f); // 橙色
        default: return Color.white;
    }
}

private Color GetRarityColor(string rarity)
{
    if (Enum.TryParse<Item.Rarity>(rarity, out var rarityEnum))
    {
        return GetRarityColor(rarityEnum);
    }
    return Color.white;
}

    // 获取类别颜色
    private Color GetCategoryColor(string category)
    {
        switch (category.ToLower())
        {
            case "weapon": return Color.red;
            case "armor": return Color.blue;
            case "potion": return Color.green;
            case "material": return Color.yellow;
            case "consumable": return new Color(1f, 0.6f, 0f); // 橙色
            case "quest": return new Color(0.5f, 0f, 0.5f); // 紫色
            default: return new Color(0.4f, 0.4f, 0.4f); // 灰色
        }
    }
    // 同时优化弹窗的样式

    
    
    
    
    private void ShowAddItemToInventoryPopup(Inventory inventory)
    {
        var allAvailableItems = allItems.Where(item => !inventory.items.Any(invItem => invItem.item == item)).ToArray();

        if (allAvailableItems.Length == 0)
        {
            EditorUtility.DisplayDialog("Info", "All available items are already in this inventory.", "OK");
            return;
        }

        var popup = ScriptableObject.CreateInstance<ItemSelectionPopup>();
        popup.Setup(allAvailableItems, (selectedItem) =>
        {
            if (selectedItem != null)
            {
                inventory.items.Add(new InventoryItem(selectedItem, 1));
                EditorUtility.SetDirty(inventory);
                inventoryDetails.MarkDirtyRepaint();

                // 显示添加成功的提示
                EditorUtility.DisplayDialog("Success",
                    $"Added {selectedItem.itemName} to {inventory.inventoryName}", "OK");
            }
        });
    }


    private void DrawNoSelectionMessage(string message)
    {
        EditorGUILayout.Space(20);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(15);
        GUIStyle boldLabelStyle = new GUIStyle(EditorStyles.boldLabel);
        boldLabelStyle.normal.textColor = Color.white;
        EditorGUILayout.LabelField(message, boldLabelStyle);
        GUILayout.Space(15);
        EditorGUILayout.EndHorizontal();
    }

    // 原有的物品编辑方法 - 保持完整不变
    private void DrawItemDetails()
    {
        var selectedItem = itemList.selectedItem as Item;

        if (selectedItem == null)
        {
            DrawNoSelectionMessage("No item selected");
            return;
        }

        var serializedObject = new SerializedObject(selectedItem);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(15);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.Space(5);

        var scrollPos = EditorGUILayout.BeginScrollView(Vector2.zero);

        // --- 样式定义 ---
        Color labelColor = Color.white;
        Color highlightLabelColor = Color.cyan;
        Color inputBgColor = new Color(0.8f, 0.8f, 0.8f);
        Color inputTextColor = new Color(0.15f, 0.15f, 0.15f);
        float fieldLeftPadding = 12f;
        float labelWidth = 130f;

        GUIStyle customHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 20,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 28
        };

        var labelStyle = EditorStyles.label;
        var textFieldStyle = EditorStyles.textField;

        Color originalLabelColor = labelStyle.normal.textColor;
        Color originalTextColor = textFieldStyle.normal.textColor;
        Texture2D originalBg = textFieldStyle.normal.background;

        // 临时修改样式
        labelStyle.focused.textColor = highlightLabelColor;
        labelStyle.hover.textColor = highlightLabelColor;
        labelStyle.normal.textColor = labelColor;

        Texture2D lightBg = new Texture2D(1, 1);
        lightBg.SetPixel(0, 0, inputBgColor);
        lightBg.Apply();

        textFieldStyle.normal.background = lightBg;
        textFieldStyle.focused.background = lightBg;
        textFieldStyle.active.background = lightBg;
        textFieldStyle.normal.textColor = inputTextColor;
        textFieldStyle.focused.textColor = inputTextColor;
        textFieldStyle.active.textColor = inputTextColor;

        EditorStyles.objectField.normal.textColor = inputTextColor;
        EditorStyles.objectField.focused.textColor = inputTextColor;
        EditorStyles.objectField.active.textColor = inputTextColor;

        SerializedProperty property = serializedObject.GetIterator();

        if (!property.NextVisible(true))
        {
            // 恢复样式
            labelStyle.focused.textColor = originalLabelColor;
            labelStyle.hover.textColor = originalLabelColor;
            labelStyle.normal.textColor = originalLabelColor;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            GUILayout.Space(15);
            EditorGUILayout.EndHorizontal();
            return;
        }

        // 检查 EquipCatergory 是否为 None
        bool showEquipmentBonus = selectedItem.equipCatergory != Item.EquipCatergory.None;

        do
        {
            if (property.name == "m_Script") continue;

            if (property.name == "itemName")
            {
                EditorGUILayout.Space(18);
                EditorGUILayout.LabelField("Basic Attributes", customHeaderStyle);
                EditorGUILayout.Space(18);
            }
            else if (property.name == "icon")
            {
                EditorGUILayout.Space(18);
                EditorGUILayout.LabelField("Visual", customHeaderStyle);
                EditorGUILayout.Space(18);
            }
            else if (property.name == "equipCatergory")
            {
                EditorGUILayout.Space(18);
                EditorGUILayout.LabelField("Equipment Attributes", customHeaderStyle);
                EditorGUILayout.Space(18);
            }
            // 【条件显示逻辑】如果 EquipCatergory 是 None，并且当前属性是 Bonus 属性之一，则跳过绘制
            else if (!showEquipmentBonus && (property.name == "bonusAttack" ||
                                             property.name == "bonusDefend" ||
                                             property.name == "bonusSpeed" ||
                                             property.name == "bonusIntelligence"))
            {
                continue;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(fieldLeftPadding);
            EditorGUILayout.BeginVertical();

            EditorGUIUtility.labelWidth = labelWidth;

            if (property.name == "description")
            {
                EditorGUILayout.PropertyField(
                    property,
                    new GUIContent(property.displayName),
                    true,
                    GUILayout.Height(80));
            }
            else
            {
                EditorGUILayout.PropertyField(property, true);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);

        } while (property.NextVisible(false));

        EditorGUILayout.EndScrollView();

        // --- 恢复默认样式 ---
        labelStyle.focused.textColor = originalLabelColor;
        labelStyle.hover.textColor = originalLabelColor;
        labelStyle.normal.textColor = originalLabelColor;

        // 恢复 TextField 样式
        textFieldStyle.normal.textColor = originalTextColor;
        textFieldStyle.focused.textColor = originalTextColor;
        textFieldStyle.active.textColor = originalTextColor;
        textFieldStyle.normal.background = originalBg;
        textFieldStyle.focused.background = originalBg;
        textFieldStyle.active.background = originalBg;

        // 恢复 ObjectField 样式
        EditorStyles.objectField.normal.textColor = originalTextColor;
        EditorStyles.objectField.focused.textColor = originalTextColor;
        EditorStyles.objectField.active.textColor = originalTextColor;

        EditorGUILayout.EndVertical();
        GUILayout.Space(15);
        EditorGUILayout.EndHorizontal();

        // 【优化 D】属性更改检查和 ListView 更新
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();

            // 检查 itemName 是否更改，并同步 ScriptableObject 的 Asset 名称
            if (selectedItem.name != selectedItem.itemName)
            {
                // 1. 更新 ScriptableObject 的 name
                selectedItem.name = selectedItem.itemName;
                // 2. 更新 Project 窗口中的资产文件名
                AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(selectedItem), selectedItem.itemName);

                // 3. 强制列表和预览更新
                ReloadAndRefreshLists(null, selectedItem);
            }
            else
            {
                // 如果只更改了其他属性 (例如 equipCatergory 改变，需要重新绘制详情面板)
                itemDetails.MarkDirtyRepaint();
                itemPreviewer.MarkDirtyRepaint();
            }
        }
    }

    // 原有的预览相关方法保持不变
    private void DrawItemPreviewer()
    {
        if (isRenderingPreview) return;
        isRenderingPreview = true;

        try
        {
            var selectedItem = itemList.selectedItem as Item;
            Rect rect = itemPreviewer.contentRect;

            if (rect.width <= 1 || rect.height <= 1)
            {
                EditorGUI.DrawRect(new Rect(0, 0, itemPreviewer.layout.width, itemPreviewer.layout.height), DarkGray);
                return;
            }

            int previewWidth = Mathf.Max(1, (int)rect.width);
            int previewHeight = Mathf.Max(1, (int)rect.height);

            if (previewRenderTexture == null ||
                previewRenderTexture.width != previewWidth ||
                previewRenderTexture.height != previewHeight)
            {
                CreatePreviewRenderTexture(previewWidth, previewHeight);
            }

            // 绘制背景
            EditorGUI.DrawRect(new Rect(0, 0, rect.width, rect.height), DarkGray);

            if (selectedItem == null || currentPreviewInstance == null)
            {
                GUI.Label(new Rect(10, 10, rect.width - 20, 20), "Select an Item with a Model to preview.", EditorStyles.whiteLabel);
                return;
            }

            // 设置相机和光源
            SetupPreviewCamera();

            // 渲染到纹理
            RenderPreviewToTexture(selectedItem);

            // 显示渲染纹理
            if (previewRenderTexture != null && previewRenderTexture.IsCreated())
            {
                GUI.DrawTexture(new Rect(0, 0, rect.width, rect.height), previewRenderTexture, ScaleMode.StretchToFill, true);
            }

            // 处理输入
            HandleCameraInput(rect);
        }
        finally
        {
            isRenderingPreview = false;
        }
    }

    private void AddItemOrInventory()
    {
        if (currentViewMode == ViewMode.Inventory)
        {
            AddInventory();
        }
        else
        {
            AddItem();
        }
    }

    private void DeleteItemOrInventory()
    {
        if (currentViewMode == ViewMode.Inventory)
        {
            DeleteInventory();
        }
        else
        {
            DeleteItem();
        }
    }

    private void AddInventory()
    {
        EnsureDirectoryExists(INVENTORY_PATH);

        Inventory newInventory = ScriptableObject.CreateInstance<Inventory>();
        newInventory.name = "New Inventory";
        newInventory.inventoryName = "New Inventory";

        string path = AssetDatabase.GenerateUniqueAssetPath(INVENTORY_PATH + "NewInventory.asset");
        AssetDatabase.CreateAsset(newInventory, path);
        AssetDatabase.SaveAssets();

        ReloadAndRefreshLists(newInventory, null);
    }

    private void DeleteInventory()
    {
        var selectedInventory = inventoryList.selectedItem as Inventory;
        if (selectedInventory == null) return;

        bool confirm = EditorUtility.DisplayDialog(
            "Confirm Deletion",
            $"Are you sure you want to delete inventory: {selectedInventory.name}?",
            "Yes", "No");

        if (confirm)
        {
            string path = AssetDatabase.GetAssetPath(selectedInventory);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();

            ReloadAndRefreshLists(null, null);
            EditorUtility.DisplayDialog("Success", $"Inventory '{selectedInventory.name}' deleted.", "OK");
        }
    }

    private void AddItem()
    {
        EnsureDirectoryExists(ITEM_PATH);

        Item newItem = ScriptableObject.CreateInstance<Item>();
        newItem.name = "New Item";
        newItem.itemName = "New Item";

        string path = AssetDatabase.GenerateUniqueAssetPath(ITEM_PATH + "NewItem.asset");
        AssetDatabase.CreateAsset(newItem, path);
        AssetDatabase.SaveAssets();

        ReloadAndRefreshLists(null, newItem);
    }

    private void DeleteItem()
    {
        var selectedItem = itemList.selectedItem as Item;
        if (selectedItem == null) return;

        bool confirm = EditorUtility.DisplayDialog(
            "Confirm Deletion",
            $"Are you sure you want to delete item: {selectedItem.name}?",
            "Yes", "No");

        if (confirm)
        {
            string path = AssetDatabase.GetAssetPath(selectedItem);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();

            ReloadAndRefreshLists(null, null);
            EditorUtility.DisplayDialog("Success", $"Item '{selectedItem.name}' deleted.", "OK");
        }
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path.Replace("Assets/", "")))
        {
            string[] pathParts = path.Split('/');
            string currentPath = pathParts[0];
            for (int i = 1; i < pathParts.Length - 1; i++)
            {
                if (!AssetDatabase.IsValidFolder(currentPath + "/" + pathParts[i]))
                {
                    AssetDatabase.CreateFolder(currentPath, pathParts[i]);
                }
                currentPath += "/" + pathParts[i];
            }
        }
    }

    private void ExportToJson()
    {
        if (currentViewMode == ViewMode.Inventory)
        {
            var selectedInventory = inventoryList.selectedItem as Inventory;
            if (selectedInventory != null)
            {
                ExportInventoryToJson(selectedInventory);
            }
            else
            {
                EditorUtility.DisplayDialog("Export Error", "Please select an inventory to export.", "OK");
            }
        }
        else
        {
            var selectedItem = itemList.selectedItem as Item;
            if (selectedItem != null)
            {
                ExportItemToJson(selectedItem);
            }
            else
            {
                EditorUtility.DisplayDialog("Export Error", "Please select an item to export.", "OK");
            }
        }
    }

    private void ExportInventoryToJson(Inventory inventory)
    {
        var exportData = new InventoryExportData
        {
            inventoryName = inventory.inventoryName,
            items = inventory.items.Select(item => new InventoryItemExportData
            {
                itemName = item.item.name,
                itemType = item.item.category.ToString(),
                quantity = item.quantity
            }).ToList()
        };

        string json = JsonUtility.ToJson(exportData, true);
        string path = EditorUtility.SaveFilePanel("Export Inventory as JSON", "", $"{inventory.inventoryName}.json", "json");

        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            EditorUtility.DisplayDialog("Export Success", $"Inventory exported to: {path}", "OK");
        }
    }

    private void ExportItemToJson(Item item)
    {
        var exportData = new ItemExportData
        {
            itemName = item.itemName,
            category = item.category.ToString(),
            rarity = item.rarity.ToString(),
            price = item.price,
            weight = item.weight,
            isStackable = item.isStackable,
            maxStackSize = item.maxStackSize,
            description = item.description
        };

        string json = JsonUtility.ToJson(exportData, true);
        string path = EditorUtility.SaveFilePanel("Export Item as JSON", "", $"{item.itemName}.json", "json");

        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            EditorUtility.DisplayDialog("Export Success", $"Item exported to: {path}", "OK");
        }
    }

    [System.Serializable]
    private class InventoryExportData
    {
        public string inventoryName;
        public List<InventoryItemExportData> items;
    }

    [System.Serializable]
    private class InventoryItemExportData
    {
        public string itemName;
        public string itemType;
        public int quantity;
    }

    [System.Serializable]
    private class ItemExportData
    {
        public string itemName;
        public string category;
        public string rarity;
        public float price;
        public float weight;
        public bool isStackable;
        public int maxStackSize;
        public string description;
    }

    // 原有的辅助方法保持不变
    private void ApplyButtonStyle(Button button, Color baseColor)
    {
        button.style.backgroundColor = baseColor * 0.6f;
        button.style.color = Color.white;
        button.style.borderTopLeftRadius = 5;
        button.style.borderTopRightRadius = 5;
        button.style.borderBottomLeftRadius = 5;
        button.style.borderBottomRightRadius = 5;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;

        // 悬停和点击时的效果
        button.RegisterCallback<MouseEnterEvent>(evt =>
        {
            button.style.backgroundColor = baseColor * 0.8f;
        });
        button.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            button.style.backgroundColor = baseColor * 0.6f;
        });

        button.RegisterCallback<MouseDownEvent>(evt =>
        {
            button.style.backgroundColor = baseColor;
        });
        button.RegisterCallback<MouseUpEvent>(evt =>
        {
            button.style.backgroundColor = baseColor * 0.8f;
        });
    }

    private void ResetCameraToDefault()
    {
        previewCameraRotation = new Vector3(45, 0, 0);
        previewCameraDistance = 3.0f;
        previewPosition = Vector3.zero;
    }

    private void CreatePreviewRenderTexture(int width, int height)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        if (previewRenderTexture != null)
        {
            if (previewCamera != null)
            {
                previewCamera.targetTexture = null;
            }
            previewRenderTexture.Release();
            DestroyImmediate(previewRenderTexture);
        }

        previewRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        previewRenderTexture.antiAliasing = 4;
        previewRenderTexture.Create();

        if (previewCamera != null)
        {
            previewCamera.targetTexture = previewRenderTexture;
        }
    }

    private void SetupPreviewInstance(Item item)
    {
        if (currentPreviewInstance != null)
        {
            DestroyImmediate(currentPreviewInstance);
            currentPreviewInstance = null;
        }

        if (item == null || item.model == null)
        {
            return;
        }

        currentPreviewInstance = Instantiate(item.model);
        currentPreviewInstance.hideFlags = HideFlags.HideAndDontSave;

        Bounds bounds = new Bounds(currentPreviewInstance.transform.position, Vector3.zero);
        bool hasRenderer = false;

        Renderer[] renderers = currentPreviewInstance.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            foreach (Renderer r in renderers.Skip(1))
            {
                bounds.Encapsulate(r.bounds);
            }
            hasRenderer = true;
        }

        if (hasRenderer)
        {
            currentPreviewInstance.transform.position -= bounds.center;
            previewPosition = Vector3.zero;

            if (previewCamera != null)
            {
                float objectSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                float halfFOV = previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float cameraDistance = objectSize / Mathf.Tan(halfFOV);
                previewCameraDistance = Mathf.Max(0.5f, cameraDistance * 1.5f);
            }
            else
            {
                previewCameraDistance = 3.0f;
            }
        }
        else
        {
            previewPosition = Vector3.zero;
            previewCameraDistance = 3.0f;
        }
    }

    private void SetupPreviewCamera()
    {
        if (previewCamera == null)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags("Preview Camera", HideFlags.HideAndDontSave, typeof(Camera));
            previewCamera = go.GetComponent<Camera>();
            previewCamera.cameraType = CameraType.Preview;
        }

        if (previewLight == null)
        {
            var lightGO = EditorUtility.CreateGameObjectWithHideFlags("Preview Light", HideFlags.HideAndDontSave, typeof(Light));
            previewLight = lightGO.GetComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.intensity = 1.2f;
            previewLight.color = Color.white;
            previewLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        if (previewRenderTexture != null && previewRenderTexture.IsCreated())
        {
            previewCamera.targetTexture = previewRenderTexture;
        }

        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 100f;
        previewCamera.fieldOfView = 60f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = DarkGray;

        Quaternion yawRotation = Quaternion.Euler(0, previewCameraRotation.y, 0);
        Quaternion pitchRotation = Quaternion.Euler(previewCameraRotation.x, 0, 0);
        Quaternion combinedRotation = yawRotation * pitchRotation;

        Vector3 orbitOffset = combinedRotation * new Vector3(0, 0, -previewCameraDistance);
        previewCamera.transform.rotation = combinedRotation;
        previewCamera.transform.position = previewPosition + orbitOffset;
    }

    private void RenderPreviewToTexture(Item selectedItem)
    {
        if (previewCamera == null || previewRenderTexture == null || !previewRenderTexture.IsCreated()) return;
        if (currentPreviewInstance == null) return;

        RenderTexture previousRT = RenderTexture.active;

        try
        {
            RenderTexture.active = previewRenderTexture;
            GL.Clear(true, true, DarkGray);
            previewCamera.Render();
        }
        finally
        {
            RenderTexture.active = previousRT;
        }
    }

    private void HandleCameraInput(Rect rect)
    {
        Event evt = Event.current;
        int controlID = GUIUtility.GetControlID(FocusType.Passive);

        switch (evt.GetTypeForControl(controlID))
        {
            case EventType.MouseDown:
                if (rect.Contains(evt.mousePosition) && evt.button == 0)
                {
                    isDragging = true;
                    GUIUtility.hotControl = controlID;
                    evt.Use();
                }
                break;

            case EventType.MouseUp:
                if (isDragging && evt.button == 0)
                {
                    isDragging = false;
                    GUIUtility.hotControl = 0;
                    evt.Use();
                }
                break;

            case EventType.MouseDrag:
                if (isDragging && evt.button == 0 && rect.Contains(evt.mousePosition))
                {
                    float rotationSpeed = 0.5f;
                    previewCameraRotation.y -= evt.delta.x * rotationSpeed;
                    previewCameraRotation.x -= evt.delta.y * rotationSpeed;
                    previewCameraRotation.x = Mathf.Clamp(previewCameraRotation.x, 5f, 89f);
                    evt.Use();
                    itemPreviewer.MarkDirtyRepaint();
                }
                break;

            case EventType.ScrollWheel:
                if (rect.Contains(evt.mousePosition))
                {
                    previewCameraDistance += evt.delta.y * 0.1f;
                    previewCameraDistance = Mathf.Clamp(previewCameraDistance, 0.5f, 20f);
                    evt.Use();
                    itemPreviewer.MarkDirtyRepaint();
                }
                break;
        }
    }

    private void SaveAllChanges()
    {
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Save Complete", "All changes have been successfully saved to disk.", "OK");
    }

    private void ReloadAndRefreshLists(Inventory inventoryToSelect, Item itemToSelect)
    {
        LoadAllData();

        inventoryList.itemsSource = allInventories;
        inventoryList.Rebuild();

        itemList.itemsSource = allItems;
        itemList.Rebuild();

        if (currentViewMode == ViewMode.Inventory && inventoryToSelect != null)
        {
            inventoryList.SetSelection(System.Array.IndexOf(allInventories, inventoryToSelect));
        }
        else if (currentViewMode == ViewMode.Item && itemToSelect != null)
        {
            itemList.SetSelection(System.Array.IndexOf(allItems, itemToSelect));
        }

        inventoryDetails.MarkDirtyRepaint();
        itemDetails.MarkDirtyRepaint();
        itemPreviewer.MarkDirtyRepaint();
    }

    private void LoadAllData()
    {
        allInventories = Resources.LoadAll<Inventory>("Inventory");
        allItems = Resources.LoadAll<Item>("Item");
    }

    private void CleanupPreviewResources()
    {
        if (previewCamera != null)
        {
            if (previewCamera.targetTexture != null)
            {
                previewCamera.targetTexture = null;
            }
            DestroyImmediate(previewCamera.gameObject);
            previewCamera = null;
        }

        if (previewLight != null)
        {
            DestroyImmediate(previewLight.gameObject);
            previewLight = null;
        }

        if (currentPreviewInstance != null)
        {
            DestroyImmediate(currentPreviewInstance);
            currentPreviewInstance = null;
        }

        if (previewRenderTexture != null)
        {
            previewRenderTexture.Release();
            DestroyImmediate(previewRenderTexture);
            previewRenderTexture = null;
        }
    }

    private void OnDisable()
    {
        CleanupPreviewResources();
    }
}

public class ItemSelectionPopup : EditorWindow
{
    private Item[] availableItems;
    private Action<Item> onItemSelected;
    private Vector2 scrollPosition;

    public void Setup(Item[] items, Action<Item> callback)
    {
        availableItems = items;
        onItemSelected = callback;
        titleContent = new GUIContent("Select Item to Add");
        minSize = new Vector2(300, 400);
        ShowUtility();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Select an item to add to inventory:", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var item in availableItems)
        {
            EditorGUILayout.BeginHorizontal("box");
            
            // 修复：使用 Sprite 的 texture 属性
            if (item.icon != null && item.icon.texture != null)
            {
                GUILayout.Label(item.icon.texture, GUILayout.Width(50), GUILayout.Height(50));
            }
            else
            {
                // 显示占位符
                GUILayout.Box("No Icon", GUILayout.Width(50), GUILayout.Height(50));
            }
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(item.itemName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Type: {item.category}, Rarity: {item.rarity}", EditorStyles.miniLabel);
            
            // 限制描述文本长度
            string shortDescription = item.description;
            if (!string.IsNullOrEmpty(shortDescription) && shortDescription.Length > 60)
            {
                shortDescription = shortDescription.Substring(0, 57) + "...";
            }
            EditorGUILayout.LabelField(shortDescription, EditorStyles.wordWrappedMiniLabel, GUILayout.MaxHeight(40));
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Add", GUILayout.Width(60), GUILayout.Height(50)))
            {
                onItemSelected?.Invoke(item);
                Close();
                break;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("Cancel"))
        {
            Close();
        }
    }
}