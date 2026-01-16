using NET.D365.TOOLS.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static Azure.Core.HttpHeader;

namespace NET.D365.TOOLS.Services
{
    public class TableMetadataHelper
    {
        private readonly string _cacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "metadata_path_cache.json");
        private string _rootPath;
        // 使用并发字典缓存路径：Key 为表名/EDT名，Value 为完整物理路径
        // 缓存：主表路径
        private readonly ConcurrentDictionary<string, string> _tablePathCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // 缓存：EDT 路径
        private readonly ConcurrentDictionary<string, string> _edtPathCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // 缓存：EUMN 路径
        private readonly ConcurrentDictionary<string, string> _enumPathCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // 缓存：EUMN Extension路径
        private readonly ConcurrentDictionary<string, List<string>> _enumExtensionCache = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        // 缓存：表扩展路径列表
        private readonly ConcurrentDictionary<string, List<string>> _extensionPathsCache = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        // 性能优化：缓存已解析的最终 EDT LabelID
        private readonly ConcurrentDictionary<string, string> _resolvedEdtLabelCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);


        private readonly ConcurrentDictionary<string, (Dictionary<string, string> Labels,
                                             Dictionary<string, string> Relations,
                                             Dictionary<string, string> FieldEdts,
                                             Dictionary<string, string> FieldEnums)> _tableMetaMemoryCache
    = new ConcurrentDictionary<string, (Dictionary<string, string>, Dictionary<string, string>, Dictionary<string, string>, Dictionary<string, string>)>(StringComparer.OrdinalIgnoreCase);
        public TableMetadataHelper(string rootPath)
        {
            _rootPath = rootPath;
            // 初始化时异步构建全量索引
            Task.Run(() => BuildPathIndex());
        }

        public void BuildPathIndex(bool forceRefresh = false)
        {
            if (!forceRefresh && File.Exists(_cacheFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_cacheFilePath);
                    var cache = JsonConvert.DeserializeObject<MetadataPathCache>(json);
                    if (cache != null)
                    {
                        // 恢复内存字典
                        _tablePathCache.Clear();
                        foreach (var kv in cache.TablePaths) _tablePathCache.TryAdd(kv.Key, kv.Value);
                        _edtPathCache.Clear();
                        foreach (var kv in cache.EdtPaths) _edtPathCache.TryAdd(kv.Key, kv.Value);
                        _enumPathCache.Clear();
                        foreach (var kv in cache.EnumPaths) _enumPathCache[kv.Key] = kv.Value;
                        _enumExtensionCache.Clear();
                        foreach (var kv in cache.EnumExtensions) _enumExtensionCache.TryAdd(kv.Key, kv.Value);
                        _extensionPathsCache.Clear();
                        foreach (var kv in cache.TableExtensions) _extensionPathsCache.TryAdd(kv.Key, kv.Value);

                        return; // 直接返回，不再执行耗时的物理扫描
                    }
                }
                catch { /* 缓存损坏则继续执行扫描 */ }
            }


            try
            {
                if (!Directory.Exists(_rootPath)) return;

                var modules = Directory.GetDirectories(_rootPath);
                Parallel.ForEach(modules, module =>
                {
                    var models = Directory.GetDirectories(module);
                    foreach (var model in models)
                    {
                        // 1. 索引所有表 (AxTable)
                        string tableDir = Path.Combine(model, "AxTable");
                        if (Directory.Exists(tableDir))
                        {
                            foreach (var file in Directory.GetFiles(tableDir, "*.xml"))
                            {
                                string name = Path.GetFileNameWithoutExtension(file);
                                _tablePathCache.TryAdd(name, file);
                            }
                        }
                        // 2. 索引所有表扩展 (AxTableExtension)
                        string extDir = Path.Combine(model, "AxTableExtension");
                        if (Directory.Exists(extDir))
                        {
                            foreach (var file in Directory.GetFiles(extDir, "*.xml"))
                            {
                                // 扩展文件名通常是 CustTable.Extension_AOFTEC.xml
                                // 我们需要提取出基类表名 "CustTable"
                                string fileName = Path.GetFileNameWithoutExtension(file);
                                string baseTableName = fileName.Split('.')[0];

                                _extensionPathsCache.AddOrUpdate(
                                    baseTableName,
                                    new List<string> { file },
                                    (key, oldList) => { oldList.Add(file); return oldList; }
                                );
                            }
                        }

                        // 3. 索引所有扩展数据类型 (AxEdt)
                        string edtDir = Path.Combine(model, "AxEdt");
                        if (Directory.Exists(edtDir))
                        {
                            foreach (var file in Directory.GetFiles(edtDir, "*.xml"))
                            {
                                string name = Path.GetFileNameWithoutExtension(file);
                                _edtPathCache.TryAdd(name, file);
                            }
                        }

                        //4. 索引所有枚举数据类型（AxEnum）
                        string enumDir = Path.Combine(model, "AxEnum");
                        if (Directory.Exists(enumDir))
                        {
                            foreach (var file in Directory.GetFiles(enumDir, "*.xml"))
                            {
                                string fileName = Path.GetFileNameWithoutExtension(file);
                                _enumPathCache[fileName] = file;
                            }
                        }

                        //索引所有枚举扩展 (AxEnumExtension)
                        string enumExtDir = Path.Combine(model, "AxEnumExtension");
                        if (Directory.Exists(enumExtDir))
                        {
                            foreach (var file in Directory.GetFiles(enumExtDir, "*.xml"))
                            {
                                string fileName = Path.GetFileNameWithoutExtension(file);
                                string baseEnumName = fileName.Split('.')[0];

                                _enumExtensionCache.AddOrUpdate(
                                    baseEnumName,
                                    new List<string> { file },
                                    (key, oldList) =>
                                    {
                                        if (!oldList.Contains(file)) oldList.Add(file);
                                        return oldList;
                                    });
                            }
                        }
                    }
                });
                SaveCacheToLocal();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"构建元数据索引失败: {ex.Message}");
            }
        }

        private void SaveCacheToLocal()
        {
            var cache = new MetadataPathCache
            {
                LastUpdateTime = DateTime.Now,
                TablePaths = _tablePathCache.ToDictionary(k => k.Key, v => v.Value),
                EdtPaths = _edtPathCache.ToDictionary(k => k.Key, v => v.Value),
                EnumPaths = _enumPathCache.ToDictionary(k => k.Key, v => v.Value),
                EnumExtensions = _enumExtensionCache.ToDictionary(k => k.Key, v => v.Value),
                TableExtensions = _extensionPathsCache.ToDictionary(k => k.Key, v => v.Value)
            };
            File.WriteAllText(_cacheFilePath, JsonConvert.SerializeObject(cache));
        }

        // 增加获取枚举明细的方法
        public List<EnumItemModel> GetEnumDetails(string enumName)
        {
            var list = new List<EnumItemModel>();
            if (_enumPathCache.TryGetValue(enumName, out string path))
            {
                ParseEnumXml(path, list);
            }
            if (_enumExtensionCache.TryGetValue(enumName, out List<string> extensionPaths))
            {
                foreach (var extPath in extensionPaths)
                {
                    ParseEnumXml(extPath, list);
                }
            }
            return list.OrderBy(x => {
                int.TryParse(x.Value, out int v);
                return 999;
            }).ToList();
        }

        private void ParseEnumXml(string path, List<EnumItemModel> list)
        {
            if (!File.Exists(path)) return;
            XDocument doc = XDocument.Load(path);

            // 处理标准项 <AxEnumValue> 和 扩展项 <AxEnumExtensionItem>
            var items = doc.Descendants().Where(d =>
                    d.Name.LocalName == "AxEnumValue" ||
                    d.Name.LocalName == "AxEnumExtensionItem" ||
                    d.Name.LocalName == "AxEnumItem");

            foreach (var item in items)
            {
                string name = item.Element("Name")?.Value;
                // 如果扩展里覆盖了原有的 Key，这里可以做去重逻辑
                if (string.IsNullOrEmpty(name)) continue;

                if (!list.Any(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(new EnumItemModel
                    {
                        Name = name,
                        Label = item.Element("Label")?.Value,
                        Value = item.Element("Value")?.Value
                    });
                }
            }
        }


        public string FindTableXmlPath(string tableName)
        {
            return _tablePathCache.TryGetValue(tableName, out string path) ? path : null;
        }

        public string GetEdtLabel(string edtName)
        {
            if (string.IsNullOrEmpty(edtName)) return null;
            if (_resolvedEdtLabelCache.TryGetValue(edtName, out string cachedLabel)) return cachedLabel;

            string label = FindLabelInEdtRecursive(edtName, 0);
            if (!string.IsNullOrEmpty(label)) _resolvedEdtLabelCache.TryAdd(edtName, label);
            return label;
        }

        private string FindLabelInEdtRecursive(string edtName, int depth)
        {
            if (string.IsNullOrEmpty(edtName) || depth > 5) return null;

            try
            {
                // 1. 从我们之前建立的路径缓存中直接获取 XML 路径
                if (_edtPathCache.TryGetValue(edtName, out string edtPath))
                {
                    XDocument doc = XDocument.Load(edtPath);

                    // 2. 检查当前 EDT 是否有 Label
                    string label = doc.Root?.Element("Label")?.Value;
                    if (!string.IsNullOrEmpty(label))
                    {
                        return label; // 命中 Label 
                    }

                    // 3. 如果没有 Label，检查是否有继承 (Extends)
                    string parentEdt = doc.Root?.Element("Extends")?.Value;
                    if (!string.IsNullOrEmpty(parentEdt))
                    {
                        // 递归查找父级
                        return FindLabelInEdtRecursive(parentEdt, depth + 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析 EDT {edtName} 出错: {ex.Message}");
            }
            return null;
        }

        public string GetEnumLabel(string enumName)
        {
            try
            {
                if (_enumPathCache.TryGetValue(enumName, out string enumPath))
                {
                    XDocument doc = XDocument.Load(enumPath);
                    return doc.Root?.Element("Label")?.Value;
                }
            }
            catch { }
            return null;
        }


        public (Dictionary<string, string> Labels,
                     Dictionary<string, string> Relations,
                     Dictionary<string, string> FieldEdts,
                     Dictionary<string, string> FieldEnums) GetMetadataFromXml(string tableName)
        {
            if (_tableMetaMemoryCache.TryGetValue(tableName, out var cachedMeta))
            {
                return cachedMeta;
            }

            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var relations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var fieldEdtMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var fieldEnumMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var allPaths = new List<string>();
            if (_tablePathCache.TryGetValue(tableName, out string mainPath)) allPaths.Add(mainPath);
            if (_extensionPathsCache.TryGetValue(tableName, out List<string> extPaths)) allPaths.AddRange(extPaths);

            foreach (var path in allPaths)
            {
                if (!File.Exists(path)) continue;
                XDocument doc = XDocument.Load(path);
                foreach (var field in doc.Descendants("AxTableField"))
                {
                    string name = field.Element("Name")?.Value;
                    if (string.IsNullOrEmpty(name)) continue;

                    labels[name] = field.Element("Label")?.Value;
                    fieldEdtMap[name] = field.Element("ExtendedDataType")?.Value;
                    fieldEnumMap[name] = field.Element("EnumType")?.Value;
                }

                foreach (var relation in doc.Descendants("AxTableRelation"))
                {
                    string relatedTable = relation.Element("RelatedTable")?.Value;
                    foreach (var constraint in relation.Descendants("AxTableRelationConstraint"))
                    {
                        string fieldName = constraint.Element("Field")?.Value;
                        if (!string.IsNullOrEmpty(fieldName) && !string.IsNullOrEmpty(relatedTable))
                        {
                            relations[fieldName] = relatedTable;
                        }
                    }
                }
            }
            var result = (labels, relations, fieldEdtMap, fieldEnumMap);
            _tableMetaMemoryCache.TryAdd(tableName, result);
            return result;
        }
    }
}
