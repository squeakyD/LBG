using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;
using System.Diagnostics;

namespace MLSandboxPOC
{
    class DownloadManager : IManager<IAsset>
    {
        private readonly ILogger _logger;
        private readonly CloudMediaContext _context;
        private readonly int _numberOfConcurrentTasks;
        private readonly bool _deleteFiles;

        private readonly ConcurrentQueue<IAsset> _assets = new ConcurrentQueue<IAsset>();
        private readonly List<Task> _currentTasks = new List<Task>();
        private readonly Task _processTask;
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        //private readonly BlobTransferClient _blobClient = new BlobTransferClient();

        private static DownloadManager _instance;

        public static DownloadManager CreateDownloadManager(CloudMediaContext context,
            int numberOfConcurrentDownloads)
        {
            Debug.Assert(_instance == null);
            if (_instance == null)
            {
                _instance = new DownloadManager(context, numberOfConcurrentDownloads);
            }
            return _instance;
        }

        private DownloadManager(CloudMediaContext context,
            int numberOfConcurrentDownloads,
            bool deleteFiles = true)
        {
            _context = context;
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
                         IAsset assetToDownload;
                         if (_assets.TryDequeue(out assetToDownload))
                         {
                             _currentTasks.Add(Task.Run(() => DoDownloadAsset(assetToDownload)));
                         }
                     }

                     token.ThrowIfCancellationRequested();
                 }
             }, token);

            return task;
        }

        public void QueueItem(IAsset asset)
        {
            _logger.Debug("QueueItem");
            _assets.Enqueue(asset);
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

                _logger.Debug("Waiting for remaining download tasks");
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

        private async Task DoDownloadAsset(IAsset asset)
        {
            _logger.Information("Downloading files from asset {asset}", asset.ToLog());

            try
            {
                //List<Task> tasks = new List<Task>();

                foreach (IAssetFile file in asset.AssetFiles)
                {
                    //var task = Task.Run(async () =>
                    //{
                    _logger.Information("Downloading {file}", file.ToLog());

                    //await file.DownloadAsync(Path.Combine(Config.Instance.OutputDirectory, file.Name), _blobClient, asset.Locators[0], CancellationToken.None);
                    file.Download(Path.Combine(Config.Instance.OutputDirectory, file.Name));

                    if (_deleteFiles)
                    {
                        _logger.Debug("Deleting output file {file} in asset {asset}", file.ToLog(), asset.ToLog());
                        //await file.DeleteAsync();
                        file.Delete();
                    }
                    //});
                    //
                    //tasks.Add(task);
                }

                //// TODO: try/catch here - https://msdn.microsoft.com/en-us/library/dd537614(v=vs.110).aspx
                //Task.WaitAll(tasks.ToArray());

                if (_deleteFiles)
                {
                    _logger.Debug("Deleting output asset {asset}", asset.ToLog());
                    await asset.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred downloading files from asset {asset}", asset.ToLog());
            }
        }
    }
}
