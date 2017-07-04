using System;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Windows;
using CredsSetup;


namespace ProtectCreds
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void setConfig_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_appId.Text) || string.IsNullOrWhiteSpace(_key.Text) || string.IsNullOrWhiteSpace(_tenant.Text) ||
                string.IsNullOrWhiteSpace(_userId.Text) || string.IsNullOrWhiteSpace(_password.Password))
            {
                MessageBox.Show("Please enter a value for all fields");
                return;
            }

            try
            {
                using (new Impersonator(_userId.Text, _domain.Text, _password.Password))
                {
                    EncryptConfigSection(_appId.Text, _key.Text, _tenant.Text);
                }
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show($"Error applying encryption for user '{_userId.Text}': {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying encryption: {ex.Message}");
                //throw;
            }

        }


        private void EncryptConfigSection(string appId, string key, string tenant)
        {
            try
            {
                string path = ConfigurationManager.AppSettings["POCPath"];

                Configuration config = ConfigurationManager.OpenExeConfiguration(Path.Combine(path, "MLSandboxPOC.exe"));
                var section = config.AppSettings;
                if (section != null)
                {
                    UpdateConfigSetting(section, "ms1", appId);
                    UpdateConfigSetting(section, "ms2", key);
                    UpdateConfigSetting(section, "ms3", tenant);

                    config.Save(ConfigurationSaveMode.Modified);

                    MessageBox.Show("Config updated");
                }
                else
                {
                    MessageBox.Show("Unable to find <appSettings> configuration section");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error encrypting config: {ex.Message}");
            }
        }

        private static void UpdateConfigSetting(AppSettingsSection section, string key, string val)
        {
            using (var sec = DataProtector.Utils.ToSecureString(val))
            {
                string enc = DataProtector.Utils.EncryptString(sec);
                section.Settings.Remove(key);
                section.Settings.Add(new KeyValueConfigurationElement(key, enc));
            }
        }
    }
}
