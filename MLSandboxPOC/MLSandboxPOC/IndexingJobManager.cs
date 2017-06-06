using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly int _numberOfConcurrentTransfers;
        private readonly string _mediaProcessor;
        private readonly bool _deleteFiles;
        private readonly FileProcessNotifier _fileProcessedNotifier;
        private readonly IManager<IAsset> _downloadManager;

        private readonly ConcurrentQueue<string> _fileNames = new ConcurrentQueue<string>();
        private readonly List<Task> _currentTasks = new List<Task>();
        private readonly Task _processTask;
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private AutoResetEvent _itemsInQueueEvent = new AutoResetEvent(false);

        private static IndexingJobManager _instance;

        public static IndexingJobManager CreateIndexingJobManager(CloudMediaContext context,
            string configuration,
            FileProcessNotifier fileProcessedNotifier,
            IManager<IAsset> downloadManager,
            int numberOfConcurrentTransfers,
            string mediaProcessor = MediaProcessorNames.AzureMediaIndexer2Preview,
            bool deleteFiles = true)
        {
            Debug.Assert(_instance == null);
            if (_instance == null)
            {
                _instance = new IndexingJobManager(context, configuration, fileProcessedNotifier, downloadManager, numberOfConcurrentTransfers, mediaProcessor, deleteFiles);
            }
            return _instance;
        }

        private IndexingJobManager(CloudMediaContext context,
            string configuration,
            FileProcessNotifier fileProcessedNotifier,
            IManager<IAsset> downloadManager,
            int numberOfConcurrentTransfers,
            string mediaProcessor = MediaProcessorNames.AzureMediaIndexer2Preview,
            bool deleteFiles = true)
        {
            _context = context;
            _configuration = configuration;
            _fileProcessedNotifier = fileProcessedNotifier;
            _downloadManager = downloadManager;
            _numberOfConcurrentTransfers = numberOfConcurrentTransfers;
            _mediaProcessor = mediaProcessor;
            _deleteFiles = deleteFiles;
            _logger = Logger.GetLog<IndexingJobManager>();

            SetMediaReservedUnits();

            _processTask = ProcessTasks();
        }

        private void SetMediaReservedUnits()
        {
            IEncodingReservedUnit encodingS1ReservedUnit = _context.EncodingReservedUnits.FirstOrDefault();
            encodingS1ReservedUnit.ReservedUnitType = ReservedUnitType.Basic; // Corresponds to S1
            encodingS1ReservedUnit.Update();
            _logger.Verbose("Reserved Unit Type: {ReservedUnitType}", encodingS1ReservedUnit.ReservedUnitType);

            encodingS1ReservedUnit.CurrentReservedUnits = _numberOfConcurrentTransfers;
            encodingS1ReservedUnit.Update();

            _logger.Verbose("Number of reserved units: {currentReservedUnits}", encodingS1ReservedUnit.CurrentReservedUnits);
        }

        //public List<string> UnprocessedFiles => new List<string>();

        private Task ProcessTasks()
        {
            var token = _tokenSource.Token;

            var task = Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    //_itemsInQueueEvent.WaitOne();

                    _currentTasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);

                    while (_currentTasks.Count < _numberOfConcurrentTransfers && _fileNames.Count > 0)
                    {
                        string inputFile;
                        if (_fileNames.TryDequeue(out inputFile))
                        {
                            _currentTasks.Add(RunJob(inputFile));
                        }
                    }

                    token.ThrowIfCancellationRequested();
                }
            }, token);

            return task;
        }

        public void QueueItem(string fileName)
        {
            _logger.Debug("Queueing {file}", fileName);
            _fileNames.Enqueue(fileName);
            _itemsInQueueEvent.Set();
        }

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

                _tokenSource.Cancel();

                try
                {
                    _processTask.Wait();
                }
                catch (AggregateException ae)
                {
                    ae.Handle(ex =>
                    {
                        if (ex is OperationCanceledException)
                        {
                            _logger.Information("Exited ProcessTasks");
                        }
                        return ex is OperationCanceledException;
                    });
                }
                finally
                {
                    _tokenSource.Dispose();
                }
            });
        }

        //private Task<IAsset> RunJob(string fileName)
        private Task RunJob(string fileName)
        {
            return Task.Run(() =>
                {
                    var job = new IndexingJob(_context, _fileProcessedNotifier, fileName, _configuration, _mediaProcessor);
                    try
                    {
                        var outputAsset = job.Run();
                        _downloadManager.QueueItem(outputAsset);
                    }
                    catch (Exception ex)
                    {
                        // In the event of an error, abort this job, but catch the exception so that other jobs can complete
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
