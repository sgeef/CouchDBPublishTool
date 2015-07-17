using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C2701_NoSQLPublishTool.Models
{
    public class DBConfiguration
    {
        private const string DESIGN_DOCUMENT_URL_PATH = "/_design/";

        //Profile information
        public string Name { get; set; }

        //Lists
        public List<DBEnvironment> Environments { get; set; }
        public List<string> DesignDocuments { get; set; }
        
        // Backup info
        public string LatestBackupFolderName { get; set; }
        public string BackupPath { get; set; }

        /// <summary>
        /// For serialization
        /// </summary>
        public DBConfiguration()
        {

        }

        public string GetDatabaseURL(string name, bool credentials = true)
        {
            DBEnvironment environment = GetEnvironment(name);
            string url = environment.URL;
            if (url.Contains("localhost") || !credentials)
            {
                return url;
            }
            else
            {
                string cleanLiveUrl = url.Replace("http://", "").Replace("https://", "").TrimEnd('/');
                return "https://" + environment.APIKey + ":" + environment.APIPassword + "@" + cleanLiveUrl + "/";
            }
        }

        public string GetDesignDocumentURL(string name, string designDocumentName)
        {
            return GetDatabaseURL(name) + DESIGN_DOCUMENT_URL_PATH + designDocumentName; 
        }

        public DBEnvironment GetEnvironment(string name)
        {
            return Environments.Where(x => x.Name.ToLower() == name.ToLower()).FirstOrDefault();
        }
    }
}
