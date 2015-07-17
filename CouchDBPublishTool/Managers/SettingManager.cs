using C2701_NoSQLPublishTool.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C2701_NoSQLPublishTool.Managers
{

    public static class SettingManager
    {
        /// <summary>
        /// All Settings from the settings file
        /// </summary>
        private static SettingsFile _settings;

        public static SettingsFile Settings
        {
            get 
            { 
                if(_settings == null)
                {
                    //Load settings if not loaded yet.
                    Load();
                }
                return _settings; 
            }
            private set { _settings = value; }
        }
        

        /// <summary>
        /// Default name of the SettingsFile
        /// </summary>
        private const string DEFAULT_FILENAME = "DatabaseSettings.json";

        /// <summary>
        /// Default method to Save the changes made to the Settings Object
        /// </summary>
        /// <param name="fileName"></param>
        public static void Save(string fileName = DEFAULT_FILENAME)
        {
            File.WriteAllText(fileName, JsonConvert.SerializeObject(Settings));
        }

        //public void Save(T pSettings, string fileName = DEFAULT_FILENAME)
        //{
        //    File.WriteAllText(fileName, JsonConvert.SerializeObject(pSettings));
        //}

        /// <summary>
        /// Default method to load the Settings from the file.
        /// </summary>
        /// <param name="fileName"></param>
        public static void Load(string fileName = DEFAULT_FILENAME)
        {
            SettingsFile t = new SettingsFile();
            if (File.Exists(fileName))
                t = JsonConvert.DeserializeObject<SettingsFile>(File.ReadAllText(fileName));
            Settings = t;
        }

        public static void Update(DBConfiguration model)
        {
            DBConfiguration oldModel = Settings.Databases.Where(x => x.Name == model.Name).FirstOrDefault();
            if (oldModel != null)
            {
                Settings.Databases.Remove(oldModel);
            }
            Settings.Databases.Add(model);
            Save();
        }
    }

}
