using System;
using System.IO;
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

        //private readonly FileProcessNotifier _fileProcessedNotifier;
        private readonly IndexJobData _jobData;
        private readonly string _configuration;
        private readonly string _mediaProcessor;
        private readonly bool _deleteFiles;

        //private static object _assetsLock = new object();
        private static object _jobsLock = new object();
        //private static object _tasksLock = new object();

        public IndexingJob(MediaServicesCredentials creds,
            //FileProcessNotifier fileProcessedNotifier, 
            IndexJobData jobData, string configuration,
            string mediaProcessor = MediaProcessorNames.AzureMediaIndexer2Preview, bool deleteFiles = true)
        {
            //_fileProcessedNotifier = fileProcessedNotifier;
            _jobData = jobData;
            _configuration = configuration;
            _mediaProcessor = mediaProcessor;
            _deleteFiles = deleteFiles;
            _logger = Logger.GetLog<IndexingJob>();
            _context = new CloudMediaContext(creds);
        }

        public IAsset Run()
        {
            _logger.Information("Running index job for {file}, using  Indexer {mediaProcessor}", _jobData, _mediaProcessor);

            _logger.Debug("Creating job");

            var processor = _context.MediaProcessors.GetLatestMediaProcessorByName(_mediaProcessor);

            string file = Path.GetFileName(_jobData.Filename);

            IJob job;
            ITask task;

            //lock (_jobsLock)
            //{
                job = _context.Jobs.Create($"Indexing Job:{file}");

                task = job.Tasks.AddNew($"Indexing Task:{file}",
                    processor,
                    _configuration,
                    TaskOptions.None);
            //}

            _logger.Debug("Created task {task} for job", task.ToLog());

            task.InputAssets.Add(_jobData.InputAsset);

            // Add an output asset to contain the results of the job.
            task.OutputAssets.AddNew($"Indexing Output for {file}", AssetCreationOptions.StorageEncrypted);

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
                _jobData, elapsed?.TotalSeconds ?? 0, _mediaProcessor);

            //_context.Locators.CreateLocator(LocatorType.Sas, job.OutputMediaAssets[0], _accessPolicy);

            _jobData.OutputAsset = job.OutputMediaAssets[0];
            return job.OutputMediaAssets[0];
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
                    //_jobData.InputAssetKeyRestored = DateTime.Now;
                    //MediaServicesUtils.RestoreEncryptionKey(_context, _jobData);
                    Console.WriteLine("Please wait...");
                    break;
                case JobState.Processing:
                    _jobData.InputAssetKeyRestored = DateTime.Now;
                    MediaServicesUtils.RestoreEncryptionKey(_context, _jobData);
                    _jobData.OutputAssetCreated = DateTime.Now;
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
            MediaServicesUtils.DeleteAsset(_jobData.InputAsset);
            _jobData.InputAsset = null;
            _jobData.InputAssetDeleted = DateTime.Now;
        }
    }
}
