#if UNITY_2021_OR_NEWER
using HotFix.UtilTool;
using Spine.Unity;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;


[InitializeOnLoad]
public static class AddComponentListen
{
    static AddComponentListen()
    {
        // 确保只在编辑器状态注册
        if (!Application.isPlaying || PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            ObjectFactory.componentWasAdded -= OnComponentAdded;
            ObjectFactory.componentWasAdded += OnComponentAdded;
        }
    }

    private static void OnComponentAdded(Component component)
    {
        // 保险起见，判断当前是否处于编辑器且非播放模式
        if (Application.isPlaying)
            return;
        
        // 仅处理编辑器手动添加的 Image
        switch (component)
        {
            //case UnityEngine.UI.Image img:
            //   // img.raycastTarget = false;
            //    EditorUtility.SetDirty(img);
            //    break;

            case TMP_Text text:
               
                text.fontSize = 36;
                text.alignment = TextAlignmentOptions.Center;
                text.horizontalAlignment = HorizontalAlignmentOptions.Center;
                text.verticalAlignment = VerticalAlignmentOptions.Middle;
                text.color = Color.white;
                text.raycastTarget = false;
                text.enableWordWrapping = true;
                text.overflowMode = TextOverflowModes.Overflow;
                text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/global/Fonts/SFArabic SDF.asset");
                EditorUtility.SetDirty(text);
                break;
            case Text text:
                
                text.fontSize = 36;
                text.color = Color.white;
                text.raycastTarget = false;
                EditorUtility.SetDirty(text);
                break;
            
            case Empty4Raycast CusImage:
                var btn = component.GetComponent<Button>();
                if(btn != null && btn.targetGraphic == null)
                {
                    btn.targetGraphic = CusImage;
                }
                break;

            case Image image:
                btn = component.GetComponent<Button>();
                if (btn != null && btn.targetGraphic == null)
                {
                    btn.targetGraphic = image;
                }
                break;
           
        }
    }
}
#endif