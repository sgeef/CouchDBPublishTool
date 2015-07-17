using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace C2701_NoSQLPublishTool.Managers
{
    public class HTTPRequestManager
    {
        private string GetResponseContent(HttpWebResponse response)
        {
            if (response == null)
            {
                return null;
            }
            Stream dataStream = null;
            StreamReader reader = null;
            string responseFromServer = null;

            try
            {
                // Get the stream containing content returned by the server.
                dataStream = response.GetResponseStream();
                // Open the stream using a StreamReader for easy access.
                reader = new StreamReader(dataStream);
                // Read the content.
                responseFromServer = reader.ReadToEnd();
                // Cleanup the streams and the response.
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {                
                if (reader != null)
                {
                    reader.Close();
                }
                if (dataStream != null)
                {
                    dataStream.Close();
                }
                response.Close();
            }
            return responseFromServer;
        }

        public async Task<HttpWebResponse> SendRequest(string uri, string content, string method, string login, string password, int? timeout = null)
        {
            HttpWebRequest request = GenerateRequest(uri, content, method, login, password, timeout);
            HttpWebResponse response = await GetResponse(request);
            //Assure to close;
            response.Close();
            return response;
        }

        public async Task<string> GetContent(string uri, string content, string method, string login, string password, int? timeout = null)
        {
            HttpWebRequest request = GenerateRequest(uri, content, method, login, password, timeout);
            HttpWebResponse response = await GetResponse(request);
            return GetResponseContent(response);
        }

        internal HttpWebRequest GenerateRequest(string uri, string content, string method, string login, string password, int? timeout = null)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }
            // Create a request using a URL that can receive a post. 
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);
            // Set the Method property of the request to POST.
            request.Method = method;

            if (timeout != null)
            {
                request.Timeout = (int)timeout;
            }

            //Cloudant advice
            request.Headers["x-cloudant-io-priority"] = "low";

            // If login is empty use defaul credentials
            if (string.IsNullOrEmpty(login))
            {
                request.Credentials = CredentialCache.DefaultNetworkCredentials;
            }
            else
            {
                request.Credentials = new NetworkCredential(login, password);
            }

            if (method == "POST")
            {
                // Convert POST data to a byte array.
                byte[] byteArray = Encoding.UTF8.GetBytes(content);
                // Set the ContentType property of the WebRequest.
                request.ContentType = "application/x-www-form-urlencoded";
                // Set the ContentLength property of the WebRequest.
                request.ContentLength = byteArray.Length;
                // Get the request stream.
                Stream dataStream = request.GetRequestStream();
                // Write the data to the request stream.
                dataStream.Write(byteArray, 0, byteArray.Length);
                // Close the Stream object.
                dataStream.Close();
            }
            return request;
        }

        public async Task<HttpWebResponse> GetResponse(HttpWebRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)await request.GetResponseAsync();                
            }
            catch (WebException ex)
            {
                response.Close();
                //Console.WriteLine("Web exception occurred. Status code: {0}", ex.Status);
                if(ex.Status == WebExceptionStatus.Timeout)
                {
                    throw new TimeoutException();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return response;
        }

    }
}
