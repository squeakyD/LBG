using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;
using Timer = System.Timers.Timer;

namespace MLSandboxPOC
{
    class DownloadManager: IManager<IAsset>
    {
        private readonly ILogger _logger;
        private readonly CloudMediaContext _context;
        private readonly string _outputDirectory;
        private readonly bool _deleteFiles;

        private readonly Queue<IAsset> _assets = new Queue<IAsset>();
        private readonly List<Task> _currentTasks = new List<Task>();
        private readonly Timer _timer;

        //private readonly BlobTransferClient _blobClient = new BlobTransferClient();

        public DownloadManager(CloudMediaContext context, string outputDirectory, int interval = 30, bool deleteFiles = true)
        {
            _context = context;
            _outputDirectory = outputDirectory;
            _deleteFiles = deleteFiles;
            _logger = Logger.GetLog<DownloadManager>();

            _timer = new Timer(interval);
            _timer.Elapsed += _timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;
        }


        private void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _currentTasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);
            
            if (_assets.Count == 0)
            {
                return;
            }

            while (_currentTasks.Count < _context.NumberOfConcurrentTransfers && _assets.Count > 0)
            {
                var assetToDownload = _assets.Dequeue();
                //++_runningTasks;
                _currentTasks.Add(Task.Run(() => DoDownloadAsset(assetToDownload)));
            }
        }

        public void QueueItem(IAsset asset)
        {
            _logger.Debug("QueueItem");
            _assets.Enqueue(asset);
        }

        //public void WaitForAllTasks()
        //{
        //    _logger.Information("Waiting for all outstanding downloads to complete");

        //    //_currentTasks.Add(Task.Run(() =>
        //    //{
        //    int numItems = 0;
        //    //Interlocked.Exchange(ref numAssets, _assets.Count);
        //    numItems = _assets.Count;

        //    int oldNumItems = numItems + 1;

        //    while (numItems > 0)
        //    {
        //        if (numItems != oldNumItems)
        //        {
        //            _logger.Information("Waiting for {n} assets to be processed ...", numItems);
        //            oldNumItems = numItems;
        //        }
        //        Thread.Sleep(2000);

        //        //Interlocked.Exchange(ref numAssets, _assets.Count);
        //        numItems = _assets.Count;
        //    }
        //    //}));

        //    _logger.Debug("Waiting for remaining download tasks");
        //    Task.WaitAll(_currentTasks.ToArray());
        //}
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

                    //await file.DownloadAsync(Path.Combine(_outputDirectory, file.Name), _blobClient, asset.Locators[0], CancellationToken.None);
                    file.Download(Path.Combine(_outputDirectory, file.Name));

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
            catch(Exception ex)
            {
                _logger.Error(ex, "Error occurred downloading files from asset {asset}", asset.ToLog()); 
            }
        }
    }
}
