#if UNITY_2021_OR_NEWER
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;


[InitializeOnLoad]
public static class AddComponentListen
{
    static AddComponentListen()
    {
        // 确保只在编辑器状态注册
        if (!Application.isPlaying)
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
                EditorUtility.SetDirty(text);
                break;
            case Text text:
                
                text.fontSize = 36;
                text.color = Color.white;
                text.raycastTarget = false;
                EditorUtility.SetDirty(text);
                break;

                
        }

        
    }
}
#endif