using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private readonly string _filePath;
        private readonly string _configuration;
        private readonly string _mediaProcessor;
        private IContentKey _storedContentKey;
        //private IAccessPolicy _accessPolicy;

        public IndexingJob(CloudMediaContext context, string filePath, string configuration,
            string mediaProcessor = MediaProcessorNames.AzureMediaIndexer2Preview)
        {
            _context = context;
            _filePath = filePath;
            _configuration = configuration;
            _mediaProcessor = mediaProcessor;
            _logger = Logger.GetLog<IndexingJob>();
        }

        public IAsset Run()
        {
            _logger.Information("Running index job for {file}, using  Indexer {mediaProcessor}", _filePath, _mediaProcessor);

            IAsset asset = CreateAssetAndUploadSingleFile(AssetCreationOptions.StorageEncrypted);

            _logger.Debug("Creating indexing job");

            IJob job = _context.Jobs.Create($"Indexing Job:{_filePath}");

            var processor = _context.MediaProcessors.GetLatestMediaProcessorByName(_mediaProcessor);

            ITask task = job.Tasks.AddNew($"Indexing Task:{_filePath}",
                processor,
                _configuration,
                TaskOptions.None);
            _logger.Debug("Created task {task} for job", task.ToLog());

            task.InputAssets.Add(asset);

            // Add an output asset to contain the results of the job.
            task.OutputAssets.AddNew($"Indexing Output Asset:{_filePath}", AssetCreationOptions.StorageEncrypted);

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
            IAsset asset = _context.Assets.CreateFromFile(_filePath, options);
            _logger.Information("Created and uploaded asset {asset} from {file}", asset.ToLog(), _filePath);

            //RemoveEncryptionKeys(asset);
            //RemoveEncryptionKey(asset);

            //var test = asset.ContentKeys;
            return asset;
        }

        private void RemoveEncryptionKey(IAsset asset)
        {
            //return; // temp
            _storedContentKey = asset.ContentKeys.AsEnumerable().FirstOrDefault(k => k.ContentKeyType == ContentKeyType.StorageEncryption);
            asset.ContentKeys.Remove(_storedContentKey);
            asset.Update();
            //    //var key=_context.ContentKeys.AsEnumerable().FirstOrDefault(k => k.Id == _storedContentKey.Id);
            //    //key.Delete();
            //    _logger.Debug("Removed encryption key {key} from asset {asset}",_storedContentKey.Id, asset.ToLog());
        }

        private void RestoreEncryptionKey(IAsset asset)
        {
            // TOOD
        }

        private void StateChanged(object sender, JobStateChangedEventArgs e)
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
