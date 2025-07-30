using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

using HotFix.CComponent;
using HotFix.Manager.Window;
using HotFix.Manager.Window.Scene;
using RTLTMPro;
using Spine.Unity;
using TMPro;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

public class ScriptGenerator
{
    private static string gap = "/";



    [MenuItem("GameObject/ScriptGenerator/UIPropertyAndListener", priority = 49)]
    public static void MemberPropertyAndListener()
    {
        string title = "AutoGenerateScript";
        GenerateScriptWindow window = EditorWindow.GetWindow<GenerateScriptWindow>(title);
        window.minSize = new Vector2(150, 200);
        window.Show();
    }

    [MenuItem("Assets/ui代码生成", false, 2000)]
    static private void UICodeGenerate()
    {
        if(Application.isPlaying)
        {
            EditorApplication.ExecuteMenuItem("Edit/Play");
            return;
        }
        string title = "AutoGenerateScript";
        GenerateScriptWindow window = EditorWindow.GetWindow<GenerateScriptWindow>(title);
        window.minSize = new Vector2(150, 200);
        window.Show();
    }

    [MenuItem("Assets/删除spine相关的资源", false, 2000)]
    static private void DelSpineRes()
    {
        var obj = Selection.activeObject;
        var path = AssetDatabase.GetAssetPath(obj);
        var folder = System.IO.Path.GetDirectoryName(path);

        folder = EasyUseEditorFuns.GetLinuxPath(folder);

        var filePath = AssetDatabase.GetDependencies(path)
         .Where((k) => k.Contains(folder))
         .ToList();



        int index = 0;
        foreach (var item in filePath)
        {
            index++;
            Debug.Log("需要删除" + item);
            EasyUseEditorFuns.DelEditorResFromDevice(item);
        }
        Debug.Log("共计文件" + index);
        // 

    }



    [MenuItem("GameObject/ui代码生成 _F1", priority = -101)]
    static void UICodeGen()
    {
        if (Application.isPlaying)
        {
            EditorApplication.ExecuteMenuItem("Edit/Play");
            return;
        }
        string title = "AutoGenerateScript";
        GenerateScriptWindow window = EditorWindow.GetWindow<GenerateScriptWindow>(title);
        window.minSize = new Vector2(150, 200);

        window.Show();
    }


    [MenuItem("GameObject/ScriptGenerator/UISwitchGroup", priority = 49)]
    public static void UISwitchGroup()
    {
        var root = Selection.activeTransform;
        if (root == null)
        {
            return;
        }

        var content = ScriptGenerator.SwitchGroupGenerator.Instance.Process(root);
        TextEditor te = new TextEditor();
        te.text = content;
        te.SelectAll();
        te.Copy();
    }

    public static void AutoGenerateScript(bool isAsync, bool isMainPage, bool isIncludeListener, UIType layerType, bool isOpenAnim, string module)
    {
        if (EasyUseEditorFuns.IsFormatCorrect(out bool isInPrefabStage))
        {
            if (isInPrefabStage)
            {
                var listPrfab = GenerateScriptWindow.GetSelectPrefabPath(Selection.activeObject as GameObject);
                Generate(listPrfab[0], isAsync, isMainPage, isIncludeListener, isOpenAnim, layerType, module);
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                Generate(path, isAsync, isMainPage, isIncludeListener, isOpenAnim, layerType, module);
            }


        }



    }

    static private StageEnum GetStage(string path)
    {
        if (path.StartsWith("hall"))
        {
            return StageEnum.hallStage;
        }

        if (path.StartsWith("login"))
        {
            return StageEnum.loginStage;
        }

        if (path.StartsWith("global"))
        {
            return StageEnum.globalStage;
        }

        if (path.StartsWith("gameCommon"))
        {
            return StageEnum.gameCommonStage;
        }

        if (path.StartsWith("gameFruit"))
        {
            return StageEnum.gameFruitStage;
        }

        if (path.StartsWith("gameBC"))
        {
            return StageEnum.gameBCStage;
        }

        if (path.StartsWith("baloot"))
        {
            return StageEnum.gameBalootStage;
        }

        if (path.StartsWith("gameGoldStriker"))
        {
            return StageEnum.gameGoldStrikerStage;
        }
        if (path.StartsWith("jackaroo"))
        {
            return StageEnum.gameJackaroo;
        }
        return StageEnum.hallStage;
    }

