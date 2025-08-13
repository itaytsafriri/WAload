using System.ComponentModel;

namespace WAload.Models
{
    public class AppSettings : INotifyPropertyChanged
    {
        private bool _screenshotXTweets = false;
        private string _downloadFolder = string.Empty;
        private bool _isMediaProcessingEnabled = false;
        private bool _saveAsMxfForAvid = false;
        private bool _downloadSocialMediaVideos = false;
        private bool _folderSorting = true; // Muli feature - folder sorting by name (enabled by default, hidden from UI)
        
        // ch13 features - Dated Folders and Folder Format Sorting (client-specific features) - DISABLED BY DEFAULT
        private bool _datedFolders = false; // ch13 feature - create date-based folders (hidden for normal release)
        private bool _folderFormatSorting = false; // ch13 feature - sort files by format into subfolders (hidden for normal release)

        public bool ScreenshotXTweets
        {
            get => _screenshotXTweets;
            set
            {
                if (_screenshotXTweets != value)
                {
                    _screenshotXTweets = value;
                    OnPropertyChanged(nameof(ScreenshotXTweets));
                }
            }
        }

        public string DownloadFolder
        {
            get => _downloadFolder;
            set
            {
                if (_downloadFolder != value)
                {
                    _downloadFolder = value;
                    OnPropertyChanged(nameof(DownloadFolder));
                }
            }
        }

        public bool IsMediaProcessingEnabled
        {
            get => _isMediaProcessingEnabled;
            set
            {
                if (_isMediaProcessingEnabled != value)
                {
                    _isMediaProcessingEnabled = value;
                    OnPropertyChanged(nameof(IsMediaProcessingEnabled));
                }
            }
        }

        public bool SaveAsMxfForAvid
        {
            get => _saveAsMxfForAvid;
            set
            {
                if (_saveAsMxfForAvid != value)
                {
                    _saveAsMxfForAvid = value;
                    OnPropertyChanged(nameof(SaveAsMxfForAvid));
                }
            }
        }

        public bool DownloadSocialMediaVideos
        {
            get => _downloadSocialMediaVideos;
            set
            {
                if (_downloadSocialMediaVideos != value)
                {
                    _downloadSocialMediaVideos = value;
                    OnPropertyChanged(nameof(DownloadSocialMediaVideos));
                }
            }
        }

        // Muli feature - folder sorting by name
        public bool FolderSorting
        {
            get => _folderSorting;
            set
            {
                if (_folderSorting != value)
                {
                    _folderSorting = value;
                    OnPropertyChanged(nameof(FolderSorting));
                }
            }
        }

        // ch13 features - Dated Folders and Folder Format Sorting (client-specific features)
        public bool DatedFolders
        {
            get => _datedFolders;
            set
            {
                if (_datedFolders != value)
                {
                    _datedFolders = value;
                    OnPropertyChanged(nameof(DatedFolders));
                }
            }
        }

        public bool FolderFormatSorting
        {
            get => _folderFormatSorting;
            set
            {
                if (_folderFormatSorting != value)
                {
                    _folderFormatSorting = value;
                    OnPropertyChanged(nameof(FolderFormatSorting));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 