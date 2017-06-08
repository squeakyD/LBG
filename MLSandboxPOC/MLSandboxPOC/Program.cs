using System;
using System.Configuration;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;

namespace MLSandboxPOC
{
    class Program
    {
        // Field for service context.
        private static CloudMediaContext _context = null;
        private static MediaServicesCredentials _cachedCredentials = null;

        private static ILogger _logger;
        //private static MSRestClient _restClient;

        static void Main(string[] args)
        {
            try
            {
                _logger = Logger.GetLog<Program>();

                //AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

                Console.WriteLine("ML Sandbox POC");
                Console.WriteLine("==============");
                Console.WriteLine();

                InitialiseMediaServicesClient();

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

                //_restClient = new MSRestClient(_cachedCredentials);

                var downloadManager = DownloadManager.CreateDownloadManager(_context, Config.Instance.NumberOfConcurrentDownloads);

                var fileProcessNotifier = FileProcessNotifier.Instance;

                string configuration = File.ReadAllText("config.json");
                var indexingJobManager = IndexingJobManager.CreateIndexingJobManager(_context, configuration, downloadManager);

                var uploadManager = UploadManager.CreateUploadManager(_context, fileProcessNotifier, indexingJobManager, Config.Instance.NumberOfConcurrentUploads);

                var fileMgr = FileSourceManager.CreateFileSourceManager(uploadManager, fileProcessNotifier);

                Console.WriteLine("Started filewatcher");
                Console.WriteLine("-> Press any key to exit");

                Console.ReadKey();

                fileMgr.ShutdownTimer();
                var t1 = uploadManager.WaitForAllTasks();
                t1.Wait();
                var t2 = indexingJobManager.WaitForAllTasks();
                t2.Wait();
                var t3 = downloadManager.WaitForAllTasks();
                t3.Wait();

                // Run indexing job.
                //uploadManager.QueueItem(src);
                //uploadManager.QueueItem(src2);

                // Download the job output asset.
                //DownloadAsset(asset, _outDir);
                //downloadManager.QueueItem(asset);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Fatal error in demo program!!!");
            }
        }

        private static void InitialiseMediaServicesClient()
        {
            CreateCredentials();

            // Used the cached credentials to create CloudMediaContext.
            _context = new CloudMediaContext(_cachedCredentials);

            if (!Config.Instance.UseDefaultNumberOfConcurrentTransfers)
            {
                _context.NumberOfConcurrentTransfers = Config.Instance.NumberOfConcurrentTransfers;
            }
            else
            {
                _logger.Information("Using default SDK value for NumberOfConcurrentTransfers");
            }

            if (!Config.Instance.UseDefaultParallelTransferThreadCount)
            {
                _context.ParallelTransferThreadCount = Config.Instance.ParallelTransferThreadCount;
            }
            else
            {
                _logger.Information("Using default SDK value for ParallelTransferThreadCount");
            }

            _logger.Information(
                "NumberOfConcurrentTransfers: {NumberOfConcurrentTransfers}, ParallelTransferThreadCount: {ParallelTransferThreadCount}",
                _context.NumberOfConcurrentTransfers, _context.ParallelTransferThreadCount);
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
            _logger.Debug("Creating asset and uploading all files in {folder}", Config.Instance.SourceDirectory);
            var asset = _context.Assets.CreateFromFolder(Config.Instance.SourceDirectory, AssetCreationOptions.StorageEncrypted);

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