    public static void Generate(string path, bool isAsync, bool isMainPage, bool includeListener, bool isOpenAnim, UIType layerType = UIType.SubNormal, string module = null)
    {
        Debug.Log($"begin generate  include {includeListener}, path = {path}");
        // path = path.Replace(".prefab", "");
        module = module.Replace("\\", "/");
        path = path.Replace("\\", "/");
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null)
        {
            go = Selection.activeGameObject;
            if (go == null)
            {
                Debug.LogError("  加载失败 ！！！  " + path);
                return;
            }
        }
        path = path.Replace("Assets/Art/", "");
        var root = go.transform;

        if (root != null)
        {
            StringBuilder strVar = new StringBuilder();
            StringBuilder strBind = new StringBuilder();
            StringBuilder strOnCreate = new StringBuilder();
            StringBuilder strCallback = new StringBuilder();
            StringBuilder strDestroy = new StringBuilder();
            var usingList = ListPool<string>.Get();
            EnterGoDic(root, root, ref strVar, ref strBind, ref strOnCreate, ref strCallback, ref strDestroy, ref usingList);

            StringBuilder strFile = new StringBuilder();
            if (includeListener)
            {
                strFile.AppendLine("using UnityEngine;");
                strFile.AppendLine("using Spine.Unity;");

                strFile.AppendLine("using UnityEngine.UI;");
                strFile.AppendLine("using TMPro;");
                strFile.AppendLine("using HotFix;");
                strFile.AppendLine("using RTLTMPro;");


                strFile.AppendLine("using HotFix.Manager.Window;");
                strFile.AppendLine("using HotFix.Manager.Window.Scene;");



                foreach (var usingItem in usingList)
                {
                    strFile.AppendLine(usingItem);
                }
                strFile.AppendLine();

                //修改命名空间 

                string codeNameSpace = module == null ? "Interface" : module.Replace('/', '.');
                if (codeNameSpace.Contains("hot_fix"))
                {
                    codeNameSpace = codeNameSpace.Replace("hot_fix", "HotFix");
                }
                var nameSpaceArray = codeNameSpace.Split('.');

                for (int i = 0; i < nameSpaceArray.Length; i++)
                {
                    nameSpaceArray[i] = CapitalizeFirstLetter(nameSpaceArray[i]);
                }
                codeNameSpace = string.Join(".", nameSpaceArray);

                strFile.Append($"namespace {codeNameSpace}" + "{\r\n");
                //修改命名空间 
                strFile.Append("\t");
                strFile.Append($"[View(StageEnum.{GetStage(path)}, \"" + path + $"\", {isAsync.ToString().ToLower()},{isMainPage.ToString().ToLower()},\"{module}\")]\r\n");
                strFile.Append("\tpartial class " + root.name + " : UIPanelBase\r\n");
                strFile.Append("\t{\r\n");
            }
            ListPool<string>.Release(usingList);
            // 脚本工具生成的代码
            strFile.Append("\t\t#region 脚本工具生成的代码\r\n");
            strFile.Append("\t\tpublic static string m_resPath = \"" + path + "\";\r\n");
            strFile.Append(strVar);
            strFile.Append("\t\tprotected override void ScriptGenerator()\r\n");
            strFile.Append("\t\t{\r\n");
            strFile.Append(strBind);
            strFile.Append(strOnCreate);
            strFile.Append("\t\t}\r\n");

            strFile.Append("\t\tprotected override void ScriptDestroy()\r\n");
            strFile.Append("\t\t{\r\n");
            strFile.Append(strDestroy);
            strFile.Append("\t\t}\r\n");
            strFile.Append("\t\t#endregion");
            strFile.AppendLine();
            strFile.AppendLine();
            if (includeListener && strCallback.Length > 0)
            {
                // #region 事件
                strFile.Append("\t\t#region 事件\r\n");
                strFile.Append(strCallback);
                strFile.Append("\t\t#endregion\r\n\r\n");
            }

            strFile.Append("\t}\r\n");
            strFile.Append("}\r\n");

            TextEditor te = new TextEditor();
            te.text = strFile.ToString();
            te.SelectAll();
            te.Copy();
            if (includeListener)
            {
                WriteFileScript(root.name, te.text, layerType, isOpenAnim, module);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 请求脚本重新加载，触发编译
            EditorUtility.RequestScriptReload();
        }
    }

