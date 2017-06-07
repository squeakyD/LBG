using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        public void RestoreEncryptionKey(IAsset asset, IContentKey contentKey)
        {
            _logger.Debug("RestoreEncryptionKey: asset {asset}", asset.ToLog());
            var newKey = new ContentKey
            {
                Name = contentKey.Name,
                ProtectionKeyId = contentKey.ProtectionKeyId,
                ContentKeyType = (int)contentKey.ContentKeyType,
                ProtectionKeyType = (int)contentKey.ProtectionKeyType,
                EncryptedContentKey = contentKey.EncryptedContentKey,
                Checksum = contentKey.Checksum
            };

            string keyResp = MakeAPICall("ContentKeys", JsonConvert.SerializeObject(newKey));

            if (!string.IsNullOrEmpty(keyResp))
            {
                JObject obj = JObject.Parse(keyResp);

                string keyId = obj["Id"].ToString();

                string addKeyCommand = $"Assets('{Uri.EscapeDataString(asset.Id)}')/$links/ContentKeys";

                string body = JsonConvert.SerializeObject(new { uri = $"{ApiUrl}/ContentKeys('{Uri.EscapeDataString(keyId)}')" });

                // DataServiceVersion: 1.0;NetFx
                // MaxDataServiceVersion: 3.0; NetFx
                // TODO: Add extra headers
                var customHdrs = new Dictionary<string, string>
                {
                    ["DataServiceVersion"] = "1.0;NetFx",
                    ["MaxDataServiceVersion"] = "3.0; NetFx"
                };
                string addKeyResp = MakeAPICall(addKeyCommand, body, customHdrs);

                // TODO: Add content key back to asset
                //var key=asset.ContentKeys.AsEnumerable().FirstOrDefault(k=>k.Id== contentKey.Id);// .Add();
            }
        }
    }

    /// <summary>
    /// Content Key to be serialised with REST API
    /// </summary>
    class ContentKey
    {
        //public string Id { get;set;  }
        public string Name { get; set; }
        public string ProtectionKeyId { get; set; }
        public int ContentKeyType { get; set; }
        public int ProtectionKeyType { get; set; }
        public string EncryptedContentKey { get; set; }
        public string Checksum { get; set; }
    }
}
