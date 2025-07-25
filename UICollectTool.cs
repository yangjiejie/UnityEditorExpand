using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class UICollectTool : EditorWindow
{
    private Texture2D iconTexture;
    private string iconPath = "";

    public enum SaveTag
    {
        button = 1,
        list = 2,
        commonUI = 3,
        bg = 4,
    }

    SaveTag _saveTag = SaveTag.button;

    private void OnGUI()
    {
        GUILayout.Label("配置图标", EditorStyles.boldLabel);

        // 方式 1：直接选择 Texture2D
        iconTexture = (Texture2D)EditorGUILayout.ObjectField("图标 Texture", iconTexture, typeof(Texture2D), false);

        EditorGUILayout.Space();

        // 方式 2：填写路径
        EditorGUILayout.LabelField("图标路径 (支持拖拽)：");
        Rect dropArea = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, iconPath, EditorStyles.textField);

        // 支持拖拽路径
        Event evt = Event.current;
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var dragged in DragAndDrop.paths)
                    {
                        if (dragged.EndsWith(".png") || dragged.EndsWith(".jpg"))
                        {
                            iconPath = dragged;
                            iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(dragged);
                            break;
                        }
                    }
                }
                evt.Use();
            }
        }

        EditorGUILayout.Space(15);
        GUI.enabled = Selection.activeGameObject != null;
        if(iconTexture == null)
        {
            iconTexture = GetIcon(Selection.activeGameObject);
        }
        

        _saveTag = (SaveTag)EditorGUILayout.EnumPopup("保存类型", _saveTag);

        if (GUILayout.Button("将选中对象保存为预设"))
        {
            SavePrefabWithIcon();
        }
        if(GUILayout.Button("打开工具箱"))
        {
            EditorWindow.GetWindow<ToolboxWindow>("工具箱");
        }

        GUI.enabled = true;
    }
    internal Texture2D GetIcon(GameObject selected)
    {
        var images = selected.GetComponentsInChildren<Image>(false);
        foreach (Image image in images)
        {
            if (image != null && image.sprite != null)
            {
                var texture2dPath = AssetDatabase.GetAssetPath(image.sprite);
                iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texture2dPath);
                break;
            }
        }
        //实战中居然有按钮是rawImage 
        if (iconTexture == null)
        {
            var raws = selected.GetComponentsInChildren<RawImage>(false);
            foreach (var raw in raws)
            {
                if (raw != null && raw.texture != null)
                {
                    var texture2dPath = AssetDatabase.GetAssetPath(raw.texture);
                    iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texture2dPath);
                    break;
                }
            }
        }

        return iconTexture;
    }
    private void SavePrefabWithIcon()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("错误", "请先在场景中选中一个对象", "OK");
            return;
        }

        // 确保目录存在
        string savePath = $"Assets/Editor/Prefabs/{_saveTag.ToString()}";
        if (!AssetDatabase.IsValidFolder(savePath))
        {
            Directory.CreateDirectory(savePath);
            AssetDatabase.Refresh();
        }

        // 生成唯一路径
        string prefabName = selected.name + ".prefab";
        string fullPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(savePath, prefabName));

        // 创建 prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(selected, fullPath, out bool sucess);
        if(!sucess)
        {
            Debug.LogError("生成预设失败");
            return;
        }

        if (prefab != null)
        {
            Debug.Log($"✅ 预设已保存: {fullPath}");

            // 给 prefab 设置图标
            if (iconTexture != null)
            {
                SetIcon(prefab, iconTexture);
            }
            else
            {
                iconTexture = GetIcon(selected);
                SetIcon(prefab, iconTexture);
            }

            Selection.activeObject = prefab;
        }
        else
        {
            Debug.LogError("❌ 保存预设失败");
        }
    }

    /// <summary>
    /// 给物体设置自定义图标（仅编辑器中显示）
    /// </summary>
    private void SetIcon(GameObject go, Texture2D icon)
    {
        EditorGUIUtility.SetIconForObject(go, icon);
    }
}
