using C2701_NoSQLPublishTool.Helpers;
using C2701_NoSQLPublishTool.Managers;
using C2701_NoSQLPublishTool.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C2701_NoSQLPublishTool
{
    public class Program
    {
        private static DatabaseManager DBManager { get; set; }

        static void Main(string[] args)
        {
            //Call Init
            Init();
        }

        static void Init()
        {
            //For user action
            Console.WriteLine("Welcome to the couchdb/cloudant Publish process console! (By Tinyloot)");
            Console.WriteLine("Type /help for more info.");
            Console.WriteLine();

            //By default let the user select an profile
            LoadProfile();

            //After this wait for new input.
            InputHelper.Wait();
        }

        /// <summary>
        /// Show help info.
        /// </summary>
        public static void ShowHelp()
        {
            Console.WriteLine("<< Help Pages >> ");
            Console.WriteLine("/add-dd\t\t Adds an design document to the Profile");
            Console.WriteLine("/add-env\t\t Adds an environment to the current Profile");
            Console.WriteLine("/create\t\t Creates a new Database Profile");
            Console.WriteLine("/help\t\t Shows information on the commands available");
            Console.WriteLine("/info\t\t Shows information about the current profile");
            Console.WriteLine("/load\t\t Let's you choose an profile");
            Console.WriteLine("/publish\t Starts the publish process on the loaded profile");
            Console.WriteLine("/quit\t\t Quits the process");
            Console.WriteLine("/remove-dd\t\t Removes an design document from the Profile");
        }

        #region Profile Methods
        /// <summary>
        /// Shows all profile data.
        /// </summary>
        public static void ShowProfileInfo()
        {
            if(DBManager == null || DBManager.Profile == null)
            {
                Console.WriteLine("No Profile selected, please call /Load");
                return;
            }

            Console.WriteLine("Profile loaded: " + DBManager.Profile.Name);
            Console.WriteLine("Environments: ");
            if (DBManager.Profile.Environments != null)
            {
                foreach (DBEnvironment env in DBManager.Profile.Environments)
                {
                    Console.WriteLine(env.Name + " URL\t" + env.URL);
                    Console.WriteLine(env.Name + " API Key:\t" + env.APIKey);
                    Console.WriteLine(env.Name + " API Password:\t" + (String.IsNullOrEmpty(env.APIPassword) ? "(None)" : "(Hidden)"));
                }
            }

            Console.Write("Design Documents: \t");

            if (DBManager.Profile.DesignDocuments != null)
            {
                foreach (string s in DBManager.Profile.DesignDocuments)
                {
                    Console.Write(s + ", ");
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Creates an Database Profile
        /// </summary>
        public static void CreateProfile()
        {
            DBConfiguration profile = new DBConfiguration();
            profile.Name = InputHelper.RequestInput("Profile Name: ");
            //profile.LiveUsername = InputHelper.RequestInput("Live Account (Username): ");
            //profile.LiveAPIKey = InputHelper.RequestInput("Live API Key: ");
            //profile.LiveAPIPassword = InputHelper.RequestInput("Live API Password: ");
            //profile.LiveURL = InputHelper.RequestInput("Live URL (without db): ", "https://" + profile.LiveUsername + ".cloudant.com/");
            //profile.LocalURL = InputHelper.RequestInput("Local URL (without db): ", "http://localhost:5984/");
            ////config.ViewNames

            //Security Checks
            if (SettingManager.Settings.Databases.Where(x => x.Name.Equals(profile.Name, StringComparison.InvariantCultureIgnoreCase)).Count() > 0)
            {
                Console.WriteLine("There already exists an profile with the name: " + profile.Name);
                Console.WriteLine("Do you want to override it? ( Y/N )");
                ConsoleKeyInfo anwser = Console.ReadKey(true);
                if (anwser.KeyChar == 'y')
                {
                    //Override!!!
                    SettingManager.Update(profile);

                    //Load Profile.
                    DBManager = new DatabaseManager(profile);
                    return;
                }
                else
                {
                    Console.WriteLine("Create aborted!");
                    return;
                }
                //END OF THIS OPTION. (We don't want to save double!)
            }
            else if (String.IsNullOrEmpty(profile.Name))
            {
                Console.WriteLine("Database name can't be empty!");
                return;
            }

            //Add the new configuration
            SettingManager.Settings.Databases.Add(profile);

            //Load Profile.
            DBManager = new DatabaseManager(profile);

            //Save the configuration to the file.
            SettingManager.Save();

            Console.WriteLine("Profile \"" + profile.Name + "\" Saved and Loaded!");
            Console.WriteLine("Please call /add-env to add environments to this profile!");
        }

        /// <summary>
        /// Let's the user choose between the available database profiles.
        /// </summary>
        public static void LoadProfile()
        {
            //Load Settings File
            SettingManager.Load();

            if (SettingManager.Settings.Databases.Count() > 0)
            {
                //Check if we can autload an profile.
                if (SettingManager.Settings.Databases.Count() == 1)
                {
                    DBManager = new DatabaseManager(SettingManager.Settings.Databases.FirstOrDefault());
                    Console.WriteLine("Database Configuration for: " + DBManager.Profile.Name + " automatically loaded!");
                    return;
                }

                //Communicate with user.
                Console.WriteLine("Which profile do you want to load?");

                //Build string with configuration profiles values
                StringBuilder builder = new StringBuilder();
                builder.Append("Profile options: ");
                foreach (DBConfiguration config in SettingManager.Settings.Databases)
                {
                    builder.Append(config.Name);
                    builder.Append(", ");
                }

                Console.WriteLine(builder.ToString().TrimEnd(' ', ','));

                //Get profile choice
                Console.Write("Profile: ");
                string profileName = Console.ReadLine();
                DBConfiguration profile = SettingManager.Settings.Databases.Where(x => x.Name.Equals(profileName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                
                //Select profile
                Console.WriteLine();

                //Check if Database is selected
                if (profile == null)
                {
                    Console.WriteLine("No Database found with the name: " + profileName);
                    LoadProfile();
                }
                else
                {
                    DBManager = new DatabaseManager(profile);
                    Console.WriteLine("Database Configuration for: " + profileName + " SuccessFully loaded!");
                }

                //End of load flow
            }
            else
            {
                Console.WriteLine("No database profiles found! Profile Creation started:");
                CreateProfile();
                //End of Create flow.
            }
            //End of handling. New Command expected.
        }

        #endregion

        internal static void Publish(bool debug = false, bool confirmOnly = false)
        {
            if (DBManager == null)
            {
                Console.WriteLine("No profile is loaded, please first call /Load");
                return;
            }
            DBManager.Publish(debug, confirmOnly);
        }

        internal static void Backup()
        {
            if(DBManager == null)
            {
                Console.WriteLine("No profile is loaded, please first call /Load");
                return;
            }
            DBManager.Backup();
        }

        internal static void AddDesignDocument()
        {
            if (DBManager == null)
            {
                Console.WriteLine("No profile is loaded, please first call /Load");
                return;
            }

            Console.WriteLine("What is the name of the design document you want to add?");
            string viewName = Console.ReadLine();

            if(DBManager.Profile.DesignDocuments == null)
            {
                DBManager.Profile.DesignDocuments = new List<string>();
            }
            DBManager.Profile.DesignDocuments.Add(viewName.ToLower());

            Console.WriteLine("The design document " + viewName + " is added!");

            //Update the settings file.
            SettingManager.Update(DBManager.Profile);
        }

        internal static void AddDBEnvironment()
        {
            if (DBManager == null)
            {
                Console.WriteLine("No profile is loaded, please first call /Load");
                return;
            }

            string name = InputHelper.RequestInput("Environment Name: ");
            string apiKey = InputHelper.RequestInput("API Key: ");
            string apiPassword = InputHelper.RequestInput("API Password: ");
            string url = InputHelper.RequestInput("URL: ");

            if (DBManager.Profile.Environments == null)
            {
                DBManager.Profile.Environments = new List<DBEnvironment>();
            }

            if (DBManager.Profile.GetEnvironment(name) == null)
            {
                DBManager.Profile.Environments.Add(new DBEnvironment()
                {
                    Name = name,
                    URL = url, 
                    APIKey = apiKey,
                    APIPassword = apiPassword
                });
                Console.WriteLine("The environment " + name + " is added!");

                //Update the settings file.
                SettingManager.Update(DBManager.Profile);
            }
            else
            {
                Console.WriteLine("The environment could not be added, due to duplicate name!");
            }

        }

        internal static void RemoveDesignDocument()
        {
            if (DBManager == null)
            {
                Console.WriteLine("No profile is loaded, please first call /Load");
                return;
            }

            Console.WriteLine("What is the name of the design document you want to remove?");
            string viewName = Console.ReadLine();

            if(!DBManager.Profile.DesignDocuments.Contains(viewName))
            {
                Console.WriteLine("The design document " + viewName + " was not found!");
                return;
            }

            DBManager.Profile.DesignDocuments.Remove(viewName.ToLower());

            Console.WriteLine("The design document " + viewName + " is removed!");

            //Update the settings file.
            SettingManager.Update(DBManager.Profile);
        }
    }
}
