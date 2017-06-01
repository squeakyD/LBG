using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        private static readonly string _outDir = ConfigurationManager.AppSettings["OutputDirectory"];

        // Field for service context.
        private static CloudMediaContext _context = null;
        private static MediaServicesCredentials _cachedCredentials = null;
        private static bool _delFiles;

        private static ILogger _logger;
        private static MSRestClient _restClient;
        private static IndexingJobManager _indexingJobManager;
        private static DownloadManager _downloadManager;

        static void Main(string[] args)
        {
            try
            {
                _logger = Logger.GetLog<Program>();

                CreateCredentials();

                // Used the cached credentials to create CloudMediaContext.
                _context = new CloudMediaContext(_cachedCredentials);

                //string src = Path.Combine(_sourceDir, "4th Apr 17_612026009250275cut.wav");
                string src = Path.Combine(_sourceDir, "612026009249280 040417_1.wav");
                string src2 = Path.Combine(_sourceDir, "612026009249280 040417_2.wav");
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
                    else if (args[0].Equals("delAfter", StringComparison.InvariantCultureIgnoreCase))
                    {
                        _delFiles = true;
                    }
                    else if (args[0].Equals("getAssets", StringComparison.InvariantCultureIgnoreCase))
                    {
                        GetAllAssetFiles();
                        return;
                    }
                    else if (args[0].Equals("-f", StringComparison.InvariantCultureIgnoreCase))
                    {
                        src = Path.Combine(_sourceDir, args[1]);
                    }
                }

                _restClient = new MSRestClient(_cachedCredentials);

                _downloadManager = new DownloadManager(_context, _outDir);
                string configuration = File.ReadAllText("config.json");
                _indexingJobManager = new IndexingJobManager(_context, configuration, _downloadManager);

                // Run indexing job.
                _indexingJobManager.QueueItem(src);
                _indexingJobManager.QueueItem(src2);
                _context.NumberOfConcurrentTransfers = 25;

                //var asset = RunIndexingJob(src, @"config.json");
                //var asset = RunIndexingJob(src, @"indexer1cfg.xml", MediaProcessorNames.AzureMediaIndexer);

                // Download the job output asset.
                //DownloadAsset(asset, _outDir);
                //_downloadManager.QueueItem(asset);

                var t1 =_indexingJobManager.WaitForAllTasks();
                t1.Wait();
                var t2 = _downloadManager.WaitForAllTasks();
                t2.Wait();
                //Task.WaitAll(t1, t2);

                //foreach (var s in new[] {src,src2,src3,src4,src5})
                //{
                //    var asset = RunIndexingJob(src, @"..\..\config.json");
                //}

                if (_delFiles)
                {
                    DeleteAssetFiles();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Fatal error in demo program!!!");
                //Console.WriteLine(ex);
                //throw;
            }
        }

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

        static IAsset RunIndexingJob(string inputMediaFilePath, string configurationFile, string mediaProcessor = MediaProcessorNames.AzureMediaIndexer2Preview)
        {
            ////_logger.Information("Running index job for {inputMediaFilePath}", _sourceDir);
            //_logger.Information("Running index job for {inputMediaFilePath}", inputMediaFilePath);
            //_logger.Information("Using Indexer {mediaProcessor}", mediaProcessor);

            //// Create an asset and upload the input media file to storage.
            //IAsset asset = CreateAssetAndUploadSingleFile(inputMediaFilePath,
            //    "My Indexing Input Asset",
            //    //AssetCreationOptions.None);
            //    AssetCreationOptions.StorageEncrypted);

            ////var asset = CreateAssetFromFolder();

            //_logger.Debug("Creating indexing job");

            //// Declare a new job.
            //IJob job = _context.Jobs.Create("My Indexing Job");

            ////var processor = GetLatestMediaProcessorByName(MediaProcessorNames.AzureMediaIndexer2Preview);
            //var processor = _context.MediaProcessors.GetLatestMediaProcessorByName(mediaProcessor);

            // Read configuration from the specified file.
            string configuration = File.ReadAllText(configurationFile);

            var job = new IndexingJob(_context, inputMediaFilePath, configuration);
            return job.Run();

            //// Create a task with the encoding details, using a string preset.
            //ITask task = job.Tasks.AddNew("My Indexing Task",
            //    processor,
            //    configuration,
            //    TaskOptions.None);

            //_logger.Debug("Created task {task} for job", task.ToLog());

            ////RestoreEncryptionKey(asset);
            ////RestoreEncryptionKeys(asset);

            //// Specify the input asset to be indexed.
            //task.InputAssets.Add(asset);

            //// Add an output asset to contain the results of the job.
            //task.OutputAssets.AddNew("My Indexing Output Asset", AssetCreationOptions.StorageEncrypted);

            //// Use the following event handler to check job progress.  
            //job.StateChanged += new EventHandler<JobStateChangedEventArgs>(StateChanged);

            //_logger.Information("Submitted job {job}", job.ToLog());

            //// Launch the job.
            //job.Submit();

            //// Check job execution and wait for job to finish.
            //Task progressJobTask = job.GetExecutionProgressTask(CancellationToken.None);

            //progressJobTask.Wait();

            //// If job state is Error, the event handling
            //// method for job progress should log errors.  Here we check
            //// for error state and exit if needed.
            //if (job.State == JobState.Error)
            //{
            //    ErrorDetail error = job.Tasks.First().ErrorDetails.First();
            //    _logger.Warning($"Error: {error.Code}. {error.Message}");
            //    return null;
            //}
            //var elapsed = job.EndTime - job.StartTime;

            //_logger.Information("-> Indexing job for {file} took {elapsed} seconds (processor={processor})", inputMediaFilePath,
            //    elapsed?.Seconds ?? 0, mediaProcessor);

            //return job.OutputMediaAssets[0];
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

        static IAsset CreateAssetAndUploadSingleFile(string filePath, string assetName, AssetCreationOptions options)
        {
            IAsset asset = _context.Assets.Create(assetName, options);
            _logger.Debug("Created asset {asset}", asset.ToLog());

            //_storedContentKey = CreateCommonTypeContentKey(asset);

            var assetFile = asset.AssetFiles.Create(Path.GetFileName(filePath));


            assetFile.Upload(filePath);
            _logger.Information("Uploaded {filePath} to {assetFile}", filePath, assetFile.ToLog());

            //RemoveEncryptionKeys(asset);
            RemoveEncryptionKey(asset);

            //var test = asset.ContentKeys;

            return asset;
        }

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


        static void DownloadAsset(IAsset asset, string outputDirectory)
        {
            _logger.Debug("Downloading files");
            foreach (IAssetFile file in asset.AssetFiles)
            {
                _logger.Information("Downloading {file}", file.ToLog());
                file.Download(Path.Combine(outputDirectory, file.Name));

                if (_delFiles)
                {
                    _logger.Debug("Deleting output file {file} in asset {asset}", file.ToLog(), asset.ToLog());
                    file.Delete();
                }
            }

            if (_delFiles)
            {
                _logger.Debug("Deleting output asset {asset}", asset.ToLog());
                asset.Delete();
            }
        }

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

        private static void StateChanged(object sender, JobStateChangedEventArgs e)
        {
            Console.WriteLine("Job state changed event:");
            Console.WriteLine("  Previous state: " + e.PreviousState);
            //Console.WriteLine("  Current state: " + e.CurrentState);
            _logger.Debug("  Current state: " + e.CurrentState);
            IJob job = (IJob)sender;

            switch (e.CurrentState)
            {
                case JobState.Finished:
                    Console.WriteLine();
                    Console.WriteLine("Job is finished.");
                    _logger.Debug("Job {job} is finished", job.ToLog());
                    Console.WriteLine();
                    break;
                case JobState.Canceling:
                case JobState.Queued:
                    Console.WriteLine("Please wait...");
                    break;
                case JobState.Scheduled:
                    var asset = job.InputMediaAssets.FirstOrDefault();
                    RestoreEncryptionKey(asset);
                    Console.WriteLine("Please wait...");
                    break;
                case JobState.Processing:
                    Console.WriteLine("Please wait...");
                    break;
                case JobState.Canceled:
                case JobState.Error:
                    // Display or log error details as needed.
                    // LogJobStop(job.Id);
                    _logger.Error("{job} job {CurrentState}", job.ToLog(), e.CurrentState);
                    break;
                default:
                    break;
            }
        }
    }

}
