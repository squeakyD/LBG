using System;
using System.IO;
using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;

namespace MLSandboxPOC
{
    class ProcessRunner
    {
        private CloudMediaContext _context;
        private FileSourceManager _fileSourceManager;
        private UploadManager _uploadManager;
        private IndexingJobManager _indexingJobManager;
        private DownloadManager _downloadManager;
        private readonly ILogger _logger = Logger.GetLog<ProcessRunner>();

        private static ProcessRunner _instance;

        public static ProcessRunner Instance => _instance ?? (_instance = new ProcessRunner());

        private ProcessRunner()
        {
        }

        public void Run()
        {
            _logger.Information("Creating processing pipeline");
            try
            {
                InitialiseMediaServicesClient();
                //_restClient = new MSRestClient(_cachedCredentials);

                _downloadManager = DownloadManager.CreateDownloadManager(Config.Instance.NumberOfConcurrentDownloads);

                var fileProcessNotifier = FileProcessNotifier.Instance;

                string configuration = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"));
                _indexingJobManager = IndexingJobManager.CreateIndexingJobManager(configuration, _downloadManager);

                _uploadManager = UploadManager.CreateUploadManager(fileProcessNotifier, _indexingJobManager,
                    Config.Instance.NumberOfConcurrentUploads);

                _fileSourceManager = FileSourceManager.CreateFileSourceManager(_uploadManager, fileProcessNotifier);

                _logger.Information("Processing pipeline created");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Fatal error in ML Sandbox POC!!!");
                throw;
            }
        }

        public void Shutdown()
        {
            _logger.Information("Shutting down processing pipeline");

            if (_fileSourceManager != null)
            {
                _fileSourceManager.ShutdownTimer();
            }
            if (_uploadManager != null)
            {
                var t1 = _uploadManager.WaitForAllTasks();
                t1.Wait();
            }
            if (_indexingJobManager != null)
            {
                var t2 = _indexingJobManager.WaitForAllTasks();
                t2.Wait();
            }
            if (_downloadManager != null)
            {
                var t3 = _downloadManager.WaitForAllTasks();
                t3.Wait();
            }
        }

        private void InitialiseMediaServicesClient()
        {
            _context = CloudMediaContextFactory.Instance.CloudMediaContext;

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

    }
}
