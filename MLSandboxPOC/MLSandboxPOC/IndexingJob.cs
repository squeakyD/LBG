using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;

namespace MLSandboxPOC
{
    class IndexingJob
    {
        private readonly ILogger _logger;
        private readonly CloudMediaContext _context;
        private readonly FileProcessNotifier _fileProcessedNotifier;
        private readonly string _filePath;
        private readonly string _configuration;
        private readonly string _mediaProcessor;
        private readonly bool _deleteFiles;

        private IAsset _asset;
        private IContentKey _storedContentKey;
        private byte[] _storedKeyData;
        //private IAccessPolicy _accessPolicy;
        private const string KeyIdentifierPrefix = "nb:kid:UUID:";

        private static object _assetsLock = new object();
        private static object _jobsLock = new object();
        //private static object _tasksLock = new object();

        public IndexingJob(CloudMediaContext context,
            FileProcessNotifier fileProcessedNotifier, 
            string filePath, string configuration,
            string mediaProcessor = MediaProcessorNames.AzureMediaIndexer2Preview, bool deleteFiles = true)
        {
            _context = context;
            _fileProcessedNotifier = fileProcessedNotifier;
            _filePath = filePath;
            _configuration = configuration;
            _mediaProcessor = mediaProcessor;
            _deleteFiles = deleteFiles;
            _logger = Logger.GetLog<IndexingJob>();
        }

        public IAsset Run()
        {
            _logger.Information("Running index job for {file}, using  Indexer {mediaProcessor}", _filePath, _mediaProcessor);

            _asset = CreateAssetAndUploadSingleFile(AssetCreationOptions.StorageEncrypted);

            _logger.Debug("Creating job");

            var processor = _context.MediaProcessors.GetLatestMediaProcessorByName(_mediaProcessor);

            IJob job;
            ITask task;

            lock (_jobsLock)
            {
                job = _context.Jobs.Create($"Indexing Job:{_filePath}");

                task = job.Tasks.AddNew($"Indexing Task:{_filePath}",
                    processor,
                    _configuration,
                    TaskOptions.None);
            }

            _logger.Debug("Created task {task} for job", task.ToLog());

            task.InputAssets.Add(_asset);

            // Add an output asset to contain the results of the job.
            task.OutputAssets.AddNew($"Indexing Output for {_filePath}", AssetCreationOptions.StorageEncrypted);

            job.StateChanged += StateChanged;
            job.Submit();

            // Check job execution and wait for job to finish.
            Task progressJobTask = job.GetExecutionProgressTask(CancellationToken.None);

            progressJobTask.Wait();

            // If job state is Error, the event handling
            // method for job progress should log errors.  Here we check
            // for error state and exit if needed.
            if (job.State == JobState.Error)
            {
                ErrorDetail error = job.Tasks.First().ErrorDetails.First();
                _logger.Warning($"Error: {error.Code}. {error.Message}");
                return null;
            }

            var elapsed = job.EndTime - job.StartTime;

            _logger.Information("-> Indexing job for {file} took {elapsed} seconds (processor={processor})",
                _filePath, elapsed?.TotalSeconds ?? 0, _mediaProcessor);

            //_context.Locators.CreateLocator(LocatorType.Sas, job.OutputMediaAssets[0], _accessPolicy);

            return job.OutputMediaAssets[0];
        }

        private IAsset CreateAssetAndUploadSingleFile(AssetCreationOptions options)
        {
            IAsset asset;

            lock (_assetsLock)
            {
                asset = _context.Assets.CreateFromFile(_filePath, options);
                _logger.Information("Created and uploaded asset {asset} from {file}", asset.ToLog(), _filePath);
            }

            RemoveEncryptionKey(asset);

            _fileProcessedNotifier.NotifyFileProcessed(_filePath);

            // TEST
            //RestoreEncryptionKey(asset);

            //var test = asset.ContentKeys;
            return asset;
        }

      private void RemoveEncryptionKey(IAsset asset)
        {
            _storedContentKey = asset.ContentKeys.AsEnumerable().FirstOrDefault(k => k.ContentKeyType == ContentKeyType.StorageEncryption);
            _storedKeyData = _storedContentKey.GetClearKeyValue();
            asset.ContentKeys.Remove(_storedContentKey);
            asset.Update();

            _logger.Verbose("Removed encryption key {key} from asset {asset}", _storedContentKey.Id, asset.ToLog());
        }

        static string GuidFromId(string id)
        {
            string guid = id.Substring(id.IndexOf(KeyIdentifierPrefix, StringComparison.OrdinalIgnoreCase) + KeyIdentifierPrefix.Length);
            return guid;
        }

        private void RestoreEncryptionKey(IAsset asset)
        {
            _logger.Verbose("Restoring key {key} to asset {asset}", _storedContentKey.Id, asset.ToLog());

            var newKey= _context.ContentKeys.Create(Guid.Parse(GuidFromId(_storedContentKey.Id)), _storedKeyData, _storedContentKey.Name, _storedContentKey.ContentKeyType);
            asset.ContentKeys.Add(newKey);
        }

        private void StateChanged(object sender, JobStateChangedEventArgs e)
        {
            Console.WriteLine("Job state changed event:");
            Console.WriteLine("  Previous state: " + e.PreviousState);
            _logger.Debug("  Current job state: " + e.CurrentState);
            IJob job = (IJob) sender;
            var asset = job.InputMediaAssets.FirstOrDefault();

            switch (e.CurrentState)
            {
                case JobState.Finished:
                    _logger.Debug("Job {job} is finished", job.ToLog());
                    if (_deleteFiles)
                    {
                        DeleteAsset();
                    }
                   break;
                case JobState.Canceling:
                case JobState.Queued:
                    Console.WriteLine("Please wait...");
                    break;
                case JobState.Scheduled:
                    //RestoreEncryptionKey(asset);
                    Console.WriteLine("Please wait...");
                    break;
                case JobState.Processing:
                    RestoreEncryptionKey(asset);
                    Console.WriteLine("Please wait...");
                    //if (_deleteFiles)
                    //{
                    //    DeleteAsset();
                    //}
                    break;
                case JobState.Canceled:
                case JobState.Error:
                    _logger.Error("{job} job {CurrentState}", job.ToLog(), e.CurrentState);
                    if (_deleteFiles)
                    {
                        DeleteAsset();
                    }
                    break;
                default:
                    break;
            }
        }

        public void DeleteAsset()
        {
            if (_asset != null)
            {
                foreach (IAssetFile file in _asset.AssetFiles)
                {
                    _logger.Verbose("Deleting file {file} in asset {asset}", file.ToLog(), _asset.ToLog());
                    file.Delete();
                }
                _logger.Verbose("Deleting asset {asset}", _asset.ToLog());
                _asset.Delete();
                _asset = null;
            }
        }
    }
}