    static string CapitalizeFirstLetter(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpper(input[0]) + input.Substring(1);
    }

    private static void WriteFileScript(string name, string text, UIType layerType = UIType.SubNormal, bool isOpenAnim = true, string module = null)
    {
        if (string.IsNullOrEmpty(module))
        {
            string pathDirectory = $"Assets/hot_fix/interface/generate/";
            WriteFileScriptToPath(pathDirectory, name, text);
            pathDirectory = $"Assets/hot_fix/interface/";
            FileInfo fileInfo = new FileInfo($"{pathDirectory}{name}.cs");
            if (fileInfo.Exists)
            {
                Debug.Log(" file exist ");
            }
            else
            {
                Debug.Log("FILE NOT exist so need create one ");
                string fileStr = GenerateUiBase(name, layerType, isOpenAnim);
                WriteFileScriptToPath(pathDirectory, name, fileStr);
            }

            Debug.Log($"name is {name}");
        }
        else
        {
            string pathDirectory = "";
            if (module.StartsWith("HotFix"))
            {
                var moduleModify = module.Replace("HotFix", "");
                pathDirectory = $"Assets/hot_fix/{moduleModify}/";
            }
            else
            {
                pathDirectory = $"Assets/hot_fix/{module}/";
            }


            WriteFileScriptToPath(pathDirectory, GenerateScriptWindow.preFix + name, text);

            FileInfo fileInfo = new FileInfo($"{pathDirectory}{name}.cs");
            if (fileInfo.Exists)
            {
                Debug.Log(" file exist ");
            }
            else
            {
                Debug.Log("FILE NOT exist so need create one ");
                string fileStr = GenerateUiBase(name, layerType, isOpenAnim, module);
                WriteFileScriptToPath(pathDirectory, name, fileStr);
            }

            Debug.Log($"name is {name}");
        }

        // WriteCompile2CsProj($"Assets\\hot_fix\\Interface\\generate\\{name}.cs"
        //     , $"Assets\\hot_fix\\Interface\\{name}.cs");
    }

    private static string GenerateUiBase(string name, UIType type = UIType.SubNormal, bool isOpenAnim = true, string module = null)
    {
        StringBuilder strFile = new StringBuilder();

        strFile.AppendLine("using HotFix.Manager.Window;");
        string[] baseMethod = { "OnStart", "OnShow", "OnClose", "OnRemove" };
        if (string.IsNullOrEmpty(module))
        {
            strFile.Append("namespace Assets.Interface{\n");
        }
        else
        {
            List<string> nameSpaceArr = module.Replace('/', '.').Split(".").ToList();
            nameSpaceArr = nameSpaceArr.ConvertAll((xx) => CapitalizeFirstLetter(xx));

            string codeNameSpace = module == null ? "Interface" : string.Join(".", nameSpaceArr);


            strFile.Append($"namespace {codeNameSpace}" + "{\n");
        }
        

        strFile.Append("\t");
        strFile.Append("partial class " + name + "\n");
        strFile.Append("\t{\n");

        for (int i = 0; i < baseMethod.Length; i++)
        {
            strFile.Append("\t");
            strFile.Append("\t");
            strFile.Append($"protected override void {baseMethod[i]}()" + "{\r\n");
            strFile.Append("\t");
            strFile.Append("\t");
            strFile.Append("}\r\n\r\n");
        }
        string layerName = Enum.GetName(typeof(UIType), type);
        strFile.Append($"\t\tprotected UIType windowLayer = UIType.{layerName};" + "\r\n");
        strFile.Append("\t\tpublic override UIType GetWindowLayer() {{ return windowLayer; }}" + "\r\n\r\n");

        strFile.Append($"\t\tpublic override bool IsOpenAnim => {isOpenAnim.ToString().ToLower()};" + "\r\n");

        strFile.Append("\t\t}\r\n");
        strFile.Append("\t}\r\n");
        return strFile.ToString();
    }

