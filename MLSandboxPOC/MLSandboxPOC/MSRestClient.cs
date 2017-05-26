using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Net;
using Newtonsoft.Json;
using Serilog;

namespace MLSandboxPOC
{
    class MSRestClient
    {
        private readonly ILogger _logger = Logger.GetLog<MSRestClient>();
        private const string _initialApiUrl = "https://media.windows.net/";

        private readonly MediaServicesCredentials _credentials;

        public Uri ApiUrl { get; private set; }

        public MSRestClient(MediaServicesCredentials credentials)
        {
            _credentials = credentials;
            Init();
        }

        //class TestAsset
        //{
        //    public string Name { get; set; }
        //    public int Options { get; set; } 
        //}
        private void Init()
        {
            HttpWebRequest request = CreateRequest(_initialApiUrl, String.Empty, WebRequestMethods.Http.Get);
            using (WebResponse response = request.GetResponse())
            {
                ApiUrl = GetAccountApiEndpointFromResponse(response);
            }

            _logger.Debug("Actual MS REST API URL is {url}", ApiUrl);

            //// TEST code

            //string command = "Assets";

            //var ass = new TestAsset {Name = "Testttt", Options = 1};
            //string body = JsonConvert.SerializeObject(ass);
            //string resp = MakeAPICall(command, body);
        }

        private static Uri GetAccountApiEndpointFromResponse(WebResponse webResponse)
        {
            HttpWebResponse httpWebResponse = (HttpWebResponse)webResponse;

            if (httpWebResponse.StatusCode == HttpStatusCode.MovedPermanently)
            {
                return new Uri(httpWebResponse.Headers[HttpResponseHeader.Location]);
            }

            if (httpWebResponse.StatusCode == HttpStatusCode.OK)
            {
                return httpWebResponse.ResponseUri;
            }

            throw new InvalidOperationException("Unexpected response code.");
        }

        public string MakeAPICall(string command, string body, Dictionary<string, string> customHeaders=null)
        {
            string url = $"{ApiUrl}/{command}";

            return DoRestCall(url, body, WebRequestMethods.Http.Post, customHeaders);
        }


        private string DoRestCall(string uri, string body = "", string method = WebRequestMethods.Http.Post, Dictionary<string, string> customHeaders=null)
        {
            HttpWebRequest request = CreateRequest(uri, body, method, customHeaders);

            // Get the response
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error from : " + uri);
                return String.Empty;
                // TODO: Do we want to throw
            }

            string result = null;
            using (var streamReader = new StreamReader(response.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }

            return result;
        }

        private HttpWebRequest CreateRequest(string URI, string body, string method, Dictionary<string, string> customHeaders=null)
        {
            Uri uri = new Uri(URI);

            // Create the request
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Headers.Add(HttpRequestHeader.Authorization, _credentials.GetAuthorizationHeader());
            request.ContentType = "application/json; charset=utf-8";
            request.Headers.Add("x-ms-version", "2.11");
            request.Accept = "application/json";
            request.Method = method;

            if (customHeaders != null)
            {
                foreach (var header in customHeaders)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            if (!string.IsNullOrEmpty(body))
            {
                using (var stream = request.GetRequestStream())
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(body);
                    }
                }
            }

            return request;
        }
    }
}
