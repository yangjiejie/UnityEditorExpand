
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using Spine.Unity;
using System.Linq;

using System.Text.RegularExpressions;
using HotFix.CComponent;
using HotFix.Manager.Window;
using HotFix.Module;
using UnityEngine.UI;
using Microsoft.CSharp;
using HotFix.UtilTool;
using UnityEditor.SceneManagement;
using Launcher;

[InitializeOnLoad]
public static class DrawHierarchyIcon
{
    static Texture2D icon;

    static DrawHierarchyIcon()
    {
        DisableDrawIcon();
        EnableDrawIcon();
    }

    public static void EnableDrawIcon()
    {
#if EDITOR_OPEN_HIERARCHY_DRAW_ICON
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGUI;
#endif
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyIcon;
    }

    public static void DisableDrawIcon()
    {
    #if EDITOR_OPEN_HIERARCHY_DRAW_ICON
        EditorApplication.hierarchyWindowItemOnGUI -= HierarchyWindowItemOnGUI;
    #endif
        EditorApplication.hierarchyWindowItemOnGUI -= HierarchyIcon;
    }

    private static void HierarchyIcon(int instanceID, Rect selectionRect)
    {
#if DEV_MODE

#else

  //   if (!Application.isPlaying) return;
#endif
        Rect r = new Rect(selectionRect);

        r.x = 5.0f;

        //调整大小的
        r.width = 20;
        //EditorUtility.InstanceIDToObject是通过一个id来变换成一个GameObj
        GameObject go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (go != null )
        {
            if(go.GetComponent<UIPanelBase>() != null)
            {
                Rect position = new Rect(selectionRect);
                position.x = position.width + (selectionRect.x - 80);
                position.width = 80;
                position.height = 16f;

                Rect pos1 = new Rect(position);
                pos1.x -= 60;
                pos1.width = 60;

                if(GUI.Button(pos1,"打开预设"))
                {
                    var pathList = EasyUseEditorFuns.GetSelectPrefabPath(go);
                    var fileNode = System.IO.Path.GetFileNameWithoutExtension(pathList[0]);
                    var resGo = AssetDatabase.LoadAssetAtPath<GameObject>(pathList[0]);
                    var cacheObj = Selection.activeObject;
                    var path = CommonUtils.GetHierarchyPath(cacheObj as GameObject);
                    var nodePath = path.Substring(path.IndexOf(fileNode));
                    nodePath = nodePath.Replace(fileNode+"/", "");
                    AssetDatabase.OpenAsset(resGo); // 双击打开
                    //定位到之前选中的结点 
                    EditorCoroutine.StartCoroutine(new EditorWaitForSeconds(0.2f, () =>
                    {
                        var root = PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot;
                        var highGo = root.transform.Find(nodePath);
                        Selection.activeObject = highGo;
                        EditorGUIUtility.PingObject(highGo);
                    }));
                    
                }

                Rect pos2 = new Rect(position);
                pos2.x += 40;
                pos2.width = 60;
                GUI.Label(position, "主ui");
                if(GUI.Button(pos2, "openC#"))
                {
                    var csName = go.GetComponent<UIPanelBase>().GetType().Name;
                    var guids  = AssetDatabase.FindAssets($"t:Script {csName}", new string[] { "Assets/hot_fix" });
                    var listPath = guids.Select((x) => AssetDatabase.GUIDToAssetPath(x)).ToList<string>();
                    string targetCsFile = "";
                    foreach (var item in listPath)
                    {
                        
                        if(!item.Contains(GenerateScriptWindow.preFix))
                        {
                            targetCsFile = item;
                            break;
                        }
                    }
                    if(!string.IsNullOrEmpty(targetCsFile))
                    {
                        Application.OpenURL(System.Environment.CurrentDirectory + "/" + targetCsFile);
                    }
                }
            }
            else if(go.GetComponent<ManagedBehaviour>() != null)
            {
                var csName = go.GetComponent<ManagedBehaviour>().GetType().Name;
                Rect position = new Rect(selectionRect);
                position.x = position.width + (selectionRect.x - 180);
                position.width = 180;
                position.height = 16f;
                GUI.Label(position, $"{csName}");
            }
            else if(go.GetComponent<LangUI>() != null)
            {
                Rect position = new Rect(selectionRect);
                position.x = position.width + (selectionRect.x - 80);
                position.width = 80;
                position.height = 16f;
                GUI.Label(position, "翻译");
            }
            else if(go.GetComponent<SkeletonGraphic>() != null || go.GetComponent<SkeletonAnimation>())
            {
                Rect position = new Rect(selectionRect);
                position.x = position.width + (selectionRect.x - 80);
                position.width = 80;
                position.height = 16f;
                GUI.Label(position, "spine");
            }
            else if(go.GetComponent<Main>() != null)
            {
                Rect position = new Rect(selectionRect);
                position.x = position.width + (selectionRect.x - 80);
                position.width = 80;
                position.height = 16f;
                GUI.Label(position, "游戏入口");
            }
            else if(go.GetComponent<ListViewCommon>() != null || go.GetComponent<LoopScrollRectBase>() != null || 
                go.GetComponent<UIDynamicGoList>() != null)
            {
                Rect position = new Rect(selectionRect);
                position.x = position.width + (selectionRect.x - 80);
                position.width = 80;
                position.height = 16f;
                GUI.Label(position, "list");
            }
        }
        

    }
    private static void HierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
    {
        GameObject gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (gameObject != null)
        {
            var trans = gameObject.GetComponentsInChildren<Transform>(true);

            Rect position = new Rect(selectionRect);
            position.x = position.width + (selectionRect.x - 40);
            position.width = 40;
            position.height = 16f;
            GUI.Label(position, trans.Length.ToString());
        }
    }
}
#endif