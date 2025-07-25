using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;
/// <summary>
/// 实现一个unity控件工具箱，方便拖拽到hierarchy面板上进行实例化
/// 但是需要注意 默认拖拽后unity系统生成的预设没有断开。
/// </summary>
public class ToolboxWindow : EditorWindow
{
    public int maxColume = 5;
    private Vector2 _scrollPosition;
    private Dictionary<int, List<GameObject>> prefabMap = new Dictionary<int, List<GameObject>>();
    public GameObject currentSelPrefab;

    private enum TabType
    {
        Button,
        List,
        CommonUI,
        Bg,
    }
    private TabType currentTab = TabType.Button;

    [MenuItem("Window/Toolbox")]
    public static void ShowWindow()
    {
        GetWindow<ToolboxWindow>("Toolbox").titleContent = new GUIContent("工具箱");
    }

    private void OnEnable()
    {
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        prefabMap.Clear();

        foreach (TabType item in Enum.GetValues(typeof(TabType)))
        {
            if (!prefabMap.ContainsKey((int)item))
            {
                prefabMap.Add((int)item, new List<GameObject>());
            }
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { $"Assets/Editor/Prefabs/{item}" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {

                    prefabMap[(int)item].Add(prefab);

                }
            }
        }




    }
    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
    }

    private void OnHierarchyChanged()
    {
        // 查找所有 Scene 中的 prefab 实例，尝试断开
        TryDisconnectPrefab(Selection.activeGameObject);
        if (Selection.activeObject != null)
        {
            EditorGUIUtility.SetIconForObject(Selection.activeObject, null);
        }
    }

    private void TryDisconnectPrefab(GameObject go)
    {
        if (go == null) return;
        if (PrefabUtility.IsPartOfPrefabInstance(go))
        {
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Debug.Log($"断开实例化对象与Prefab的关联: {go.name}");
        }

        // 遍历子物体
        foreach (Transform child in go.transform)
        {
            TryDisconnectPrefab(child.gameObject);
        }
    }

    private void OnGUI()
    {
        // 页签工具栏
        currentTab = (TabType)GUILayout.Toolbar((int)currentTab, new string[] {
            "按钮",
            "列表",
            "一般ui","背景"}
        );
        GUILayout.Label("拖拽下列预制体到 Hierarchy 面板,选中可定位，双击打开模版预设", EditorStyles.boldLabel);
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        DrawTab((int)currentTab);

        EditorGUILayout.EndScrollView();
    }

    void DrawTab(int tabId)
    {
        var _prefabs = prefabMap[tabId];
        GUILayout.BeginVertical();
        for (int i = 0; i < _prefabs.Count;)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < i + maxColume && j < _prefabs.Count; j++)
            {
                DrawDraggableItem(_prefabs[j]);
                GUILayout.Space(10); // 每个 item 后加一些空隙
            }
            GUILayout.EndHorizontal();
            i += maxColume;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawDraggableItem(GameObject prefab)
    {
        // 获取缩略图（优先大图，没有就小图）
        Texture2D preview = AssetPreview.GetAssetPreview(prefab);
        if (preview == null)
        {
            preview = AssetPreview.GetMiniThumbnail(prefab);
        }
        Rect boxRect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(false));
        if (currentSelPrefab == prefab)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 0f, 1f);
            GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);
            GUI.color = oldColor;
        }
        else
        {
            GUI.Box(boxRect, GUIContent.none); // 背景
        }





        // 绘制图标（居中）
        if (preview != null)
        {
            Rect imgRect = new Rect(
                boxRect.x + (boxRect.width - 80) / 2,
                boxRect.y + 5,
                80,
                80
            );
            GUI.DrawTexture(imgRect, preview, ScaleMode.ScaleToFit);
        }

        // 绘制名字
        Rect labelRect = new Rect(
            boxRect.x + 5,
            boxRect.y + 60,
            boxRect.width - 10,
            20
        );
        GUI.Label(labelRect, prefab.name, EditorStyles.centeredGreyMiniLabel);

        // 拖拽处理
        Event evt = Event.current;
        if (boxRect.Contains(evt.mousePosition))
        {
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {

                currentSelPrefab = prefab;
                GUI.FocusControl(null);
                Repaint();
                if (evt.clickCount == 1)
                {

                    EditorGUIUtility.PingObject(prefab);
                    
                }
                // 双击检测
                if (evt.clickCount == 2)
                {
                    AssetDatabase.OpenAsset(prefab); // 双击打开
                    evt.Use();
                    return;
                }




                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new UnityEngine.Object[] { prefab }; // 原始 prefab
                DragAndDrop.StartDrag(prefab.name);

                evt.Use();
            }
            // 当拖拽完成到 Hierarchy 面板时
            if (evt.type == EventType.DragPerform)
            {
                if (DragAndDrop.GetGenericData("ToolboxPrefabPath") is string path)
                {
                    GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (sourcePrefab != null)
                    {
                        GameObject go = GameObject.Instantiate(sourcePrefab);
                        go.name = sourcePrefab.name;
                        Undo.RegisterCreatedObjectUndo(go, "Instantiate Prefab (Unlinked)");

                        Selection.activeObject = go;
                    }
                }

                DragAndDrop.AcceptDrag();
                DragAndDrop.SetGenericData("ToolboxPrefabPath", null);
                evt.Use();
            }
        }
    }
}
