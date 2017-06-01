using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace MLSandboxPOC
{
    //class ManagerBase
    //{

    //}

    interface IManager<T> where T : class
    {
        void QueueItem(T item);
    }

    class IndexingJobManager : IManager<string>
    {
        private readonly ILogger _logger;
        private readonly CloudMediaContext _context;
        private readonly string _configuration;
        private readonly string _mediaProcessor;
        private readonly bool _deleteFiles;
        //private readonly string _inputFileDirectory;
        private IManager<IAsset> _downloadManager;

        private readonly Queue<string> _fileNames = new Queue<string>();
        //private readonly List<Task<IAsset>> _currentTasks = new List<Task<IAsset>>();
        private readonly List<Task> _currentTasks = new List<Task>();
        //private Task _checkJobs;
        private readonly Timer _timer;

        public IndexingJobManager(CloudMediaContext context, string configuration,
            IManager<IAsset> downloadManager,
            string mediaProcessor = MediaProcessorNames.AzureMediaIndexer2Preview,
            int interval = 30, bool deleteFiles = true)
        {
            _context = context;
            _configuration = configuration;
            _downloadManager = downloadManager;
            _mediaProcessor = mediaProcessor;
            _deleteFiles = deleteFiles;
            _logger = Logger.GetLog<IndexingJobManager>();

            _timer = new Timer(interval);
            _timer.Elapsed += _timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;

            // TODO: 
            //_checkJobs = Task.Run(()=>);
        }

        public List<string> UnprocessedFiles => new List<string>();

        private void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _currentTasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);

            if (_fileNames.Count == 0)
            {
                return;
            }

            while (_currentTasks.Count < _context.NumberOfConcurrentTransfers && _fileNames.Count > 0)
            {
                string inputFile = _fileNames.Dequeue();
                _currentTasks.Add(RunJob(inputFile));
            }
        }

        public void QueueItem(string fileName)
        {
            _fileNames.Enqueue(fileName);
        }

        //public void WaitForAllTasks()
        //{
        //    _logger.Information("Waiting for all outstanding indexing jobs to complete");
        //    int numItems = 0;

        //    numItems = _fileNames.Count;

        //    int oldNumItems = numItems + 1;
        //    while (numItems > 0)
        //    {
        //        if (numItems != oldNumItems)
        //        {
        //            _logger.Information("Waiting for {n} files to be processed ...", numItems);
        //            oldNumItems = numItems;
        //        }
        //        Thread.Sleep(2000);

        //        numItems = _fileNames.Count;
        //    }

        //    _logger.Debug("Waiting for remaining indexing tasks");
        //    Task.WaitAll(_currentTasks.ToArray());
        //}

        public Task WaitForAllTasks()
        {
            return Task.Run(() =>
            {
                _logger.Information("Waiting for all outstanding indexing jobs to complete");
                int numItems = 0;

                numItems = _fileNames.Count;

                int oldNumItems = numItems + 1;
                while (numItems > 0)
                {
                    if (numItems != oldNumItems)
                    {
                        _logger.Information("Waiting for {n} files to be processed ...", numItems);
                        oldNumItems = numItems;
                    }
                    Thread.Sleep(1000);

                    numItems = _fileNames.Count;
                }

                _logger.Debug("Waiting for remaining indexing tasks");
                Task.WaitAll(_currentTasks.ToArray());
            });
        }

        //private Task<IAsset> RunJob(string fileName)
        private Task RunJob(string fileName)
        {
            return Task.Run(() =>
                {
                    var job = new IndexingJob(_context, fileName, _configuration);
                    try
                    {
                        var outputAsset = job.Run();
                        _downloadManager.QueueItem(outputAsset);
                    }
                    catch (Exception ex)
                    {
                        // In the event of an error, abort this job, but catch the exception so that other jobs can complete
                        UnprocessedFiles.Add(fileName);
                        _logger.Error(ex, "Error uploading/indexing {file}", fileName);
                    }
                    finally
                    {
                        job.DeleteAsset();
                    }
                }
            );
        }
    }
}
