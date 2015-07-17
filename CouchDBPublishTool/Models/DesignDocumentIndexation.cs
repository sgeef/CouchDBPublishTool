using C2701_NoSQLPublishTool.Helpers;
using C2701_NoSQLPublishTool.Managers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace C2701_NoSQLPublishTool.Models
{
    public class DesignDocumentIndexation
    {
        public string DesignDocumentURL { get; private set; }
        public string DesignDocumentName { get; private set; }
        public string TemporalDesignDocumentName
        {
            get
            {
                return DatabaseManager.TEMPORAL_PREFIX + DesignDocumentName; 
            }
        }
        public string DBEnvironmentName { get; private set; }
        public bool IsTemporal { get; private set; }
        public bool Debug { get; set; }
        private string APIPassword { get; set; }
        private DBConfiguration Profile { get; set; }
        private string ViewURL { get; set; }
        private Task IndexTask { get; set; }
        private bool Succeeded { get; set; }

        public DesignDocumentIndexation(DBConfiguration profile, string apiPassword, string designDocumentName, string dbEnvironmentName, bool isTemporal, bool debug = false)
        {
            //for main check
            Succeeded = false;

            //Set design document name
            DesignDocumentName = designDocumentName;
            APIPassword = apiPassword;
            Profile = profile;

            //Set is Live and is temporal
            DBEnvironmentName = dbEnvironmentName;
            IsTemporal = isTemporal;

            //Set url to call
            if (!IsTemporal)
            {
                DesignDocumentURL = profile.GetDesignDocumentURL(DBEnvironmentName, DesignDocumentName);
            }
            else
            {
                DesignDocumentURL = profile.GetDesignDocumentURL(DBEnvironmentName, TemporalDesignDocumentName);
            }

            //Start the indexation!
            Start();
        }

        /// <summary>
        /// Try to start the indexation task.
        /// </summary>
        /// <returns></returns>
        public bool Start()
        {
            if (IndexTask == null || (IndexTask != null && IndexTask.IsCompleted && Succeeded == false))
            {
                IndexTask = CheckDesignDocumentIndexation();
                return true;
            }
            return false;
        }

        public bool IsCompleted(bool restartIfPossible = false)
        {
            bool isCompleted = (IndexTask != null && IndexTask.IsCompleted && Succeeded);

            if ((IndexTask.IsCompleted || IndexTask.IsFaulted || IndexTask.IsCanceled) && Succeeded == false && restartIfPossible)
            {
                //Start new task, if the task finished and did not succeed.
                IndexTask = CheckDesignDocumentIndexation();
                OutputHelper.WriteProgress("Restarted indexation for " + DesignDocumentName);
            }

            return isCompleted;
        }

        /// <summary>
        /// This method checks if the view is indexed by requesting output with low priority
        /// </summary>
        /// <param name="designDocumentName"></param>
        /// <param name="isLive"></param>
        /// <param name="isTemporal"></param>
        /// <returns></returns>
        private async Task CheckDesignDocumentIndexation()
        {
            try
            {
                //Advice from cloudant.
                //Calling the view will make it build at an elevated rate versus the background level we do, 
                //so I would suggest adding a special HTTP header 'x-cloudant-io-priority: low', to ensure it runs at the background priority.
                HTTPRequestManager RequestManager = new HTTPRequestManager();
                if (String.IsNullOrEmpty(ViewURL))
                {

                    //start the get request
                    string response;

                    if (Debug)
                    {
                        OutputHelper.WriteProgress("step1: get view names for (" + DesignDocumentName + ")");
                        OutputHelper.WriteProgress("step1 (url): " + DesignDocumentURL);
                    }

                    //Check the  design document
                    response = await RequestManager.GetContent(DesignDocumentURL, null, "GET", Profile.GetEnvironment(DBEnvironmentName).APIKey, APIPassword);//900000
 
                    if (response == null)
                    {
                        OutputHelper.WriteProgress("No response received: design document not found (" + DesignDocumentName + ")");
                        throw new Exception("DesignDocument not found");
                    }


                    //Convert Response to model, to get revisionNumber.
                    DesignDocument designdoc = JsonConvert.DeserializeObject<DesignDocument>(response);

                    if (designdoc == null || designdoc.views == null || designdoc.views.Count() == 0)
                    {
                        OutputHelper.WriteProgress("Something went wrong in the publish and the design document was not found.(" + DesignDocumentName + ")");
                        throw new Exception("DesignDocument not found");
                    }

                    //Debug message
                    if (Debug)
                        OutputHelper.WriteProgress("step2: viewNames received (" + DesignDocumentName + ")");

                    //List viewnames
                    List<string> viewNames = designdoc.views.Select(x => x.Key).ToList();

                    //Create an viewurl to request.
                    ViewURL = DesignDocumentURL + "/_view/" + viewNames.FirstOrDefault();
                }

                //start the get request
                HttpWebResponse indexCheckResponse;

                // Check indexation
                indexCheckResponse = await RequestManager.SendRequest(ViewURL, null, "GET", Profile.GetEnvironment(DBEnvironmentName).APIKey, APIPassword);//900000


                if ((int)indexCheckResponse.StatusCode != 200)
                {
                    OutputHelper.WriteProgress("Received Bad response (" + (int)indexCheckResponse.StatusCode + ") on indexation check: " + DesignDocumentName);
                    OutputHelper.WriteProgress("Indexation of " + DesignDocumentName + " failed.");
                    throw new Exception("Index check Response != 200");
                }

                OutputHelper.WriteProgress("Successfully checked indexation on: " + DesignDocumentName);
                Succeeded = true;
            }
            catch (Exception exc)
            {
                //Notify that an error occured.. (Automaticly it will be restarted)
                OutputHelper.WriteProgress("Indexation of " + DesignDocumentName + " failed.");
            }
        }
    }
}
