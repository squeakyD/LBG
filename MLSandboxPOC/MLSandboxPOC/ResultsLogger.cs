using System;
using Serilog;

namespace MLSandboxPOC
{
    class ResultsLogger
    {
        private readonly ILogger _logger;
        //private const string Separator = "|";

        private static ResultsLogger _instance = new ResultsLogger();

        public static ResultsLogger Instance => _instance;

        private ResultsLogger()
        {
            _logger = new LoggerConfiguration()
                .WriteTo.RollingFile(Config.Instance.OutputLogDirectory + @"\MLSandboxPOC-JobResults-{Date}.txt")
                .CreateLogger();

            _logger.Verbose("|File|Total duration (s)|input Asset vulnerable (s)|output Asset vulnerable (s)");
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
                "|{Filename}|{dur}|{t1}|{t2}"
                , jobData.Filename, dur.TotalSeconds, t1.TotalSeconds, t2.TotalSeconds);
        }       
    }
}