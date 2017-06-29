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
            InitialiseMediaServicesClient();
        }

        public void Run()
        {
            try
            {
                //_restClient = new MSRestClient(_cachedCredentials);

                _downloadManager = DownloadManager.CreateDownloadManager(Config.Instance.NumberOfConcurrentDownloads);

                var fileProcessNotifier = FileProcessNotifier.Instance;

                string configuration = File.ReadAllText("config.json");
                _indexingJobManager = IndexingJobManager.CreateIndexingJobManager(configuration, _downloadManager);

                _uploadManager = UploadManager.CreateUploadManager(fileProcessNotifier, _indexingJobManager,
                    Config.Instance.NumberOfConcurrentUploads);

                _fileSourceManager = FileSourceManager.CreateFileSourceManager(_uploadManager, fileProcessNotifier);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Fatal error in ML Sandbox POC!!!");
            }
        }

        public void Shutdown()
        {
            _fileSourceManager.ShutdownTimer();
            var t1 = _uploadManager.WaitForAllTasks();
            t1.Wait();
            var t2 = _indexingJobManager.WaitForAllTasks();
            t2.Wait();
            var t3 = _downloadManager.WaitForAllTasks();
            t3.Wait();
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
