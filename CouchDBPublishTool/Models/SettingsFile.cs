using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C2701_NoSQLPublishTool.Models
{
    /// <summary>
    /// Container for Multiple db configurations
    /// </summary>
    public class SettingsFile
    {
        public SettingsFile()
        {
            Databases = new List<DBConfiguration>();
        }

        public List<DBConfiguration> Databases { get; set; }
    }
}
