using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Serilog;
using Timer = System.Timers.Timer;
using System.IO;
using System.Configuration;

namespace MLSandboxPOC
{
   // delegate void NotifyFileProcessed(string fileName);

    class FileSourceManager
    {
        private readonly ILogger _logger;
        private readonly string _sourceDirectory;
        private readonly string _processedDirectory;
        private readonly IManager<string> _indexingJobManager;
        private readonly int _interval;
        private readonly Timer _timer;

        private static readonly string _searchPattern = ConfigurationManager.AppSettings["SourceFilePattern"];

        private NotifyFileProcessed;

        public FileSourceManager(string sourceDirectory,string processedDirectory,
            IManager<string> indexingJobManager,
            int interval = 30)
        {
            _sourceDirectory = sourceDirectory;
            _processedDirectory = processedDirectory;
            _indexingJobManager = indexingJobManager;
            _interval = interval;
            _logger = Logger.GetLog<FileSourceManager>();

            _timer = new Timer(interval);
            _timer.Elapsed += _timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;

            //NotifyFileProcessed = FileProcessed;
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach(string fileName in Directory.EnumerateFiles(_sourceDirectory, _searchPattern))
            {
                _indexingJobManager.QueueItem(fileName);

            }
        }

        private void FileProcessed(string fileName)
        {

        }
    }
}
