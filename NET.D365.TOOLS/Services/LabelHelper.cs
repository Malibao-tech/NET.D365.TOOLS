using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NET.D365.TOOLS.Services
{
    public static class LabelHelper
    {
        private static ConcurrentDictionary<string, string> _labelCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly string _labelCachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "label_cache.json");
        /// <summary>
        /// 异步扫描并加载所有中文 Label 文件
        /// </summary>
        /// <param name="rootPath">UNC 共享路径，例如 \\172.31.10.54\PackagesLocalDirectory</param>
        public static async Task<Dictionary<string, string>> LoadAllLabelsAsync(string rootPath, bool forceRefresh = false)
        {
            if (!forceRefresh && File.Exists(_labelCachePath))
            {
                try
                {
                    string json = File.ReadAllText(_labelCachePath);
                    var diskCache = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (diskCache != null && diskCache.Count > 0)
                    {
                        _labelCache = new ConcurrentDictionary<string, string>(diskCache, StringComparer.OrdinalIgnoreCase);
                        return diskCache;
                    }
                }
                catch { /* 读取失败则重新扫描 */ }
            }
            await Task.Run(() =>
            {
                // 1. 获取模块和模型
                var modules = Directory.GetDirectories(rootPath);
                Parallel.ForEach(modules, module =>
                {
                    var models = Directory.GetDirectories(module);
                    foreach (var model in models)
                    {
                        string resourcePath = Path.Combine(model, "AxLabelFile", "LabelResources", "zh-Hans");
                        if (Directory.Exists(resourcePath))
                        {
                            var files = Directory.GetFiles(resourcePath, "*.label.txt");
                            foreach (var file in files)
                            {
                                ParseLabelTxtFile(file);
                            }
                        }
                    }
                });
            });
            try
            {
                string json = JsonConvert.SerializeObject(_labelCache);
                File.WriteAllText(_labelCachePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存缓存失败: {ex.Message}");
            }
            return _labelCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase); 
        }


        private static void ParseLabelTxtFile(string filePath)
        {
            try
            {
                // 1. 获取 Label ID 前缀 (例如从 AccountsPayableMobile.zh-Hans.label.txt 提取 AccountsPayableMobile)
                string fileName = Path.GetFileName(filePath);
                string labelFileId = fileName.Split('.')[0]; // 拿到标识符

                // 使用 FileStream 提高大文件读取性能
                using (var reader = new StreamReader(filePath, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith(" ;")) continue;

                        int index = line.IndexOf('=');
                        if (index > 0)
                        {
                            {
                                string id = line.Substring(0, index).Trim();
                                string text = line.Substring(index + 1).Trim();
                                string cleanId = id.TrimStart('@');

                                // 同时支持两种 Key 格式以匹配 D365 元数据 [cite: 15]
                                _labelCache[$"@{labelFileId}:{cleanId}"] = text;
                                _labelCache[$"@{cleanId}"] = text;
                            }
                        }
                    }
                }
            }
            catch { /* 忽略单个文件读取错误 */ }
        }


        public static string GetText(string labelId, Dictionary<string, string> labelCache)
        {
            if (string.IsNullOrEmpty(labelId)) return "";

            if (labelCache.TryGetValue(labelId, out string text))
            {
                return text;
            }

            string cleanInput = labelId.StartsWith("@") ? labelId : "@" + labelId;
            if (labelCache.TryGetValue(cleanInput, out text)) return text;

            return labelId;
        }
    }
}
