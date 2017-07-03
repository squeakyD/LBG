using System.ComponentModel;
using System.Configuration.Install;
using Serilog;

namespace MLSandboxPOC
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        private readonly ILogger _logger = Logger.GetLog<ProjectInstaller>();

        public ProjectInstaller()
        {
            InitializeComponent();

            serviceInstaller1.AfterInstall += ServiceInstaller1_AfterInstall;
            serviceInstaller1.AfterUninstall += ServiceInstaller1_AfterUninstall;
            serviceInstaller1.AfterRollback += ServiceInstaller1_AfterRollback;
        }

        private void ServiceInstaller1_AfterRollback(object sender, InstallEventArgs e)
        {
            _logger.Information("Service installation: Rollback");
        }

        private void ServiceInstaller1_AfterUninstall(object sender, InstallEventArgs e)
        {
            _logger.Information("Service installation: Uninstalled");
        }

        private void ServiceInstaller1_AfterInstall(object sender, InstallEventArgs e)
        {
            _logger.Information("Service installation: Installed");
        }
    }
}
