using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NET.D365.TOOLS.Models
{
    public class TableMetadata
    {
        public string TableName { get; set; }
        public string Extends { get; set; }
        public List<TableFieldModel> Fields { get; set; } = new List<TableFieldModel>();

        // 这里的 Relations 用于记录哪个字段对应哪个表，方便 DataGridView 显示
        public Dictionary<string, string> Relations { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
