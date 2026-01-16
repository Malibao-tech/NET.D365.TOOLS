using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NET.D365.TOOLS.Models
{
    public class MetadataPathCache
    {
        public DateTime LastUpdateTime { get; set; }
        public Dictionary<string, string> TablePaths { get; set; }
        public Dictionary<string, string> EdtPaths { get; set; }
        public Dictionary<string, string> EnumPaths { get; set; }
        public Dictionary<string, List<string>> EnumExtensions { get; set; }
        public Dictionary<string, List<string>> TableExtensions { get; set; }
    }
}
