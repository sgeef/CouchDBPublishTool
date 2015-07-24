using C2701_NoSQLPublishTool.Helpers;
using C2701_NoSQLPublishTool.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace C2701_NoSQLPublishTool.Managers
{
    public class DatabaseManager
    {
        public DBConfiguration Profile { get; private set; }
        private string APIPassword { get; set; }
        public static List<DesignDocumentIndexation> IndexationCheckList { get; set; }
        private Boolean Debug = false;
        private Boolean ConfirmOnly = false;

        public const string TEMPORAL_PREFIX = "temp_";

        public DatabaseManager(DBConfiguration profile)
        {
            Profile = profile;
        }
        /// <summary>
        /// This method publishes to Local or live according to what the user specifies
        /// </summary>
        public void Publish(bool debug = false, bool confirmOnly = false)
        {
            Debug = debug;
            ConfirmOnly = confirmOnly;
            Publish(Profile.LatestBackupFolderName).Wait();
        }

        /// <summary>
        /// This method publishes to Local or live according to what the user specifies
        /// All DesignDocuments specified in an profile will be searched.
        /// All documents will be checked for when indexed is finished, so that we can overwrite the main view. 
        /// This way we have zero downtime.
        /// </summary>
        /// <param name="specificBackup"></param>
        public async Task Publish(string specificBackup)
        {
            if (!SecurityChecks() && !DoesBackupExist(specificBackup))
            {
                return;
            }

            //Get isLive and check if APIKey is present.
            string dbEnvironment = RequestEnvironment();

            //Communicate backup folder used
            //TODO: make variable.
            Console.WriteLine("Following folder is selected for publishing: " + Profile.LatestBackupFolderName + "\n");

            //Start publish.
            await DoPublishAndCheck(Profile.DesignDocuments, dbEnvironment, true);

            //Ask if the developer likes to continue. (As the indexation for maybe 80% is already done, he might want to republish after this to fix
            //any failed design doc.)
            Console.WriteLine();
            Console.WriteLine("Do you want to completed the publish an overwrite the  design documents? ( Y/N )");
            ConsoleKeyInfo anwser = Console.ReadKey();
            if (anwser.KeyChar == 'y')
            {
                //By default override all
                List<string> designDocumentsToOverride = IndexationCheckList.Select(x => x.DesignDocumentName).ToList();
                OutputHelper.WriteProgress("Publish overwrite process started");

                //Step 3) When done, overwrite the main view && Step 4) Query the main view to check everything is ok.
                await DoPublishAndCheck(Profile.DesignDocuments, dbEnvironment, false);
                OutputHelper.WriteProgress("Overwrite and indexation checks finished");
            }
            else
            {
                OutputHelper.WriteProgress("Publish aborted!!!!");
            }

            //If published or not, we will cleanup any temporal documents.
            //Step 5) cleanup!
            CleanupTemporalPublish(IndexationCheckList.Select(x => x.DesignDocumentName).ToList(), dbEnvironment).Wait();
            OutputHelper.WriteProgress("Publish process ended");
        }

        /// <summary>
        /// This is the method that does the real publish magic and Checks if everything goes well..
        /// </summary>
        /// <param name="designDocuments"></param>
        /// <param name="isLive"></param>
        /// <param name="isTemporal"></param>
        private async Task DoPublishAndCheck(List<string> designDocuments, string dbEnvironment, bool isTemporal)
        {
            //Init IndexedCheckList for indexing tasks.
            IndexationCheckList = new List<DesignDocumentIndexation>();

            foreach (string designDocument in designDocuments)
            {
                //Step 1) create an Copy View for every view on the live server
                if ((isTemporal && ConfirmOnly) || PublishSingleDesignDocument(designDocument, dbEnvironment, isTemporal))
                {
                    //Step2) Start indexation check.
                    IndexationCheckList.Add(new DesignDocumentIndexation(Profile, APIPassword, designDocument, dbEnvironment, isTemporal, Debug));
                }
            }

            //3) Notification Process / Check if Indexation is finished or not?
            while (IndexationCheckList.Any(x=>!x.IsCompleted(true)))
            {
                int completedTasks = IndexationCheckList.Where(x => x.IsCompleted()).Count(); 
                int tasksLeftToFinish = IndexationCheckList.Count() - completedTasks;

                //Inform the user.
                OutputHelper.WriteProgress(completedTasks + " check(s) finished, Waiting for " + tasksLeftToFinish + " check(s).");

                await Task.Delay(5000);
            }

            //Final stats.
            OutputHelper.WriteProgress("Indexation checks ended");
        }



        /// <summary>
        /// Deletes a list of temporal design documents. (cleanup)
        /// </summary>
        /// <param name="designDocuments"></param>
        /// <param name="isLive"></param>
        private async Task CleanupTemporalPublish(List<string> designDocuments, string dbEnvironment)
        {
            HTTPRequestManager RequestManager = new HTTPRequestManager();
            Console.WriteLine();
            OutputHelper.WriteProgress("Cleanup of temporal design documents started");
            foreach (string designDocumentName in designDocuments)
            {
                try
                {
                    //Rewrite document name to temporal documentName
                    string tempDesignDocumentName = TEMPORAL_PREFIX + designDocumentName;

                    // create deleteURL
                    string designDocumentURL = Profile.GetDesignDocumentURL(dbEnvironment, tempDesignDocumentName);

                    //Get revision for deletion
                    string response = await RequestManager.GetContent(designDocumentURL, null, "GET", Profile.GetEnvironment(dbEnvironment).APIKey, APIPassword);

                    //Convert Response to model, to get revisionNumber.
                    DesignDocument ddToDelete = JsonConvert.DeserializeObject<DesignDocument>(response);

                    // MAKE HTTP Delete request
                    HttpWebResponse deleteResponse = await RequestManager.SendRequest(designDocumentURL + "?rev=" + ddToDelete._rev, null, "DELETE", Profile.GetEnvironment(dbEnvironment).APIKey, APIPassword);

                    if ((int)deleteResponse.StatusCode == 200)
                    {
                        OutputHelper.WriteProgress("Design document " + tempDesignDocumentName + " successfully deleted");
                    }
                    else
                    {
                        OutputHelper.WriteProgress("Deletion of temporal design document: " + tempDesignDocumentName + " failed! Try manually.");
                    }
                }
                catch (Exception exc)
                {
                    OutputHelper.WriteProgress("Deletion of temporal design document: " + TEMPORAL_PREFIX + designDocumentName + " failed! Try manually.");
                }
            }

            OutputHelper.WriteProgress("Clean-up finished");
        }


        /// <summary>
        /// Publishes an single view. (temporal or new/overwrite of existing)
        /// </summary>
        /// <param name="designDocumentName"></param>
        /// <param name="isLive"></param>
        /// <param name="isBackup"></param>
        /// <returns></returns>
        private bool PublishSingleDesignDocument(string designDocumentName, string dbEnvironment, bool isTemporal)
        {
            string backupOriginalDesignDocumentName = designDocumentName;
            string backupFolderPath = Profile.BackupPath + "/" + Profile.LatestBackupFolderName;
            string backupDesignDocumentPath = Profile.BackupPath + "/" + Profile.LatestBackupFolderName + "/" + designDocumentName;

            if (isTemporal)
            {
                designDocumentName = TEMPORAL_PREFIX + designDocumentName;
            }

            //Checks
            if (!Directory.Exists(backupFolderPath))
            {
                OutputHelper.WriteProgress("Backup folder not found \"" + backupFolderPath + "\"");
                return false;
            }

            if (!Directory.Exists(backupDesignDocumentPath))
            {
                OutputHelper.WriteProgress("Backup folder of design document not found \"" + backupDesignDocumentPath + "\"");
                return false;
            }

            //TEMPORAL CODE:
            //REwrite _id file in the view folder generated by erica.
            if (isTemporal)
                RewriteViewId(backupDesignDocumentPath, "_design/" + designDocumentName);

            //Launch erica
            bool Success = LaunchErica("push \"" + backupDesignDocumentPath + "\" " + Profile.GetDatabaseURL(dbEnvironment));// + " --docid " + "_design/" + designDocumentName);

            //TEMPORAL CODE:
            //REwrite back
            if (isTemporal)
                RewriteViewId(backupDesignDocumentPath, "_design/" + backupOriginalDesignDocumentName);

            if (Success)
            {
                OutputHelper.WriteProgress("Push of design document: " + designDocumentName + " done");
                return true;
            }
            else
            {
                OutputHelper.WriteProgress("Push failed for design document " + designDocumentName);
                return false;
            }
        }

        private void RewriteViewId(string backupDesignDocumentPath, string p)
        {
            File.WriteAllText(backupDesignDocumentPath + "/_id", p);
        }

        /// <summary>
        /// This method backups local or live, according to what the user specifies. 
        /// </summary>
        public void Backup()
        {
            //Ask if local or live. (Should be same process)
            if (!SecurityChecks())
            {
                return;
            }

            //Get isLive and check if APIKey is present.
            string dbEnvironment = RequestEnvironment();

            //Create backup and call erica.
            string backupFolderName = "backup_" + DateTime.UtcNow.ToString("yyMMdd_HHmmss") + "_" + dbEnvironment;
            string backupFolderPath = Profile.BackupPath + "/" + backupFolderName;

            //Check if we know the DesignDocuments that we must backup
            if (Profile.DesignDocuments == null || Profile.DesignDocuments.Count() == 0)
            {
                if (!RequestDesignDocumentNames())
                {
                    //Break if now design documents get specified.
                    return;
                }
            }

            foreach (string designDocument in Profile.DesignDocuments)
            {
                if (!BackupSingleDesignDocument(backupFolderPath, designDocument, dbEnvironment))
                {
                    //Delete folder and subfolders.
                    Directory.Delete(backupFolderPath, true);
                    Console.WriteLine("Something went wrong with the backup. Aborting Backup...");
                    Console.WriteLine("Directory is removed to prevent incomplete submissions.");
                    return;
                }
            }

            //If everything went wel.. Save backup folder name
            Profile.LatestBackupFolderName = backupFolderName;
            SettingManager.Update(Profile);

            Console.WriteLine("Backup completed Successfully");
        }

        /// <summary>
        /// This method gets the DesignDocuments and return false on any invalid input.
        /// </summary>
        /// <returns></returns>
        private bool RequestDesignDocumentNames()
        {
            Console.WriteLine("No design documents are set in your profile, please specify the design documents you like to backup.");
            Console.WriteLine("The design document names must be separated by an comma");
            string designDocument = Console.ReadLine();

            if (String.IsNullOrEmpty(designDocument))
            {
                return false;
            }

            //Remove any extra spaces
            designDocument = designDocument.Trim();

            //Remove any extra commas
            designDocument = designDocument.TrimEnd(',');

            //Convert the comma separated string to an list.
            Profile.DesignDocuments = designDocument.Split(',').ToList();
            SettingManager.Update(Profile);

            //Complete.
            return true;
        }

        /// <summary>
        /// This method checks if the users wants to publish/backup to live and if the API password is present.
        /// </summary>
        /// <returns></returns>
        private string RequestEnvironment()
        {
            DBEnvironment environment = null;

            Console.Write("On which environment do you want to complete this action? ( ");
            foreach(DBEnvironment env in Profile.Environments)
            {
                Console.Write(env.Name + " ");
            }
            Console.WriteLine(")");

            string anwser = Console.ReadLine();
            environment = Profile.GetEnvironment(anwser);
                        
            Console.WriteLine(Profile.GetEnvironment(anwser).Name + "environment selected!\n");

            //Set default password
            APIPassword = environment.APIPassword;

            //Check if APIKey is present
            if (String.IsNullOrEmpty(APIPassword))
            {
                //Request password
                Console.WriteLine("Please provide the ApiPassword to publish to live");
                Console.Write("APIPassword: ");
                APIPassword = Console.ReadLine();
            }
            return environment.Name;
        }

        /// <summary>
        /// handles an single backup.
        /// </summary>
        /// <param name="backupFolderPath"></param>
        /// <param name="designDocumentName"></param>
        /// <param name="isLive"></param>
        /// <returns></returns>
        private bool BackupSingleDesignDocument(string backupFolderPath, string designDocumentName, string dbEnvironment)
        {
            //check if folder exists
            string backupPath = backupFolderPath + "/" + designDocumentName;
            if (!Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }

            //Launch Erica
            bool Success = LaunchErica("clone " + Profile.GetDesignDocumentURL(dbEnvironment, designDocumentName) + " \"" + backupPath + "\"");
            if (Success)
            {
                Console.WriteLine("Backup of " + designDocumentName + " completed");
                return true;
            }
            else
            {
                Console.WriteLine("Backup failed for " + designDocumentName);
                return false;
            }

        }

        /// <summary>
        /// Checks Profile and if backup path is available, if not requests it.
        /// </summary>
        /// <returns></returns>
        private bool SecurityChecks()
        {
            // 0) Check if Profile is set
            if (Profile == null)
            {
                Console.WriteLine("No Profile found, please load a profile by calling /Load");
                return false;
            }

            // 1) Check if BackupPath is set && Check if the BackupPath is valid
            if (Profile.BackupPath == null || !Directory.Exists(Profile.BackupPath))
            {
                if (!RequestBackupPath())
                {
                    Console.WriteLine("No valid backup Path was retrieved, please restart the process.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if the backups exists for the publish process.
        /// </summary>
        /// <param name="backupFolderName"></param>
        /// <returns></returns>
        private bool DoesBackupExist(string backupFolderName)
        {
            if (Directory.Exists(Profile.BackupPath + "/" + backupFolderName))
            {
                return true;
            }
            Console.WriteLine("The specified backup was not found. Publish process stopped.");
            return false;
        }

        /// <summary>
        /// Get's a backup path from the user.
        /// </summary>
        /// <returns></returns>
        private bool RequestBackupPath()
        {
            //Try to find a path
            Console.WriteLine("No valid backup path found, please specify one.");
            Console.Write("path: ");
            Profile.BackupPath = Console.ReadLine();
            Console.WriteLine();

            //Check if the path is correct:
            if (!Directory.Exists(Profile.BackupPath))
            {
                Profile.BackupPath = null;
                return false;
            }

            //Save profile changes
            SettingManager.Update(Profile);
            return true;
        }

        /// <summary>
        /// Launches the erica program. For more information: https://github.com/benoitc/erica
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private bool LaunchErica(string arguments)
        {
            //erica download url: https://github.com/benoitc/erica

            if (Debug)
            {
                Console.WriteLine("Debug >>" + "erica " + arguments);
            }

            ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", "/c erica " + arguments);
            procStartInfo.UseShellExecute = false;
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.CreateNoWindow = true;

            var proc = new Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            //Start Diagnostics if an error occures.
            if (output.Contains("ERROR"))
            {
                Console.WriteLine();
                Console.WriteLine("Diagnostic of Error: \n " + output);
                Console.WriteLine();
            }
            //var exitCode = proc.ExitCode;
            proc.Close();

            //Return true if not contains error.
            return !output.Contains("ERROR");
        }
    }
}
