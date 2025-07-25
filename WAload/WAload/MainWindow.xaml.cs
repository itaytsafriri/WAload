using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using WAload.Models;
using WAload.Services;
using QRCoder;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Windows.Media.Animation;
using System.Windows.Data;
using System.Text.Json;

namespace WAload
{
    public class InvertedBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly VideoProcessingService _videoProcessingService;
        private readonly ObservableCollection<MediaItem> _mediaItems;
        private readonly ObservableCollection<WhatsGroup> _groups;
        private string _downloadFolder = string.Empty;
        private string _statusMessage = "Ready";
        private bool _isConnected;
        private bool _isMonitoring;
        private bool _isGroupsLoaded;
        private string _loggedInUserName = string.Empty;
        private bool _isLoggingOut = false;
        private FileSystemWatcher? _fileWatcher;
        private System.Threading.Timer? _refreshTimer;
        private bool _refreshPending = false;

        public ObservableCollection<MediaItem> MediaItems => _mediaItems;
        public ObservableCollection<WhatsGroup> Groups => _groups;

        public string DownloadFolder
        {
            get => _downloadFolder;
            set
            {
                if (_downloadFolder != value)
                {
                    _downloadFolder = value;
                    OnPropertyChanged(nameof(DownloadFolder));
                    LoadExistingMedia();
                    SetupFileWatcher();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                }
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(ConnectionStatus));
                    OnPropertyChanged(nameof(IsGroupsLoadedAndNotMonitoring));
                }
            }
        }

        public bool IsMonitoring
        {
            get => _isMonitoring;
            set
            {
                if (_isMonitoring != value)
                {
                    _isMonitoring = value;
                    OnPropertyChanged(nameof(IsMonitoring));
                    OnPropertyChanged(nameof(IsGroupsLoadedAndNotMonitoring));
                }
            }
        }

        public bool IsGroupsLoaded
        {
            get => _isGroupsLoaded;
            set
            {
                if (_isGroupsLoaded != value)
                {
                    _isGroupsLoaded = value;
                    OnPropertyChanged(nameof(IsGroupsLoaded));
                    OnPropertyChanged(nameof(IsGroupsLoadedAndNotMonitoring));
                }
            }
        }

        public bool IsGroupsLoadedAndNotMonitoring => IsGroupsLoaded && !IsMonitoring;

        public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            
            _whatsAppService = new WhatsAppService();
            _videoProcessingService = new VideoProcessingService();
            _mediaItems = new ObservableCollection<MediaItem>();
            _groups = new ObservableCollection<WhatsGroup>();

            DataContext = this;
            
            // Check FFmpeg availability
            if (_videoProcessingService.IsFFmpegAvailable())
            {
                var ffmpegVersion = _videoProcessingService.GetFFmpegVersion();
                System.Diagnostics.Debug.WriteLine($"FFmpeg available: {ffmpegVersion}");
                StatusMessage = $"FFmpeg ready: {ffmpegVersion}";
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Warning: FFmpeg not available - video thumbnails may not work");
                StatusMessage = "Warning: FFmpeg not found - video thumbnails disabled";
            }
            
            // Set default download folder
            DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WAload Downloads");
            
            // Ensure download folder exists
            if (!Directory.Exists(DownloadFolder))
            {
                Directory.CreateDirectory(DownloadFolder);
            }

            SetupEventHandlers();
            LoadExistingMedia();
            SetupFileWatcher();
        }

        private void SetupEventHandlers()
        {
            _whatsAppService.QrCodeReceived += OnQrCodeReceived;
            _whatsAppService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _whatsAppService.UserNameReceived += OnUserNameReceived;
            _whatsAppService.GroupsUpdated += OnGroupsUpdated;
            _whatsAppService.MediaMessageReceived += OnMediaMessageReceived;
            _whatsAppService.MonitoringStatusChanged += OnMonitoringStatusChanged;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusMessage = "Connecting to WhatsApp...";
                ConnectButton.IsEnabled = false;
                
                await _whatsAppService.InitializeAsync();
                
                StatusMessage = "Waiting for QR code...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection failed: {ex.Message}";
                ConnectButton.IsEnabled = true;
                System.Windows.MessageBox.Show($"Failed to connect: {ex.Message}", "Connection Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GetGroupsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusMessage = "Fetching groups...";
                await _whatsAppService.GetGroupsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to get groups: {ex.Message}";
                System.Windows.MessageBox.Show($"Failed to get groups: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MonitorButton_Click(object sender, RoutedEventArgs e)
        {
            if (GroupsComboBox.SelectedValue is string groupId)
            {
                try
                {
                    StatusMessage = "Starting monitoring...";
                    await _whatsAppService.StartMonitoringAsync(groupId);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to start monitoring: {ex.Message}";
                    System.Windows.MessageBox.Show($"Failed to start monitoring: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a group to monitor.", "No Group Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void StopMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusMessage = "Stopping monitoring...";
                await _whatsAppService.StopMonitoringAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to stop monitoring: {ex.Message}";
                System.Windows.MessageBox.Show($"Failed to stop monitoring: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusMessage = "Logging out...";
                await _whatsAppService.LogoutAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Logout failed: {ex.Message}";
                System.Windows.MessageBox.Show($"Logout failed: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            _fileWatcher?.Dispose();
            _refreshTimer?.Dispose();
            base.OnClosed(e);
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Select download folder";
            dialog.SelectedPath = DownloadFolder;
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadFolder = dialog.SelectedPath;
            }
        }





        private void GroupsComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Handle group selection if needed
        }

        private void MediaListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                OpenFile(item);
            }
        }

        private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                OpenFile(item);
            }
        }

        private void OpenFileLocationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                OpenFileLocation(item);
            }
        }

        private void CopyFilePathMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                CopyFilePath(item);
            }
        }

        private void RenameFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                RenameFile(item);
            }
        }

        private void DeleteFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                DeleteFile(item);
            }
        }

        private void CancelQrButton_Click(object sender, RoutedEventArgs e)
        {
            QrCodeOverlay.Visibility = Visibility.Collapsed;
            ConnectButton.IsEnabled = true;
            StatusMessage = "QR code scan cancelled";
            // Stop QR border animation
            var sb = (Storyboard)FindResource("QrBorderGlowAnimation");
            sb.Stop(QrAnimatedBorder);
        }

        private void OnQrCodeReceived(object? sender, string qrCode)
        {
            System.Diagnostics.Debug.WriteLine($"QR code event received in MainWindow: {qrCode.Substring(0, Math.Min(50, qrCode.Length))}...");
            
            Dispatcher.Invoke(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Generating QR code image...");
                    var qrGenerator = new QRCodeGenerator();
                    var qrCodeData = qrGenerator.CreateQrCode(qrCode, QRCodeGenerator.ECCLevel.Q);
                    var qrCodeBitmap = new QRCode(qrCodeData).GetGraphic(20);
                    
                    using var memory = new MemoryStream();
                    qrCodeBitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;
                    
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = memory;
                    bitmapImage.EndInit();
                    
                    QrCodeImage.Source = bitmapImage;
                    QrCodeOverlay.Visibility = Visibility.Visible;
                    StatusMessage = "QR code displayed - scan with your phone";
                    System.Diagnostics.Debug.WriteLine("QR code overlay made visible");

                    // Start QR border animation
                    var sb = (Storyboard)FindResource("QrBorderGlowAnimation");
                    sb.Begin(QrAnimatedBorder, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"QR code generation error: {ex.Message}");
                    StatusMessage = $"Failed to generate QR code: {ex.Message}";
                }
            });
        }

        private async void OnConnectionStatusChanged(object? sender, bool connected)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                IsConnected = connected;
                ConnectButton.IsEnabled = !connected;
                
                if (connected)
                {
                    QrCodeOverlay.Visibility = Visibility.Collapsed;
                    StatusMessage = $"Connected as {_loggedInUserName}";
                    
                    // Automatically start fetching groups when connected
                    try
                    {
                        ShowProgressModal("Loading Groups", "Fetching WhatsApp groups...");
                        await _whatsAppService.GetGroupsAsync();
                    }
                    catch (Exception ex)
                    {
                        HideProgressModal();
                        StatusMessage = $"Failed to load groups: {ex.Message}";
                    }
                }
                else
                {
                    StatusMessage = "Disconnected";
                    IsMonitoring = false;
                    IsGroupsLoaded = false;
                    _groups.Clear();
                }
            });
        }

        private void OnUserNameReceived(object? sender, string userName)
        {
            Dispatcher.Invoke(() =>
            {
                _loggedInUserName = userName;
                if (IsConnected)
                {
                    StatusMessage = $"Connected as {userName}";
                }
            });
        }

        private void OnGroupsUpdated(object? sender, List<WhatsGroup> groups)
        {
            Dispatcher.Invoke(() =>
            {
                _groups.Clear();
                // Sort groups by name to match WhatsApp order (alphabetical)
                var sortedGroups = groups.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
                
                foreach (var group in sortedGroups)
                {
                    _groups.Add(group);
                }
                
                IsGroupsLoaded = groups.Count > 0;
                StatusMessage = $"Loaded {groups.Count} groups";
                HideProgressModal();
                // Auto-open the dropdown to show user they need to select a group
                if (groups.Count > 0)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                    {
                        GroupsComboBox.IsDropDownOpen = true;
                    }));
                }
            });
        }

        private async void OnMediaMessageReceived(object? sender, MediaMessage mediaMessage)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Media message received: {mediaMessage.Type} - {mediaMessage.Filename}");
                    StatusMessage = "Downloading media...";
                    
                    var mediaItem = await DownloadMediaAsync(mediaMessage);
                    if (mediaItem != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Adding media item to UI: {mediaItem.FileName} at path: {mediaItem.FilePath}");
                        _mediaItems.Add(mediaItem);
                        
                        // Create JSON file for the media item
                        CreateMediaJsonFile(mediaItem);
                        
                        StatusMessage = $"Downloaded: {mediaItem.FileName}";
                        
                        // Force UI update
                        OnPropertyChanged(nameof(MediaItems));
                        System.Diagnostics.Debug.WriteLine($"Media items count: {_mediaItems.Count}, File exists: {File.Exists(mediaItem.FilePath)}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Media item is null - download failed");
                        StatusMessage = "Download failed: Could not process media";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in OnMediaMessageReceived: {ex.Message}");
                    StatusMessage = $"Download failed: {ex.Message}";
                }
            });
        }

        private void OnMonitoringStatusChanged(object? sender, bool monitoring)
        {
            Dispatcher.Invoke(() =>
            {
                IsMonitoring = monitoring;
                StatusMessage = monitoring ? "Monitoring active" : "Monitoring stopped";
            });
        }

        private async Task<MediaItem?> DownloadMediaAsync(MediaMessage mediaMessage)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Starting download for: {mediaMessage.Filename} ({mediaMessage.Type})");
                
                // Decode base64 data
                var mediaData = Convert.FromBase64String(mediaMessage.Data);
                System.Diagnostics.Debug.WriteLine($"Decoded data length: {mediaData.Length} bytes");
                
                // Determine file extension
                var extension = GetFileExtension(mediaMessage.Type);
                var fileName = GetSafeFileName(mediaMessage.Filename, extension);
                var filePath = Path.Combine(DownloadFolder, fileName);
                
                // Ensure unique filename
                var counter = 1;
                while (File.Exists(filePath))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var newFileName = $"{nameWithoutExt}_{counter}{extension}";
                    filePath = Path.Combine(DownloadFolder, newFileName);
                    counter++;
                }
                
                // Write file
                await File.WriteAllBytesAsync(filePath, mediaData);
                
                // Create media item
                var mediaItem = new MediaItem
                {
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    MediaType = mediaMessage.Type,
                    FileSize = mediaMessage.Size,
                    Timestamp = mediaMessage.DateTime,
                    SenderName = mediaMessage.SenderName,
                    Extension = extension
                };
                
                // Generate thumbnail
                await GenerateThumbnailAsync(mediaItem);
                
                System.Diagnostics.Debug.WriteLine($"Successfully created media item: {mediaItem.FileName} at {mediaItem.FilePath}");
                return mediaItem;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading media: {ex.Message}");
                return null;
            }
        }

        private string GetFileExtension(string mediaType)
        {
            return mediaType.ToLower() switch
            {
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "video/mp4" => ".mp4",
                "video/avi" => ".avi",
                "video/mov" => ".mov",
                "video/wmv" => ".wmv",
                "audio/mp3" => ".mp3",
                "audio/wav" => ".wav",
                "audio/ogg" => ".ogg",
                "application/pdf" => ".pdf",
                "application/msword" => ".doc",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
                _ => ".bin"
            };
        }

        private string GetSafeFileName(string fileName, string extension)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"media_{DateTime.Now:yyyyMMdd_HHmmss}";
            }
            
            // Remove invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            fileName = invalidChars.Aggregate(fileName, (current, c) => current.Replace(c, '_'));
            
            // Ensure it has the correct extension
            if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                fileName += extension;
            }
            
            return fileName;
        }

        private async Task GenerateThumbnailAsync(MediaItem mediaItem)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Generating thumbnail for {mediaItem.FileName} - IsImage: {mediaItem.IsImage}, IsVideo: {mediaItem.IsVideo}, MediaType: {mediaItem.MediaType}");
                
                if (mediaItem.IsImage)
                {
                    System.Diagnostics.Debug.WriteLine($"Generating image thumbnail for {mediaItem.FileName}");
                    try
                    {
                        // Create bitmap on UI thread to avoid threading issues
                        await Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                // Use absolute URI to ensure proper loading
                                var absolutePath = Path.GetFullPath(mediaItem.FilePath);
                                var uri = new Uri(absolutePath);
                                
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.UriSource = uri;
                                bitmap.DecodePixelWidth = 120;
                                bitmap.DecodePixelHeight = 80;
                                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                                bitmap.EndInit();
                                bitmap.Freeze();
                                
                                mediaItem.Thumbnail = bitmap;
                                System.Diagnostics.Debug.WriteLine($"Set image thumbnail for {mediaItem.FileName} - Thumbnail is null: {mediaItem.Thumbnail == null}, Width: {bitmap.PixelWidth}, Height: {bitmap.PixelHeight}");
                                // Force UI refresh
                                OnPropertyChanged(nameof(MediaItems));
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error loading image thumbnail for {mediaItem.FileName}: {ex.Message}");
                                mediaItem.Thumbnail = null;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in image thumbnail generation for {mediaItem.FileName}: {ex.Message}");
                    }
                }
                else if (mediaItem.IsVideo)
                {
                    System.Diagnostics.Debug.WriteLine($"Generating video thumbnail for {mediaItem.FileName}");
                    
                    // Check if FFmpeg is available
                    if (!_videoProcessingService.IsFFmpegAvailable())
                    {
                        System.Diagnostics.Debug.WriteLine($"FFmpeg not available - skipping video thumbnail for {mediaItem.FileName}");
                        await Dispatcher.InvokeAsync(() =>
                        {
                            mediaItem.Thumbnail = null;
                        });
                        return;
                    }
                    
                    try
                    {
                        var thumbnailPath = Path.Combine(DownloadFolder, $"thumb_{Path.GetFileNameWithoutExtension(mediaItem.FileName)}.jpg");
                        
                        // Check if thumbnail already exists
                        if (File.Exists(thumbnailPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"Using existing thumbnail for {mediaItem.FileName}");
                            await Dispatcher.InvokeAsync(() =>
                            {
                                try
                                {
                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.UriSource = new Uri(thumbnailPath);
                                    bitmap.DecodePixelWidth = 120;
                                    bitmap.DecodePixelHeight = 80;
                                    bitmap.EndInit();
                                    bitmap.Freeze();
                                    
                                    mediaItem.Thumbnail = bitmap;
                                    System.Diagnostics.Debug.WriteLine($"Set existing video thumbnail for {mediaItem.FileName} - Thumbnail is null: {mediaItem.Thumbnail == null}");
                                    // Force UI refresh
                                    OnPropertyChanged(nameof(MediaItems));
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error loading existing video thumbnail for {mediaItem.FileName}: {ex.Message}");
                                    mediaItem.Thumbnail = null;
                                }
                            });
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Generating new thumbnail for {mediaItem.FileName} using FFmpeg");
                            var duration = await _videoProcessingService.GetVideoDurationAsync(mediaItem.FilePath);
                            var thumbnailPosition = TimeSpan.FromSeconds(duration.TotalSeconds / 2);
                            var result = await _videoProcessingService.GenerateThumbnailAsync(mediaItem.FilePath, thumbnailPath, thumbnailPosition);
                            if (result != null)
                            {
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    try
                                    {
                                        var bitmap = new BitmapImage();
                                        bitmap.BeginInit();
                                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                        bitmap.UriSource = new Uri(thumbnailPath);
                                        bitmap.DecodePixelWidth = 120;
                                        bitmap.DecodePixelHeight = 80;
                                        bitmap.EndInit();
                                        bitmap.Freeze();
                                        
                                        mediaItem.Thumbnail = bitmap;
                                        System.Diagnostics.Debug.WriteLine($"Set new video thumbnail for {mediaItem.FileName} - Thumbnail is null: {mediaItem.Thumbnail == null}");
                                        // Force UI refresh
                                        OnPropertyChanged(nameof(MediaItems));
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error loading new video thumbnail for {mediaItem.FileName}: {ex.Message}");
                                        mediaItem.Thumbnail = null;
                                    }
                                });
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to generate video thumbnail for {mediaItem.FileName}");
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    mediaItem.Thumbnail = null;
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error generating video thumbnail for {mediaItem.FileName}: {ex.Message}");
                        await Dispatcher.InvokeAsync(() =>
                        {
                            mediaItem.Thumbnail = null;
                        });
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No thumbnail for {mediaItem.FileName} - not image or video");
                    await Dispatcher.InvokeAsync(() =>
                    {
                        mediaItem.Thumbnail = null;
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating thumbnail for {mediaItem.FileName}: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    mediaItem.Thumbnail = null;
                });
            }
        }

        private void SetupFileWatcher()
        {
            try
            {
                // Dispose existing watcher if any
                _fileWatcher?.Dispose();
                
                if (string.IsNullOrEmpty(DownloadFolder) || !Directory.Exists(DownloadFolder))
                {
                    System.Diagnostics.Debug.WriteLine("Cannot setup file watcher - download folder is null/empty or doesn't exist");
                    return;
                }
                
                _fileWatcher = new FileSystemWatcher(DownloadFolder)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    Filter = "*.*",
                    EnableRaisingEvents = true
                };
                
                _fileWatcher.Created += OnFileCreated;
                _fileWatcher.Deleted += OnFileDeleted;
                _fileWatcher.Renamed += OnFileRenamed;
                _fileWatcher.Changed += OnFileChanged;
                
                System.Diagnostics.Debug.WriteLine($"File watcher setup for folder: {DownloadFolder}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up file watcher: {ex.Message}");
            }
        }
        
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"File created: {e.Name}");
            
            // Skip JSON files - they should not trigger UI updates
            if (Path.GetExtension(e.Name).ToLower() == ".json")
            {
                System.Diagnostics.Debug.WriteLine($"Skipping JSON file creation event: {e.Name}");
                return;
            }
            
            ScheduleRefresh();
        }
        
        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"File deleted: {e.Name}");
            ScheduleRefresh();
        }
        
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"File renamed: {e.OldName} -> {e.Name}");
            ScheduleRefresh();
        }
        
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"File changed: {e.Name}");
            ScheduleRefresh();
        }
        
        private void ScheduleRefresh()
        {
            if (_refreshPending) return;
            
            _refreshPending = true;
            _refreshTimer?.Dispose();
            _refreshTimer = new System.Threading.Timer(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    LoadExistingMedia();
                    _refreshPending = false;
                });
            }, null, 500, Timeout.Infinite); // 500ms delay
        }
        
        private void LoadExistingMedia()
        {
            System.Diagnostics.Debug.WriteLine($"LoadExistingMedia called - DownloadFolder: {DownloadFolder}");
            if (string.IsNullOrEmpty(DownloadFolder) || !Directory.Exists(DownloadFolder))
            {
                System.Diagnostics.Debug.WriteLine($"Download folder is null/empty or doesn't exist: {DownloadFolder}");
                return;
            }

            try
            {
                _mediaItems.Clear();
                System.Diagnostics.Debug.WriteLine($"Cleared media items collection");
                
                // Clean up orphaned JSON files first
                CleanupOrphanedJsonFiles();
                
                var files = Directory.GetFiles(DownloadFolder, "*.*", SearchOption.TopDirectoryOnly);
                System.Diagnostics.Debug.WriteLine($"Found {files.Length} files in download folder");
                
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    System.Diagnostics.Debug.WriteLine($"Processing file: {fileInfo.Name} ({fileInfo.Length} bytes)");
                    
                    // Skip JSON files - they should not appear in the table
                    if (fileInfo.Extension.ToLower() == ".json")
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping JSON file: {fileInfo.Name}");
                        continue;
                    }
                    
                    var mediaItem = new MediaItem
                    {
                        FileName = fileInfo.Name,
                        FilePath = file,
                        FileSize = fileInfo.Length,
                        Timestamp = fileInfo.CreationTime,
                        Extension = fileInfo.Extension,
                        SenderName = "Locally Added" // Set sender for local files
                    };
                    
                    // Determine media type from extension
                    mediaItem.MediaType = GetMediaTypeFromExtension(fileInfo.Extension);
                    System.Diagnostics.Debug.WriteLine($"Set MediaType for {mediaItem.FileName}: {mediaItem.MediaType} (IsImage: {mediaItem.IsImage}, IsVideo: {mediaItem.IsVideo})");
                    
                    // Try to load metadata from JSON file if it exists
                    LoadMediaMetadataFromJson(mediaItem);
                    
                    _mediaItems.Add(mediaItem);
                    System.Diagnostics.Debug.WriteLine($"Added media item: {mediaItem.FileName} (Type: {mediaItem.MediaType})");
                    
                    // Create JSON file for the media item (only if it doesn't exist)
                    if (!File.Exists(Path.Combine(DownloadFolder, Path.GetFileNameWithoutExtension(mediaItem.FileName) + ".json")))
                    {
                        CreateMediaJsonFile(mediaItem);
                    }
                    
                    // Generate thumbnail for existing files
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await GenerateThumbnailAsync(mediaItem);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error generating thumbnail for {mediaItem.FileName}: {ex.Message}");
                        }
                    });
                }
                
                StatusMessage = $"Loaded {_mediaItems.Count} existing files";
                System.Diagnostics.Debug.WriteLine($"LoadExistingMedia completed - {_mediaItems.Count} items loaded");
                
                // Regenerate missing thumbnails after loading all files
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RegenerateMissingThumbnailsAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in thumbnail regeneration: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadExistingMedia: {ex.Message}");
                StatusMessage = $"Error loading existing media: {ex.Message}";
            }
        }

        private string GetMediaTypeFromExtension(string extension)
        {
            return extension.ToLower() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".mp4" => "video/mp4",
                ".avi" => "video/avi",
                ".mov" => "video/mov",
                ".wmv" => "video/wmv",
                ".mp3" => "audio/mp3",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }

        private void CreateMediaJsonFile(MediaItem mediaItem)
        {
            try
            {
                var jsonFileName = Path.GetFileNameWithoutExtension(mediaItem.FileName) + ".json";
                var jsonFilePath = Path.Combine(DownloadFolder, jsonFileName);
                
                var mediaInfo = new
                {
                    FileName = mediaItem.FileName,
                    FilePath = mediaItem.FilePath,
                    MediaType = mediaItem.MediaType,
                    FileSize = mediaItem.FileSize,
                    Timestamp = mediaItem.Timestamp,
                    SenderName = mediaItem.SenderName,
                    GroupId = mediaItem.GroupId,
                    Extension = mediaItem.Extension,
                    IsVideo = mediaItem.IsVideo,
                    IsImage = mediaItem.IsImage,
                    IsDocument = mediaItem.IsDocument,
                    CreatedAt = DateTime.Now
                };
                
                var jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(mediaInfo, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(jsonFilePath, jsonContent);
                
                System.Diagnostics.Debug.WriteLine($"Created JSON file: {jsonFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating JSON file for {mediaItem.FileName}: {ex.Message}");
            }
        }

        private void LoadMediaMetadataFromJson(MediaItem mediaItem)
        {
            try
            {
                var jsonFileName = Path.GetFileNameWithoutExtension(mediaItem.FileName) + ".json";
                var jsonFilePath = Path.Combine(DownloadFolder, jsonFileName);
                
                if (File.Exists(jsonFilePath))
                {
                    var jsonContent = File.ReadAllText(jsonFilePath);
                    var mediaInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonContent);
                    
                    if (mediaInfo != null)
                    {
                        // Update media item with metadata from JSON
                        if (mediaInfo.SenderName != null && !string.IsNullOrEmpty(mediaInfo.SenderName.ToString()))
                        {
                            mediaItem.SenderName = mediaInfo.SenderName.ToString();
                        }
                        
                        if (mediaInfo.GroupId != null && !string.IsNullOrEmpty(mediaInfo.GroupId.ToString()))
                        {
                            mediaItem.GroupId = mediaInfo.GroupId.ToString();
                        }
                        
                        if (mediaInfo.MediaType != null && !string.IsNullOrEmpty(mediaInfo.MediaType.ToString()))
                        {
                            mediaItem.MediaType = mediaInfo.MediaType.ToString();
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Loaded metadata from JSON for {mediaItem.FileName}: Sender={mediaItem.SenderName}, Group={mediaItem.GroupId}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading metadata from JSON for {mediaItem.FileName}: {ex.Message}");
            }
        }

        private void CleanupOrphanedJsonFiles()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Cleaning up orphaned JSON files...");
                
                var jsonFiles = Directory.GetFiles(DownloadFolder, "*.json", SearchOption.TopDirectoryOnly);
                var mediaFiles = Directory.GetFiles(DownloadFolder, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.EndsWith(".json"))
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .ToHashSet();
                
                foreach (var jsonFile in jsonFiles)
                {
                    var jsonFileName = Path.GetFileNameWithoutExtension(jsonFile);
                    
                    // If no corresponding media file exists, delete the JSON file
                    if (!mediaFiles.Contains(jsonFileName))
                    {
                        System.Diagnostics.Debug.WriteLine($"Deleting orphaned JSON file: {Path.GetFileName(jsonFile)}");
                        File.Delete(jsonFile);
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("Orphaned JSON files cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cleaning up orphaned JSON files: {ex.Message}");
            }
        }

        private async Task RegenerateMissingThumbnailsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Scanning for files without thumbnails...");
                var regeneratedCount = 0;
                
                // Create a copy of the collection to avoid modification during enumeration
                var mediaItemsCopy = _mediaItems.ToList();
                
                foreach (var mediaItem in mediaItemsCopy)
                {
                    // Check if thumbnail is null or if it's a video without a thumbnail file
                    if (mediaItem.Thumbnail == null)
                    {
                        if (mediaItem.IsVideo)
                        {
                            var thumbnailPath = Path.Combine(DownloadFolder, $"thumb_{Path.GetFileNameWithoutExtension(mediaItem.FileName)}.jpg");
                            if (!File.Exists(thumbnailPath))
                            {
                                System.Diagnostics.Debug.WriteLine($"Regenerating missing thumbnail for video: {mediaItem.FileName}");
                                await GenerateThumbnailAsync(mediaItem);
                                regeneratedCount++;
                            }
                        }
                        else if (mediaItem.IsImage)
                        {
                            System.Diagnostics.Debug.WriteLine($"Regenerating missing thumbnail for image: {mediaItem.FileName}");
                            await GenerateThumbnailAsync(mediaItem);
                            regeneratedCount++;
                        }
                    }
                }
                
                // Force UI refresh
                await Dispatcher.InvokeAsync(() =>
                {
                    OnPropertyChanged(nameof(MediaItems));
                    System.Diagnostics.Debug.WriteLine($"Regenerated {regeneratedCount} thumbnails and refreshed UI");
                });
                
                System.Diagnostics.Debug.WriteLine("Missing thumbnails regeneration completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error regenerating missing thumbnails: {ex.Message}");
            }
        }

        // Debug method to force regenerate all thumbnails
        private async Task ForceRegenerateAllThumbnailsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Force regenerating ALL thumbnails...");
                var regeneratedCount = 0;
                
                // Log current state
                LogMediaItemsState("Before regeneration");
                
                // Create a copy of the collection to avoid modification during enumeration
                var mediaItemsCopy = _mediaItems.ToList();
                
                foreach (var mediaItem in mediaItemsCopy)
                {
                    System.Diagnostics.Debug.WriteLine($"Force regenerating thumbnail for: {mediaItem.FileName} (IsImage: {mediaItem.IsImage}, IsVideo: {mediaItem.IsVideo})");
                    await GenerateThumbnailAsync(mediaItem);
                    regeneratedCount++;
                }
                
                // Force UI refresh
                await Dispatcher.InvokeAsync(() =>
                {
                    OnPropertyChanged(nameof(MediaItems));
                    System.Diagnostics.Debug.WriteLine($"Force regenerated {regeneratedCount} thumbnails and refreshed UI");
                });
                
                // Log final state
                LogMediaItemsState("After regeneration");
                
                System.Diagnostics.Debug.WriteLine("Force thumbnail regeneration completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error force regenerating thumbnails: {ex.Message}");
            }
        }

        private void LogMediaItemsState(string stage)
        {
            System.Diagnostics.Debug.WriteLine($"=== {stage} ===");
            foreach (var item in _mediaItems)
            {
                System.Diagnostics.Debug.WriteLine($"  {item.FileName}: IsImage={item.IsImage}, IsVideo={item.IsVideo}, Thumbnail={item.Thumbnail != null}, MediaType={item.MediaType}");
            }
            System.Diagnostics.Debug.WriteLine("==================");
        }

        private void OpenFile(MediaItem item)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open file: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFileLocation(MediaItem item)
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open file location: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyFilePath(MediaItem item)
        {
            try
            {
                System.Windows.Clipboard.SetText(item.FilePath);
                StatusMessage = "File path copied to clipboard";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to copy file path: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenameFile(MediaItem item)
        {
            // Implement file renaming functionality
            StatusMessage = "File renaming not implemented yet";
        }

        private void DeleteFile(MediaItem item)
        {
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete '{item.FileName}'?", 
                "Confirm Delete", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    File.Delete(item.FilePath);
                    
                    // Also delete the corresponding JSON file
                    var jsonFileName = Path.GetFileNameWithoutExtension(item.FileName) + ".json";
                    var jsonFilePath = Path.Combine(DownloadFolder, jsonFileName);
                    if (File.Exists(jsonFilePath))
                    {
                        File.Delete(jsonFilePath);
                        System.Diagnostics.Debug.WriteLine($"Deleted JSON file: {jsonFilePath}");
                    }
                    
                    _mediaItems.Remove(item);
                    StatusMessage = $"Deleted: {item.FileName}";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to delete file: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ShowProgressModal(string title, string message)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressModal.Visibility = Visibility.Visible;
                ProgressTitle.Text = title;
                ProgressMessage.Text = message;
            });
        }

        private void HideProgressModal()
        {
            Dispatcher.Invoke(() =>
            {
                ProgressModal.Visibility = Visibility.Collapsed;
            });
        }

        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isLoggingOut)
                return;
            e.Cancel = true;
            _isLoggingOut = true;
            ShowProgressModal("Logging out", "Logging out, please wait...");
            try
            {
                var logoutTask = _whatsAppService.LogoutAsync();
                var completedTask = await Task.WhenAny(logoutTask, Task.Delay(10000));
                // If logout completes or timeout, proceed
            }
            catch { /* Ignore errors during shutdown */ }
            HideProgressModal();
            _isLoggingOut = false;
            System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}