    private static void WriteCompile2CsProj(params string[] fullPath)
    {
        XmlDocument document = new XmlDocument();
        document.Load("./hot_fix.csproj");
        var xmlElement = document.ChildNodes[1].ChildNodes[4];
        var compileList = xmlElement.ChildNodes;
        bool[] find = new bool[fullPath.Length];
        for (int i = 0; i < compileList.Count; i++)
        {
            var xmlNode = compileList[i];

            var xmlNodeName = xmlNode.Name;

            if (!xmlNodeName.Equals("Compile"))
            {
                continue;
            }

            Debug.Log($" xmlname name is {xmlNodeName}");

            for (int j = 0; j < fullPath.Length; j++)
            {
                Debug.Log($" find full path i   and  value is {fullPath[j]}");

                Debug.Log($" attr include  full path i   and  value is {xmlNode.Attributes["Include"].Value}");
                if (xmlNode.Attributes["Include"].Value.Equals(fullPath[j]))
                {
                    find[j] = true;
                    Debug.Log($" find result  {j} value is {fullPath[j]}");
                    break;
                }
            }
        }

        for (int i = 0; i < find.Length; i++)
        {
            Debug.Log($"find data is {find[i]}");
            if (!find[i])
            {
                Debug.Log(" cteate new node ==========");
                XmlElement element = document.CreateElement("Compile");
                element.SetAttribute("Include", fullPath[i]);
                xmlElement.AppendChild(element);
            }
        }

        if (find.Any(e => e == false))
        {
            document.Save("./hot_fix.csproj");
        }
    }


    private static void WriteFileScriptToPath(string filePath, string name, string text)
    {
        text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
        var pathName = $"{filePath}{name}.cs";
        EasyUseEditorFuns.CreateDir(Path.GetDirectoryName(pathName));
        File.WriteAllText(pathName, text, Encoding.UTF8);
        Debug.Log(" write file script  " + name);
        //var fileStream = File.Open($"{filePath}{name}.cs", FileMode.Create);
        //byte[] result = Encoding.UTF8.GetBytes(text);
        //fileStream.Write(result, 0, result.Length);
        //fileStream.Flush();
        //fileStream.Close();
    }


    private static void EnterGoDic(Transform root, Transform transform, ref StringBuilder strVar,
        ref StringBuilder strBind, ref StringBuilder strOnCreate, ref StringBuilder strCallback,
        ref StringBuilder strOnDestroy, ref List<string> usingList)
    {
        for (int i = 0; i < transform.childCount; ++i)
        {
            Transform child = transform.GetChild(i);
            WriteScript(root, child, ref strVar, ref strBind, ref strOnCreate, ref strCallback, ref strOnDestroy,
                ref usingList);
            if (child.name.StartsWith("m_item"))
            {
                continue;
            }

            EnterGoDic(root, child, ref strVar, ref strBind, ref strOnCreate, ref strCallback, ref strOnDestroy,
                ref usingList);
        }
    }

    private static string GetRelativePath(Transform child, Transform root)
    {
        StringBuilder path = new StringBuilder();
        path.Append(child.name);
        while (child.parent != null && child.parent != root)
        {
            child = child.parent;
            path.Insert(0, gap);
            path.Insert(0, child.name);
        }

        return path.ToString();
    }

    private static string GetBtnFuncName(string varName)
    {
        return "OnClick" + varName.Replace("m_btn", string.Empty) + "Btn";
    }

    private static string GetToggleFuncName(string varName)
    {
        return "OnToggle" + varName.Replace("m_toggle", string.Empty) + "Change";
    }

    private static string GetButtonToggleFuncName(string varName)
    {
        return "OnToggle" + varName.Replace("m_togBtn", string.Empty) + "Change";
    }

