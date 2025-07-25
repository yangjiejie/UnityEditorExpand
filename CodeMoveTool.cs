using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GuideReplace;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static GuidReplace;






public class CodeMoveTool : EditorWindow
{
    public static readonly Regex CompiledPattern = new Regex(
        @"\/~[^\/]+\/",
        RegexOptions.Compiled
    );
    private enum TabType
    {
        CodeMoveTool,
        FolderTreeEditor,
        NameSpaceTool,
        ReplaceGuid,
    }
    private TabType currentTab = TabType.CodeMoveTool;
    GuidReplace guidReplace;
    private NameSpaceFix nameSpaceFix;
    private string sourceFolder = "";
    private string targetFolder = "";
    private Vector2 scrollPosition;
    private bool includeSubfolders = true;
    private bool overwriteFiles = true;
    private bool showLog = true;
    private string logText = "";
    private bool forbidResDelHook  = true;
    private string codeSuffixDesc = "*.txt"; 

    public string CodeLibKey
    {
        get
        {
            var lastFoderName = Path.GetFileNameWithoutExtension(Environment.CurrentDirectory + ".exe");
            return Path.Combine(lastFoderName, "codeMoveTool", "codeLib");
        }
    }
    public string TargetCodeLibKey
    {
        get
        {
            return Path.Combine(Application.productName, "codeMoveTool", "targetCodeLib");
        }
    }

    public string[] suffixArrayTitle = new string[]
    {
        "a包后缀",
        "b包后缀",
        
    };

    public string filterFolders; 

    private string[] options = { "a包", "b包" ,"常规包"};

    public enum PackageDefine
    {
        PACKAGE_A = 0,
        PACKAGE_B = 1,
        PACKAGE_NORMAL = 2,
    }


    public string[] suffixArray = new string[]
   {
        "_a",
        "_b",
   };

    private  List<string> ingoreFolderList = new  List<string>()
    {
        "hot_fix","HotFix","Assets","Editor"
    };

    private List<string> targetCsFolderList =new   List<string>  ()
    {
        "hot_fix","Launcher","Runtime",
    };
   
    private string targetCSFolder; 

    
    public static void ShowWindow()
    {
        GetWindow<CodeMoveTool>("CodeMoveTool");
    }
    void ReadJson()
    {

       // JsonConvert.SerializeObject();
        //JsonConvert.DeserializeObject<xx>();
    }


    private int selectedIndex;
    private bool isTest;

