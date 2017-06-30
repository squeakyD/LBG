using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace MLSandboxPOC
{
    partial class MLSBService : ServiceBase
    {
        private readonly ILogger _logger = Logger.GetLog<MLSBService>();

        public MLSBService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _logger.Information("Starting service");

            // Start pipeline asynchronously to prevent risk of service start timeout.
            Task.Run(() => ProcessRunner.Instance.Run());
        }

        protected override void OnStop()
        {
            _logger.Warning("Stopping service");
            ProcessRunner.Instance.Shutdown();
        }
    }
}