    public static Dictionary<string, string> dicWidget = new Dictionary<string, string>()
    {
        { "m_go", "GameObject" },
        { "m_gList", "UIGoList" },
        { "m_gdList", "UIDynamicGoList" },
        { "m_item", "GameObject" },
        { "m_tf", "Transform" },
        { "m_rect", "RectTransform" },
        { "m_text", "Text" },
        { "m_rtl", "RTLTextMeshPro" },
        { "m_skeleton", "SkeletonGraphic" },
        { "m_txt", "TMP_Text" },
        { "m_richText", "RichTextItem" },
        { "m_tbtn", "TextButtonItem" },
        { "m_btn", "Button" },
        { "m_img", "Image" },
        { "m_rimg", "RawImage" },
        { "m_scroll", "ScrollRect" },
        { "m_loopScroll", "LoopScrollRectBase" },
        { "m_sc", "ScrollController" },
        { "m_input", "InputField" },
        { "m_tmpInput", "TMP_InputField" },
        { "m_grid", "GridLayoutGroup" },
        { "m_clay", "CircleLayoutGroup" },
        { "m_hlay", "HorizontalLayoutGroup" },
        { "m_vlay", "VerticalLayoutGroup" },
        { "m_slider", "Slider" },
        { "m_group", "ToggleGroup" },
        { "m_toggle", "Toggle" },
        { "m_togBtn", nameof(ButtonToggle) },
        { "m_curve", "AnimationCurve" },
        { "m_tab", "UITab" },
        { "m_particle", "ParticleSystem" },
    };

