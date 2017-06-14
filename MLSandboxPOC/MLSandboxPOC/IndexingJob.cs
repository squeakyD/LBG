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
        private IJob _job;

        //private readonly FileProcessNotifier _fileProcessedNotifier;

        private readonly string _configuration;
        private readonly string _mediaProcessor;
        private readonly bool _deleteFiles;

        public IndexingJob(MediaServicesCredentials creds,
            //FileProcessNotifier fileProcessedNotifier, 
            IndexJobData jobData, string configuration,
            string mediaProcessor = MediaProcessorNames.AzureMediaIndexer2Preview, bool deleteFiles = true)
        {
            //_fileProcessedNotifier = fileProcessedNotifier;
            JobData = jobData;
            _configuration = configuration;
            _mediaProcessor = mediaProcessor;
            _deleteFiles = deleteFiles;
            _logger = Logger.GetLog<IndexingJob>();
            _context = new CloudMediaContext(creds);
        }

        public Task JobTask { get; private set; }

        public IndexJobData JobData { get; private set; }

        public Task CreateJob()
        {
            _logger.Information("Running index job for {file}, using  Indexer {mediaProcessor}", JobData, _mediaProcessor);

            _logger.Debug("Creating job");

            var processor = _context.MediaProcessors.GetLatestMediaProcessorByName(_mediaProcessor);

            string file = Path.GetFileName(JobData.Filename);

            _job = _context.Jobs.Create($"Indexing Job:{file}");

            ITask task = _job.Tasks.AddNew($"Indexing Task:{file}",
                processor,
                _configuration,
                TaskOptions.None);

            _logger.Debug("Created task {task} for job", task.ToLog());

            task.InputAssets.Add(JobData.InputAsset);

            // Add an output asset to contain the results of the job.
            task.OutputAssets.AddNew($"Indexing Output for {file}", AssetCreationOptions.StorageEncrypted);

            _job.StateChanged += StateChanged;

            JobData.InputAssetKeyRestored = DateTime.Now;
            MediaServicesUtils.RestoreEncryptionKey(_context, JobData);

            _job.Submit();

            // Check job execution and wait for job to finish.
            JobTask = _job.GetExecutionProgressTask(CancellationToken.None);
            return JobTask;
        }

        // This must be called once JobTask is in the completed state 
        public void HandleJobResult()
        {
            // If job state is Error, the event handling
            // method for job progress should log errors.  Here we check
            // for error state and exit if needed.
            if (_job.State == JobState.Error)
            {
                ErrorDetail error = _job.Tasks.First().ErrorDetails.First();
                _logger.Warning($"Error: {error.Code}. {error.Message}");
                return;
            }

            var elapsed = _job.EndTime - _job.StartTime;

            _logger.Information("-> Indexing job for {file} took {elapsed} seconds (processor={processor})",
                JobData, elapsed?.TotalSeconds ?? 0, _mediaProcessor);

            //_context.Locators.CreateLocator(LocatorType.Sas, job.OutputMediaAssets[0], _accessPolicy);

            JobData.OutputAsset = _job.OutputMediaAssets[0];
        }

        private void StateChanged(object sender, JobStateChangedEventArgs e)
        {
            Console.WriteLine("Job state changed event:");
            Console.WriteLine("  Previous state: " + e.PreviousState);
            _logger.Verbose("  Current job state: " + e.CurrentState);
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
                    Console.WriteLine("Please wait...");
                    break;
                case JobState.Processing:
                    //JobData.InputAssetKeyRestored = DateTime.Now;
                    //MediaServicesUtils.RestoreEncryptionKey(_context, JobData);
                    JobData.OutputAssetCreated = DateTime.Now;
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
            MediaServicesUtils.DeleteAsset(JobData.InputAsset);
            JobData.InputAsset = null;
            JobData.InputAssetDeleted = DateTime.Now;
        }
    }
}
