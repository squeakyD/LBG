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

            _logger.Information("|File|Total duration (s)|input Asset vulnerable (s)|output Asset vulnerable (s)");
        }

        public void WriteResults(IndexJobData jobData)
        {
            TimeSpan dur = jobData.OutputAssetDeleted - jobData.InputFileUploaded;
            TimeSpan t1 = jobData.InputAssetDeleted - jobData.InputAssetKeyRestored;
            TimeSpan t2 = jobData.OutputAssetDeleted - jobData.OutputAssetCreated;

            //_logger.Information(
            //    "|File {Filename}|Total duration (s) {dur}|input Asset vulnerable (s) {t1}|output Asset vulnerable (s) {t2}"
            //    , Filename, dur.TotalSeconds, t1.TotalSeconds, t2.TotalSeconds);

            // Excel friendly logging output
            _logger.Information(
                $"|{jobData.Filename}|{dur.TotalSeconds}|{t1.TotalSeconds}|{t2.TotalSeconds}");
        }
    }
}