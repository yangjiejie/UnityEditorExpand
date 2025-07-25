using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace GuideReplace
{
    public class AssetDatabaseExpand
    {
        public class GuidFileInfo
        {
            public string filePathName;
            public string fileGuid;
        }

        public static Dictionary<string, AssetDatabaseExpand.GuidFileInfo> fileMapGuids = new Dictionary<string, AssetDatabaseExpand.GuidFileInfo>();

        public static Dictionary<string, AssetDatabaseExpand.GuidFileInfo> guidMapFiles = new Dictionary<string, AssetDatabaseExpand.GuidFileInfo>();

        public static List<GuidFileInfo> allUnityRes ;

        public static Regex guidRegex = new Regex(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);

        public static  Regex filterIngoreFolder = new Regex(@"([\\/])(~[^\\/]+|[^\\/]+~)([\\/]|$)",RegexOptions.Compiled);

        public static Action<string, float,string> progressAction;

        public static string projectRootPath = "";
        public static void Init(string projectPath)
        {
            
            fileMapGuids.Clear();
            guidMapFiles.Clear();

            projectRootPath = projectPath.Substring(0, projectPath.IndexOf("Assets") - 1);

             var all = System.IO.Directory.GetFiles(projectPath, "*.*", System.IO.SearchOption.AllDirectories).Where((xx) => !xx.EndsWith(".meta") && !xx.EndsWith(".gitignore")).ToList();


            all = all.ConvertAll(xx => xx.ToLinuxPath());

            //去掉unity中的无效文件 
            all = all.Where((xx) => !filterIngoreFolder.IsMatch(xx)).ToList();

            if(allUnityRes == null)
            {
                allUnityRes = new List<GuidFileInfo>();
            }
            allUnityRes?.Clear();
            int nProgress = 0;
            foreach (var item in all)
            {
                if (progressAction != null)
                {
                    progressAction("处理unity资源", ++nProgress / (float)all.Count, $"{nProgress} / {all.Count}");
                }
           
                var info = new GuidFileInfo();
                allUnityRes.Add(info);
                var metaPath = item + ".meta";
                info.filePathName = item.ToUnityPath();
                if (!System.IO.File.Exists(metaPath))
                {
                    info.fileGuid = null;
                }
                else
                {
                    var metaContent = EasyUseEditorFuns.ReadAllText(metaPath);
                    var matches = guidRegex.Match(metaContent);
                    info.fileGuid = matches.Groups[1].Value;
                }
                if (!fileMapGuids.ContainsKey(info.filePathName))
                {
                    fileMapGuids[info.filePathName] = info;
                    if (info.fileGuid != null)
                    {
                        guidMapFiles[info.fileGuid] = info;
                    }
                    
                }
                else
                {

                }
            }
            var packageFolder = Path.Combine(projectRootPath, "Packages").ToLinuxPath();
            all = System.IO.Directory.GetFiles(packageFolder, "*.*", System.IO.SearchOption.AllDirectories).Where((xx) => !xx.EndsWith(".meta") && !xx.EndsWith(".gitignore")).ToList();


            all = all.ConvertAll(xx => xx.ToLinuxPath());

            foreach (var item in all)
            {
                var info = new GuidFileInfo();
                allUnityRes.Add(info);
                var fileName = item.Substring(item.IndexOf("Packages"));
                var metaPath = item + ".meta";
                info.filePathName = fileName;
                if (!System.IO.File.Exists(metaPath))
                {
                    info.fileGuid = null;
                }
                else
                {
                    var metaContent = EasyUseEditorFuns.ReadAllText(metaPath);
                    var matches = guidRegex.Match(metaContent);
                    info.fileGuid = matches.Groups[1].Value;
                }
                if (!fileMapGuids.ContainsKey(info.filePathName))
                {
                    fileMapGuids[info.filePathName] = info;
                    if (info.fileGuid != null)
                    {
                        guidMapFiles[info.fileGuid] = info;
                    }

                }
                else
                {

                }
            }


        }
        public static string GUIDToAssetPath(string guid)
        {
            if(guidMapFiles.TryGetValue(guid, out var tmp))
            {
                return tmp.filePathName;
            }
            return null;
        }

        public static string AssetPathToGUID(string filePath)
        {
            if (fileMapGuids.TryGetValue(filePath, out var tmp))
            {
                return tmp.fileGuid;
            }
            return null;
        }
        public static List<GuidFileInfo> GetAllAssetPaths()
        {
            return new List<GuidFileInfo>( allUnityRes);
        }
    }

}