    private static void WriteScript(Transform root, Transform child, ref StringBuilder strVar,
        ref StringBuilder strBind, ref StringBuilder strOnCreate, ref StringBuilder strCallback,
        ref StringBuilder strOnDestroy, ref List<string> usingList)
    {
        string varName = child.name;
        string varType = string.Empty;
        string pattern = @"@(.*)"; // 正则表达式
        var mathch = Regex.Match(varName, pattern);
        if (mathch.Success)
        {
            varName = Regex.Match(varName, @"^(.*?)@").Groups[1].Value;

            varType = mathch.Groups[1].Value;
            StringBuilder sb = new StringBuilder();
            int index = 0;
            foreach (var ch in varType)
            {
                sb.Append(index++ == 0 ? char.ToUpper(ch) : ch);
            }
            varType = sb.ToString();

            if (varType.ToLower() == "text")
            {
                if (child.GetComponent<Text>() != null)
                {
                    varType = "Text";
                }
                else if (child.GetComponent<RTLTextMeshPro>() != null)
                {
                    if (!usingList.Contains("using RTLTMPro;"))
                    {
                        usingList.Add("using RTLTMPro;");
                    }
                    varType = "RTLTextMeshPro";
                }
                else if (child.GetComponent<TextMeshPro>() != null)
                {
                    varType = "TextMeshPro";
                }

            }
            else if (varType.ToLower() == "list")
            {
                if (child.GetComponent<ListViewCommon>() != null)
                {
                    varType = "ListViewCommon";
                    if (!usingList.Contains("using HotFix.CComponent;"))
                    {
                        usingList.Add("using HotFix.CComponent;");
                    }
                }
                else if (child.GetComponent<UIDynamicGoList>() != null)
                {
                    varType = "UIDynamicGoList";
                }
                else if (child.GetComponent<HScollView>() != null)
                {
                    varType = "HScollView";
                    if (!usingList.Contains("using HotFix.CComponent;"))
                    {
                        usingList.Add("using HotFix.CComponent;");
                    }
                    
                }
                else if (child.GetComponent<LoopVerticalScrollRect>() != null)
                {
                    varType = "LoopVerticalScrollRect";
                }
                else if (child.GetComponent<LoopHorizontalScrollRect>() != null)
                {
                    varType = "LoopHorizontalScrollRect";
                }

            }
            else if(varType.ToLower() == "spine")
            {
                if (child.GetComponent<SkeletonGraphic>() != null)
                {
                    varType = "SkeletonGraphic";
                    if (!usingList.Contains("using Spine.Unity;"))
                    {
                        usingList.Add("using Spine.Unity;");
                    }
                }
                else if (child.GetComponent<SkeletonAnimation>() != null)
                {
                    varType = "SkeletonAnimation";
                    if (!usingList.Contains("using Spine.Unity;"))
                    {
                        usingList.Add("using Spine.Unity;");
                    }
                }
            }
        }
        else
        {
            foreach (var pair in dicWidget)
            {
                if (varName.StartsWith(pair.Key))
                {
                    varType = pair.Value;
                    break;
                }
            }
        }

        if (varType == string.Empty)
        {
            return;
        }

        string varPath = GetRelativePath(child, root);
        if (!string.IsNullOrEmpty(varName))
        {
            strVar.Append("\t\tprivate " + varType + " " + varName + ";\n");
            switch (varType)
            {
                case "Transform":
                    strBind.Append(string.Format("\t\t\t{0} = FindChild(\"{1}\");\r\n", varName, varPath));
                    break;

                case "GameObject":
                    strBind.Append(string.Format("\t\t\t{0} = FindChild(\"{1}\").gameObject;\r\n", varName, varPath));
                    break;

                case "AnimationCurve":
                    strBind.Append(string.Format(
                        "\t\t\t{0} = FindChildComponent<AnimCurveObject>(\"{1}\").m_animCurve;\r\n", varName, varPath));
                    break;

                case "RichItemIcon":
                    strBind.Append(string.Format("\t\t\t{0} = CreateWidgetByType<{1}>(\"{2}\");\r\n", varName, varType,
                        varPath));
                    break;

                case "RedNoteBehaviour":
                case "TextButtonItem":
                case "SwitchTabItem":
                case "UIActorWidget":
                case "UIEffectWidget":
                    strBind.Append(
                        string.Format("\t\t\t{0} = CreateWidget<{1}>(\"{2}\");\r\n", varName, varType, varPath));
                    break;

                default:
                    strBind.Append(string.Format("\t\t\t{0} = FindChildComponent<{1}>(\"{2}\");\r\n", varName, varType,
                        varPath));
                    break;
            }

            Debug.Log($" var name is {varName}");
            if (varType == "Button")
            {
                string varFuncName = GetBtnFuncName(varName);
                strOnCreate.Append(string.Format("\t\t\t{0}.onClick.AddListener({1});\r\n", varName, $"() => {{\r\n\t\t\t\t{varFuncName}();\r\n\t\t\t}}"));
                if (varName != "m_btnBack")
                {
                    strCallback.Append(string.Format("\t\tpartial void {0}();\r\n", varFuncName));
                }

                // strCallback.Append("\t\t{\n\t\t}\n");

                strOnDestroy.AppendLine($"\t\t\tif(this.{varName}!=null)");
                strOnDestroy.AppendLine("\t\t\t{");
                strOnDestroy.AppendLine($"\t\t\t\tthis.{varName}.onClick.RemoveAllListeners();");
                strOnDestroy.AppendLine("\t\t\t}");
            }

            if (varType == "Toggle")
            {
                string varFuncName = GetToggleFuncName(varName);
                strOnCreate.Append(string.Format("\t\t\t{0}.onValueChanged.AddListener({1});\r\n", varName, $"(isOn) => {{\r\n\t\t\t\t{varFuncName}(isOn);\r\n\t\t\t}}"));
                strCallback.Append(string.Format("\t\tpartial void {0}(bool isOn);\n", varFuncName));
                // strCallback.Append("\t\t{\n\t\t}\n");
                strOnDestroy.AppendLine($"\t\t\tif(this.{varName}!=null)");
                strOnDestroy.AppendLine("\t\t\t{");
                strOnDestroy.AppendLine($"\t\t\t\tthis.{varName}.onValueChanged.RemoveAllListeners();");
                strOnDestroy.AppendLine("\t\t\t}");
            }

            if (varType == "ButtonToggle")
            {
                string varFuncName = GetButtonToggleFuncName(varName);
                strOnCreate.Append(string.Format("\t\t\t{0}.onValueChanged.AddListener({1});\n", varName, $"(isOn) => {{\r\n\t\t\t\t{varFuncName}(isOn);\r\n\t\t\t}}"));
                strCallback.Append(string.Format("\t\tpartial void {0}(bool isOn);\n", varFuncName));
                // strCallback.Append("\t\t{\n\t\t}\n");
                strOnDestroy.AppendLine($"\t\t\tif(this.{varName}!=null)");
                strOnDestroy.AppendLine("\t\t\t{");
                strOnDestroy.AppendLine($"\t\t\t\tthis.{varName}.onValueChanged.RemoveAllListeners();");
                strOnDestroy.AppendLine("\t\t\t}");
            }

            strOnDestroy.AppendLine($"\t\t\tthis.{varName}=null;");
        }
    }

