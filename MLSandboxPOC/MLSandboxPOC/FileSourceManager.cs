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
        private readonly IManager<string> _indexingJobManager;
        private readonly Timer _timer;

        private static FileSourceManager _instance;

        private readonly static object _fileProcessorLock = new object();

        public static FileSourceManager CreateFileSourceManager(IManager<string> indexingJobManager, FileProcessNotifier notifier)
        {
            Debug.Assert(_instance == null);
            _instance = new FileSourceManager(indexingJobManager);
            notifier.AddFileObserver(_instance);
            return _instance;
        }

        private FileSourceManager(IManager<string> indexingJobManager)
        {
            _indexingJobManager = indexingJobManager;
            _logger = Logger.GetLog<FileSourceManager>();

            CheckDirectoriesExist();

            _timer = new Timer(Config.Instance.FileWatcherInterval * 1000);
            _timer.Elapsed += _timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;
        }

        public void ShutdownTimer()
        {
            _timer.Stop();
        }

        private void CheckDirectoriesExist()
        {
            if (!Directory.Exists(Config.Instance.ProcessingDirectory))
            {
                _logger.Warning("Creating intermediate processing directory ({dir}) as it was not found", Config.Instance.ProcessingDirectory);
                Directory.CreateDirectory(Config.Instance.ProcessingDirectory);
            }
            if (!Directory.Exists(Config.Instance.ProcessedDirectory))
            {
                _logger.Warning("Creating intermediate processed files directory ({dir}) as it was not found", Config.Instance.ProcessedDirectory);
                Directory.CreateDirectory(Config.Instance.ProcessedDirectory);
            }
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CheckSourceDirectory();
        }

        private void CheckSourceDirectory()
        {
            _logger.Verbose("Checking source directory for new files");

            foreach (string filePath in Directory.EnumerateFiles(Config.Instance.SourceDirectory, Config.Instance.SourceFilePattern))
            {
                try
                {
                    string fileToProcess = Path.Combine(Config.Instance.ProcessingDirectory, Path.GetFileName(filePath));
                    string dest = Path.Combine(Config.Instance.ProcessingDirectory, fileToProcess);

                    if (File.Exists(dest))
                    {
                        File.Delete(dest);
                        _logger.Warning("Found and deleted {file} in {processing} directory which matches a previously uploaded filename", dest, Config.Instance.ProcessingDirectory);
                    }

                    File.Move(filePath, dest);
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
                    string dest = Path.Combine(Config.Instance.ProcessedDirectory, Path.GetFileName(filePath));
                    if (File.Exists(dest))
                    {
                        File.Delete(dest);
                        _logger.Warning("Found and deleted {file} in {processed} directory which matches a previously processed filename", dest, Config.Instance.ProcessedDirectory);
                    }

                    File.Move(filePath, dest);

                    _logger.Debug("Processed {file}", Path.GetFileName(filePath));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error moving {file} to {dest}", filePath, Config.Instance.ProcessedDirectory);
                }
            }
        }
    }
}
