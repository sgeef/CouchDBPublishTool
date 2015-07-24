using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C2701_NoSQLPublishTool.Helpers
{
    public class OutputHelper
    {
        public static void WriteProgress(string message)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss") + " - Publishing] " + message);
        }
    }
}
