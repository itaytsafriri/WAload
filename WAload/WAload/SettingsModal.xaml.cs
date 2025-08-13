using System.Windows;
using System.Diagnostics;
using System.IO;
using WAload.Models;
using WAload.Services;

namespace WAload
{
    public partial class SettingsModal : Window
    {
        public AppSettings Settings { get; private set; }
        private readonly VideoProcessingService _videoProcessingService;

        public SettingsModal(AppSettings currentSettings)
        {
            InitializeComponent();
            Settings = new AppSettings
            {
                ScreenshotXTweets = currentSettings.ScreenshotXTweets,
                DownloadFolder = currentSettings.DownloadFolder,
                IsMediaProcessingEnabled = currentSettings.IsMediaProcessingEnabled,
                SaveAsMxfForAvid = currentSettings.SaveAsMxfForAvid,
                DownloadSocialMediaVideos = currentSettings.DownloadSocialMediaVideos,
                FolderSorting = true, // Muli feature - always enabled, hidden from UI
                
                // ch13 features - client-specific features (hidden from normal release - always disabled)
                DatedFolders = false, // currentSettings.DatedFolders,
                FolderFormatSorting = false // currentSettings.FolderFormatSorting
            };

            // Initialize video processing service
            _videoProcessingService = new VideoProcessingService();

            // Bind the toggles to the settings
            ScreenshotXTweetsToggle.IsChecked = Settings.ScreenshotXTweets;
            MediaProcessingToggle.IsChecked = Settings.IsMediaProcessingEnabled;
            SaveAsMxfToggle.IsChecked = Settings.SaveAsMxfForAvid;
            DownloadSocialMediaVideosToggle.IsChecked = Settings.DownloadSocialMediaVideos;
            // FolderSortingToggle.IsChecked = Settings.FolderSorting; // Muli feature - toggle hidden from UI
            
            // ch13 features - bind toggles for client-specific features (hidden for normal release)
            // DatedFoldersToggle.IsChecked = Settings.DatedFolders;
            // FolderFormatSortingToggle.IsChecked = Settings.FolderFormatSorting;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Update settings from UI
            Settings.ScreenshotXTweets = ScreenshotXTweetsToggle.IsChecked ?? false;
            Settings.IsMediaProcessingEnabled = MediaProcessingToggle.IsChecked ?? false;
            Settings.SaveAsMxfForAvid = SaveAsMxfToggle.IsChecked ?? false;
            Settings.DownloadSocialMediaVideos = DownloadSocialMediaVideosToggle.IsChecked ?? false;
            Settings.FolderSorting = true; // Muli feature - always enabled, not controlled by UI
            
            // ch13 features - update settings from toggles (hidden for normal release - features disabled)
            Settings.DatedFolders = false; // DatedFoldersToggle.IsChecked ?? false;
            Settings.FolderFormatSorting = false; // FolderFormatSortingToggle.IsChecked ?? false;
            
            // ch13 features - Create folders if features are enabled and download folder exists
            if (!string.IsNullOrEmpty(Settings.DownloadFolder) && Directory.Exists(Settings.DownloadFolder))
            {
                try
                {
                    var folderStructureService = new FolderStructureService(Settings);
                    
                    if (Settings.DatedFolders)
                    {
                        folderStructureService.EnsureDateFolderExists(Settings.DownloadFolder);
                        System.Diagnostics.Debug.WriteLine("[ch13] Created date folder on settings save");
                    }
                    
                    if (Settings.FolderFormatSorting)
                    {
                        // Create format folders in the base download folder and any existing date folders
                        folderStructureService.EnsureFormatFoldersExist(Settings.DownloadFolder);
                        
                        // If both features are enabled, also create format folders in today's date folder
                        if (Settings.DatedFolders)
                        {
                            var dateFolder = DateTime.Now.ToString("dd-MM-yy");
                            var dateFolderPath = Path.Combine(Settings.DownloadFolder, dateFolder);
                            if (Directory.Exists(dateFolderPath))
                            {
                                folderStructureService.EnsureFormatFoldersExist(dateFolderPath);
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine("[ch13] Created format folders on settings save");
                    }
                    
                    folderStructureService.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ch13] Error creating folders on settings save: {ex.Message}");
                }
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ResetVideoProcessingSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "This will reset all video processing settings to their default values. This action cannot be undone.\n\nAre you sure you want to continue?",
                "Reset Video Processing Settings",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    var success = _videoProcessingService.ResetVideoProcessingSettings();
                    if (success)
                    {
                        System.Windows.MessageBox.Show(
                            "Video processing settings have been reset to default values successfully.",
                            "Settings Reset",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            "Failed to reset video processing settings. Please check the application logs for more details.",
                            "Reset Failed",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"An error occurred while resetting video processing settings: {ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void OpenVideoSettingsFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsPath = _videoProcessingService.GetVideoSettingsPath();
                if (File.Exists(settingsPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = settingsPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "Video settings file not found. The file will be created when you first use video processing features.",
                        "File Not Found",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"An error occurred while opening the video settings file: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
} 