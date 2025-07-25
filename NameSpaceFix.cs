using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

using System;
using UnityEditorInternal;
using System.Net.Http.Headers;
using UnityEditorPool;




public class NameSpaceFix 
{
    private List<string> targetDirectories = new List<string>();
    private string codeSuffix = "*.cs";
    string filterFolderDesc;
    List<string> filterFolder = new List<string> ()
    {
        "ctrl","Control","ctr","conf"
    };

    
    
    private bool showHiddenFiles;
    private bool isTest = false;
    private Vector2 scrollPos;
    private Texture2D folderIcon;
    private Texture2D fileIcon;
    private bool showMergeView = true; // 是否合并显示所有目录内容
    private bool forceAddNameSpace;
  
   
    private HashSet<string> allUsedNameSpace = new HashSet<string>();
 
    

    

    private static void FindNamespacesInText(string text, HashSet<string> namespaces)
    {
        // 匹配namespace声明，考虑可能的注释和格式变化
        Regex namespaceRegex = new Regex(@"namespace\s+([\w\.]+)\s*\{");

        MatchCollection matches = namespaceRegex.Matches(text);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                string ns = match.Groups[1].Value;
                namespaces.Add(ns);
            }
        }
    }
    public Action<string> LogAction { get; set; }
    void Log(string str)
    {
        if(LogAction != null)
        {
            LogAction(str);
        }
    }

    public void OnGUi()
    {
        DrawToolbar();
        

        codeSuffix = EditorGUILayout.TextField("代码后缀", codeSuffix);
        if(string.IsNullOrEmpty( filterFolderDesc))
        {
            filterFolderDesc = string.Join(";", filterFolder);
        }
        EditorGUI.BeginChangeCheck();
        {
            filterFolderDesc = EditorGUILayout.TextField("过滤文件夹", filterFolderDesc);
        }
        if(EditorGUI.EndChangeCheck())
        {
            if(filterFolderDesc.Split(';').Length <= 0)
            {
                Debug.LogError("警告，格式不正确,用;分割");
            }
            filterFolder = filterFolderDesc.Split(';').ToList();
        }
        
        forceAddNameSpace = EditorGUILayout.ToggleLeft("是否强制添加命名空间", forceAddNameSpace);
        


        GUILayout.BeginHorizontal();
        if (GUILayout.Button("处理命名空间"))
        {
            var allFiles = new List<string>();
            fileNameSpaceList?.Clear();

            foreach (var dir in targetDirectories)
            {
                if (!Directory.Exists(dir)) continue;

                allFiles.AddRange(Directory.GetFiles(dir, codeSuffix,SearchOption.AllDirectories)
                    .Where(f => (showHiddenFiles || !IsHidden(f))));

               
            }
            //去掉 过滤的文件夹 路径 
            allFiles = allFiles.Where((file) => !filterFolder.Any((k) => Regex.IsMatch(file,k))).ToList();
            
            // 去重并排序

            var uniqueFiles = allFiles.Distinct().OrderBy(f => f).ToArray();


            List<string> needAddNameSpaceFileList = new List<string>();
            
            if(forceAddNameSpace)
            {
                // 遍历所有的cs文本 确定哪些cs是没有命名空间的 没有则添加 
                foreach (var filePath in uniqueFiles)
                {
                    if(!File.Exists(filePath))
                    {
                        continue;
                    }
                    var csContent = File.ReadAllText(filePath);
                    if(EasyUseEditorFuns.HasNamespaceDeclaration(csContent))
                    {
                        continue;
                    }
                    needAddNameSpaceFileList.Add(filePath);
                }
            }
            try
            {
                float fProgress = 0;
                foreach (var filePath in uniqueFiles)
                {
                    bool canCancel = EditorUtility.DisplayCancelableProgressBar("修复命名空间", "", ++fProgress / uniqueFiles.Length);
                    var tmpNameSpace = EasyUseEditorFuns.GetNameSpaceName(filePath);
                    UpdateFileNamespace(filePath, tmpNameSpace);
                    if (canCancel)
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
            catch(Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh();
            EditorUtility.RequestScriptReload();
        }
        if(GUILayout.Button("代码跳转"))
        {
            GotoButton(@"GUILayout\.Button\(""处理命名空间""\)");
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
            
        if(GUILayout.Button("修复命名空间报错1"))
        {
            FixUsingNameSpace();
        }
        if(GUILayout.Button("代码跳转"))
        {
            GotoButton(@"GUILayout\.Button\(""修复命名空间报错1""\)");
        }
        if(GUILayout.Button("修复完全限定名报错"))
        {
            FixFullSpaceName();
        }
        EditorGUILayout.EndHorizontal();
        

            DrawDragDropArea();
        DrawDirectoriesList();
        DrawDirectoryContents();
    }
    void FixFullSpaceName()
    {
        try
        {


            fileNameSpaceDealRecord?.Clear();
            fileContent?.Clear();
            var allCsFiles = EasyUseEditorFuns.GetAllUserLayerCSCode();
            allCsFiles = allCsFiles.Distinct().OrderBy(f => f).ToList();

            int nProgress = 0;
            foreach (var item in allCsFiles)
            {
                if (File.Exists(item))
                {
                    bool cancel = EditorUtility.DisplayCancelableProgressBar(
                        "处理资源中",
                        $"正在处理: {item}",
                        (float)++nProgress / allCsFiles.Count
                    );

                    var content = File.ReadAllText(item);
                    var key = item.ToLinuxPath();

                    //所有受改动的命名空间 
                    for (int i = 0; i < fileNameSpaceList.Count; i++)
                    {
                        if (key == fileNameSpaceList[i].filePathName) // 不改自己 
                        {
                            continue;
                        }
                        if (!fileNameSpaceDealRecord.ContainsKey(key))
                        {
                            fileNameSpaceDealRecord[key] = new List<int>();
                        }
                        if (fileNameSpaceDealRecord[key].Contains(fileNameSpaceList[i].hashKey))
                        {
                            continue;
                        }

                        var spArr = fileNameSpaceList[i].oldNameSpace.Split('.');
                        if (!spArr.Any((xx) => content.Contains(xx)))
                        {
                            continue;
                        }




                        var oldNameSpaceList = new List<string>();
                        var newNameSpaceList = new List<string>();

                        ContainNameSpace(key, fileNameSpaceList[i].oldNameSpace,
                            fileNameSpaceList[i].newNameSpace,  oldNameSpaceList,
                             newNameSpaceList);
                        
                        fileNameSpaceDealRecord[key].Add(fileNameSpaceList[i].hashKey);
                        bool hasChange = false;
                        for (int j = 0; j < oldNameSpaceList.Count; ++j)
                        {
                            var oldNamespace = oldNameSpaceList[j];
                            var newNamespace = newNameSpaceList[j];

                            var oldContent = content;
                            // 0. 修复完全限定名替换 （这里不用执行 留给后续流程）
                            content = Regex.Replace(
                                content,
                                @"(?<!\w|\.)"
                                + Regex.Escape(oldNamespace) +
                                @"(?=\.\w+)",
                                newNamespace,
                                RegexOptions.Multiline);

                            if(oldContent != content)
                            {
                                hasChange = true;
                            }

                            
                        }
                        if(hasChange)
                        {
                            fileContent[key] = new List<string>() { content };
                        }

                        

                        if (cancel)
                        {
                            EditorUtility.ClearProgressBar();
                            Debug.Log("用户取消了处理");
                            break;
                        }

                    }



                }
            }
            EditorUtility.ClearProgressBar();
            nProgress = 0;
            foreach (var item in fileContent)
            {
                bool cancel = EditorUtility.DisplayCancelableProgressBar(
                       "处理资源中",
                       $"正在处理: {item}",
                       (float)++nProgress / allCsFiles.Count
                   );
                File.WriteAllLines(item.Key, item.Value);
                if (cancel)
                {
                    EditorUtility.ClearProgressBar();
                }


            }
            EditorUtility.ClearProgressBar();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            AssetDatabase.Refresh();
            Log(("修复完毕"));
        }
    }

    void ContainNameSpace(string fileName, string nameSpaceOldStr, string nameSpaceNewStr, List<string> nameSpaceOldlist,  List<string> nameSpaceNewList)
    {


        nameSpaceOldlist.Add(nameSpaceOldStr);
        nameSpaceNewList.Add(nameSpaceNewStr);


        var sbOld = new StringBuilder();
        var spOldArr = nameSpaceOldStr.Split('.');
        var spNewArr = nameSpaceNewStr.Split('.');
        if (spOldArr.Length <= 1)
        {
            return;
        }
        var nCount = spOldArr.Length - 1;
        while (nCount > 0)
        {
            sbOld.Clear();
            for (int i = 0; i < nCount; ++i)
            {
                sbOld.Append(spOldArr[i]);
                sbOld.Append(i != nCount - 1 ? "." : "");
            }

            nameSpaceOldlist.Add(sbOld.ToString());
            var sbNew = new StringBuilder();
            for (int j = 0; j < nCount; j++)
            {

                sbNew.Append(spNewArr[j]);
                sbNew.Append(j != nCount - 1 ? "." : "");

            }
            nameSpaceNewList.Add(sbNew.ToString());
            --nCount;
        }
    }
    void FixUsingNameSpace()
    {
        try
        {
            fileNameSpaceDealRecord?.Clear();
            fileContent?.Clear();
            string[] ingoreFolder = new string[]
            {
                "Assets/3rdParty","Assets/Adjust","Assets/Plugins","Assets/FacebookSDK"
                ,"Assets/RTLTMPro"
            };
            var allCsFiles = EasyUseEditorFuns.GetAllUserLayerCSCode()
               .ConvertAll((xx) => xx.ToLinuxPath()).ToList();
            allCsFiles.Where((xx) => !ingoreFolder.Any((ll) => xx.Contains(ll))).ToList();
            allCsFiles = allCsFiles.Distinct().OrderBy(f => f).ToList();

            int nProgress = 0;
            foreach (var item in allCsFiles)
            {
                if (File.Exists(item))
                {
                    bool cancel = EditorUtility.DisplayCancelableProgressBar(
                        "读取cs到文件中",
                        $"正在处理: {item}",
                        (float)++nProgress / allCsFiles.Count
                    );

                    var content = File.ReadAllLines(item).ToList();
                    var key = item.ToLinuxPath();
                    fileContent[key] = content;

                    if (cancel)
                    {
                        EditorUtility.ClearProgressBar();
                        Debug.Log("用户取消了处理");
                        break;
                    }
                }
            }

            EditorUtility.ClearProgressBar();
            var allKeyes = fileContent.Keys.ToList();
            var allProgress = allKeyes.Count;
            nProgress = 0;
            foreach (var key in allKeyes)
            {
                var content = fileContent[key];
                var fProgress = ++nProgress * 1.0f / allProgress;
                bool cancel = EditorUtility.DisplayCancelableProgressBar(
                    $"修复命名空间报错{(100 * fProgress).ToString("F2")}%",
                    $"正在处理: {key}",
                    fProgress
                );


                if (cancel)
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("用户取消了处理");
                    break;
                }
                ///优化一下 前300行大概就是using和namespace声明的代码避免几千行代码的正则 
                var content300 = string.Join(Environment.NewLine, content.Take(300));
                var contentAfter300 = string.Join(Environment.NewLine, content.Skip(300));

                bool hasChanged = false;
                //所有受改动的命名空间 
                for (int i = 0; i < fileNameSpaceList.Count; i++)// A.B.C.D  
                {

                    if (!fileNameSpaceDealRecord.ContainsKey(key))
                    {
                        fileNameSpaceDealRecord[key] = new List<int>();
                    }
                    if (fileNameSpaceDealRecord[key].Contains(fileNameSpaceList[i].hashKey))
                    {
                        continue;
                    }


                    var spArr = fileNameSpaceList[i].oldNameSpace.Split('.');
                    if (!spArr.Any((xx) => content300.Contains(xx)))
                    {
                        continue;
                    }
                    // 避免gc过高 
                    var oldNameSpaceList = ListPool<string>.Get();
                    var newNameSpaceList = ListPool<string>.Get();

                    ContainNameSpace(key, fileNameSpaceList[i].oldNameSpace,
                        fileNameSpaceList[i].newNameSpace, oldNameSpaceList,
                         newNameSpaceList);
                    //if (key.Contains("/Editor/"))
                    //{

                    //    var tar = EasyUseEditorFuns.baseCustomTmpCache + "/" + key.ToUnityPath();
                    //    EasyUseEditorFuns.UnitySaveCopyFile(key, tar, true, true, true, false);

                    //}
                   
                    for (int j = 0; j < oldNameSpaceList.Count; ++j)
                    {
                        if (!content300.Contains(oldNameSpaceList[j]))
                        {
                            continue;
                        }
                        var oldNamespace = oldNameSpaceList[j];
                        var newNamespace = newNameSpaceList[j];
                        var hasKey = (oldNamespace, newNamespace).GetHashCode();
                        var oldContent300 = content300;
                        content300 = Regex.Replace(
                                 content300,
                                 @"(^|\s)namespace\s+" + Regex.Escape(oldNamespace) + @"(?=\s*[;{])",
                                 "namespace " + newNamespace,
                                 RegexOptions.Multiline);




                        content300 = Regex.Replace(
                           content300,
                           @"(^|\s)(using\s+" + Regex.Escape(oldNamespace) + @"\s*;)",
                           $"using {newNamespace};",
                           RegexOptions.Multiline);

                        if(oldContent300 != content300)
                        {
                            hasChanged = true;
                        }
                    }


                    ListPool<string>.Release(oldNameSpaceList);
                    ListPool<string>.Release(newNameSpaceList);
                    if (hasChanged)
                    {
                        fileContent[key] = new List<string>() { content300 + "\r\n" + contentAfter300 };
                    }

                    if (cancel)
                    {
                        EditorUtility.ClearProgressBar();
                        Debug.Log("用户取消了处理");
                        break;
                    }
                }
            }
            EditorUtility.ClearProgressBar();
            nProgress = 0;
            foreach (var item in fileContent)
            {
                bool cancel = EditorUtility.DisplayCancelableProgressBar(
                       "处理资源中",
                       $"正在处理: {item}",
                       (float)++nProgress / allCsFiles.Count
                   );
                File.WriteAllLines(item.Key, item.Value);
                if (cancel)
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            EditorUtility.ClearProgressBar();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            AssetDatabase.Refresh();
            Log(("修复完毕"));
        }
    }
   
   
    private class UsingStatement
    {
        public string FullText { get; set; }
        public string Namespace { get; set; }
        public string Alias { get; set; }
    }
    private List<UsingStatement> GetUsingStatements(string content)
    {
        var usings = new List<UsingStatement>();
        var matches = Regex.Matches(content, @"^using\s+([^=;\s]+)(?:\s*=\s*([^;\s]+))?\s*;", RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            usings.Add(new UsingStatement
            {
                FullText = match.Value,
                Namespace = match.Groups[1].Value,
                Alias = match.Groups[2].Success ? match.Groups[2].Value : null
            });
        }

        return usings;
    }

    private string RemoveStringsAndComments(string content)
    {
        // 移除字符串
        content = Regex.Replace(content, @"""[^""]*""", "\"\"");
        // 移除单行注释
        content = Regex.Replace(content, @"//.*$", "", RegexOptions.Multiline);
        // 移除多行注释
        content = Regex.Replace(content, @"/\*.*?\*/", "", RegexOptions.Singleline);

        return content;
    }
   
    
   
  

  
   

   

    public class NameSpaceReplaceNode
    {
        public string oldNameSpace;
        public string oldStaticNameSpaceUse;
        
        public string newStaticNameSpaceUse;
        public string newNameSpace;
        public int hashKey;
        public string filePathName;
    }


    List<NameSpaceReplaceNode> fileNameSpaceList = new List<NameSpaceReplaceNode>();
    Dictionary<string, List<string>> fileContent = new Dictionary<string, List<string>>();

    Dictionary<string, List<int>> fileNameSpaceDealRecord = new Dictionary<string, List<int>>();




    private bool UpdateFileNamespace(string filePath, string newNamespace)
    {
        string content = File.ReadAllText(filePath);

        string className = EasyUseEditorFuns.ExtractClassName(content);

        // 使用正则表达式查找命名空间声明
        var namespaceRegex = new Regex(@"^\s*namespace\s+([^\s;{]+)", RegexOptions.Multiline);
        var match = namespaceRegex.Match(content);

        if (match.Success)
        {
            string oldNamespace = match.Groups[1].Value;

            

            // 如果命名空间已经正确，则跳过   
            //if (oldNamespace == newNamespace )
            //{
            //    return false;
            //}
            if(oldNamespace == "UnityEngine.UI")
            {
                return false;
            }
            if (oldNamespace == "HotFix.Ctrl")
            {
                return false;
            }
            
            if (oldNamespace == "Assets.Util")
            {
                return false;
            }
            if (oldNamespace == "Assets.Interface")
            {
                return false;

            }
            if (oldNamespace.StartsWith( "Assets."))
            {
                return false;

            }



            if(fileNameSpaceList.Any((xx)=>xx.oldNameSpace.Contains(oldNamespace)))
            {
                //什么都不用做 
            }
            else if(fileNameSpaceList.Any((xx)=>oldNamespace.Contains(xx.oldNameSpace)))
            {
                var firt = fileNameSpaceList.FirstOrDefault((xx) => oldNamespace.Contains(xx.oldNameSpace));
                if(firt != null )
                {
                    firt.oldNameSpace = oldNamespace;

                    firt.oldNameSpace = oldNamespace;
                    firt.oldStaticNameSpaceUse = $"{oldNamespace}.{className}";
                    
                    firt.newNameSpace = newNamespace;
                    firt.newStaticNameSpaceUse = $"{newNamespace}.{className}";
                    firt.hashKey = (oldNamespace,newNamespace).GetHashCode();
                    firt.filePathName = filePath.ToLinuxPath();

                }
            }
            else
            {
                var node = new NameSpaceReplaceNode();
                fileNameSpaceList.Add(node);
                node.oldNameSpace = oldNamespace;
                node.oldStaticNameSpaceUse = $"{oldNamespace}.{className}";
                
                node.newNameSpace = newNamespace;
                node.newStaticNameSpaceUse = $"{newNamespace}.{className}";


                node.hashKey = (oldNamespace , newNamespace).GetHashCode();
                node.filePathName = filePath.ToLinuxPath();
            }
            


            // 替换命名空间
            content = namespaceRegex.Replace(content, $"namespace {newNamespace}", 1);


           
            // 保存文件
            File.WriteAllText(filePath, content.ToWindowsContent(), Encoding.UTF8);
            return true;
        }
        else if(forceAddNameSpace)
        {
            var targetNamespace = EasyUseEditorFuns.GetNameSpaceName(filePath);
            
            int classPos = EasyUseEditorFuns.FindClassDefinitionPosition(content, className);
            string closingBrace = "\n}\n";
            string namespaceDeclaration = $"\nnamespace {targetNamespace}\n{{\n";
            content = content.Insert(classPos, namespaceDeclaration);
            content += closingBrace;
            
            File.WriteAllText(filePath, content.ToWindowsContent(), Encoding.UTF8);
            return true;
        }

   

        return false;
    }


    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // 搜索过滤器
        isTest = GUILayout.Toggle(isTest, isTest?"测试":"正式", EditorStyles.toolbarButton);

        if (isTest)
        {
            codeSuffix = "*.txt";
        }
        else
        {
            codeSuffix = "*.cs";
        }


        // 显示隐藏文件选项
        showHiddenFiles = GUILayout.Toggle(showHiddenFiles, "显示隐藏文件", EditorStyles.toolbarButton);

        // 合并视图选项
        showMergeView = GUILayout.Toggle(showMergeView, "合并视图", EditorStyles.toolbarButton);

        // 清除按钮
        if (GUILayout.Button("清除", EditorStyles.toolbarButton))
        {
            targetDirectories.Clear();
        }

        // 刷新按钮
        if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
        {
            RefreshDirectory();
        }
        if(GUILayout.Button("跳转到代码行"))
        {
            var final = Application.dataPath + $"/Editor/EditorExpand/NameSpaceFix.cs";
            UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(final, 10);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawDirectoriesList()
    {
        if (targetDirectories.Count > 0)
        {
            EditorGUILayout.LabelField("已选目录:", EditorStyles.boldLabel);

            for (int i = 0; i < targetDirectories.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                // 目录路径
                EditorGUILayout.LabelField(targetDirectories[i]);

                // 移除按钮
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    targetDirectories.RemoveAt(i);
                    return; // 避免在循环中修改集合
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private void DrawDragDropArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 40.0f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, targetDirectories.Count == 0 ?
            "拖拽文件夹到这里 (支持多选)" : $"已选择 {targetDirectories.Count} 个目录", EditorStyles.helpBox);

        HandleDragAndDrop(dropArea);
    }

    private void DrawDirectoryContents()
    {
        if (targetDirectories.Count == 0) return;

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (showMergeView)
        {
            // 合并显示所有目录内容
            DrawMergedContents();
        }
        else
        {
            // 分别显示每个目录内容
            foreach (var dir in targetDirectories)
            {
                if (!Directory.Exists(dir)) continue;

                EditorGUILayout.LabelField(Path.GetFileName(dir), EditorStyles.boldLabel);
                DrawSingleDirectoryContents(dir);
                EditorGUILayout.Space();
            }
        }

        

        EditorGUILayout.EndScrollView();
    }

    private void DrawMergedContents()
    {
        try
        {
            // 合并所有目录的文件和子目录
            var allFiles = new List<string>();
            var allDirectories = new List<string>();

            foreach (var dir in targetDirectories)
            {
                if (!Directory.Exists(dir)) continue;

                allFiles.AddRange(Directory.GetFiles(dir, codeSuffix, SearchOption.AllDirectories)
                    .Where(f => (showHiddenFiles || !IsHidden(f)))  );

                allFiles  = allFiles.Where((ll) => !Regex.IsMatch(ll, @"\\ctrl\\") && !Regex.IsMatch(ll, @"\\ctr\\")).ToList();
                

                allDirectories.AddRange(Directory.GetDirectories(dir)
                    .Where(d => showHiddenFiles || !IsHidden(d)));
            }

            // 去重并排序
            var uniqueDirs = allDirectories.Distinct().OrderBy(d => d).ToArray();
            var uniqueFiles = allFiles.Distinct().OrderBy(f => f).ToArray();

            // 显示子目录
            foreach (string dir in uniqueDirs)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(folderIcon, GUILayout.Width(20), GUILayout.Height(20));
                EditorGUILayout.LabelField(Path.GetFileName(dir));
                EditorGUILayout.LabelField($"({Path.GetDirectoryName(dir)})", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            // 显示文件
            foreach (string file in uniqueFiles)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(fileIcon, GUILayout.Width(20), GUILayout.Height(20));
                EditorGUILayout.LabelField(Path.GetFileName(file));
                EditorGUILayout.LabelField($"({Path.GetDirectoryName(file)})", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }
        catch (System.Exception e)
        {
            EditorGUILayout.HelpBox($"无法读取目录: {e.Message}", MessageType.Error);
        }
    }

    private void DrawSingleDirectoryContents(string directory)
    {
        try
        {
            var files = Directory.GetFiles(directory, codeSuffix)
                .Where(f => showHiddenFiles || !IsHidden(f))
                .OrderBy(f => f)
                .ToArray();

            var directories = Directory.GetDirectories(directory)
                .Where(d => showHiddenFiles || !IsHidden(d))
                .OrderBy(d => d)
                .ToArray();

            // 显示子目录
            foreach (string dir in directories)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(folderIcon, GUILayout.Width(20), GUILayout.Height(20));
                if (GUILayout.Button(Path.GetFileName(dir), EditorStyles.label))
                {
                    // 点击子目录可以添加新目录
                    if (!targetDirectories.Contains(dir))
                    {
                        targetDirectories.Add(dir);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            // 显示文件
            foreach (string file in files)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(fileIcon, GUILayout.Width(20), GUILayout.Height(20));
                EditorGUILayout.LabelField(Path.GetFileName(file));
                EditorGUILayout.EndHorizontal();
            }
        }
        catch (System.Exception e)
        {
            EditorGUILayout.HelpBox($"无法读取目录 {directory}: {e.Message}", MessageType.Error);
        }
    }

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition))
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    if (DragAndDrop.paths.Length > 0)
                    {
                        foreach (string path in DragAndDrop.paths)
                        {
                            if (Directory.Exists(path) && !targetDirectories.Contains(path))
                            {
                                targetDirectories.Add(path);
                            }
                            else if (File.Exists(path))
                            {
                                string dir = Path.GetDirectoryName(path);
                                if (!targetDirectories.Contains(dir))
                                {
                                    targetDirectories.Add(dir);
                                }
                            }
                        }
                    }
                }
                break;
        }
    }
    public Action RepaintAction;
    public void Repatint()
    {
        if(RepaintAction != null)
        {
            RepaintAction();
        }
    }
    private void RefreshDirectory()
    {
        // 强制重绘
        Repatint();
    }

    private bool IsHidden(string path)
    {
        FileAttributes attr = File.GetAttributes(path);
        return (attr & FileAttributes.Hidden) == FileAttributes.Hidden;
    }
    void GotoButton(string str)
    {
        var final = Application.dataPath + $"/Editor/EditorExpand/NameSpaceFix.cs";
        var contents = File.ReadAllLines(final);
        int lineNum = 0;
        foreach (var item in contents)
        {
            if (Regex.IsMatch(item, str))
            {
                break;
            }
            ++lineNum;
        }
        InternalEditorUtility.OpenFileAtLineExternal(final, lineNum + 1);
    }
}