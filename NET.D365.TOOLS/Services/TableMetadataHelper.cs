using Dapper;
using Microsoft.Data.SqlClient;
using NET.D365.TOOLS.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
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

        private readonly ConcurrentDictionary<string, List<EnumItemModel>> _enumContentCache = new ConcurrentDictionary<string, List<EnumItemModel>>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, (Dictionary<string, string> Labels,
                                             Dictionary<string, string> Relations,
                                             Dictionary<string, string> FieldEdts,
                                             Dictionary<string, string> FieldEnums)> _tableMetaMemoryCache
    = new ConcurrentDictionary<string, (Dictionary<string, string>, Dictionary<string, string>, Dictionary<string, string>, Dictionary<string, string>)>(StringComparer.OrdinalIgnoreCase);

        // 在 TableMetadataHelper 类中添加全局缓存
        private static List<RelationEdge> _globalRelations = new List<RelationEdge>();
        private readonly string _relationCachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "relation_cache.json");


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
        public List<EnumItemModel> GetEnumDetails(string enumName, string _connString)
        {
            //if (_enumContentCache.TryGetValue(enumName, out var cachedList)) return cachedList;

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

            var usedValues = new HashSet<int>();
            foreach (var item in list)
            {
                if (int.TryParse(item.Value, out int v))
                {
                    usedValues.Add(v);
                }
            }

            int nextCandidate = 0;

            foreach (var item in list.Where(i => string.IsNullOrEmpty(i.Value)))
            {
                if (string.IsNullOrEmpty(item.Value))
                {
                    // 如果是 None 或 Create，且 0 还没被占用，优先给 0
                    if ((item.Name.Equals("None", StringComparison.OrdinalIgnoreCase) ||
                         item.Name.Equals("Create", StringComparison.OrdinalIgnoreCase)) &&
                        !usedValues.Contains(0))
                    {
                        item.Value = "0";
                        usedValues.Add(0);
                    }
                    else
                    {
                        // 寻找下一个最小的可用的非负整数
                        while (usedValues.Contains(nextCandidate))
                        {
                            nextCandidate++;
                        }
                        item.Value = nextCandidate.ToString();
                        usedValues.Add(nextCandidate);
                    }
                }
            }

            if (enumName == "NoYes")
            {
                list.Add(new EnumItemModel
                {
                    Name = "No",
                    Label = "否",
                    Value = "0"
                });
                list.Add(new EnumItemModel
                {
                    Name = "Yes",
                    Label = "是",
                    Value = "1"
                });
            }

            var sortedList = list.OrderBy(x => {
                int.TryParse(x.Value, out int v);
                return v;
            }).ToList();

            // 存入缓存
            _enumContentCache.TryAdd(enumName, sortedList);

            return sortedList;
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

                    if (string.IsNullOrEmpty(relatedTable)) continue;
                    foreach (var constraint in relation.Descendants("AxTableRelationConstraint"))
                    {
                        string fieldName = constraint.Element("Field")?.Value;
                        string relatedField = constraint.Element("RelatedField")?.Value;
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

        public async Task<List<TableFieldModel>> GetTableFieldsAsync(string tableName, string connString, Dictionary<string, string> labelCache)
        {
            List<TableFieldModel> _allFieldsData = new List<TableFieldModel>();
            var meta = GetMetadataFromXml(tableName);
            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connString))
            {
                string sql = @"SELECT f.name AS FieldName, t.name AS DataType, f.max_length AS Length 
                           FROM sys.columns f 
                           INNER JOIN sys.tables tb ON f.object_id = tb.object_id 
                           INNER JOIN sys.types t ON f.user_type_id = t.user_type_id 
                           WHERE tb.name = @tableName
                            ORDER BY f.name ";

                var dbFields = await conn.QueryAsync<dynamic>(sql, new { tableName });

                _allFieldsData = await Task.Run(() => {
                    var meta = GetMetadataFromXml(tableName);

                    return dbFields.Select(f => {
                        if (!meta.Labels.TryGetValue(f.FieldName, out string labelId) || string.IsNullOrEmpty(labelId))
                        {
                            if (meta.FieldEdts.TryGetValue(f.FieldName, out string edtName))
                            {
                                if (!string.IsNullOrEmpty(edtName))
                                {
                                    labelId = GetEdtLabel(edtName);
                                }
                                else
                                {
                                    if (meta.FieldEnums.TryGetValue(f.FieldName, out string enumName))
                                    {
                                        labelId = GetEnumLabel(enumName);
                                    }
                                }
                            }
                        }

                        string originalCaseFieldName = f.FieldName;

                        var actualKey = meta.Labels.Keys.FirstOrDefault(k => k.Equals(f.FieldName, StringComparison.OrdinalIgnoreCase));

                        if (actualKey != null)
                        {
                            originalCaseFieldName = actualKey;
                        }

                        string currentEnum = meta.FieldEnums.ContainsKey(f.FieldName) ? meta.FieldEnums[f.FieldName] : "";
                        return new TableFieldModel
                        {
                            EnumType = currentEnum,
                            FieldName = originalCaseFieldName,
                            ChineseName = LabelHelper.GetText(labelId, labelCache),
                            DataType = f.DataType,
                            Length = f.Length,
                            RelatedTable = meta.Relations.ContainsKey(f.FieldName) ? meta.Relations[f.FieldName] : ""
                        };
                    }).ToList();
                });
                return _allFieldsData;
            }
        }

        public List<RelationEdge> GetGlobalRelations()
        {
            if (_globalRelations.Count == 0 && File.Exists(_relationCachePath))
            {
                try
                {
                    string json = File.ReadAllText(_relationCachePath);
                    var cache = JsonConvert.DeserializeObject<List<RelationEdge>>(json);
                    if (cache != null) _globalRelations.AddRange(cache);
                }
                catch { /* 忽略读取错误 */ }
            }
            return _globalRelations;
        }

        // 程序启动或第一次打开窗口时调用，预扫描核心模块的所有 AxTable XML
        public void BuildGlobalRelationMap(string remotePath, bool forceRefresh = false)
        {
            if (!forceRefresh && _globalRelations.Count > 0) return;
            if (!forceRefresh && File.Exists(_relationCachePath))
            {
                GetGlobalRelations(); // 直接从缓存读
                return;
            }

            // 使用并发集合，确保多线程安全
            var concurrentRelations = new ConcurrentBag<RelationEdge>();
            //string[] corePackages = { "ApplicationSuite", "ApplicationFoundation", "ApplicationPlatform", "AOF","GeneralLedger","AGTI","IWS"
            //        , "IWS_InferfaceOutbound", "IWS_InterfaceBase", "IWS_InterfaceInbound","Directory","Dimensions" };
            var packages = Directory.GetDirectories(remotePath);
            // 获取所有待处理的文件夹路径
            var targetFolders = new List<(string FolderPath, bool IsExtension)>();
            foreach (var package in packages)
            {
                string packagePath = Path.Combine(remotePath, package);
                if (!Directory.Exists(packagePath)) continue;

                var modelDirs = Directory.GetDirectories(packagePath);
                foreach (var modelDir in modelDirs)
                {
                    if (Path.GetFileName(modelDir).Equals("Descriptor", StringComparison.OrdinalIgnoreCase)) continue;
                    string tablePath = Path.Combine(modelDir, "AxTable");
                    if (Directory.Exists(tablePath)) targetFolders.Add((tablePath, false));

                    // 2. 核心：扫描扩展表文件夹
                    string extPath = Path.Combine(modelDir, "AxTableExtension");
                    if (Directory.Exists(extPath)) targetFolders.Add((extPath, true));
                }
            }

            // 使用并行处理提升 3-5 倍速度
            Parallel.ForEach(targetFolders, folder =>
            {
                var files = Directory.GetFiles(folder.FolderPath, "*.xml");
                foreach (var file in files)
                {
                    string tableName = Path.GetFileNameWithoutExtension(file);
                    if (folder.IsExtension && tableName.Contains("."))
                    {
                        tableName = tableName.Split('.')[0];
                    }

                    ParseRelationsWithReader(file, tableName, concurrentRelations);
                }
            });

            _globalRelations = concurrentRelations.ToList();

            // 序列化保存（这一步也比较耗时，异步处理）
            SaveRelationCache();
        }

        private void ParseRelationsWithReader(string filePath, string tableName, ConcurrentBag<RelationEdge> list)
        {
            try
            {
                using (var reader = XmlReader.Create(filePath))
                {
                    string currentRelatedTable = "";
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            // 依然寻找 RelatedTable 节点
                            if (reader.Name == "RelatedTable")
                            {
                                currentRelatedTable = reader.ReadElementContentAsString();
                            }
                            else if (reader.Name == "AxTableRelationConstraint")
                            {
                                using (var subReader = reader.ReadSubtree())
                                {
                                    string f = "", rf = "";
                                    while (subReader.Read())
                                    {
                                        if (subReader.Name == "Field") f = subReader.ReadElementContentAsString();
                                        if (subReader.Name == "RelatedField") rf = subReader.ReadElementContentAsString();
                                    }
                                    if (!string.IsNullOrEmpty(currentRelatedTable))
                                    {
                                        list.Add(new RelationEdge
                                        {
                                            FromTable = tableName, // 此时已处理为 SalesTable
                                            FromField = f,
                                            ToTable = currentRelatedTable,
                                            ToField = rf
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { /* 忽略错误 */ }
        }

        private void SaveRelationCache()
        {
            try
            {
                // 1. 将 List 转换为格式化的 JSON 字符串
                // 使用 Newtonsoft.Json 序列化
                string json = JsonConvert.SerializeObject(_globalRelations, Newtonsoft.Json.Formatting.Indented);

                // 2. 写入到本地文件 (_relationCachePath 是你定义的 JSON 路径)
                File.WriteAllText(_relationCachePath, json);
            }
            catch (Exception ex)
            {
                // 这种后台缓存操作如果失败，建议记录日志或静默处理，不要卡死主流程
                System.Diagnostics.Debug.WriteLine("关联关系缓存保存失败: " + ex.Message);
            }
        }

        // 使用流式读取，不加载整个 XML 树
        private void ParseRelationsWithReader(string filePath, ConcurrentBag<RelationEdge> list)
        {
            string tableName = Path.GetFileNameWithoutExtension(filePath);
            try
            {
                using (var reader = XmlReader.Create(filePath))
                {
                    string currentRelatedTable = "";
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name == "RelatedTable")
                            {
                                currentRelatedTable = reader.ReadElementContentAsString();
                            }
                            else if (reader.Name == "AxTableRelationConstraint")
                            {
                                // 进入约束节点，提取 Field 和 RelatedField
                                using (var subReader = reader.ReadSubtree())
                                {
                                    string f = "", rf = "";
                                    while (subReader.Read())
                                    {
                                        if (subReader.Name == "Field") f = subReader.ReadElementContentAsString();
                                        if (subReader.Name == "RelatedField") rf = subReader.ReadElementContentAsString();
                                    }
                                    if (!string.IsNullOrEmpty(currentRelatedTable))
                                    {
                                        list.Add(new RelationEdge
                                        {
                                            FromTable = tableName,
                                            FromField = f,
                                            ToTable = currentRelatedTable,
                                            ToField = rf
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { /* 忽略异常文件 */ }
        }

        public bool TableExists(string tableName)
        {
            // 检查这个表是否在任何关系中作为起始表或目标表出现过
            return GetGlobalRelations().Any(r =>
                r.FromTable.Equals(tableName, StringComparison.OrdinalIgnoreCase) ||
                r.ToTable.Equals(tableName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取两个表之间详细的字段对应关系
        /// </summary>
        /// <param name="mainTable">当前正在查看的主表</param>
        /// <param name="relatedTable">点击的关联表名</param>
        public List<RelationConstraintModel> GetTableRelationDetails(string mainTable, string relatedTable, string currentFieldName)
        {
            var details = new List<RelationConstraintModel>();

            // 1. 获取主表的 XML 路径
            // 建议在你的 TableMetadataHelper 中维护一个 Dictionary<string, string> tableNameToPath
            _tablePathCache.TryGetValue(mainTable, out string xmlPath);

            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath)) return details;

            try
            {
                XElement xml = XElement.Load(xmlPath);
                var relationNodes = xml.Element("Relations")?.Elements("AxTableRelation")
                    .Where(r => r.Element("RelatedTable")?.Value.Equals(relatedTable, StringComparison.OrdinalIgnoreCase) == true);

                if (relationNodes != null)
                {
                    foreach (var rel in relationNodes)
                    {
                        var constraints = rel.Element("Constraints")?.Elements().ToList();
                        if (constraints == null) continue;

                        // 检查该关联是否包含我们点击的字段
                        bool isTargetRelation = constraints.Any(c =>
                            c.Element("Field")?.Value.Equals(currentFieldName, StringComparison.OrdinalIgnoreCase) == true);

                        if (isTargetRelation)
                        {
                            foreach (var con in constraints)
                            {
                                string type = con.Attribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"))?.Value
                                              ?? con.Name.LocalName;

                                var model = new RelationConstraintModel();

                                // 1. 处理标准字段对应关系
                                if (type.Contains("AxTableRelationConstraintField"))
                                {
                                    model.SourceField = con.Element("Field")?.Value;
                                    model.TargetField = con.Element("RelatedField")?.Value;
                                }
                                // 2. 处理固定值过滤 (RelatedFixed) - 解决您提到的枚举值显示问题
                                else if (type.Contains("AxTableRelationConstraintRelatedFixed"))
                                {
                                    model.SourceField = "(固定过滤)";
                                    string relatedField = con.Element("RelatedField")?.Value;
                                    string valueStr = con.Element("ValueStr")?.Value ?? con.Element("Value")?.Value;
                                    model.TargetField = $"{relatedField} == {valueStr}";
                                }

                                if (model.TargetField != null) details.Add(model);
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex) { /* 错误处理 */ }
            return details;

            return details;
        }
    }
}
