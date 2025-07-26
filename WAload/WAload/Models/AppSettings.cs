using System.ComponentModel;

namespace WAload.Models
{
    public class AppSettings : INotifyPropertyChanged
    {
        private bool _downloadXTweets = false;
        private string _downloadFolder = string.Empty;
        private bool _isMediaProcessingEnabled = false;

        public bool DownloadXTweets
        {
            get => _downloadXTweets;
            set
            {
                if (_downloadXTweets != value)
                {
                    _downloadXTweets = value;
                    OnPropertyChanged(nameof(DownloadXTweets));
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 