using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using UnityEditor.AnimatedValues;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HotFix;
using TMPro;
using System.Text.RegularExpressions;
using UnityEditor.SceneManagement;

using HotFix.Manager.Window.Scene;
using HotFix.Manager.Window;
using HotFix.CComponent.UIWidgets.UtilitesFolder;
using RuntimePrefabEditor;

public class GenerateScriptWindow : EditorWindow
{
    AnimBool showInfo = new AnimBool(true);
    private GUIStyle titleStyle;
    private int _selectedIndex = 0; // 下拉框选中的索引
    private string[] _options;
    private static Dictionary<string, List<string>> _index;
    private bool _isIncludeListener = true;
    private bool _isAsync = true;
    private bool _isMainPage = false;
    private bool _isOpenAnim = true;
    private string module = ""; 
    private Dictionary<int, UIType> _enumMap;
    private string prefabPath;
    private GameObject selectPrefabRoot;
    private string csPath;
    private bool isCsInitAttribute;
    public static string preFix = "GenCode_";
    private void OnDestroy()
    {
       
        _index?.Clear();
        _index = null;
    }

    public static List<string> GetSelectPrefabPath(GameObject instance)
    {
        
        if (_index == null)
        {
            _index = new Dictionary<string, List<string>>();
            string[] assetGUIDs = AssetDatabase.FindAssets("t:prefab");
            foreach (string guid in assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                string filename = System.IO.Path.GetFileNameWithoutExtension(path);
                List<string> paths = null;
                if (!_index.TryGetValue(filename.ToLower(), out paths))
                {
                    paths = new List<string>();
                    //可能会有同名的预设 ，但是路径不相同 
                    _index[filename.ToLower()] = paths;
                }
                paths.Add(path); // 这里可以加一个排重 
            }
        }

        
        List<string> candidates = new List<string>();

        GameObject instanceRoot = instance;
        while (instanceRoot != null)
        {
            string candidateName = EditorUtils.WithoutClonePostfix(instanceRoot.name).ToLower();

            List<string> paths;
            if (_index.TryGetValue(candidateName, out paths))
            {
                foreach (var path in paths)
                {
                    GameObject prefabRoot = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
                    if (!prefabRoot) continue;

                    var prefabPath = EditorUtils.GetPathForObjectInHierarchy(instance, instanceRoot);
                    GameObject prefab = prefabRoot.GetChildByPath(prefabPath);

                    if (prefab == null) { continue; }
                    candidates.Add(path);
                }
            }

            instanceRoot = instanceRoot.GetParent();
        }

        // longer path is preferable
        if (candidates.Count > 0)
        {
            candidates.Sort((c1, c2) => c2.Length.CompareTo(c1.Length));
        }

        return candidates;
    }

