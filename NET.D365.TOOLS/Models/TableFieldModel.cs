using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NET.D365.TOOLS.Models
{
    public class TableFieldModel
    {
        public string FieldName { get; set; }
        public string DataType { get; set; }
        public int Length { get; set; }
        public string ChineseName { get; set; }
        public string RelatedTable { get; set; }

        // 新增：存储详细的关联映射，例如 "SalesId == SalesId"
        public string RelationConstraint { get; set; }

        // 新增：标识是否为外键，用于 UI 染色
        public bool IsForeignKey => !string.IsNullOrEmpty(RelatedTable);

        public string EnumType { get; set; }
    }
}
