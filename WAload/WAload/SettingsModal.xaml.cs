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
                ScreenshotXTweets = currentSettings.ScreenshotXTweets,
                DownloadFolder = currentSettings.DownloadFolder,
                IsMediaProcessingEnabled = currentSettings.IsMediaProcessingEnabled,
                SaveAsMxfForAvid = currentSettings.SaveAsMxfForAvid,
                DownloadSocialMediaVideos = currentSettings.DownloadSocialMediaVideos
            };

            // Bind the toggles to the settings
            ScreenshotXTweetsToggle.IsChecked = Settings.ScreenshotXTweets;
            MediaProcessingToggle.IsChecked = Settings.IsMediaProcessingEnabled;
            SaveAsMxfToggle.IsChecked = Settings.SaveAsMxfForAvid;
            DownloadSocialMediaVideosToggle.IsChecked = Settings.DownloadSocialMediaVideos;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Update settings from UI
            Settings.ScreenshotXTweets = ScreenshotXTweetsToggle.IsChecked ?? false;
            Settings.IsMediaProcessingEnabled = MediaProcessingToggle.IsChecked ?? false;
            Settings.SaveAsMxfForAvid = SaveAsMxfToggle.IsChecked ?? false;
            Settings.DownloadSocialMediaVideos = DownloadSocialMediaVideosToggle.IsChecked ?? false;

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