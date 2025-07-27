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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 