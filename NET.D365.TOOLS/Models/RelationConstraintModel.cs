using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NET.D365.TOOLS.Models
{
    public class RelationConstraintModel
    {
        public string SourceField { get; set; }
        public string TargetField { get; set; }
        public string ConstraintType { get; set; } // 系统、用户定义、或 EDT
    }
}
