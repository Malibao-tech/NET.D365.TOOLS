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

        public string EnumType { get; set; }
    }
}
