using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;

namespace MLSandboxPOC
{
    class UploadManager : IManager<string>
    {
        private readonly MediaServicesCredentials _credentials;
        private readonly FileProcessNotifier _fileProcessedNotifier;
        private readonly IManager<IndexJobData> _indexingManager;
        private readonly int _numberOfConcurrentTasks;
        private readonly ILogger _logger = Logger.GetLog<UploadManager>();

        private readonly ConcurrentQueue<string> _fileNames = new ConcurrentQueue<string>();
        private readonly List<Task> _currentTasks = new List<Task>();
        private readonly Task _processTask;
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private AutoResetEvent _itemsInQueueEvent = new AutoResetEvent(false);
        private static object _assetsLock = new object();

        private static UploadManager _instance;

        public static UploadManager CreateUploadManager(MediaServicesCredentials creds, FileProcessNotifier fileProcessedNotifier,
            IManager<IndexJobData> indexingManager,
            int numberOfConcurrentTasks)
        {
            Debug.Assert(_instance == null);
            if (_instance == null)
            {
                _instance = new UploadManager(creds, fileProcessedNotifier, indexingManager, numberOfConcurrentTasks);
            }
            return _instance;
        }

        private UploadManager(MediaServicesCredentials creds, FileProcessNotifier fileProcessedNotifier, IManager<IndexJobData> indexingManager,
            int numberOfConcurrentTasks)
        {
            _credentials = creds;
            _fileProcessedNotifier = fileProcessedNotifier;
            _indexingManager = indexingManager;
            _numberOfConcurrentTasks = numberOfConcurrentTasks;

            _logger.Information("Number of concurrent upload tasks: {numberOfConcurrentTasks}", _numberOfConcurrentTasks);

            _processTask = ProcessTasks();
        }

        private Task ProcessTasks()
        {
            var token = _tokenSource.Token;

            var task = Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    //_itemsInQueueEvent.WaitOne();

                    _currentTasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);

                    while (_currentTasks.Count < _numberOfConcurrentTasks && _fileNames.Count > 0)
                    {
                        string inputFile;
                        if (_fileNames.TryDequeue(out inputFile))
                        {
                            _currentTasks.Add(UploadFile(inputFile));
                        }
                    }

                    token.ThrowIfCancellationRequested();
                }
            }, token);

            return task;
        }

        private Task UploadFile(string fileName)
        {
            return Task.Run(() =>
            {
                try
                {
                    var context = new CloudMediaContext(_credentials);
                    var data = CreateAssetAndUploadSingleFile(context, fileName, AssetCreationOptions.StorageEncrypted);
                    _indexingManager.QueueItem(data);
                }
                catch (Exception ex)
                {
                    // In the event of an error, abort this job, but catch the exception so that other jobs can complete
                    _logger.Error(ex, "Error uploading/indexing {file}", fileName);
                }
            });
        }

        private IndexJobData CreateAssetAndUploadSingleFile(CloudMediaContext context, string filePath, AssetCreationOptions options)
        {
            //IAsset asset;
            var data = new IndexJobData {Filename = filePath};

            try
            {
                lock (_assetsLock)
                {
                    data.InputAsset = context.Assets.CreateFromFile(filePath, options);
                    data.InputFileUploaded = DateTime.Now;
                    _logger.Information("Created and uploaded asset {asset} from {file}", data.InputAsset.ToLog(), filePath);
                }

                MediaServicesUtils.RemoveEncryptionKey(data);
                // var ck = context.ContentKeys.Where(k => k.ContentKeyType == ContentKeyType.StorageEncryption && k.Id==data.ContentKey.Id).ToArray();
               // MediaServicesUtils.RestoreEncryptionKey(context, data);

                _fileProcessedNotifier.NotifyFileProcessed(filePath);
            }
            catch
            {
                if (data.InputAsset != null)
                {
                    MediaServicesUtils.DeleteAsset(data.InputAsset);
                }
                throw;
            }
            // TEST
            //RestoreEncryptionKey(asset);

            //var test = asset.ContentKeys;
            return data;
        }

        public Task WaitForAllTasks()
        {
            return Task.Run(() =>
            {
                _logger.Information("Waiting for all outstanding uploads to complete");
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

        void IManager<string>.QueueItem(string fileName)
        {
            _logger.Debug("Queueing {file}", fileName);
            _fileNames.Enqueue(fileName);
            _itemsInQueueEvent.Set();
        }

    }
}
