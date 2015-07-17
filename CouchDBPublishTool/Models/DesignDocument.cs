using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C2701_NoSQLPublishTool.Models
{
    public class DesignDocument
    {
        public string _id { get; set; }
        public string _rev { get; set; }
        public Dictionary<string, object> views {get; set;}
    }
}
