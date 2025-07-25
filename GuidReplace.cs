using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GuideReplace;
using JetBrains.Annotations;


using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public   class GuidReplace
{
    public class GuidFileInfo
    {
        public string filePathName;
        public string fileGuid;
    }
    public CodeMoveTool condeMovetool;
    public GuidReplace(CodeMoveTool condeMovetool)
    {
        this.condeMovetool = condeMovetool;
    }

    public class GuidNode
    {
        public string originFilePathName;
        public string originGuid;
        public List<GuidNode> dependecy;
    }
    public Dictionary<string,GuidNode> lastGuidNodeCache = new Dictionary<string,GuidNode>();
    public Dictionary<string,GuidNode> nowGuidNodeCache = new Dictionary<string,GuidNode>();
    public string[] ingoreFilePath = new string[]
    {
        "com.unity.ugui","unity_builtin_extra"
    };

    public string[] startIngorePath = new string[]
    {
        "Assets/Editor/","Assets/Plugins/Editor"
    };
    public static Regex guidRegex = new Regex(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
    public List<GuidFileInfo> allPackageFiles = new List<GuidFileInfo>();

   
    GuidNode GetGuidNode(string guid, Func<string, string> guidToAssetPath,bool isNow = false)
    {
        var cacheNode = isNow ? nowGuidNodeCache : lastGuidNodeCache;
        if (cacheNode.TryGetValue(guid,out GuidNode tmp))
        {
            return tmp;
        }
        var path = guidToAssetPath(guid).ToLinuxPath();
        if(string.IsNullOrEmpty(path))
        {
            return null;
        }
       
        
        if (ingoreFilePath.Any((xx) => path.Contains(xx)))
        {
            return null;
        }

        if (startIngorePath.Any((xx) => path.StartsWith(xx)))
        {
            return null;
        }
        if (path.EndsWith(".dll") || path.EndsWith(".pdb"))
        {
            return null;
        }



        var node = new GuidNode();
        node.originGuid = guid;
        node.originFilePathName = guidToAssetPath(guid).ToLinuxPath();
        cacheNode.Add(node.originGuid, node);
        List<string> arr = null; 
        if (node.originFilePathName.EndsWith(".asmdef"))
        {
            var content = EasyUseEditorFuns.ReadAllText(node.originFilePathName);

            string pattern = "\"GUID:([a-f0-9]+)\"";
            var matches = Regex.Matches(content, pattern);
             arr = matches.Cast<Match>().Select(m => m.Groups[1].Value).ToList();

            
        }
        else
        {
            if (File.Exists(node.originFilePathName))
            {
                var content = EasyUseEditorFuns.ReadAllText(node.originFilePathName);
                var matches = Regex.Matches(content, @"guid:\s*([0-9a-fA-F]{32})");
                arr = new List<string>();
                foreach (Match match in matches)
                {
                    string tmpGuid = match.Groups[1].Value;
                    arr.Add(tmpGuid);
                }
            }


        }

        for (int i = 0; arr != null && i < arr.Count; i++)
        {
            if (node.dependecy == null)
            {
                node.dependecy = new List<GuidNode>();
            }
            var subNode = GetGuidNode(arr[i],guidToAssetPath, isNow);
            if (subNode != null)
            {
                node.dependecy.Add(subNode);
            }

        }


        return node;
    }

    public bool CreateMapByJson()
    {

        var basepath = (EasyUseEditorFuns.baseCustomTmpCache + "/Editor/~config/allRes.json");
        basepath = Path.GetFullPath(basepath);
       
        if(!File.Exists(basepath))
        {
            condeMovetool.ShowNotification(new GUIContent("清先创建" + basepath));
            return false;
        }
        var content = EasyUseEditorFuns.ReadAllText(basepath);
        lastGuidNodeCache = JsonConvert.DeserializeObject<Dictionary<string, GuidNode>>(content);
        return true;
    }
    /// <summary>
    /// 老资源路径构建 资源路径-guid绑定
    /// </summary>
    public void BuildOldResMap()
    {
        lastGuidNodeCache.Clear();

        AssetDatabaseExpand.progressAction = (title, progress,desc) =>
        {
            EditorUtility.DisplayCancelableProgressBar(
                    title,
                  desc,
                  progress
              );
        };
        AssetDatabaseExpand.Init(Path.Combine(Environment.CurrentDirectory, "Assets").ToLinuxPath());

        EditorUtility.ClearProgressBar();



        var allReses = AssetDatabaseExpand.GetAllAssetPaths().Select((xx)=> AssetDatabaseExpand.AssetPathToGUID(xx.filePathName)).Where((xx=>!string.IsNullOrEmpty(xx))).ToList();
        int i = 0;
        foreach (var guid in allReses)
        {
            bool cancel = EditorUtility.DisplayCancelableProgressBar(
                  "处理所有资源",
                  $"正在处理: {guid}",
                  ++i / (float)allReses.Count
              );
            if(string.IsNullOrEmpty(guid))
            {
                continue;
            }
            GetGuidNode(guid, AssetDatabaseExpand.GUIDToAssetPath);
            if (cancel)
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("用户取消了处理");
                break;
            }
        }
        SaveJson();

    }

    string MyGuidToAssetPath(string guid)
    {
        if (allPackageFiles.Count == 0) return null;
        var findRst = allPackageFiles.FirstOrDefault((xx) => xx.fileGuid == guid);
        if (findRst == null)
        {
            return null;
        }
        return findRst.filePathName;
    }
    bool HasRes(string resPath,out GuidNode node)
    {
        var originPath = this.condeMovetool.ConvertPathWithExclusions(resPath, true);
        var rst = lastGuidNodeCache.Values.FirstOrDefault((xx) => xx.originFilePathName == originPath);
        if (rst == null || rst == default)
        {
            node = null;
            return false;
        }
        node = rst;
        return true;
    }
   

    string GetNowRes(string oldRes)
    {
        return this.condeMovetool.ConvertPathWithExclusions(oldRes, false);
    }

    public void ReplaceGuid(List<string> allGuids)
    {
        nowGuidNodeCache.Clear();

        foreach (var guid in allGuids)
        {
            GetGuidNode(guid, AssetDatabase.GUIDToAssetPath, true);
        }

        //遍历所有现有文件 现有文件的meta不要变 要改prefab文件本身 

        foreach (var item in lastGuidNodeCache.Values)
        {
            var res = item.originFilePathName;
            //遍历所有依赖 
            if (item.dependecy != null && item.dependecy.Count > 0)
            {
                for (global::System.Int32 j = 0; j < item.dependecy.Count; j++)
                {
                    var subResNode = item.dependecy[j];
                    var oldRes = subResNode.originFilePathName;
                    var nowRes = this.condeMovetool.ConvertPathWithExclusions(oldRes, false);

                    var rst = nowGuidNodeCache.FirstOrDefault((xx) => xx.Value.originFilePathName == nowRes);
                    if (rst.Value != null)
                    {
                        var content = EasyUseEditorFuns.ReadAllText(res);
                        var newContent = Regex.Replace(content, subResNode.originGuid, rst.Value.originGuid);
                        File.WriteAllText(res, newContent);
                    }
                }
            }

        }
        AssetDatabase.Refresh();

    }

    public void UpdateGuidProp(List<string> allGuids)
    {
        try
        {
            nowGuidNodeCache.Clear();

            foreach (var guid in allGuids)
            {
                GetGuidNode(guid, AssetDatabase.GUIDToAssetPath,true);
            }

            //遍历所有现有文件  
            int nTotalProgres = lastGuidNodeCache.Values.Count;
            int progress = 0;
            foreach (var item in lastGuidNodeCache.Values)
            {
                var res = item.originFilePathName;

                bool cancel = EditorUtility.DisplayCancelableProgressBar(
                           "处理资源中",
                           $"正在处理: {res}",
                           ++progress / (float)nTotalProgres
                       );

                if (!res.EndsWith(".meta"))
                {
                    var nowRes = GetNowRes(res);
                    var metaPathName = EasyUseEditorFuns.baseCustomTmpCache + "/metas/" + res.ToUnityPath() + ".meta";
                    if (!File.Exists(nowRes + ".meta"))
                    {
                        continue;
                    }
                    var content = EasyUseEditorFuns.ReadAllText(nowRes + ".meta");
                    var matches = Regex.Match(content, @"guid:\s*([0-9a-fA-F]{32})");
                    var nowGuid = matches.Groups[1].Value;
                    if (File.Exists(metaPathName))
                    {
                        var metaContet = EasyUseEditorFuns.ReadAllText(metaPathName);
                        string replaced = Regex.Replace(
               metaContet,
               @"guid:\s*[a-f0-9]{32}",
               m => "guid: " + nowGuid
           );
                        File.WriteAllText(res + ".meta", replaced);
                    }

                }

                if (cancel)
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("用户取消了处理");
                    break;
                }
            }
        }
        catch(Exception e)
        {
            this.condeMovetool.ShowNotification(new GUIContent(e.ToString()));
        }
        finally
        {
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }
    }

    void SaveJson()
    {
        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
        var str = JsonConvert.SerializeObject(lastGuidNodeCache, settings);

        var path = EasyUseEditorFuns.baseCustomTmpCache + "/Editor/~config/allRes.json";
        path = Path.GetFullPath(path);

        EasyUseEditorFuns.CreateDir(path);
        File.WriteAllText(path, str);
    }
}