    List<string> allCsFiles = new List<string>();
    private void Awake()
    {
        allCsFiles =  AssetDatabase.FindAssets("t:script", new string[] { "Assets/hot_fix" })
            .Select((xx)=>AssetDatabase.GUIDToAssetPath(xx))
            .OrderBy(l=>l).ToList();

        titleStyle = new GUIStyle();
        titleStyle.fontSize = 20;
        titleStyle.normal.textColor = Color.yellow;
        _enumMap = Enum.GetValues(typeof(UIType)).Cast<UIType>().ToDictionary(e => (int)e, e => e);
        _options = new string[_enumMap.Count];
        foreach (var type in _enumMap)
        {
            string layerName = Enum.GetName(typeof(UIType), type.Value);
            _options[type.Key] = layerName;
        }

        if (EasyUseEditorFuns.IsFormatCorrect(out bool isInPrefabStage) )
        {
            if(isInPrefabStage)
            {

                string[] assetGUIDs = AssetDatabase.FindAssets("t:prefab");
                if(_index == null)
                {
                    _index = new Dictionary<string, List<string>>();
                }
                
                foreach (string guid in assetGUIDs)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    string filename = System.IO.Path.GetFileNameWithoutExtension(path);
                    List<string> paths = null;
                    if (!_index.TryGetValue(filename.ToLower(), out paths))
                    {
                        paths = new List<string>();
                        //可能会有同名的预设 ，但是路径不相同 
                        _index[filename.ToLower()] = paths;
                    }
                    paths.Add(path); // 这里可以加一个排重 
                }

                var activeGameObject = Selection.activeObject as GameObject;


                var rootGo = activeGameObject.transform;
                while(rootGo != null )
                {
                    if(Regex.IsMatch(rootGo.parent.name, @"(Environment)"))
                    {
                        break;
                    }
                    rootGo = rootGo.parent;
                }


                var name = rootGo.name;
                
                var listPrfab = GetSelectPrefabPath(rootGo.gameObject);
                prefabPath = listPrfab[0];
                selectPrefabRoot = rootGo.gameObject;
                LoadExitProperty(listPrfab[0]);
            }
            else
            {
                selectPrefabRoot = (Selection.activeObject as GameObject);
                string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                prefabPath = path;
                LoadExitProperty(path);
            }

            
        }
    }
    
    

    private void OnGUI()
    {
        if (EasyUseEditorFuns.IsFormatCorrect(out bool isInPrefabStage) == false )
        {
            Close();
            EditorUtility.DisplayDialog("Tips", $"Please select prefab file", "ok");
        }

        DrawInfo();
        
        GUILayout.Space(100);
        if (GUILayout.Button("Generator", EditorStyles.miniButtonLeft, GUILayout.MinWidth(90f)))
        {
            Close();
            int layerIndex = _selectedIndex;
            UIType type = GetEnumByValue(layerIndex);
            Debug.LogWarning(
                $"toggleValue:{_isIncludeListener},_isAsync:{_isAsync},_isMainPage:{_isMainPage},type:{type},_isOpenAnim:{_isOpenAnim}");
            if(string.IsNullOrEmpty(module))
            {
                this.ShowNotification(new GUIContent("请设置module！"));
                Debug.LogError("请设置module！");
            }
            ScriptGenerator.AutoGenerateScript(_isAsync, _isMainPage, _isIncludeListener, type, _isOpenAnim,module);
        }
        GUILayout.Space(20);
        //清理生成的c#文件 
        if (GUILayout.Button("clean cs", EditorStyles.miniButtonLeft, GUILayout.MinWidth(90f)))
        {
            if (File.Exists(csPath))
            {
                SafeDeleteUnityResHook.forbidHook = true;
                if(!string.IsNullOrEmpty(module))
                {
                    var csName = System.IO.Path.GetFileName(csPath);
                    var csFolderPath = System.IO.Path.GetDirectoryName(csPath);
                    var csGenCodePath = Path.Combine(  csFolderPath , preFix , csName);
                    File.Delete(csPath); File.Delete(csPath + ".meta");
                    File.Delete(csGenCodePath); File.Delete(csGenCodePath + ".meta");
                    AssetDatabase.Refresh();
                    EditorUtility.RequestScriptReload();
                }
                else
                {
                    if (csPath.Contains("generate"))
                    {
                        var csGenCodePath = csPath;
                        var csInterface = csPath.Replace("/generate", "");
                        File.Delete(csPath); File.Delete(csPath + ".meta");
                        File.Delete(csInterface); File.Delete(csInterface + ".meta");
                        AssetDatabase.Refresh();
                        EditorUtility.RequestScriptReload();
                    }
                    else
                    {
                        var csGenCodePath = csPath.Replace("interface", "interface/generate");
                        File.Delete(csPath); File.Delete(csPath + ".meta");
                        File.Delete(csGenCodePath); File.Delete(csGenCodePath + ".meta");
                        AssetDatabase.Refresh();
                        EditorUtility.RequestScriptReload();
                    }
                }
               
                SafeDeleteUnityResHook.forbidHook = false;
            }
        }
    }

    void LoadExitProperty(string path)
    {
        Debug.Log("  LoadExitProperty  " + path);
        string filename = Path.GetFileNameWithoutExtension(path);
        Debug.Log("  LoadExitProperty  " + filename);
        string typeName = "Assets.Interface." + filename;
        var _gameAss = AppDomain.CurrentDomain.GetAssemblies()
            .First(assembly => assembly.GetName().Name == "hot_fix");

        var type = _gameAss.GetType(typeName);
        if (type == null)
            type = _gameAss.GetTypes().FirstOrDefault((tt) => tt.Name == filename);
        
        if (type == null)
        {
            
            Debug.LogError($"没有找到类型：{typeName}");
            return;
        }

        var attribute = type.GetCustomAttribute<ViewAttribute>();
        if (attribute == null)
        {
            Debug.LogError($"没有找到ui属性");
            return;
        }

        _isAsync = attribute.isAsync;
        _isMainPage = attribute.isMainPage;
        var GetWindowLayer = type.GetMethod("GetWindowLayer");
        if (GetWindowLayer == null)
        {
            Debug.LogError($"没有找到方法:GetWindowLayer");
            return;
        }

        UIType deepth = (UIType)GetWindowLayer.Invoke(Activator.CreateInstance(type), null);
        string layerName = Enum.GetName(typeof(UIType), deepth);
        for (int i = 0; i < _options.Length; i++)
        {
            if (layerName == _options[i])
            {
                _selectedIndex = i;
                break;
            }
        }
    }
    private void DrawAttributeField(string name, object value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, GUILayout.Width(120));

        try
        {
            var valueType = value?.GetType();
            if (valueType != null)
            {
                value = valueType switch
                {
                    Type t when t == typeof(int) => EditorGUILayout.IntField((int)value),
                    Type t when t == typeof(float) => EditorGUILayout.FloatField((float)value),
                    Type t when t == typeof(string) => EditorGUILayout.TextField((string)value),
                    Type t when t == typeof(bool) => EditorGUILayout.Toggle((bool)value),
                    Type t when t == typeof(Vector2) => EditorGUILayout.Vector2Field("", (Vector2)value),
                    Type t when t == typeof(Vector3) => EditorGUILayout.Vector3Field("", (Vector3)value),
                    Type t when t == typeof(Color) => EditorGUILayout.ColorField((Color)value),
                    Type t when t.IsEnum => EditorGUILayout.EnumPopup((Enum)value),
                    _ => DrawCustomTypeField(valueType)
                };
            }
            else
            {
                GUILayout.Label("null");
            }
        }
        catch
        {
            GUILayout.Label($"Unsupported type: {value?.GetType().Name}");
        }

        GUILayout.EndHorizontal();
    }

    private object DrawCustomTypeField(Type type)
    {
        // 这里可以处理自定义类型，暂时返回 null
        return null;
    }
    private void DrawInfo()
    {
        GUILayout.Space(20);
        string path = "";
        if (!string.IsNullOrEmpty(prefabPath))
        {
            path = prefabPath;
        }
        else
        {
            path = AssetDatabase.GetAssetPath(Selection.activeObject);
        }
        
        EditorTools.DrawSeparator();
        GUILayout.Space(5);

        if(!isCsInitAttribute)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var csName = path.Substring(path.LastIndexOf("/") + 1, path.LastIndexOf(".prefab") - path.LastIndexOf("/") - 1);
            var csFile = allCsFiles.Find((xx) => xx.EndsWith(csName + ".cs"));
            Type csType = null;
            if (File.Exists(csFile))
            {
                csPath = csFile;
                // 遍历每个程序集
                foreach (Assembly assembly in assemblies)
                {
                    if (assembly.GetName().Name == "hot_fix")
                    {
                        var allTypes = assembly.GetTypes();

                        foreach (var oneType in allTypes)
                        {
                            if (oneType.Name == csName)
                            {
                                csType = oneType;
                                break;
                            }
                        }
                        break;
                    }

                }
                if(csType == null)
                {
                    ShowNotification(new GUIContent("遍历程序集出错"));
                }
                var viewAtt = csType?.GetCustomAttribute<ViewAttribute>();
                _isAsync = viewAtt.isAsync;
                module = viewAtt.module;
                _isMainPage = viewAtt.isMainPage;
                path = viewAtt.prefabPath;
                isCsInitAttribute = true;
            }
        }
        if (EditorGUILayout.BeginFadeGroup(showInfo.faded))
        {
            GUILayout.Space(5);
            EditorGUILayout.LabelField("Prefab:", $"{selectPrefabRoot}", GUILayout.ExpandWidth(true));
            GUILayout.Space(5);
            EditorGUILayout.LabelField("Path:", $"{path}");
            GUILayout.Space(5);

            GUILayout.Space(5);
            EditorGUILayout.LabelField("csPath:", $"{csPath}");
            GUILayout.Space(5);

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
             EditorGUILayout.LabelField("module:", module);
            if(string.IsNullOrEmpty(module))
            {
                module = Path.GetDirectoryName(csPath);
                module = module.Substring(module.IndexOf("Assets/"));
                module = module.Substring("Assets/".Length);
                if (module.Contains("hot_fix"))
                {
                    module = module.Replace("hot_fix", "HotFix");
                }
                if (module.Contains("interface"))
                {
                    module = module.Replace("interface", "Interface");
                }
            }
            if(GUILayout.Button("...",GUILayout.Width(50)))
            {
                var oldModule = module;
                if(string.IsNullOrEmpty(module))
                {
                    module = EditorUtility.OpenFolderPanel("模块名", "Assets/hot_fix/module", "");
                }
                else
                {
                    
                    module = EditorUtility.OpenFolderPanel("模块名", Path.GetDirectoryName(csPath), "");
                }
                
                if(string.IsNullOrEmpty(module))
                {
                    module = oldModule;
                }
                else
                {
                    module = module.Substring(module.IndexOf("Assets/"));
                    module = module.Substring("Assets/".Length );
                    if (module.Contains("hot_fix"))
                    {
                        module = module.Replace("hot_fix", "HotFix");
                    }
                    if (module.Contains("interface"))
                    {
                        module = module.Replace("interface", "Interface");
                    }
                }
               
            }
            

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            _isIncludeListener = EditorGUILayout.Toggle("IncludeListener:", _isIncludeListener);
            GUILayout.Space(5);
            // 使用反射获取相关的属性  
            
            _isAsync = EditorGUILayout.Toggle("IsAsync:", _isAsync);
            GUILayout.Space(5);
            _isMainPage = EditorGUILayout.Toggle("IsMainPage:", _isMainPage);
            GUILayout.Space(5);
            // 使用反射获取相关的属性   end 
            if(_options.Length > 0 && _options[0] == null)
            {
                foreach (var type in _enumMap)
                {
                    string layerName = Enum.GetName(typeof(UIType), type.Value);
                    _options[type.Key ] = layerName;
                }
            }
            _selectedIndex = EditorGUILayout.Popup("Layer:", _selectedIndex, _options);
            GUILayout.Space(5);
            _isOpenAnim = EditorGUILayout.Toggle("IsOpenAnim:", _isOpenAnim);
            GUILayout.Space(5);

            if (GUI.changed)
            {
                Debug.Log("Selected option: " + _options[_selectedIndex]);
            }
        }

        EditorGUILayout.EndFadeGroup();
    }

    UIType GetEnumByValue(int value)
    {
        return _enumMap.TryGetValue(value, out UIType result) ? result : UIType.SubNormal;
    }

    public void OnInspectorUpdate()
    {
        this.Repaint();
    }
}