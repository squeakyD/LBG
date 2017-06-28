using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;

namespace MLSandboxPOC
{
    class CloudMediaContextFactory
    {
        private readonly AzureAdTokenProvider _tokenProvider;
        private readonly ILogger _logger = Logger.GetLog<CloudMediaContextFactory>();

        private static CloudMediaContextFactory _instance;

        public static CloudMediaContextFactory Instance => _instance ?? (_instance = new CloudMediaContextFactory());

        private CloudMediaContextFactory()
        {
            try
            {
                // See https://azure.microsoft.com/en-us/blog/azure-media-service-aad-auth-and-acs-deprecation/

                string tenant;
                string id;
                string key;

                using (var secTenant = DataProtector.Utils.DecryptString(Config.Instance.MediaServicesAppTenant))
                {
                    tenant = DataProtector.Utils.ToInsecureString(secTenant);
                }
                using (var secId = DataProtector.Utils.DecryptString(Config.Instance.MediaServicesAppId))
                {
                    id = DataProtector.Utils.ToInsecureString(secId);
                }
                using (var secKey = DataProtector.Utils.DecryptString(Config.Instance.MediaServicesAppKey))
                {
                    key = DataProtector.Utils.ToInsecureString(secKey);
                }

                var tokenCredentials = new AzureAdTokenCredentials(tenant, new AzureAdClientSymmetricKey(id, key),
                    AzureEnvironments.AzureCloudEnvironment);

                _tokenProvider = new AzureAdTokenProvider(tokenCredentials);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialise CloudMediaContextFactory - unable to login to Azure Media Services");
                throw;
            }
        }

        public CloudMediaContext CloudMediaContext => new CloudMediaContext(new Uri(Config.Instance.AzMediaServicesApiUrl), _tokenProvider);
    }
}
