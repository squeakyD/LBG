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

    class IndexingJobManager : IManager<IndexJobData>
    {
        private readonly ILogger _logger;
        private readonly string _configuration;
        private readonly string _mediaProcessor;
        private readonly IManager<IndexJobData> _downloadManager;

        private readonly ConcurrentQueue<IndexJobData> _indexJobQueue = new ConcurrentQueue<IndexJobData>();
        private readonly List<IndexingJob> _currentJobs = new List<IndexingJob>();
        private readonly Task _processTask;
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private AutoResetEvent _itemsInQueueEvent = new AutoResetEvent(false);

        // Max Media Services Media Reserved Units for concurrent jobs
        private const int NumMediaReservedUnits = 25;

        private static IndexingJobManager _instance;

        public static IndexingJobManager CreateIndexingJobManager(
            string configuration,
            IManager<IndexJobData> downloadManager,
            string mediaProcessor = MediaProcessorNames.AzureMediaIndexer2Preview)
        {
            Debug.Assert(_instance == null);
            if (_instance == null)
            {
                _instance = new IndexingJobManager(configuration, downloadManager, mediaProcessor);
            }
            return _instance;
        }

        private IndexingJobManager(string configuration,
            IManager<IndexJobData> downloadManager,
            string mediaProcessor = MediaProcessorNames.AzureMediaIndexer2Preview)
        {
            _configuration = configuration;
            _downloadManager = downloadManager;
            _mediaProcessor = mediaProcessor;
            _logger = Logger.GetLog<IndexingJobManager>();

            SetMediaReservedUnits();

            _processTask = ProcessTasks();
        }

        private void SetMediaReservedUnits()
        {
            var context= CloudMediaContextFactory.Instance.CloudMediaContext;
            IEncodingReservedUnit encodingS1ReservedUnit = context.EncodingReservedUnits.FirstOrDefault();
            encodingS1ReservedUnit.ReservedUnitType = ReservedUnitType.Basic; // Corresponds to S1
            encodingS1ReservedUnit.Update();
            _logger.Verbose("Reserved Unit Type: {ReservedUnitType}", encodingS1ReservedUnit.ReservedUnitType);

            encodingS1ReservedUnit.CurrentReservedUnits = NumMediaReservedUnits;
            encodingS1ReservedUnit.Update();

            _logger.Verbose("Number of Media Reserved Units: {currentReservedUnits}", encodingS1ReservedUnit.CurrentReservedUnits);
        }

        private Task ProcessTasks()
        {
            var token = _tokenSource.Token;

            var task = Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    //_itemsInQueueEvent.WaitOne();

                    while (_currentJobs.Count < NumMediaReservedUnits && _indexJobQueue.Count > 0)
                    {
                        IndexJobData jobData;
                        if (_indexJobQueue.TryDequeue(out jobData))
                        {
                            _currentJobs.Add(CreateJob(jobData));
                        }
                    }

                    if (_currentJobs.Count > 0)
                    {
                        var allTasks = _currentJobs.Select(j => j.JobTask).ToArray();
                        int idx = Task.WaitAny(allTasks, 100);

                        if (idx != -1)
                        {
                            var finishedJob = _currentJobs[idx];

                            finishedJob.HandleJobResult();

                            _downloadManager.QueueItem(finishedJob.JobData);

                            _currentJobs.Remove(finishedJob);
                        }
                    }

                    token.ThrowIfCancellationRequested();
                }
            }, token);

            return task;
        }
        public void QueueItem(IndexJobData data)
        {
            _logger.Debug("Queueing job for {file}", data.Filename);
            _indexJobQueue.Enqueue(data);
            _itemsInQueueEvent.Set();
        }

        public Task WaitForAllTasks()
        {
            return Task.Run(() =>
            {
                _logger.Information("Waiting for all outstanding indexing jobs to complete");
                int numItems = 0;

                numItems = _indexJobQueue.Count;

                int oldNumItems = numItems + 1;
                while (numItems > 0)
                {
                    if (numItems != oldNumItems)
                    {
                        _logger.Information("Waiting for {n} files to be processed ...", numItems);
                        oldNumItems = numItems;
                    }
                    Thread.Sleep(1000);

                    numItems = _indexJobQueue.Count;
                }

                _logger.Debug("Waiting for remaining indexing tasks");

                var allTasks = _currentJobs.Select(j => j.JobTask).ToArray();
                Task.WaitAll(allTasks);

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

        private IndexingJob CreateJob(IndexJobData jobData)
        {
            var job = new IndexingJob(jobData, _configuration, _mediaProcessor);
            try
            {
                job.CreateJob();
            }
            catch (Exception ex)
            {
                job.DeleteAsset();

                if (!(ex is AggregateException))
                {
                    // In the event of an error, abort this job, but catch the exception so that other jobs can complete
                    _logger.Error(ex, "Error indexing {file}", jobData.Filename);
                }
                else
                {
                    throw;
                }
            }

            return job;
        }
    }
}
