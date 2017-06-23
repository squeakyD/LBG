using System;
using Serilog;

namespace MLSandboxPOC
{
    class ResultsLogger
    {
        private readonly ILogger _logger;
        //private const string Separator = "|";

        private static ResultsLogger _instance;

        public static ResultsLogger Instance => _instance ?? (_instance = new ResultsLogger());

        private ResultsLogger()
        {
            _logger = new LoggerConfiguration()
                .WriteTo.RollingFile(Config.Instance.OutputLogDirectory + @"\MLSandboxPOC-JobResults-{Date}.txt")
                .CreateLogger();

            _logger.Information("|File|File Size|Total duration (s)|Create asset and upload file (s)|Input Asset vulnerable (s)|Output Asset vulnerable (s)");
        }

        public void WriteResults(IndexJobData jobData)
        {
            TimeSpan dur = jobData.OutputAssetDeleted - jobData.InputAssetUploadStart;
            TimeSpan t1 = jobData.InputFileUploaded - jobData.InputAssetUploadStart;
            TimeSpan t2 = jobData.InputAssetDeleted - jobData.InputAssetKeyRestored;
            TimeSpan t3 = jobData.OutputAssetDeleted - jobData.OutputAssetCreated;

            // Excel friendly logging output
            _logger.Information(
                $"|{jobData.Filename}|{jobData.FileSize}|{dur.TotalSeconds}|{t1.TotalSeconds}|{t2.TotalSeconds}|{t3.TotalSeconds}");
        }
    }
}