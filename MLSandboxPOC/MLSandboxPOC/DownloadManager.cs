using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;
using System.Diagnostics;
using System.Linq;

namespace MLSandboxPOC
{
    class DownloadManager : IManager<IndexJobData>
    {
        private readonly ILogger _logger;
        private readonly CloudMediaContext _context;
        private readonly int _numberOfConcurrentTasks;
        private readonly bool _deleteFiles;

        private readonly ConcurrentQueue<IndexJobData> _assets = new ConcurrentQueue<IndexJobData>();
        private readonly List<Task> _currentTasks = new List<Task>();
        private readonly Task _processTask;
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        //private readonly BlobTransferClient _blobClient = new BlobTransferClient();

        private static DownloadManager _instance;

        public static DownloadManager CreateDownloadManager(int numberOfConcurrentDownloads)
        {
            Debug.Assert(_instance == null);
            if (_instance == null)
            {
                _instance = new DownloadManager(numberOfConcurrentDownloads);
            }
            return _instance;
        }

        private DownloadManager(int numberOfConcurrentDownloads, bool deleteFiles = true)
        {
            _context = CloudMediaContextFactory.Instance.CloudMediaContext;
            _numberOfConcurrentTasks = numberOfConcurrentDownloads;
            _deleteFiles = deleteFiles;
            _logger = Logger.GetLog<DownloadManager>();

            _logger.Information("Number of concurrent download tasks: {numberOfConcurrentTasks}", _numberOfConcurrentTasks);

            _processTask = ProcessTasks();
        }

        private Task ProcessTasks()
        {
            var token = _tokenSource.Token;

            var task = Task.Run(() =>
             {
                 while (!token.IsCancellationRequested)
                 {
                     _currentTasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);

                     while (_currentTasks.Count < _numberOfConcurrentTasks && _assets.Count > 0)
                     {
                         IndexJobData data;
                         if (_assets.TryDequeue(out data))
                         {
                             _currentTasks.Add(Task.Run(() => DoDownloadAsset(data)));
                         }
                     }

                     token.ThrowIfCancellationRequested();
                 }
             }, token);

            return task;
        }

        public void QueueItem(IndexJobData jobData)
        {
            _logger.Debug("QueueItem");
            _assets.Enqueue(jobData);
        }

        public Task WaitForAllTasks()
        {
            return Task.Run(() =>
            {
                _logger.Information("Waiting for all outstanding downloads to complete");
                int numItems = 0;
                Interlocked.Exchange(ref numItems, _assets.Count);
                numItems = _assets.Count;

                int oldNumItems = numItems + 1;

                while (numItems > 0)
                {
                    if (numItems != oldNumItems)
                    {
                        _logger.Information("Waiting for {n} assets to be processed ...", numItems);
                        oldNumItems = numItems;
                    }
                    Thread.Sleep(1000);

                    Interlocked.Exchange(ref numItems, _assets.Count);
                    //numItems = _assets.Count;
                }

                _logger.Verbose("Waiting for remaining download tasks");
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

        private async Task DoDownloadAsset(IndexJobData data)
        {
            _logger.Information("Downloading files from asset {asset}", data.OutputAsset.ToLog());

            try
            {
                //List<Task> tasks = new List<Task>();

                foreach (IAssetFile file in data.OutputAsset.AssetFiles)
                {
                    //var task = Task.Run(async () =>
                    //{
                    _logger.Information("Downloading {file}", file.ToLog());

                    //await file.DownloadAsync(Path.Combine(Config.Instance.OutputDirectory, file.Name), _blobClient, asset.Locators[0], CancellationToken.None);
                    file.Download(Path.Combine(Config.Instance.OutputDirectory, file.Name));

                    _logger.Verbose("Deleting output file {file} in asset {asset}", file.ToLog(), data.OutputAsset.ToLog());
                    await file.DeleteAsync();
                    //});
                    //
                    //tasks.Add(task);
                }

                //// TODO: try/catch here - https://msdn.microsoft.com/en-us/library/dd537614(v=vs.110).aspx
                //Task.WaitAll(tasks.ToArray());

                _logger.Verbose("Deleting output asset {asset}", data.OutputAsset.ToLog());
                await data.OutputAsset.DeleteAsync();

                int numAssets = _context.Assets?.Count() ?? 0;
                _logger.Verbose("Total number of assets now in Azure: {numAssets}", numAssets);

                data.OutputAssetDeleted = DateTime.Now;

                ResultsLogger.Instance.WriteResults(data);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred downloading files from asset {asset}", data.OutputAsset.ToLog());
            }
        }
    }
}
