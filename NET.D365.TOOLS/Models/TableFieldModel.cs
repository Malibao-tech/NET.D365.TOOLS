using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NET.D365.TOOLS.Models
{
    public class TableFieldModel
    {
        public string FieldName { get; set; }    // 字段名
        public string ChineseName { get; set; }  // 中文名 (Label)
        public string DataType { get; set; }     // 数据类型 (SQL)
        public int? Length { get; set; }         // 长度 (SQL)
        public string RelatedTable { get; set; } // 关联表
        public string EnumType { get; set; }     // 枚举类型名
    }
}
