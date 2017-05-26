using Serilog;

namespace MLSandboxPOC
{
    public static class Logger
    {
        static Logger()
        {
            // Create the global logger once only
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .Enrich.WithThreadId()
                .CreateLogger();
        }

        public static ILogger GetLog<T>() where T : class
        {
            // Per class logger
            return Log.Logger.ForContext<T>();
        }
    }
}
