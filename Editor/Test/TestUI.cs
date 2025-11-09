using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

public class TestUI : EditorWindow
{
    [MenuItem("Tool/Three Panel Test")]
    public static void RenderWindow()
    {
        TestUI window = GetWindow<TestUI>("Three Panel Test");
        window.minSize = new Vector2(500, 400);
    }

    private void OnEnable()
    {
        if (rootVisualElement != null)
            rootVisualElement.Clear();

        VisualElement root = rootVisualElement;
        
        // 1. 【根容器布局】：设置为垂直堆叠，以容纳顶部区域和底部栏
        root.style.flexDirection = FlexDirection.Row;
        
        // --- 顶部区域 (左 + 右) ---
        VisualElement topSection = new VisualElement
        {
            style = { 
                flexDirection = FlexDirection.Row, // 子元素水平排列
                flexGrow = 1 // 占据窗口的绝大部分垂直空间
            }
        };
        
        // 2. 创建并添加 Panel A (左侧列表)
        VisualElement leftPanel = CreateLeftPanel();
        topSection.Add(leftPanel);

        // 3. 创建并添加 Panel B (右侧主内容)
        VisualElement rightContent = CreateRightContent();
        topSection.Add(rightContent);

        root.Add(topSection);
        
        // --- 底部区域 ---
        // 4. 创建并添加 Panel C (底部状态栏)
        VisualElement bottomBar = CreateBottomBar();
        root.Add(bottomBar);
    }
    
    // =======================================================
    // Panel A: 左侧面板 (固定宽度)
    // =======================================================
    private VisualElement CreateLeftPanel()
    {
        VisualElement panel = new VisualElement
        {
            style =
            {
                width = 150, // Panel A: 固定宽度
                backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f)),
                flexShrink = 0, // 不收缩
                paddingLeft = 5
            }
        };

        Label header = new Label("Panel A: Left List") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
        panel.Add(header);
        
        // 示例内容
        Button addButton = new Button(() => { Debug.Log("A Clicked"); }) { text = "Add Item" };
        panel.Add(addButton);
        
        return panel;
    }
    
    // =======================================================
    // Panel B: 右侧内容 (占据剩余空间)
    // =======================================================
    private VisualElement CreateRightContent()
    {
        VisualElement content = new VisualElement
        {
            style =
            {
                flexGrow = 1, // Panel B: 占据水平剩余所有空间
                backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f)),
                paddingLeft = 10
            }
        };

        Label titleLabel = new Label("Panel B: Main Content") { style = { fontSize = 20, unityFontStyleAndWeight = FontStyle.Bold, color = Color.white } };
        content.Add(titleLabel);
        
        // 示例内容
        Button clickButton = new Button(() => { Debug.Log("B Clicked"); }) { text = "Main Action" };
        content.Add(clickButton);
        
        return content;
    }

    // =======================================================
    // Panel C: 底部工具栏/状态栏 (占据底部固定高度)
    // =======================================================
    private VisualElement CreateBottomBar()
    {
        VisualElement bar = new VisualElement
        {
            style = 
            {
                height = 30, // Panel C: 固定高度
                backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f)),
                flexShrink = 0, // 不允许垂直收缩
                flexDirection = FlexDirection.Row, // 子元素水平排列
                alignItems = Align.Center,
                paddingLeft = 10
            }
        };

        Label statusLabel = new Label("Panel C: Status Bar Ready.");
        bar.Add(statusLabel);

        Button clickButton = new Button(() => { Debug.Log("ssss"); }){text = "你好世界"};
        bar.Add(clickButton);
        return bar;
    }
}