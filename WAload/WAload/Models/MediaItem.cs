using System;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace WAload.Models
{
    public class MediaItem : INotifyPropertyChanged
    {
        private string _fileName = string.Empty;
        private string _filePath = string.Empty;
        private string _mediaType = string.Empty;
        private long _fileSize;
        private DateTime _timestamp;
        private string _senderName = string.Empty;
        private string _groupId = string.Empty;
        private BitmapImage? _thumbnail;
        private bool _isVideo;
        private bool _isImage;
        private bool _isDocument;
        private string _extension = string.Empty;

        public string FileName
        {
            get => _fileName;
            set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    OnPropertyChanged(nameof(FileName));
                }
            }
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                }
            }
        }

        public string MediaType
        {
            get => _mediaType;
            set
            {
                if (_mediaType != value)
                {
                    _mediaType = value;
                    OnPropertyChanged(nameof(MediaType));
                    UpdateMediaTypeFlags();
                }
            }
        }

        public long FileSize
        {
            get => _fileSize;
            set
            {
                if (_fileSize != value)
                {
                    _fileSize = value;
                    OnPropertyChanged(nameof(FileSize));
                    OnPropertyChanged(nameof(FileSizeFormatted));
                }
            }
        }

        public string FileSizeFormatted
        {
            get
            {
                if (_fileSize < 1024)
                    return $"{_fileSize} B";
                else if (_fileSize < 1024 * 1024)
                    return $"{_fileSize / 1024.0:F1} KB";
                else if (_fileSize < 1024 * 1024 * 1024)
                    return $"{_fileSize / (1024.0 * 1024.0):F1} MB";
                else
                    return $"{_fileSize / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                if (_timestamp != value)
                {
                    _timestamp = value;
                    OnPropertyChanged(nameof(Timestamp));
                    OnPropertyChanged(nameof(TimestampFormatted));
                }
            }
        }

        public string TimestampFormatted => _timestamp.ToString("yyyy-MM-dd HH:mm:ss");

        public string SenderName
        {
            get => _senderName;
            set
            {
                if (_senderName != value)
                {
                    _senderName = value;
                    OnPropertyChanged(nameof(SenderName));
                }
            }
        }

        public string GroupId
        {
            get => _groupId;
            set
            {
                if (_groupId != value)
                {
                    _groupId = value;
                    OnPropertyChanged(nameof(GroupId));
                }
            }
        }

        public BitmapImage? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (_thumbnail != value)
                {
                    _thumbnail = value;
                    OnPropertyChanged(nameof(Thumbnail));
                }
            }
        }

        public bool IsVideo
        {
            get => _isVideo;
            set
            {
                if (_isVideo != value)
                {
                    _isVideo = value;
                    OnPropertyChanged(nameof(IsVideo));
                }
            }
        }

        public bool IsImage
        {
            get => _isImage;
            set
            {
                if (_isImage != value)
                {
                    _isImage = value;
                    OnPropertyChanged(nameof(IsImage));
                }
            }
        }

        public bool IsDocument
        {
            get => _isDocument;
            set
            {
                if (_isDocument != value)
                {
                    _isDocument = value;
                    OnPropertyChanged(nameof(IsDocument));
                }
            }
        }

        public string Extension
        {
            get => _extension;
            set
            {
                if (_extension != value)
                {
                    _extension = value;
                    OnPropertyChanged(nameof(Extension));
                }
            }
        }

        private void UpdateMediaTypeFlags()
        {
            IsVideo = _mediaType.StartsWith("video/");
            IsImage = _mediaType.StartsWith("image/");
            IsDocument = !IsVideo && !IsImage;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 