using System.Windows;
using WAload.Models;

namespace WAload
{
    public partial class SettingsModal : Window
    {
        public AppSettings Settings { get; private set; }

        public SettingsModal(AppSettings currentSettings)
        {
            InitializeComponent();
            Settings = new AppSettings
            {
                DownloadXTweets = currentSettings.DownloadXTweets,
                DownloadFolder = currentSettings.DownloadFolder,
                IsMediaProcessingEnabled = currentSettings.IsMediaProcessingEnabled
            };

            // Bind the toggles to the settings
            DownloadXTweetsToggle.IsChecked = Settings.DownloadXTweets;
            MediaProcessingToggle.IsChecked = Settings.IsMediaProcessingEnabled;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Update settings from UI
            Settings.DownloadXTweets = DownloadXTweetsToggle.IsChecked ?? false;
            Settings.IsMediaProcessingEnabled = MediaProcessingToggle.IsChecked ?? false;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 