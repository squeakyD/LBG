using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace MLSandboxPOC
{
    class Program
    {
        // Read values from the App.config file.
        //private static readonly string _mediaServicesAccountName = ConfigurationManager.AppSettings["MediaServicesAccountName"];
        //private static readonly string _mediaServicesAccountKey = ConfigurationManager.AppSettings["MediaServicesAccountKey"];
        private static readonly string _sourceDir = ConfigurationManager.AppSettings["SourceDirectory"];
        private static readonly string _processedDirectory = ConfigurationManager.AppSettings["ProcessedDirectory"];
        private static readonly string _outDir = ConfigurationManager.AppSettings["OutputDirectory"];

        // Field for service context.
        private static CloudMediaContext _context = null;
        private static MediaServicesCredentials _cachedCredentials = null;

        private static ILogger _logger;
        private static MSRestClient _restClient;
        private static IndexingJobManager _indexingJobManager;
        private static DownloadManager _downloadManager;

        static void Main(string[] args)
        {
            try
            {
                _logger = Logger.GetLog<Program>();

                //AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

                Console.WriteLine("ML Sandbox POC");
                Console.WriteLine("==============");
                Console.WriteLine();

                CreateCredentials();

                // Used the cached credentials to create CloudMediaContext.
                _context = new CloudMediaContext(_cachedCredentials);

                //string src = Path.Combine(_sourceDir, "4th Apr 17_612026009250275cut.wav");
                //string src = Path.Combine(_sourceDir, "612026009249280 040417_1.wav");
                //string src2 = Path.Combine(_sourceDir, "612026009249280 040417_2.wav");
                //string src = Path.Combine(_sourceDir, "612026009249280 040417 0932_1191101cut.wav");
                //string src = Path.Combine(_sourceDir, "612026009249955 040417 1028_1191101cut.wav");
                //string src = Path.Combine(_sourceDir, "612026009250579 040417 1110_1191101cut.wav");
                //string src = Path.Combine(_sourceDir, "612026009280132 110417_ATJStest1191100.wav");

                if (args.Length > 0)
                {
                    if (args[0].Equals("delOnly", StringComparison.InvariantCultureIgnoreCase))
                    {
                        DeleteAssetFiles();
                        return;
                    }
                    else if (args[0].Equals("getAssets", StringComparison.InvariantCultureIgnoreCase))
                    {
                        GetAllAssetFiles();
                        return;
                    }
                    //else if (args[0].Equals("-f", StringComparison.InvariantCultureIgnoreCase))
                    //{
                    //    src = Path.Combine(_sourceDir, args[1]);
                    //}
                }

                _context.NumberOfConcurrentTransfers = 25;

                _restClient = new MSRestClient(_cachedCredentials);

                _downloadManager = DownloadManager.CreateDownloadManager(_context, _outDir, _context.NumberOfConcurrentTransfers / 2);

                var fileProcessNotifier = FileProcessNotifier.Instance;

                string configuration = File.ReadAllText("config.json");
                _indexingJobManager = IndexingJobManager.CreateIndexingJobManager(_context, configuration, fileProcessNotifier, _downloadManager, _context.NumberOfConcurrentTransfers / 2);

                var fileMgr = FileSourceManager.CreateFileSourceManager(_sourceDir, _processedDirectory, _indexingJobManager, fileProcessNotifier);

                Console.WriteLine("Started filewatcher");
                Console.WriteLine("-> Press any key to exit");

                Console.ReadKey();

                var t1 = _indexingJobManager.WaitForAllTasks();
                t1.Wait();
                var t2 = _downloadManager.WaitForAllTasks();
                t2.Wait();

                // Run indexing job.
                //_indexingJobManager.QueueItem(src);
                //_indexingJobManager.QueueItem(src2);

                // Download the job output asset.
                //DownloadAsset(asset, _outDir);
                //_downloadManager.QueueItem(asset);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Fatal error in demo program!!!");
            }
        }

        //private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        //{
        //}

        private static void GetAllAssetFiles()
        {
            CreateCredentials();

            _logger.Information("Downloading all stored audio file info");

            foreach (var asset in _context.Assets)
            {
                var key = asset.ContentKeys.FirstOrDefault();
                if (key == null)
                {
                    _logger.Information("Asset {asset} has no ContentKey.", asset.ToLog());
                }
                else
                {
                    _logger.Information("Asset {asset} has content key: {key} ", asset.ToLog(),
                        new { key.ContentKeyType, key.EncryptedContentKey, key.ProtectionKeyId });
                }

                foreach (var af in asset.AssetFiles)    
                {
                    _logger.Information("   Asset has file {file}", af.ToLog());
                }
            }
        }

        private static void CreateCredentials()
        {
            _logger.Debug("Logging in");

            string accountName;
            string accountKey;

            string cfg = Path.Combine(ConfigurationManager.AppSettings["AzureCfg"], "azure.cfg");
            using (var st = File.OpenText(cfg))
            {
                accountName = st.ReadLine();
                accountKey = st.ReadLine();
            }

            _cachedCredentials = new MediaServicesCredentials(accountName, accountKey);
        }

        private static IAsset CreateAssetFromFolder()
        {
            _logger.Debug("Creating asset and uploading all files in {folder}", _sourceDir);
            var asset = _context.Assets.CreateFromFolder(_sourceDir, AssetCreationOptions.StorageEncrypted);

            // Create a manifest file that contains all the asset file names and upload to storage.
            _logger.Debug("Creating manifest");
            string manifestFile = "input.lst";

            var filenames = asset.AssetFiles.AsEnumerable().Select(f => f.Name);
            File.WriteAllLines(manifestFile, filenames);
            var assetFile = asset.AssetFiles.Create(Path.GetFileName(manifestFile));
            assetFile.Upload(manifestFile);

            var assetFile2 = asset.AssetFiles.Create(Path.GetFileName("indexerCfg.xml"));
            assetFile2.Upload("indexerCfg.xml");
            return asset;
        }

        //private static List<IContentKey> _savedStorageKeys = new List<IContentKey>();

        private static IContentKey _storedContentKey;

        //static IContentKey CreateCommonTypeContentKey(IAsset asset)
        //{
        //    // Create common encryption content key
        //    Guid keyId = Guid.NewGuid();
        //    byte[] contentKey = GetRandomBuffer(16);

        //    IContentKey key = _context.ContentKeys.Create(
        //        keyId,
        //        contentKey,
        //        "MyContentKey",
        //        ContentKeyType.CommonEncryption);

        //    // Associate the key with the asset.
        //    asset.ContentKeys.Add(key);

        //    _logger.Debug("Created common encryption key {key} for asset {asset}", key.Id, asset.ToLog());

        //    return key;
        //}

        //private static byte[] GetRandomBuffer(int length)
        //{
        //    var returnValue = new byte[length];

        //    using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
        //    {
        //        rng.GetBytes(returnValue);
        //    }

        //    return returnValue;
        //}

        private static void RemoveEncryptionKey(IAsset asset)
        {
            return; // temp
            _storedContentKey = asset.ContentKeys.AsEnumerable().FirstOrDefault(k => k.ContentKeyType == ContentKeyType.StorageEncryption);
            asset.ContentKeys.Remove(_storedContentKey);
            asset.Update();
            //    //var key=_context.ContentKeys.AsEnumerable().FirstOrDefault(k => k.Id == _storedContentKey.Id);
            //    //key.Delete();
            //    _logger.Debug("Removed encryption key {key} from asset {asset}",_storedContentKey.Id, asset.ToLog());
        }

        class MyContentKey
        {
            //public string Id { get;set;  }
            public string Name { get; set; }
            public string ProtectionKeyId { get; set; }
            public int ContentKeyType { get; set; }
            public int ProtectionKeyType { get; set; }
            public string EncryptedContentKey { get; set; }
            public string Checksum { get; set; }
        }

        private static void RestoreEncryptionKey(IAsset asset)
        {
            return; // temp
            //asset.ContentKeys.Add(_storedContentKey);
            _logger.Debug("RestoreEncryptionKey: asset {asset}", asset.ToLog());
            var newKey = new MyContentKey
            {
                Name = _storedContentKey.Name,
                ProtectionKeyId = _storedContentKey.ProtectionKeyId,
                ContentKeyType = (int) _storedContentKey.ContentKeyType,
                ProtectionKeyType = (int) _storedContentKey.ProtectionKeyType,
                EncryptedContentKey = _storedContentKey.EncryptedContentKey,
                Checksum = _storedContentKey.Checksum
            };

            string keyResp = _restClient.MakeAPICall("ContentKeys", JsonConvert.SerializeObject(newKey));

            if (!string.IsNullOrEmpty(keyResp))
            {
                JObject obj = JObject.Parse(keyResp);

                string keyId = obj["Id"].ToString();

                string addKeyCommand = $"Assets('{Uri.EscapeDataString(asset.Id)}')/$links/ContentKeys";

                string body = JsonConvert.SerializeObject(new {uri = $"{_restClient.ApiUrl}/ContentKeys('{Uri.EscapeDataString(keyId)}')"});

                // DataServiceVersion: 1.0;NetFx
                // MaxDataServiceVersion: 3.0; NetFx
                // TODO: Add extra headers
                var customHdrs = new Dictionary<string, string>
                {
                    ["DataServiceVersion"] = "1.0;NetFx",
                    ["MaxDataServiceVersion"] = "3.0; NetFx"
                };
                string addKeyResp = _restClient.MakeAPICall(addKeyCommand, body, customHdrs);

                //var key=asset.ContentKeys.AsEnumerable().FirstOrDefault(k=>k.Id== _storedContentKey.Id);// .Add();
            }
        }
    
        
        //private static void RemoveEncryptionKeys(IAsset asset)
        //{
        //    bool error = false;
        //    List<string> KeysListIDs = new List<string>();

        //    try
        //    {
        //        var StorageKeys = asset.ContentKeys.Where(k => k.ContentKeyType == ContentKeyType.StorageEncryption).ToList();
        //        KeysListIDs = StorageKeys.Select(k => k.Id).ToList(); // create a list of IDs
        //        var cks = _context.ContentKeys.ToArray();   // TEST

        //        // removing key
        //        foreach (var key in StorageKeys)
        //        {
        //            _savedStorageKeys.Add(key);
        //            asset.ContentKeys.Remove(key);
        //        }

        //        _logger.Debug("Removed {count} asset keys", StorageKeys.Count);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.Error(ex, "Error deleting storage key from asset");
        //        error = true;
        //    }
        //    if (!error)
        //    {
        //        // deleting key
        //        foreach (var key in _context.ContentKeys.ToList().Where(k => KeysListIDs.Contains(k.Id)).ToList())
        //        {
        //            try
        //            {
        //                key.Delete();
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.Error(ex, "Error deleting storage key {id} from CloudMediaContext", key.Id);
        //            }
        //        }
        //    }
        //}

        static string GuidFromId(string id)
        {
            string guid = id.Substring(id.IndexOf("UUID:", StringComparison.OrdinalIgnoreCase) + 5);
            return guid;
        }

        //private static void RestoreEncryptionKeys(IAsset asset)
        //{
        //    //asset.ContentKeys.Clear();

        //    _savedStorageKeys.ForEach(key =>
        //    {
        //        _logger.Debug("Adding key {key} to asset {asset} and context", key.Id, asset.ToLog());

        //        //byte[] rawKey = Convert.FromBase64String(key.EncryptedContentKey);
        //        //Guid keyId = Guid.NewGuid();
        //        //_context.ContentKeys.Create(Guid.Parse(GuidFromId(key.Id)), rawKey);//, key.Name, key.ContentKeyType);
        //        //var newKey= _context.ContentKeys.Create(keyId, rawKey, key.Name, key.ContentKeyType);
        //        var newKey = _context.ContentKeys.AsEnumerable().FirstOrDefault(k => k.ContentKeyType == ContentKeyType.StorageEncryption);
        //        //newKey.EncryptedContentKey
        //        //asset.ContentKeys[0] = key;
        //        //var ck = _context.ContentKeys.AsEnumerable().FirstOrDefault(k => k.Id == key.Id);
        //        asset.ContentKeys.Add(newKey);
        //    });
        //}            

        private static void DeleteAssetFiles()
        {
            foreach (var asset in _context.Assets)
            {
                foreach (var af in asset.AssetFiles)
                {
                    _logger.Information("Deleting {file} from asset {asset}", af.ToLog(), asset.ToLog());
                    af.Delete();
                }

                _logger.Information("Deleting asset {asset}", asset.ToLog());
                asset.Delete();
            }
        }
    }

}