    public class GeneratorHelper : EditorWindow
    {
        [MenuItem("GameObject/ScriptGenerator/About", priority = 49)]
        public static void About()
        {
            ScriptGenerator.GeneratorHelper welcomeWindow =
                (ScriptGenerator.GeneratorHelper)EditorWindow.GetWindow(typeof(ScriptGenerator.GeneratorHelper), false,
                    "About ScriptGenerator");
        }

        public void Awake()
        {
            minSize = new Vector2(400, 600);
        }

        protected void OnGUI()
        {
            GUILayout.BeginVertical();
            foreach (var item in ScriptGenerator.dicWidget)
            {
                GUILayout.Label(item.Key + "：\t" + item.Value);
            }
            GUILayout.EndVertical();
        }
    }

    public class SwitchGroupGeneratorHelper : EditorWindow
    {
        [MenuItem("GameObject/ScriptGenerator/AboutSwitchGroup", priority = 50)]
        public static void About()
        {
            GetWindow(typeof(SwitchGroupGeneratorHelper), false, "AboutSwitchGroup");
        }

        public void Awake()
        {
            minSize = new Vector2(400, 600);
        }

        protected void OnGUI()
        {
            GUILayout.BeginVertical();
            GUILayout.Label(SwitchGroupGenerator.CONDITION + "：\t" + "SwitchTabItem[]");
            GUILayout.EndVertical();
        }
    }

    public class SwitchGroupGenerator
    {
        /*
         遍历子节点，找到所有名为 m_switchGroup 开始的节点，输出该节点
         */

        public const string CONDITION = "m_switchGroup";

        public static readonly SwitchGroupGenerator Instance = new SwitchGroupGenerator();

        public string Process(Transform root)
        {
            var sbd = new StringBuilder();
            var list = new List<Transform>();
            Collect(root, list);
            foreach (var node in list)
            {
                sbd.AppendLine(Process(root, node)).AppendLine();
            }

            return sbd.ToString();
        }

        public void Collect(Transform node, List<Transform> nodeList)
        {
            if (node.name.StartsWith(CONDITION))
            {
                nodeList.Add(node);
                return;
            }

            var childCnt = node.childCount;
            for (var i = 0; i < childCnt; i++)
            {
                var child = node.GetChild(i);
                Collect(child, nodeList);
            }
        }

        public string Process(Transform root, Transform groupTf)
        {
            var parentPath = GetPath(root, groupTf);
            var _name = groupTf.name;
            var sbd = new StringBuilder(@"
var _namePath = ""#parentPath"";
var _nameTf = FindChild(_namePath);
var childCnt = _nameTf.childCount;
SwitchTabItem[] _name;
_name = new SwitchTabItem[childCnt];
for (var i = 0; i < childCnt; i++)
{
    var child = _nameTf.GetChild(i);
    _name[i] = CreateWidget<SwitchTabItem>(_namePath + ""/"" + child.name);
}");
            sbd.Replace("_name", _name);
            sbd.Replace("#parentPath", parentPath);
            return sbd.ToString();
        }

        public string GetPath(Transform root, Transform childTf)
        {
            if (childTf == null)
            {
                return string.Empty;
            }

            if (childTf == root)
            {
                return childTf.name;
            }

            var parentPath = GetPath(root, childTf.parent);
            if (parentPath == string.Empty)
            {
                return childTf.name;
            }

            return parentPath + "/" + childTf.name;
        }
    }
}