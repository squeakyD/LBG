using System;
using System.Timers;
using Serilog;
using Timer = System.Timers.Timer;
using System.IO;
using System.Configuration;
using System.Diagnostics;

namespace MLSandboxPOC
{
    interface IFileProcessedObserver
    {
        void FileProcessed(string fileName);
    }

    class FileSourceManager: IFileProcessedObserver
    {
        private readonly ILogger _logger;
        private readonly string _sourceDirectory;
        private readonly string _processedDirectory;
        private readonly IManager<string> _indexingJobManager;
        private readonly int _interval;
        private readonly Timer _timer;

        private static readonly string _processingDirectory = ConfigurationManager.AppSettings["ProcessingDirectory"];
        private static readonly string _searchPattern = ConfigurationManager.AppSettings["SourceFilePattern"];

        private static FileSourceManager _instance;

        private readonly static object _fileProcessorLock = new object();

        public static FileSourceManager CreateFileSourceManager(string sourceDirectory, string processedDirectory,
            IManager<string> indexingJobManager, FileProcessNotifier notifier,
            int interval = 30)
        {
            Debug.Assert(_instance == null);
            _instance = new FileSourceManager(sourceDirectory, processedDirectory, indexingJobManager, interval);
            notifier.AddFileObserver(_instance);
            return _instance;
        }

        private FileSourceManager(string sourceDirectory,string processedDirectory,
            IManager<string> indexingJobManager,
            int interval = 30)
        {
            _sourceDirectory = sourceDirectory;
            _processedDirectory = processedDirectory;
            _indexingJobManager = indexingJobManager;
            _interval = interval;
            _logger = Logger.GetLog<FileSourceManager>();

            CheckDirectoriesExist();

            CheckSourceDirectory();

            _timer = new Timer(interval);
            _timer.Elapsed += _timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;
        }

        private void CheckDirectoriesExist()
        {
            if (!Directory.Exists(_processingDirectory))
            {
                _logger.Warning("Creating intermediate processing directory ({dir}) as it was not found", _processingDirectory);
                Directory.CreateDirectory(_processingDirectory);
            }
            if (!Directory.Exists(_processedDirectory))
            {
                _logger.Warning("Creating intermediate processed files directory ({dir}) as it was not found", _processedDirectory);
                Directory.CreateDirectory(_processedDirectory);
            }
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CheckSourceDirectory();
        }

        private void CheckSourceDirectory()
        {
            _logger.Verbose("Checking source directory for new files");

            foreach (string filePath in Directory.EnumerateFiles(_sourceDirectory, _searchPattern))
            {
                try
                {
                    string fileToProcess = Path.Combine(_processingDirectory, Path.GetFileName(filePath));
                    File.Move(filePath, Path.Combine(_processingDirectory, fileToProcess));

                    _indexingJobManager.QueueItem(fileToProcess);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing source {file}", filePath);
                }
            }
        }

        void IFileProcessedObserver.FileProcessed(string filePath)
        {
            lock(_fileProcessorLock)
            {
                try
                {
                    string dest = Path.Combine(_processedDirectory, Path.GetFileName(filePath));
                    File.Move(filePath, dest);

                    _logger.Debug("Processed {file}", Path.GetFileName(filePath));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error moving {file} to {dest}", filePath, _processedDirectory);
                }
            }
        }
    }
}