    public void CopyCode()
    {
        var allFiles = EasyUseEditorFuns.GetAllFiles(new string[] { "Assets" }, "*.*", (xx) =>
        {
            return !xx.EndsWith(".meta");
        });
        allFiles = allFiles.ConvertAll((xx) => xx.ToLinuxPath());
        int nProgress = 0;
        try
        {
            foreach (var item in allFiles)
            {
                bool cancel = EditorUtility.DisplayCancelableProgressBar(
                    "处理资源中",
                    $"正在处理: {item}",
                    ++nProgress / (float)allFiles.Count
                );
                var unityPath = EasyUseEditorFuns.GetUnityAssetPath(item);
                var target = Path.Combine(EasyUseEditorFuns.baseCustomTmpCache, unityPath);
                target = EasyUseEditorFuns.GetLinuxPath(target);

                var targetPathFileName = target.ToLinuxPath();
                EasyUseEditorFuns.UnitySaveCopyFile(item, targetPathFileName, true, withPathMetaFile: true,isShowLog:false);
                if (cancel)
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);

        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
    }
    private void DrawDragTextField()
    {


        targetCSFolder = string.Join(";", targetCsFolderList);
        targetCSFolder = targetCSFolder.Replace("Assets/", "");

        EditorGUI.EndChangeCheck();
        {
            targetCSFolder = EditorGUILayout.TextField("目标目录", targetCSFolder);
        }
       
        if(EditorGUI.EndChangeCheck())
        {
            targetCsFolderList = targetCSFolder.Split(';').ToList();
        }
        // 拖拽区域提示
        var dropRect = GUILayoutUtility.GetLastRect();
        GUI.Box(dropRect, GUIContent.none);
        HandleDragAndDrop(dropRect);

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

                            if (Directory.Exists(path) && !targetCsFolderList.Contains(path))
                            {
                                targetCsFolderList.Add(path);
                            }
                            else if (File.Exists(path))
                            {
                                string dir = Path.GetDirectoryName(path);
                                if (!targetCsFolderList.Contains(dir))
                                {
                                    targetCsFolderList.Add(dir);
                                }
                            }
                        }
                    }
                }
                break;
        }
    }
    void DrawCodeMoveTool()
    {

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        isTest = GUILayout.Toggle(isTest, isTest ? "测试" : "正式", EditorStyles.toolbarButton);

        EditorGUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        EasyUseEditorFuns.baseVersion = EditorGUILayout.TextField("版本号：", EasyUseEditorFuns.baseVersion);
        if (GUILayout.Button("清理仓库"))
        {
            EasyUseEditorFuns.DelFolderAllContens(EasyUseEditorFuns.baseCustomTmpCache, false);
            this.ShowNotification(new GUIContent("清理本地库完成"));
        }
        if (GUILayout.Button("整个工程入库"))
        {
           

            var fullpath = Path.GetFullPath(Application.dataPath);
            fullpath = EasyUseEditorFuns.GetLinuxPath(fullpath);
            CopyCode();
            AssetDatabase.Refresh();

        }

        if (GUILayout.Button("使用仓库代码"))
        {
            FindRepeatRes.ReverseLocalSvn();
            AssetDatabase.Refresh();
            EasyUseEditorFuns.CleanEmptyDirectories(Application.dataPath);
            AssetDatabase.Refresh();
        }
        if (GUILayout.Button("跳转到版本"))
        {
            var svnPath = EasyUseEditorFuns.baseCustomTmpCache.ToFullPath();
            EasyUseEditorFuns.CreateDir(svnPath);
            EditorUtility.RevealInFinder(svnPath);
        }
        GUILayout.EndHorizontal();
        for (int i = 0; i < options.Length; i++)
        {
            bool isSelected = (selectedIndex == i);
            bool newSelected = EditorGUILayout.Toggle(options[i], isSelected);

            if (newSelected != isSelected && newSelected)
            {
                selectedIndex = i;

                Repaint(); // 确保UI立即更新
            }
        }

        for (int i = 0; i < suffixArray.Length; i++)
        {
            suffixArray[i] = EditorGUILayout.TextField(suffixArrayTitle[i], suffixArray[i]);
        }

        if(string.IsNullOrEmpty(filterFolders))
        {
            filterFolders = string.Join(";", ingoreFolderList);
            
        }
        filterFolders = EditorGUILayout.TextField("过滤文件夹", filterFolders);

       
        DrawDragTextField();



        if (isTest)
        {
            codeSuffixDesc = "*.txt";
        }
        else
        {
            codeSuffixDesc = "*.cs";
        }
        codeSuffixDesc = EditorGUILayout.TextField("代码后缀", codeSuffixDesc);


        GUILayout.Label("Code Synchronization Tool", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        // Source folder selection
        EditorGUILayout.BeginHorizontal();
        if(string.IsNullOrEmpty(sourceFolder))
        {
            if(EditorPrefs.HasKey(CodeLibKey))
            {
                sourceFolder = EditorPrefs.GetString(CodeLibKey, "");
            }
        }
        sourceFolder = EditorGUILayout.TextField("Source Folder", sourceFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(80)))
        {
            var lastSourceFolder = sourceFolder;
            sourceFolder = EditorUtility.OpenFolderPanel("Select Source Folder", sourceFolder, "");
            if (string.IsNullOrEmpty(sourceFolder))
            {
                if (EditorPrefs.HasKey(CodeLibKey))
                {
                    sourceFolder = EditorPrefs.GetString(CodeLibKey, "");
                }
            }
            else if(lastSourceFolder != sourceFolder)
            {
                if(folderTreeEditor == null)
                {
                    InitFolderTreeNode();
                }
                else
                {
                    folderTreeEditor.rootPath = sourceFolder;
                    folderTreeEditor.RefreshTree();
                }
            }
        }
        if (GUILayout.Button("存档", GUILayout.Width(80)))
        {
            EditorPrefs.SetString(CodeLibKey, sourceFolder);
            ShowNotification(new GUIContent($"已经存档，{sourceFolder}"));
        }
        if (GUILayout.Button("跳转目录", GUILayout.Width(80)))
        {
            if(Directory.Exists(sourceFolder))
            {
                EditorUtility.RevealInFinder(sourceFolder);
            }
            
            
        }
        EditorGUILayout.EndHorizontal();

        // Target folder selection
        EditorGUILayout.BeginHorizontal();
        if (string.IsNullOrEmpty(targetFolder))
        {
            if (EditorPrefs.HasKey(TargetCodeLibKey))
            {
                targetFolder = EditorPrefs.GetString(TargetCodeLibKey, "");
            }
        }
        targetFolder = EditorGUILayout.TextField("Target Folder", targetFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(80)))
        {
            targetFolder = EditorUtility.OpenFolderPanel("Select Target Folder", targetFolder, "");
            if (string.IsNullOrEmpty(targetFolder))
            {
                if (EditorPrefs.HasKey(TargetCodeLibKey))
                {
                    targetFolder = EditorPrefs.GetString(TargetCodeLibKey, "");
                }
            }
        }
        if (GUILayout.Button("存档", GUILayout.Width(80)))
        {
            EditorPrefs.SetString(TargetCodeLibKey, targetFolder);
            ShowNotification(new GUIContent($"已经存档，{targetFolder}"));
        }
        if (GUILayout.Button("跳转目录", GUILayout.Width(80)))
        {
            if (Directory.Exists(targetFolder))
            {
                EditorUtility.RevealInFinder(targetFolder);
            }

        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Options
        includeSubfolders = EditorGUILayout.Toggle("Include Subfolders", includeSubfolders);
        overwriteFiles = EditorGUILayout.Toggle("Overwrite Existing Files", overwriteFiles);
        showLog = EditorGUILayout.Toggle("Show Operation Log", showLog);

        EditorGUILayout.Space();



        // Operation buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("拉取代码"))
        {
           
            if (folderTreeEditor == null)
            {
                InitFolderTreeNode();
            }
            try
            {
                //先移除代码 


                AssetDatabase.Refresh();
                MoveCode(sourceFolder, targetFolder);
                AssetDatabase.Refresh();


            }
            catch(Exception e)
            {
                Debug.LogError(e);
                ShowNotification(new GUIContent(e.ToString()));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EasyUseEditorFuns.CleanEmptyDirectories(Application.dataPath);
                AssetDatabase.Refresh();
                //var allAsm = EasyUseEditorFuns.GetAllFiles(new string[] { "Assets" }, "*.asmdef", (file) =>
                //{
                //    return targetCsFolderList.Any((li) => file.EndsWith(li + ".asmdef"));
                //});
                //foreach(var asm in allAsm)
                //{
                //    var folderName = Path.GetDirectoryName(asm);
                //    var unityPath = asm.ToUnityPath();
                //    var noAssetPath = unityPath.Replace("Assets/", "");
                //    var topFolderName = noAssetPath.Substring(0, noAssetPath.IndexOf("/"));
                //    var newPathName = folderName + "/"+  topFolderName + ".asmdef";
                //    AssetDatabase.RenameAsset(unityPath, topFolderName + ".asmdef");
                //    var saveArchive = EasyUseEditorFuns.baseCustomTmpCache + "/" + newPathName.ToUnityPath() + ".path";
                //    EasyUseEditorFuns.WriteFileToTargetPath(saveArchive, newPathName.ToUnityPath(), false);
                //}
                AssetDatabase.Refresh();
                EditorUtility.RequestScriptReload();
            }

            
            ShowNotification(new GUIContent("拉取代码完成"));
            GotoButton(@"GUILayout\.Button\(""拉取代码""\)");
        }
        if (GUILayout.Button("上传代码"))
        {
            GotoButton(@"GUILayout\.Button\(""上传代码""\)");
            if (folderTreeEditor == null)
            {
                InitFolderTreeNode();
            }
            try
            {
                MoveCode(targetFolder, sourceFolder, true);
            }
            catch (Exception e)
            {
                MoveCode(targetFolder, sourceFolder, true,true);
                LogError(e.ToString());
                ShowNotification(new GUIContent(e.ToString()));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                EditorUtility.RequestScriptReload();
            }

            ShowNotification(new GUIContent("上传代码完成"));
        }
       


        
       

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("移除本地工程代码(自动缓存到本地库)"))
        {
            GotoButton(@"GUILayout\.Button\(""移除本地工程代码\(自动缓存到本地库\)""\)");
            foreach (var item in targetCsFolderList)
            {
                if (string.IsNullOrEmpty(item)) continue;
                var folderPath = Path.Combine(Environment.CurrentDirectory, "Assets", item).ToLinuxPath();
                EasyUseEditorFuns.DelFolderAllContens(folderPath, false,".cs",isSaveArchive:true);
                EasyUseEditorFuns.DelFolderAllContens(folderPath, false, ".asmdef", isSaveArchive: true);
            }
            
            


            AssetDatabase.Refresh();
            EditorUtility.RequestScriptReload();
        }
        
        if (GUILayout.Button("删除项目空目录"))
        {
        //    GotoButton(@"GUILayout\.Button\(""删除项目空目录""\)");
            EasyUseEditorFuns.CleanEmptyDirectories(Application.dataPath);
            AssetDatabase.Refresh();
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Log display
        if (showLog)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(logText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Clear Log"))
            {
                logText = "";
            }
        }
    }

    void MoveCode(string source,string target,bool isUpLoadCode = false, bool debug = false)
    {
        
        if (ValidatePaths(source, target))
        {
            
            
            if (!source.EndsWith("Assets") || !target.EndsWith("Assets"))
            {
                ShowNotification(new GUIContent("必须是unity的目录根"));
                return;
            }
            List<string> folderList =  new List<string>();
            foreach (var li in targetCsFolderList)
            {
                folderList.Add(EasyUseEditorFuns.GetLinuxPath(source + "/" + li));
            }
            List<string> allCsFiles = new List<string>();
            foreach (var li in folderList)
            {
                if (!Directory.Exists(li)) continue;
                var tmpFiles = Directory.GetFiles(li, codeSuffixDesc, SearchOption.AllDirectories)
                 .Concat(Directory.GetFiles(li, "*.asmdef", SearchOption.AllDirectories)).ToArray();
                allCsFiles.AddRange(tmpFiles);
            }
            int nProgres = 0;
            foreach (var item in allCsFiles)
            {
                var fileName = Path.GetFileName(item);
                var unityAsset = EasyUseEditorFuns.GetUnityAssetPath(item, false);
                var folder = Path.GetDirectoryName(unityAsset);
                folder = EasyUseEditorFuns.GetLinuxPath(folder);
                string convertedPath = ConvertPathWithExclusions(folder, isUpLoadCode);

                var targetFilePath = EasyUseEditorFuns.GetLinuxPath(target + "/" + convertedPath + "/" + fileName);

                var targetFilePathParent = Path.GetDirectoryName(targetFilePath);
                EasyUseEditorFuns.CreateDir(targetFilePathParent);

                bool cancel = EditorUtility.DisplayCancelableProgressBar(
                "处理资源中",
                $"正在处理: {fileName}",
                (float)nProgres / allCsFiles.Count
                );
                if (!isUpLoadCode) // 如果是拉取代码 则存档 saveArchive
                {
                    if(!File.Exists(targetFilePath) && !debug)
                    {
                    var saveArchive = EasyUseEditorFuns.baseCustomTmpCache + "/" + targetFilePath.ToUnityPath() + ".path";
                        EasyUseEditorFuns.CreateDir(saveArchive);
                        EasyUseEditorFuns.WriteFileToTargetPath(saveArchive, targetFilePath.ToUnityPath(), false);
                    }
                       
                }
                if(!debug)
                {
                    EasyUseEditorFuns.CreateDir(targetFilePath);
                    try
                    {
                        File.Copy(item, targetFilePath, true);
                    }
                    catch(Exception e)
                    {
                        Debug.LogError(e.ToString() + "item = "+item + "->" + targetFilePath);
                    }
                    
                }
                
             //   File.Copy(item + ".meta", targetFilePath + ".meta", true);

                if (cancel)
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("用户取消了处理");
                    break;
                }
            }


            EditorUtility.ClearProgressBar();
           

        }
    }
    void OnDestroy()
    {
        if(forbidResDelHook == SafeDeleteUnityResHook.forbidHook)
        {
            SafeDeleteUnityResHook.forbidHook = !forbidResDelHook;
        }
        folderTreeEditor = null;
    }
    private void OnGUI()
    {
        SafeDeleteUnityResHook.forbidHook = forbidResDelHook;
        if (nameSpaceFix == null) nameSpaceFix = new NameSpaceFix();
        // 页签工具栏
        currentTab = (TabType)GUILayout.Toolbar((int)currentTab, new string[] { 
            "Code Move Tool", 
            "Folder Tree Editor",
            "namespaceTool","guid替换"}
        );

        InitFolderTreeEditor();

        // 根据当前页签显示不同内容
        switch (currentTab)
        {
            case TabType.CodeMoveTool:
                DrawCodeMoveTool();
                break;
            case TabType.FolderTreeEditor:
                DrawFolderTreeEditor();
                break;
            case TabType.NameSpaceTool:
                DrawNameSpaceFixEditor();
                break;
            case TabType.ReplaceGuid:
                ReplaceGuid();
                break;
        }

        
    }
    public static FolderTreeEditor folderTreeEditor;


    
    void InitFolderTreeNode()
    {
        if (folderTreeEditor == null)
        {
            folderTreeEditor = new FolderTreeEditor();
        }
        folderTreeEditor.logAction = (ss) =>
        {
            this.ShowNotification(new GUIContent(ss));
        };
        folderTreeEditor.rootPath = sourceFolder;
        folderTreeEditor.BuildDirectoryTree();
    }
    void DrawFolderTreeEditor()
    {
        InitFolderTreeEditor();
        folderTreeEditor.DrawUI();
    }

    void InitFolderTreeEditor()
    {
        if (folderTreeEditor == null)
        {
            folderTreeEditor = new FolderTreeEditor();
        }
        folderTreeEditor.logAction = (ss) =>
        {
            this.ShowNotification(new GUIContent(ss));
        };
        folderTreeEditor.rootPath = sourceFolder;
        //folderTreeEditor.ReadJson(false);
    }

    void DrawNameSpaceFixEditor()
    {
        if(nameSpaceFix == null)
        {
            nameSpaceFix = new NameSpaceFix();
            nameSpaceFix.RepaintAction = () =>
            {
                Repaint();
            };
            nameSpaceFix.LogAction = (str) =>
            {
                ShowNotification(new GUIContent( str ) );
            };
        }
        nameSpaceFix.OnGUi();
    }
    void ReplaceGuid()
    {
        if (folderTreeEditor == null)
        {
            InitFolderTreeNode();
        }
        else
        {
            folderTreeEditor.rootPath = sourceFolder;
            folderTreeEditor.RefreshTree();
        }
        if (guidReplace == null)
        {
            guidReplace = new GuidReplace(this);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("老资源路径 --> guid绑定(写入本地文件)"))
        {
            try
            {
               
                guidReplace.BuildOldResMap();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                this.ShowNotification(new GUIContent("构建映射完毕!" + Application.dataPath + "/Editor/~config/allRes.json"));
            }

        }


        if (GUILayout.Button("读取老资源-->guid（内存）"))
        {
            try
            {
                bool sucess =  guidReplace.CreateMapByJson();

                if(sucess)
                {
                    ShowNotification(new GUIContent("读取配置成功"));
                }
            }
            catch(Exception e)
            {
                Debug.LogError(e);
                ShowNotification(new GUIContent(e.ToString()));
            }
            finally
            {
                
            }
        }

        EditorGUILayout.EndHorizontal();


        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("所有meta文件存库"))
        {
            AssetDatabaseExpand.progressAction = (title, progress, desc) =>
            {
                EditorUtility.DisplayCancelableProgressBar(
                        title,
                      desc,
                      progress
                  );
            };
            AssetDatabaseExpand.Init(Path.Combine(Environment.CurrentDirectory, "Assets").ToLinuxPath());
            var basePath = EasyUseEditorFuns.baseCustomTmpCache + "/metas/";
            EasyUseEditorFuns.DelFolderAllContens(basePath,false);
            var allFiles = AssetDatabaseExpand.GetAllAssetPaths();
             
            
            for (int i = 0; i < allFiles.Count; i++)
            {
                bool cancel = EditorUtility.DisplayCancelableProgressBar(
                    $"meta入库{100 * (float)i / allFiles.Count}%",
                    $"正在处理: {allFiles[i]}",
                    (float)i / allFiles.Count
                );
                if (allFiles[i].filePathName.StartsWith("Packages"))
                {
                    int n = 1;
                }
                if (CompiledPattern.IsMatch(allFiles[i].filePathName))
                {
                    continue;
                }
                var fullPath =  allFiles[i].filePathName.ToFullPath();

                if (File.Exists(fullPath + ".meta"))
                {
                    string filePath = null;
                    int indexPackage = allFiles[i].filePathName.IndexOf("Packages");
                    if (indexPackage >= 0)
                    {
                        filePath = basePath + allFiles[i].filePathName.Substring(indexPackage)+".meta";
                    }
                    else
                    {
                        filePath = basePath + allFiles[i].filePathName.ToUnityPath() + ".meta";
                    }
                    filePath = Path.GetFullPath(filePath);
                    EasyUseEditorFuns.CreateDir(filePath);
                    System.IO.File.Copy(fullPath + ".meta", filePath, true);
                }
            }



            // 特别的需要处理 YooAsset收集  
            //要做的功能 第一 mySvn可以挪个位置   
            var specialPath  = EasyUseEditorFuns.baseCustomTmpCache + "/special";
            EasyUseEditorFuns.DelFolderAllContens(specialPath, false);

            var abCollectSource = Path.Combine(Environment.CurrentDirectory, "Assets", "AssetBundleCollectorSetting.asset").ToLinuxPath();
            var abCollectTarget = Path.Combine(specialPath, abCollectSource.ToUnityPath()).ToLinuxPath();
            EasyUseEditorFuns.UnitySaveCopyFile(abCollectSource, abCollectTarget,true,false,false,false);


            ShowNotification(new GUIContent($"meta文件已经存入本地{basePath}--{specialPath}"   + "2秒后关闭客户端，请执行bat清理资源"));
            
            EditorCoroutine.StartCoroutine(new EditorWaitForSeconds(2f, () =>
            {
                EditorUtility.RevealInFinder(Environment.CurrentDirectory + "/mySvn/testcode/Assets");
                EditorApplication.Exit(0);
            }));


        }

        
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("跳转到版本"))
        {
            var svnPath = EasyUseEditorFuns.baseCustomTmpCache.ToFullPath();
            EasyUseEditorFuns.CreateDir(svnPath);
            EditorUtility.RevealInFinder(svnPath);
        }
    }

    public  string ConvertPathWithExclusions(string originalPath,bool isOrigin)
    {
        var strArray = originalPath.Split('/');
        var append = "";
        var pathArray = "";
        foreach (var item in strArray)
        {
            if(string.IsNullOrEmpty(append))
            {
                append = item;
            }
            else
            {
                append += "/" + item;
            }
            
            var node = folderTreeEditor.FindNodeByPath(append);
            if(node == null)
            {
                Debug.Log("node == null");
            }
            if(string.IsNullOrEmpty( pathArray))
            {
                pathArray = node!= null ? node.Name : (pathArray  + append);
            }
            else
            {
                if(node == null)
                {
                    pathArray = append;
                }
                else
                {
                    if(!isOrigin)
                    {
                        pathArray += "/" + node.Name;
                    }
                    else
                    {

                        var originName = Path.GetFileName(node.OriginUnityPath);
                        pathArray += "/" + originName;
                    }
                    
                }
            }
            
        }

        
        return pathArray;
    }

    private bool ValidatePaths(string folderA,string folderB)
    {
        if (string.IsNullOrEmpty(folderA) || !Directory.Exists(folderA))
        {
            LogError("Invalid source folder path");
            return false;   
        }

        if (string.IsNullOrEmpty(folderB) || !Directory.Exists(folderB))
        {
            LogError("Invalid target folder path");
            return false;
        }

        if (folderA.Equals(folderB, StringComparison.OrdinalIgnoreCase))
        {
            LogError("Source and target folders cannot be the same");
            return false;
        }

        return true;
    }

    private void SyncFolders(string fromFolder, string toFolder, bool isReverseSync)
    {
        try
        {
            logText = "";
            Log($"Starting {(isReverseSync ? "reverse " : "")}sync from:\n{fromFolder}\nto:\n{toFolder}");

            // Get all .cs files in source folder
            SearchOption searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] files = Directory.GetFiles(fromFolder, "*.cs", searchOption);

            int copiedCount = 0;
            int skippedCount = 0;

            foreach (string sourceFilePath in files)
            {
                // Get relative path
                string relativePath = sourceFilePath.Substring(fromFolder.Length).TrimStart(Path.DirectorySeparatorChar);
                string targetFilePath = Path.Combine(toFolder, relativePath);

                // Create target directory if needed
                string targetDir = Path.GetDirectoryName(targetFilePath);
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    Log($"Created directory: {targetDir}");
                }

                // Check if file exists and compare content
                bool shouldCopy = true;
                if (File.Exists(targetFilePath))
                {
                    if (!overwriteFiles)
                    {
                        skippedCount++;
                        Log($"Skipped (no overwrite): {relativePath}");
                        continue;
                    }

                    string sourceContent = File.ReadAllText(sourceFilePath);
                    string targetContent = File.ReadAllText(targetFilePath);

                    if (sourceContent == targetContent)
                    {
                        skippedCount++;
                        Log($"Skipped (identical): {relativePath}");
                        continue;
                    }
                }

                // Copy the file
                File.Copy(sourceFilePath, targetFilePath, overwriteFiles);
                copiedCount++;
                Log($"Copied: {relativePath}");
            }

            Log($"\nSync completed!\nCopied: {copiedCount} files\nSkipped: {skippedCount} files");
        }
        catch (Exception ex)
        {
            LogError($"Error during sync: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        logText += $"{message}\n";
        Debug.Log(message);
    }

    private void LogError(string message)
    {
        logText += $"ERROR: {message}\n";
        Debug.LogError(message);
    }

    void GotoButton(string str)
    {
        var final = Application.dataPath + $"/Editor/EditorExpand/CodeMoveTool.cs";
        if(!File.Exists(final))
        {
            return;
        }
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