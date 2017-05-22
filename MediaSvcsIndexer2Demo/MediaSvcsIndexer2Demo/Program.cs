using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;

namespace MediaSvcsIndexer2Demo
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

        static void Main(string[] args)
        {
            try
            {
                _logger = Logger.GetLog<Program>();

                CreateCredentials();

                // Used the cached credentials to create CloudMediaContext.
                _context = new CloudMediaContext(_cachedCredentials);

                if (args.Length > 0)
                {
                    if (args[0].Equals("delOnly", StringComparison.InvariantCultureIgnoreCase))
                    {
                        DeleteAssetFiles();
                        return;
                    }

                    if (args[0].Equals("delAfter", StringComparison.InvariantCultureIgnoreCase))
                    {
                        _delFiles = true;
                    }
                }

                // Run indexing job.
                string src = Path.Combine(_sourceDir, "4th Apr 17_612026009250275cut.wav");
                var asset = RunIndexingJob(src, @"..\..\config.json");

                // Download the job output asset.
                DownloadAsset(asset, _outDir);

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

        static IAsset RunIndexingJob(string inputMediaFilePath, string configurationFile)
        {
            _logger.Information("Running index job for {inputMediaFilePath}", inputMediaFilePath);

            // Create an asset and upload the input media file to storage.
            IAsset asset = CreateAssetAndUploadSingleFile(inputMediaFilePath,
                "My Indexing Input Asset",
                AssetCreationOptions.None);

            // Declare a new job.
            IJob job = _context.Jobs.Create("My Indexing Job");

            // Get a reference to Azure Media Indexer 2 Preview.
            const string MediaProcessorName = "Azure Media Indexer 2 Preview";

            var processor = GetLatestMediaProcessorByName(MediaProcessorName);

            // Read configuration from the specified file.
            string configuration = File.ReadAllText(configurationFile);

            // Create a task with the encoding details, using a string preset.
            ITask task = job.Tasks.AddNew("My Indexing Task",
                processor,
                configuration,
                TaskOptions.None);

            _logger.Debug("Created task {taskId} for job", task.Id);

            // Specify the input asset to be indexed.
            task.InputAssets.Add(asset);

            // Add an output asset to contain the results of the job.
            task.OutputAssets.AddNew("My Indexing Output Asset", AssetCreationOptions.None);

            // Use the following event handler to check job progress.  
            job.StateChanged += new EventHandler<JobStateChangedEventArgs>(StateChanged);

            _logger.Information("Submitted job {jobId}", job.Id);

            // Launch the job.
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

            return job.OutputMediaAssets[0];
        }

        static IAsset CreateAssetAndUploadSingleFile(string filePath, string assetName, AssetCreationOptions options)
        {
            IAsset asset = _context.Assets.Create(assetName, options);

            var assetFile = asset.AssetFiles.Create(Path.GetFileName(filePath));
            assetFile.Upload(filePath);
            _logger.Information("Uploaded {filePath}", filePath);

            return asset;
        }

        static void DownloadAsset(IAsset asset, string outputDirectory)
        {
            _logger.Debug("Downloading files");
            foreach (IAssetFile file in asset.AssetFiles)
            {
                _logger.Information($"Downloading {file.Name}");
                file.Download(Path.Combine(outputDirectory, file.Name));

                if (_delFiles)
                {
                    _logger.Debug($"Deleting output file {file.Name} in asset {asset.Name}");
                    file.Delete();
                }
            }

            if (_delFiles)
            {
                _logger.Debug($"Deleting output asset {asset.Name}");
                asset.Delete();
            }
        }

        private static void DeleteAssetFiles()
        {
            foreach (var asset in _context.Assets)
            {
                foreach (var af in asset.AssetFiles)
                {
                    _logger.Information("Deleting {file} from asset {asset}", af.Name, new {asset.Name, asset.Id});
                    af.Delete();
                }

                _logger.Information($"Deleting asset {asset}", new {asset.Name, asset.Id});
                asset.Delete();
            }
        }

        static IMediaProcessor GetLatestMediaProcessorByName(string mediaProcessorName)
        {
            var processor = _context.MediaProcessors
                .Where(p => p.Name == mediaProcessorName)
                .ToList()
                .OrderBy(p => new Version(p.Version))
                .LastOrDefault();

            if (processor == null)
                throw new ArgumentException($"Unknown media processor - {mediaProcessorName}");

            return processor;
        }

        private static void StateChanged(object sender, JobStateChangedEventArgs e)
        {
            Console.WriteLine("Job state changed event:");
            Console.WriteLine("  Previous state: " + e.PreviousState);
            Console.WriteLine("  Current state: " + e.CurrentState);

            switch (e.CurrentState)
            {
                case JobState.Finished:
                    Console.WriteLine();
                    Console.WriteLine("Job is finished.");
                    _logger.Debug("Job is finished");
                    Console.WriteLine();
                    break;
                case JobState.Canceling:
                case JobState.Queued:
                case JobState.Scheduled:
                case JobState.Processing:
                    Console.WriteLine("Please wait...");
                    break;
                case JobState.Canceled:
                case JobState.Error:
                    // Cast sender as a job.
                    IJob job = (IJob)sender;
                    // Display or log error details as needed.
                    // LogJobStop(job.Id);
                    Console.WriteLine($"{job.Name} job {e.CurrentState}");
                    break;
                default:
                    break;
            }
        }
    }

}
