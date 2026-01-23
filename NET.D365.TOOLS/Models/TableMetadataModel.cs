using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NET.D365.TOOLS.Models
{
    /// <summary>
    /// 表元数据中间模型：用于汇总从 XML 解析出的元数据
    /// </summary>
    public class TableMetadataModel
    {
        public string TableName { get; set; }

        /// <summary>
        /// 字段名 -> 翻译后的中文名 (来自 XML 的 Label 或 EDT 继承)
        /// </summary>
        public Dictionary<string, string> FieldLabels { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 字段名 -> 关联的表名 (来自 XML 的 Relations)
        /// </summary>
        public Dictionary<string, string> Relations { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 字段名 -> 枚举类型名 (来自 XML 的 EnumType)
        /// </summary>
        public Dictionary<string, string> FieldEnums { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> FieldEdts { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 最终用于 GridView 显示的字段集合（包含 SQL 物理信息和 XML 逻辑信息）
        /// </summary>
        public List<TableFieldModel> Fields { get; set; } = new List<TableFieldModel>();
    }
}
