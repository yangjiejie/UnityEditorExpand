using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

[CustomEditor(typeof(RectTransform)), CanEditMultipleObjects]
//RectTransformInspector 理论上应该继承 RectTransformEditor 但是unity把它
// 定义成了Internal 
public class RectTransformInspector : Editor
{
    private Editor baseEditor;
    private Type originalEditorType;
    private float scaleRatio = 1f;
    private bool showScaler = false;
    private static readonly string kInitialSizeKey = "RectScaler_InitialSize_";

    private void OnEnable()
    {
        // 获取 Unity 内部 RectTransformEditor 类型
        originalEditorType = Type.GetType("UnityEditor.RectTransformEditor, UnityEditor");

        if (originalEditorType != null)
        {
            baseEditor = CreateEditor(targets, originalEditorType);
        }
    }

    private void OnDisable()
    {
        if (baseEditor != null)
        {
            DestroyImmediate(baseEditor);
        }
    }

    public override void OnInspectorGUI()
    {
        if (baseEditor != null)
        {
            baseEditor.OnInspectorGUI(); // 绘制原始的 RectTransform Inspector
        }
        else
        {
            EditorGUILayout.HelpBox("无法加载 RectTransform 原始 Inspector。", MessageType.Warning);
            base.OnInspectorGUI();
        }

        // 你自己的等比缩放功能
        EditorGUILayout.Space();
        showScaler = EditorGUILayout.Foldout(showScaler, "等比缩放宽高工具");

        if (showScaler)
        {
            EditorGUILayout.LabelField("输入比例，例如：", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("1 = 原始宽高，0.5 = 一半大小", EditorStyles.miniLabel);

            scaleRatio = EditorGUILayout.Slider("缩放比例", scaleRatio, 0.1f, 10f);

            if (GUILayout.Button("应用等比缩放"))
            {
                ApplyScaleToSelection(scaleRatio);
            }

            if (GUILayout.Button("重置原始尺寸缓存"))
            {
                ResetInitialSizeCache();
            }
        }
    }

    void ApplyScaleToSelection(float scale)
    {
        foreach (var obj in targets)
        {
            var rt = obj as RectTransform;
            if (rt == null) continue;

            Undo.RecordObject(rt, "Scale RectTransform Size");

            // 从缓存或当前尺寸中获取原始尺寸
            Vector2 originSize = GetOrStoreInitialSize(rt);

            // 计算新尺寸
            rt.sizeDelta = originSize * scale;
        }
    }

    Vector2 GetOrStoreInitialSize(RectTransform rt)
    {
        string key = kInitialSizeKey + rt.GetInstanceID();
        if (EditorPrefs.HasKey(key + "_w") && EditorPrefs.HasKey(key + "_h"))
        {
            float w = EditorPrefs.GetFloat(key + "_w");
            float h = EditorPrefs.GetFloat(key + "_h");
            return new Vector2(w, h);
        }
        else
        {
            Vector2 size = rt.sizeDelta;
            EditorPrefs.SetFloat(key + "_w", size.x);
            EditorPrefs.SetFloat(key + "_h", size.y);
            return size;
        }
    }

    void ResetInitialSizeCache()
    {
        scaleRatio = 1;
        ApplyScaleToSelection(scaleRatio);
        foreach (var obj in targets)
        {
            string key = kInitialSizeKey + obj.GetInstanceID();
            EditorPrefs.DeleteKey(key + "_w");
            EditorPrefs.DeleteKey(key + "_h");
        }
        Debug.Log("初始尺寸缓存已清空。");
    }
}
