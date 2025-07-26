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
using System.Threading;

namespace WAload
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly VideoProcessingService _videoProcessingService;
        private readonly XTweetScreenshotService _xTweetScreenshotService;
        private readonly SettingsService _settingsService;
        private AppSettings _appSettings;
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
        private bool _isMediaProcessingEnabled = false;
        private bool _isProcessingMedia = false;
        private string _processingFileName = string.Empty;
        private double _processingProgress = 0.0;
        private string _processingProgressText = string.Empty;
        private System.Threading.Timer? _processingTimeoutTimer;
        private CancellationTokenSource? _processingCancellationTokenSource;
        private readonly string _tempProcessingDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WAloadTemp");
        private bool _isLicenseValid = false;
        private readonly HashSet<string> _processedMediaIds = new HashSet<string>();
        
        public ObservableCollection<MediaItem> MediaItems => _mediaItems;
        public ObservableCollection<WhatsGroup> Groups => _groups;

        public string DownloadFolder
        {
            get => _downloadFolder;
            set
            {
                if (_downloadFolder != value)
                {
                    var oldFolder = _downloadFolder;
                    _downloadFolder = value;
                    OnPropertyChanged(nameof(DownloadFolder));
                    
                    // Update status message to inform user of folder change
                    if (!string.IsNullOrEmpty(oldFolder) && !string.IsNullOrEmpty(value))
                    {
                        StatusMessage = $"Download folder changed to: {Path.GetFileName(value)}";
                        System.Diagnostics.Debug.WriteLine($"Download folder changed from {oldFolder} to {value}");
                    }
                    
                    // Save to settings
                    if (_appSettings != null)
                    {
                        _appSettings.DownloadFolder = value;
                        _settingsService?.SaveSettings(_appSettings);
                    }
                    
                    // Update X tweet service download folder
                    _xTweetScreenshotService?.SetDownloadFolder(value);
                    
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
                    OnPropertyChanged(nameof(ConnectButtonText));
                    OnPropertyChanged(nameof(ConnectButtonStyle));
                    OnPropertyChanged(nameof(IsMonitorButtonEnabled));
                    OnPropertyChanged(nameof(IsGroupSelectionEnabled));
                    OnPropertyChanged(nameof(IsConnectButtonEnabled));
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
                    OnPropertyChanged(nameof(MonitorButtonText));
                    OnPropertyChanged(nameof(MonitorButtonStyle));
                    OnPropertyChanged(nameof(IsGroupSelectionEnabled));
                    OnPropertyChanged(nameof(IsConnectButtonEnabled));
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
                    OnPropertyChanged(nameof(IsMonitorButtonEnabled));
                }
            }
        }

        public bool IsGroupsLoadedAndNotMonitoring => IsGroupsLoaded && !IsMonitoring;

        public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";

        public bool IsMediaProcessingEnabled
        {
            get => _isMediaProcessingEnabled;
            set
            {
                if (_isMediaProcessingEnabled != value)
                {
                    _isMediaProcessingEnabled = value;
                    OnPropertyChanged(nameof(IsMediaProcessingEnabled));
                    
                    // Save to settings
                    if (_appSettings != null)
                    {
                        _appSettings.IsMediaProcessingEnabled = value;
                        _settingsService?.SaveSettings(_appSettings);
                    }
                }
            }
        }

        public bool IsProcessingMedia
        {
            get => _isProcessingMedia;
            set
            {
                if (_isProcessingMedia != value)
                {
                    _isProcessingMedia = value;
                    OnPropertyChanged(nameof(IsProcessingMedia));
                }
            }
        }

        public string ProcessingFileName
        {
            get => _processingFileName;
            set
            {
                if (_processingFileName != value)
                {
                    _processingFileName = value;
                    OnPropertyChanged(nameof(ProcessingFileName));
                }
            }
        }

        public double ProcessingProgress
        {
            get => _processingProgress;
            set
            {
                if (_processingProgress != value)
                {
                    _processingProgress = value;
                    OnPropertyChanged(nameof(ProcessingProgress));
                }
            }
        }

        public string ProcessingProgressText
        {
            get => _processingProgressText;
            set
            {
                if (_processingProgressText != value)
                {
                    _processingProgressText = value;
                    OnPropertyChanged(nameof(ProcessingProgressText));
                }
            }
        }

        // Dual-purpose button properties
        public string ConnectButtonText => IsConnected ? "Logout" : "Connect";
        public Style ConnectButtonStyle => IsConnected ? (FindResource("RedButton") as Style)! : (FindResource("SuccessButton") as Style)!;
        public bool IsConnectButtonEnabled => !IsConnected || !IsMonitoring; // Enabled when disconnected or when connected but not monitoring

        public string MonitorButtonText => IsMonitoring ? "Stop Monitoring" : "Start Monitoring";
        public Style MonitorButtonStyle => IsMonitoring ? (FindResource("RedButton") as Style)! : (FindResource("SuccessButton") as Style)!;
        public bool IsMonitorButtonEnabled => IsConnected && IsGroupsLoaded;

        public bool IsGroupSelectionEnabled => IsConnected && !IsMonitoring;

        public bool IsLicenseValid
        {
            get => _isLicenseValid;
            set
            {
                if (_isLicenseValid != value)
                {
                    _isLicenseValid = value;
                    OnPropertyChanged(nameof(IsLicenseValid));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            
            _whatsAppService = new WhatsAppService();
            _videoProcessingService = new VideoProcessingService();
            _xTweetScreenshotService = new XTweetScreenshotService();
            _settingsService = new SettingsService();
            _appSettings = _settingsService.LoadSettings();
            
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
            
            // Set download folder from settings or default
            if (!string.IsNullOrEmpty(_appSettings.DownloadFolder) && Directory.Exists(_appSettings.DownloadFolder))
            {
                DownloadFolder = _appSettings.DownloadFolder;
            }
            else
            {
                DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WAload Downloads");
                if (!Directory.Exists(DownloadFolder))
                {
                    Directory.CreateDirectory(DownloadFolder);
                }
                _appSettings.DownloadFolder = DownloadFolder;
            }

            // Set media processing from settings
            IsMediaProcessingEnabled = _appSettings.IsMediaProcessingEnabled;
            
            // Set download folder in X tweet service
            _xTweetScreenshotService.SetDownloadFolder(DownloadFolder);

            SetupEventHandlers();
            LoadExistingMedia();
            SetupFileWatcher();
            
            // Check license on startup
            CheckLicense();

            // In MainWindow constructor or OnLoaded, hook up the toggle and animation
            Loaded += (s, e) =>
            {
                // Start Connect button flash animation with delay to ensure everything is loaded
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        StartConnectButtonFlash();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to start connect button flash: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                
                var border = MediaProcLabelBorder;
                var toggle = MediaProcessingToggle;
                var flashAnim = new ColorAnimation
                {
                    From = Colors.Transparent,
                    To = Colors.DeepSkyBlue,
                    Duration = TimeSpan.FromMilliseconds(500),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                var borderBrush = new SolidColorBrush(Colors.Transparent);
                border.BorderBrush = borderBrush;
                border.BorderThickness = new Thickness(3);
                toggle.Checked += (s2, e2) =>
                {
                    borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, flashAnim);
                };
                toggle.Unchecked += (s2, e2) =>
                {
                    borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                    borderBrush.Color = Colors.Transparent;
                };
                // If already checked on load
                if (toggle.IsChecked == true)
                {
                    borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, flashAnim);
                }
            };
        }

        private void SetupEventHandlers()
        {
            _whatsAppService.QrCodeReceived += OnQrCodeReceived;
            _whatsAppService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _whatsAppService.UserNameReceived += OnUserNameReceived;
            _whatsAppService.GroupsUpdated += OnGroupsUpdated;
            _whatsAppService.MediaMessageReceived += OnMediaMessageReceived;
            _whatsAppService.TextMessageReceived += OnTextMessageReceived;
            _whatsAppService.MonitoringStatusChanged += OnMonitoringStatusChanged;
        }

        private void CheckLicense()
        {
            try
            {
                var licenseService = new Services.LicenseService();
                var result = licenseService.ValidateLicense();
                
                if (!result.IsValid)
                {
                    IsLicenseValid = false;
                    // Show license modal
                    var licenseModal = new LicenseModal();
                    licenseModal.LicenseValidated += (s, e) => IsLicenseValid = true;
                    licenseModal.ShowDialog();
                    
                    // If license is still not valid after modal, exit
                    if (!licenseModal.IsLicenseValid)
                    {
                        System.Windows.Application.Current.Shutdown();
                    }
                    else
                    {
                        IsLicenseValid = true;
                    }
                }
                else
                {
                    IsLicenseValid = true;
                    System.Diagnostics.Debug.WriteLine($"[License] {result.Message}");
                }
            }
            catch (Exception ex)
            {
                IsLicenseValid = false;
                System.Diagnostics.Debug.WriteLine($"[License] Error checking license: {ex.Message}");
                // Show license modal on error
                var licenseModal = new LicenseModal();
                licenseModal.LicenseValidated += (s, e) => IsLicenseValid = true;
                licenseModal.ShowDialog();
                
                if (!licenseModal.IsLicenseValid)
                {
                    System.Windows.Application.Current.Shutdown();
                }
                else
                {
                    IsLicenseValid = true;
                }
            }
        }

        private void LicenseIndicator_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var licenseService = new Services.LicenseService();
                var result = licenseService.ValidateLicense();
                
                if (result.IsValid && result.LicenseData != null)
                {
                    // Show current license info
                    var message = $"Current License Status:\n\n" +
                                 $"Machine ID: {licenseService.GetCurrentMachineId()}\n" +
                                 $"Expiry Date: {result.LicenseData.ExpiryDate:yyyy-MM-dd}\n" +
                                 $"Features: {string.Join(", ", result.LicenseData.Features)}\n\n" +
                                 $"Would you like to change the license?";
                    
                    var response = System.Windows.MessageBox.Show(message, "License Information", 
                        System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Information);
                    
                    if (response == System.Windows.MessageBoxResult.Yes)
                    {
                        // Show license modal to change license
                        var licenseModal = new LicenseModal();
                        licenseModal.LicenseValidated += (s, e) => IsLicenseValid = true;
                        licenseModal.ShowDialog();
                        
                        // Update license status after modal
                        var newResult = licenseService.ValidateLicense();
                        IsLicenseValid = newResult.IsValid;
                        
                        if (!newResult.IsValid)
                        {
                            System.Windows.Application.Current.Shutdown();
                        }
                    }
                }
                else
                {
                    // Show license modal to enter license
                    var licenseModal = new LicenseModal();
                    licenseModal.LicenseValidated += (s, e) => IsLicenseValid = true;
                    licenseModal.ShowDialog();
                    
                    var newResult = licenseService.ValidateLicense();
                    IsLicenseValid = newResult.IsValid;
                    
                    if (!newResult.IsValid)
                    {
                        System.Windows.Application.Current.Shutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error accessing license: {ex.Message}", "License Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void StartConnectButtonFlash()
        {
            try
            {
                var storyboard = FindResource("ConnectButtonFlashAnimation") as Storyboard;
                if (storyboard != null && ConnectButton != null)
                {
                    storyboard.Begin(ConnectButton);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting connect button flash: {ex.Message}");
            }
        }

        private void StopConnectButtonFlash()
        {
            try
            {
                var storyboard = FindResource("ConnectButtonFlashAnimation") as Storyboard;
                if (storyboard != null && ConnectButton != null)
                {
                    storyboard.Stop(ConnectButton);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping connect button flash: {ex.Message}");
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsConnected)
                {
                    // Logout functionality
                    StatusMessage = "Logging out...";
                    await _whatsAppService.LogoutAsync();
                }
                else
                {
                    // Connect functionality
                    StatusMessage = "Connecting to WhatsApp...";
                    ConnectButton.IsEnabled = false;
                    StopConnectButtonFlash(); // Stop flashing when clicked
                    
                    await _whatsAppService.InitializeAsync();
                    
                    StatusMessage = "Waiting for QR code...";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Operation failed: {ex.Message}";
                ConnectButton.IsEnabled = true;
                if (!IsConnected)
                {
                    StartConnectButtonFlash(); // Resume flashing if connection fails
                }
                System.Windows.MessageBox.Show($"Operation failed: {ex.Message}", "Error", 
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
            if (IsMonitoring)
            {
                // Stop monitoring functionality
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
            else
            {
                // Start monitoring functionality
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
        }


        
        protected override void OnClosed(EventArgs e)
        {
            _fileWatcher?.Dispose();
            _refreshTimer?.Dispose();
            base.OnClosed(e);
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select Download Folder",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadFolder = dialog.SelectedPath;
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Manual refresh triggered by user");
            LoadExistingMedia();
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

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            // Handle Delete key for selected items
            if (e.Key == System.Windows.Input.Key.Delete && MediaListView.SelectedItem is MediaItem item)
            {
                DeleteFile(item);
                e.Handled = true;
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

        private void MediaProcessingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListView.SelectedItem is MediaItem item)
            {
                ProcessSelectedMediaFile(item);
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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsModal = new SettingsModal(_appSettings);
            settingsModal.Owner = this;
            
            if (settingsModal.ShowDialog() == true)
            {
                // Update settings
                _appSettings = settingsModal.Settings;
                _settingsService.SaveSettings(_appSettings);
                
                // Update UI based on new settings
                IsMediaProcessingEnabled = _appSettings.IsMediaProcessingEnabled;
                
                // Update download folder if changed
                if (!string.IsNullOrEmpty(_appSettings.DownloadFolder) && Directory.Exists(_appSettings.DownloadFolder))
                {
                    DownloadFolder = _appSettings.DownloadFolder;
                }
                
                StatusMessage = "Settings saved successfully";
            }
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
                // Allow logout when connected and not monitoring
                ConnectButton.IsEnabled = !connected || !IsMonitoring;
                
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
                    // Check for duplicate media using ID + timestamp + sender combination
                    string mediaKey = $"{mediaMessage.Id}_{mediaMessage.Timestamp}_{mediaMessage.From}";
                    if (_processedMediaIds.Contains(mediaKey))
                    {
                        System.Diagnostics.Debug.WriteLine($"[OnMediaMessageReceived] Duplicate media detected, skipping: {mediaKey}");
                        return;
                    }
                    
                    // Add to processed set
                    _processedMediaIds.Add(mediaKey);
                    System.Diagnostics.Debug.WriteLine($"[OnMediaMessageReceived] Processing new media: {mediaKey}");
                    
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
                
                // Update ConnectButton enabled state based on monitoring status
                if (IsConnected)
                {
                    ConnectButton.IsEnabled = !monitoring;
                }
            });
        }

        private async void OnTextMessageReceived(object? sender, TextMessage textMessage)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Text message received: {textMessage.Text?.Substring(0, Math.Min(100, textMessage.Text?.Length ?? 0))}...");
                    
                    // Check if the text contains any URLs
                    if (!string.IsNullOrEmpty(textMessage.Text))
                    {
                        var urls = ExtractUrls(textMessage.Text);
                        
                        foreach (var url in urls)
                        {
                            // Check if it's an X tweet URL
                            if (_xTweetScreenshotService.IsTweetUrl(url))
                            {
                                System.Diagnostics.Debug.WriteLine($"X tweet URL detected: {url}");
                                
                                // Check if X tweet downloads are enabled
                                if (_appSettings.DownloadXTweets)
                                {
                                    StatusMessage = "Processing X tweet...";
                                    
                                    // Take screenshot of the tweet
                                    var screenshotPath = await _xTweetScreenshotService.TakeTweetScreenshotAsync(
                                        url, 
                                        textMessage.SenderName ?? "Unknown", 
                                        DateTimeOffset.FromUnixTimeSeconds(textMessage.Timestamp ?? 0).DateTime
                                    );
                                    
                                    if (!string.IsNullOrEmpty(screenshotPath))
                                    {
                                        StatusMessage = $"X tweet screenshot saved: {Path.GetFileName(screenshotPath)}";
                                        
                                        // Add to media items list as a special type
                                        var mediaItem = new MediaItem
                                        {
                                            FileName = Path.GetFileName(screenshotPath),
                                            FilePath = screenshotPath,
                                            MediaType = "X Tweet Screenshot",
                                            FileSize = new FileInfo(screenshotPath).Length,
                                            Timestamp = DateTimeOffset.FromUnixTimeSeconds(textMessage.Timestamp ?? 0).DateTime,
                                            SenderName = textMessage.SenderName ?? "Unknown",
                                            GroupId = textMessage.From ?? string.Empty,
                                            Thumbnail = CreateBitmapImageFromFile(screenshotPath) // Use the screenshot as its own thumbnail
                                        };
                                        
                                        _mediaItems.Add(mediaItem);
                                        OnPropertyChanged(nameof(MediaItems));
                                    }
                                    else
                                    {
                                        StatusMessage = "Failed to take X tweet screenshot";
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("X tweet downloads are disabled in settings");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing text message: {ex.Message}");
                }
            });
        }

        private List<string> ExtractUrls(string text)
        {
            var urls = new List<string>();
            var urlPattern = @"https?://[^\s]+";
            var matches = System.Text.RegularExpressions.Regex.Matches(text, urlPattern);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                urls.Add(match.Value);
            }
            
            return urls;
        }

        private BitmapImage? CreateBitmapImageFromFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.UriSource = new Uri(filePath);
                    bitmapImage.EndInit();
                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating BitmapImage from file {filePath}: {ex.Message}");
            }
            return null;
        }

        private async Task<MediaItem?> DownloadMediaAsync(MediaMessage mediaMessage)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Starting download for: {mediaMessage.Filename} ({mediaMessage.Type})");
                
                // Check if media data is empty
                if (string.IsNullOrEmpty(mediaMessage.Data))
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Media data is empty or null - cannot download");
                    StatusMessage = "Download failed: No media data received";
                    return null;
                }
                
                // Decode base64 data
                var mediaData = Convert.FromBase64String(mediaMessage.Data);
                System.Diagnostics.Debug.WriteLine($"Decoded data length: {mediaData.Length} bytes");
                
                // Use filename directly from WhatsApp.js (already in correct format)
                var filePath = Path.Combine(DownloadFolder, mediaMessage.Filename);
                
                // Ensure unique filename to avoid conflicts
                var counter = 1;
                while (File.Exists(filePath))
                {
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(mediaMessage.Filename);
                    var extension = Path.GetExtension(mediaMessage.Filename);
                    filePath = Path.Combine(DownloadFolder, $"{fileNameWithoutExt}_{counter}{extension}");
                    counter++;
                }
                
                // Write file
                await File.WriteAllBytesAsync(filePath, mediaData);
                
                // Create media item (let the app handle JSON/thumbnails)
                var mediaItem = new MediaItem
                {
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    MediaType = mediaMessage.Type,
                    FileSize = mediaMessage.Size,
                    Timestamp = mediaMessage.DateTime,
                    SenderName = mediaMessage.SenderName,
                    Extension = Path.GetExtension(filePath)
                };
                
                System.Diagnostics.Debug.WriteLine($"Successfully created media item: {mediaItem.FileName} at {mediaItem.FilePath}");
                return mediaItem;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading media: {ex.Message}");
                StatusMessage = $"Download failed: {ex.Message}";
                return null;
            }
        }


        private async Task ProcessMediaFileAsync(string originalFilePath, string mediaType)
        {
            // Create cancellation token source for this specific processing operation
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            // Add debug logging to track cancellation
            var cancellationRegistration = cancellationToken.Register(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Cancellation token was cancelled for: {Path.GetFileName(originalFilePath)}");
                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Stack trace: {Environment.StackTrace}");
            });

            try
            {
                // Check if this is a processable media type
                var extension = Path.GetExtension(originalFilePath).ToLowerInvariant();
                var isProcessable = extension == ".jpg" || extension == ".jpeg" || extension == ".png" || 
                                   extension == ".bmp" || extension == ".gif" || extension == ".mp4" || 
                                   extension == ".avi" || extension == ".mov" || extension == ".wmv";

                if (!isProcessable)
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Skipping non-processable file: {Path.GetFileName(originalFilePath)}");
                    return;
                }

                // IMPORTANT: Skip files that already have _processed in their name to prevent infinite loops
                if (Path.GetFileName(originalFilePath).Contains("_processed"))
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Skipping already processed file: {Path.GetFileName(originalFilePath)}");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Starting processing for: {Path.GetFileName(originalFilePath)}");

                // Show progress overlay only if no other file is being processed
                Dispatcher.Invoke(() =>
                {
                    if (!IsProcessingMedia)
                    {
                        IsProcessingMedia = true;
                        ProcessingFileName = Path.GetFileName(originalFilePath);
                        ProcessingProgress = 0.0;
                        ProcessingProgressText = "Initializing...";
                        
                        // Update window title to indicate processing
                        Title = $"WhatUPload - Processing {Path.GetFileName(originalFilePath)}...";
                        
                        // Start the rotation and pulse animations
                        var rotationStoryboard = FindResource("ProcessingIconRotationAnimation") as Storyboard;
                        var pulseStoryboard = FindResource("ProcessingIconPulseAnimation") as Storyboard;
                        rotationStoryboard?.Begin();
                        pulseStoryboard?.Begin();
                    }
                });

                // Create processed filename
                var fileName = Path.GetFileNameWithoutExtension(originalFilePath);
                var newFileName = $"{fileName}_processed{extension}";
                var processedFilePath = Path.Combine(Path.GetDirectoryName(originalFilePath)!, newFileName);

                // Ensure unique filename
                var counter = 1;
                while (File.Exists(processedFilePath))
                {
                    newFileName = $"{fileName}_processed_{counter}{extension}";
                    processedFilePath = Path.Combine(Path.GetDirectoryName(originalFilePath)!, newFileName);
                    counter++;
                }

                // Process the file - let it complete naturally
                var success = await _videoProcessingService.ConvertTo16x9WithBlurredBackground(
                    originalFilePath, 
                    processedFilePath,
                    progress => 
                    {
                        // Update progress overlay
                        Dispatcher.Invoke(() =>
                        {
                            ProcessingProgress = progress;
                            ProcessingProgressText = $"Processing... {(progress * 100):F0}%";
                        });
                    },
                    cancellationToken);

                // CRITICAL FIX: Check if processed file exists, regardless of success flag
                var processedFileExists = File.Exists(processedFilePath);
                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Processed file exists: {processedFileExists}, Success flag: {success}");

                if (processedFileExists)
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Successfully processed: {Path.GetFileName(originalFilePath)}");
                    
                    // Generate thumbnail for the processed file
                    var processedMediaItem = new MediaItem
                    {
                        FilePath = processedFilePath,
                        FileName = Path.GetFileName(processedFilePath),
                        MediaType = GetMediaType(Path.GetExtension(processedFilePath)),
                        SenderName = "Locally Added",
                        GroupId = "",
                        Timestamp = DateTime.Now
                    };

                    // Generate thumbnail asynchronously
                    _ = Task.Run(async () => await GenerateThumbnailAsync(processedMediaItem));

                    // Schedule refresh to update UI
                    ScheduleRefresh();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Failed to process: {Path.GetFileName(originalFilePath)}");
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Processing was cancelled for: {Path.GetFileName(originalFilePath)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Error processing {Path.GetFileName(originalFilePath)}: {ex.Message}");
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Entering finally block for {Path.GetFileName(originalFilePath)}");
                
                // Dispose of the cancellation token source
                cancellationTokenSource?.Dispose();
                
                // Dispose of the cancellation registration
                cancellationRegistration.Dispose();
                
                // Hide the progress overlay only if this was the last processing operation
                Dispatcher.Invoke(() =>
                {
                    // Check if this was the file being shown in the progress overlay
                    if (ProcessingFileName == Path.GetFileName(originalFilePath))
                    {
                        IsProcessingMedia = false;
                        ProcessingFileName = string.Empty;
                        ProcessingProgress = 0.0;
                        ProcessingProgressText = string.Empty;
                        
                        // Restore window title
                        Title = "WhatUPload";
                        
                        // Stop the rotation and pulse animations
                        var rotationStoryboard = FindResource("ProcessingIconRotationAnimation") as Storyboard;
                        var pulseStoryboard = FindResource("ProcessingIconPulseAnimation") as Storyboard;
                        rotationStoryboard?.Stop();
                        pulseStoryboard?.Stop();
                    }
                });
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
            
            return fileName + extension;
        }

        private string GetThumbnailsDirectory()
        {
            var thumbnailsDir = Path.Combine(DownloadFolder, ".thumbnails");
            if (!Directory.Exists(thumbnailsDir))
            {
                Directory.CreateDirectory(thumbnailsDir);
                // Hide the directory using Windows API
                try
                {
                    var dirInfo = new DirectoryInfo(thumbnailsDir);
                    dirInfo.Attributes = dirInfo.Attributes | FileAttributes.Hidden | FileAttributes.System;
                    
                    // Also try to set it as a system folder
                    System.Diagnostics.Debug.WriteLine($"Set thumbnails directory attributes: {dirInfo.Attributes}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not hide thumbnails directory: {ex.Message}");
                }
            }
            else
            {
                // Ensure existing directory is hidden
                try
                {
                    var dirInfo = new DirectoryInfo(thumbnailsDir);
                    if ((dirInfo.Attributes & FileAttributes.Hidden) == 0)
                    {
                        dirInfo.Attributes = dirInfo.Attributes | FileAttributes.Hidden | FileAttributes.System;
                        System.Diagnostics.Debug.WriteLine($"Updated thumbnails directory attributes: {dirInfo.Attributes}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not update thumbnails directory attributes: {ex.Message}");
                }
            }
            return thumbnailsDir;
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
                        var thumbnailPath = Path.Combine(GetThumbnailsDirectory(), $"thumb_{Path.GetFileNameWithoutExtension(mediaItem.FileName)}.jpg");
                        
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
        
        private bool IsFileReady(string filename)
        {
            // Try to open the file exclusively. If it fails, the file is still locked.
            try
            {
                using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return true;
                }
            }
            catch (IOException)
            {
                return false;
            }
        }
        
        private void EnsureTempProcessingDir()
        {
            if (!Directory.Exists(_tempProcessingDir))
            {
                Directory.CreateDirectory(_tempProcessingDir);
                // Set hidden attribute
                var dirInfo = new DirectoryInfo(_tempProcessingDir);
                dirInfo.Attributes |= FileAttributes.Hidden;
            }
        }
        
        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                // CRITICAL FIX: Check if media processing is enabled before proceeding
                if (!IsMediaProcessingEnabled)
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Media processing is disabled, skipping: {e.Name}");
                    return;
                }

                // Check if this specific file is currently being processed
                if (ProcessingFileName == e.Name)
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaProcessing] File {e.Name} is currently being processed, skipping");
                    return;
                }

                // Check if this is a media file we can process
                var extension = Path.GetExtension(e.Name ?? "").ToLower();
                if (!IsMediaFile(extension))
                {
                    System.Diagnostics.Debug.WriteLine($"Skipping non-media file: {e.Name} (extension: {extension})");
                    return;
                }

                // Skip processed files
                if ((e.Name ?? "").Contains("_processed"))
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Skipping OnFileCreated for processed file: {e.Name}");
                    return;
                }

                // Wait until the file is ready (not locked)
                int maxTries = 20;
                int delayMs = 500;
                int tries = 0;
                while (!IsFileReady(e.FullPath) && tries < maxTries)
                {
                    await Task.Delay(delayMs);
                    tries++;
                }
                if (!IsFileReady(e.FullPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaProcessing] File {e.Name} is still locked after waiting, skipping");
                    return;
                }

                // Ensure temp processing dir exists and is hidden
                EnsureTempProcessingDir();

                // Copy file to temp dir (do not move)
                var tempFileName = Path.Combine(_tempProcessingDir, Path.GetFileName(e.FullPath));
                try
                {
                    File.Copy(e.FullPath, tempFileName, overwrite: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Failed to copy file to temp dir: {ex.Message}");
                    return;
                }

                // Trigger processing for the new file in temp dir
                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Triggering processing for file in temp dir: {Path.GetFileName(tempFileName)}");
                _ = Task.Run(async () =>
                {
                    await ProcessMediaFileAsync(tempFileName, GetMediaType(extension));
                    // After processing, move processed file back to original folder
                    var processedFileName = Path.GetFileNameWithoutExtension(tempFileName) + "_processed" + extension;
                    var processedFilePath = Path.Combine(_tempProcessingDir, processedFileName);
                    var destProcessedFilePath = Path.Combine(Path.GetDirectoryName(e.FullPath) ?? "", processedFileName);
                    if (File.Exists(processedFilePath))
                    {
                        try
                        {
                            File.Move(processedFilePath, destProcessedFilePath, overwrite: true);
                            System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Moved processed file back to: {destProcessedFilePath}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Failed to move processed file back: {ex.Message}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Error in OnFileCreated: {ex.Message}");
            }
        }
        
        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            // Skip processed files
            if ((e.Name ?? "").Contains("_processed"))
            {
                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Skipping OnFileDeleted for processed file: {e.Name}");
                return;
            }
            System.Diagnostics.Debug.WriteLine($"File deleted: {e.Name}");
            
            // Delete corresponding thumbnail if it exists
            try
            {
                var thumbnailPath = Path.Combine(GetThumbnailsDirectory(), $"thumb_{Path.GetFileNameWithoutExtension(e.Name ?? "")}.jpg");
                if (File.Exists(thumbnailPath))
                {
                    File.Delete(thumbnailPath);
                    System.Diagnostics.Debug.WriteLine($"Deleted thumbnail: {thumbnailPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting thumbnail for {e.Name}: {ex.Message}");
            }
            
            ScheduleRefresh();
        }
        
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            // Skip processed files
            if ((e.Name ?? "").Contains("_processed") || (e.OldName ?? "").Contains("_processed"))
            {
                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Skipping OnFileRenamed for processed file: {e.OldName} -> {e.Name}");
                return;
            }
            System.Diagnostics.Debug.WriteLine($"File renamed: {e.OldName} -> {e.Name}");
            ScheduleRefresh();
        }
        
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Skip processed files
            if ((e.Name ?? "").Contains("_processed"))
            {
                System.Diagnostics.Debug.WriteLine($"[MediaProcessing] Skipping OnFileChanged for processed file: {e.Name}");
                return;
            }
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
                    
                    // Skip thumbnail files (only files that start with "thumb_")
                    if (fileInfo.Name.StartsWith("thumb_"))
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping thumbnail file: {fileInfo.Name}");
                        continue;
                    }
                    
                    // Skip hidden files and directories
                    if (fileInfo.Name.StartsWith("."))
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping hidden file: {fileInfo.Name}");
                        continue;
                    }
                    
                    // Note: Processed files are now visible in UI since we use a separate temp directory
                    // The infinite loop prevention is handled by using a separate temp directory
                    
                    var mediaItem = new MediaItem
                    {
                        FileName = fileInfo.Name,
                        FilePath = file,
                        FileSize = fileInfo.Length,
                        Timestamp = fileInfo.CreationTime,
                        Extension = fileInfo.Extension,
                        SenderName = fileInfo.Name.Contains("_processed") ? "Processed" : "Locally Added" // Distinguish processed files
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
                
                // Set the JSON file as hidden
                if (File.Exists(jsonFilePath))
                {
                    File.SetAttributes(jsonFilePath, File.GetAttributes(jsonFilePath) | FileAttributes.Hidden);
                }
                
                System.Diagnostics.Debug.WriteLine($"Created hidden JSON file: {jsonFilePath}");
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
                    .Where(f => !Path.GetFileName(f).StartsWith("thumb_")) // Skip thumbnail files
                    .Where(f => !Path.GetFileName(f).StartsWith(".")) // Skip hidden files
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
                            var thumbnailPath = Path.Combine(GetThumbnailsDirectory(), $"thumb_{Path.GetFileNameWithoutExtension(mediaItem.FileName)}.jpg");
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

        private void ProcessSelectedMediaFile(MediaItem item)
        {
            try
            {
                if (!File.Exists(item.FilePath))
                {
                    System.Windows.MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var extension = Path.GetExtension(item.FilePath).ToLowerInvariant();
                var isProcessable = extension == ".jpg" || extension == ".jpeg" || extension == ".png" || 
                                   extension == ".bmp" || extension == ".gif" || extension == ".mp4" || 
                                   extension == ".avi" || extension == ".mov" || extension == ".wmv";

                if (!isProcessable)
                {
                    System.Windows.MessageBox.Show("This file type is not supported for processing.", "Unsupported Format", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Skip files that already have _processed in their name
                if (Path.GetFileName(item.FilePath).Contains("_processed"))
                {
                    System.Windows.MessageBox.Show("This file has already been processed.", "Already Processed", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Start processing asynchronously without blocking the UI
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessMediaFileAsync(item.FilePath, item.MediaType);
                        
                        // Update status on UI thread after completion
                        Dispatcher.Invoke(() =>
                        {
                            StatusMessage = $"Processed {item.FileName}";
                        });
                    }
                    catch (Exception ex)
                    {
                        // Handle errors on UI thread
                        Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show($"Failed to process file: {ex.Message}", "Processing Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            StatusMessage = "Processing failed";
                        });
                    }
                });

                StatusMessage = $"Processing {item.FileName}...";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to start processing: {ex.Message}", "Processing Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Processing failed";
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

        private void CancelProcessingButton_Click(object sender, RoutedEventArgs e)
        {
            // CRITICAL FIX: Manual escape mechanism - hide the modal immediately
            System.Diagnostics.Debug.WriteLine("[MediaProcessing] User cancelled processing - hiding modal");
            
            // Cancel the processing to prevent file locking
            if (_processingCancellationTokenSource != null)
            {
                _processingCancellationTokenSource.Cancel();
            }
            _videoProcessingService.CancelCurrentProcessing();
            
            // Dispose of the timeout timer
            _processingTimeoutTimer?.Dispose();
            _processingTimeoutTimer = null;
            
            // Dispose of the cancellation token source
            _processingCancellationTokenSource?.Dispose();
            _processingCancellationTokenSource = null;
            
            IsProcessingMedia = false;
            ProcessingFileName = string.Empty;
            ProcessingProgress = 0.0;
            ProcessingProgressText = string.Empty;
            
            // Restore window title
            Title = "WhatUPload";
            
            // Stop the rotation and pulse animations
            var rotationStoryboard = FindResource("ProcessingIconRotationAnimation") as Storyboard;
            var pulseStoryboard = FindResource("ProcessingIconPulseAnimation") as Storyboard;
            rotationStoryboard?.Stop();
            pulseStoryboard?.Stop();
            
            StatusMessage = "Processing cancelled by user";
        }

        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // If processing is active, prevent closing and show message
            if (IsProcessingMedia)
            {
                e.Cancel = true;
                var result = System.Windows.MessageBox.Show(
                    "Processing is currently active. Closing now will cancel the operation and may result in incomplete files.\n\nDo you want to continue closing?",
                    "Processing Active",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // User confirmed - allow closing and cancel processing
                    _processingCancellationTokenSource?.Cancel();
                    _videoProcessingService.CancelCurrentProcessing();
                }
                else
                {
                    // User cancelled - keep application open
                    return;
                }
            }

            if (_isLoggingOut)
                return;
            e.Cancel = true;
            _isLoggingOut = true;
            
            // Cancel any ongoing processing to prevent file locking
            _processingCancellationTokenSource?.Cancel();
            _videoProcessingService.CancelCurrentProcessing();
            
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

        /// <summary>
        /// Checks if a file extension represents a media file that can be processed
        /// </summary>
        private bool IsMediaFile(string extension)
        {
            var mediaExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".mp4", ".avi", ".mov", ".wmv" };
            return mediaExtensions.Contains(extension.ToLower());
        }

        /// <summary>
        /// Gets the media type from a file extension
        /// </summary>
        private string GetMediaType(string extension)
        {
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv" };

            if (imageExtensions.Contains(extension.ToLower()))
                return "image";
            else if (videoExtensions.Contains(extension.ToLower()))
                return "video";
            else
                return "unknown";
        }


    }
}