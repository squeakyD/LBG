using System;
using System.Configuration;
using Serilog;

namespace MLSandboxPOC
{
    class Config
    {
        //private readonly string _mediaServicesAccountName = ConfigurationManager.AppSettings["MediaServicesAccountName"];
        //private readonly string _mediaServicesAccountKey = ConfigurationManager.AppSettings["MediaServicesAccountKey"];
        private readonly string _sourceDirectory = ConfigurationManager.AppSettings["SourceDirectory"];

        private readonly string _processedDirectory = ConfigurationManager.AppSettings["ProcessedDirectory"];
        private readonly string _outputDirectory = ConfigurationManager.AppSettings["OutputDirectory"];
        private readonly string _processingDirectory = ConfigurationManager.AppSettings["ProcessingDirectory"];
        private readonly string _searchPattern = ConfigurationManager.AppSettings["SourceFilePattern"];
        private readonly int _fileWatcherInterval;
        private readonly int _numberOfConcurrentUploads;
        private readonly int _numberOfConcurrentDownloads;
        private readonly int _numberOfConcurrentTransfers;
        private readonly bool _useDefNumberOfConcurrentTransfers;
        private readonly int _parallelTransferThreadCount;
        private readonly bool _useDefParallelTransferThreadCount;
        private readonly string _outputLogDir;

        private const int DefFileWatcherInterval = 10;

        // Copied from Media Services Client SDK (blob updload/download code and CloudMediaContext)
        private const int DefaultConnectionLimitMultiplier = 8;

        private readonly int DefNumberOfConcurrentTransfers = 2;    // in CloudMediaContext
        private readonly int DefParallelTransferThreadCount = 10;    // in CloudMediaContext
        private readonly int MaxNumberOfConcurrentTransfers = Environment.ProcessorCount * DefaultConnectionLimitMultiplier;
        private readonly int MaxParallelTransferThreadCount = Environment.ProcessorCount * DefaultConnectionLimitMultiplier;

        private readonly int DefNumberOfConcurrentFileTasks = Environment.ProcessorCount;

        private readonly ILogger _logger = Logger.GetLog<Config>();

        private static Config _instance = new Config();

        public static Config Instance => _instance;

        private Config()
        {
            _fileWatcherInterval = GetVal("FileWatcherInterval", DefFileWatcherInterval);

            _numberOfConcurrentUploads = GetVal("NumberOfConcurrentUploads", DefNumberOfConcurrentFileTasks);
            _numberOfConcurrentDownloads = GetVal("NumberOfConcurrentDownloads", DefNumberOfConcurrentFileTasks);

            _numberOfConcurrentTransfers = GetVal("NumberOfConcurrentTransfers", DefNumberOfConcurrentTransfers, MaxNumberOfConcurrentTransfers);
            _parallelTransferThreadCount = GetVal("ParallelTransferThreadCount", DefParallelTransferThreadCount, MaxParallelTransferThreadCount);
            _useDefNumberOfConcurrentTransfers = ConfigurationManager.AppSettings["NumberOfConcurrentTransfers"]
                .Equals("Default", StringComparison.OrdinalIgnoreCase);
            _useDefParallelTransferThreadCount = ConfigurationManager.AppSettings["ParallelTransferThreadCount"]
                .Equals("Default", StringComparison.OrdinalIgnoreCase);

            _outputLogDir = ConfigurationManager.AppSettings["OutputLogDirectory"];
        }

        private int GetVal(string name, int def = int.MinValue)
        {
            //return !string.IsNullOrEmpty(prop) ? prop : (prop = ConfigurationManager.AppSettings[name]);
            string val = ConfigurationManager.AppSettings[name];
            if ((string.IsNullOrEmpty(val) || val.Equals("Default", StringComparison.OrdinalIgnoreCase)) && def != int.MinValue)
            {
                return def;
            }

            try
            {
                return Int32.Parse(val);
            }
            catch
            {
                _logger.Error("Error parsing {name} configuration value", name);
                throw;
            }
        }

        private int GetVal(string name, int def, int max)
        {
            string val = ConfigurationManager.AppSettings[name];
            if (string.IsNullOrEmpty(val) || val.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                return def;
            }

            if (val.Equals("Max", StringComparison.OrdinalIgnoreCase))
            {
                return max;
            }

            try
            {
                return Int32.Parse(val);
            }
            catch
            {
                _logger.Error("Error parsing {name} configuration value", name);
                throw;
            }
        }

        //public string MediaServicesAccountName => GetStringVal("MediaServicesAccountName", ref _mediaServicesAccountName);
        //public string MediaServicesAccountKey => GetStringVal("MediaServicesAccountKey", ref _mediaServicesAccountKey);
        public string SourceDirectory => _sourceDirectory;

        public string ProcessedDirectory => _processedDirectory;
        public string OutputDirectory => _outputDirectory;
        public string ProcessingDirectory => _processingDirectory;
        public string SourceFilePattern => _searchPattern;
        public string OutputLogDirectory => _outputLogDir;
        public int FileWatcherInterval => _fileWatcherInterval;
        public int NumberOfConcurrentUploads => _numberOfConcurrentUploads;
        public int NumberOfConcurrentDownloads => _numberOfConcurrentDownloads;
        public int NumberOfConcurrentTransfers => _numberOfConcurrentTransfers;
        public bool UseDefaultNumberOfConcurrentTransfers => _useDefNumberOfConcurrentTransfers;
        public int ParallelTransferThreadCount => _parallelTransferThreadCount;
        public bool UseDefaultParallelTransferThreadCount => _useDefParallelTransferThreadCount;
    }
}