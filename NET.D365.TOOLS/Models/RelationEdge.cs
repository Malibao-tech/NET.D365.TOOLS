using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NET.D365.TOOLS.Models
{
    public class RelationEdge
    {
        public string FromTable { get; set; }
        public string FromField { get; set; }
        public string ToTable { get; set; }
        public string ToField { get; set; }

        public string RelationType { get; set; }
    }
}
