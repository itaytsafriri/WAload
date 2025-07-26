using System;
using System.Windows;
using WAload.Services;

namespace WAload
{
    public partial class LicenseModal : Window
    {
        private readonly LicenseService _licenseService;
        public bool IsLicenseValid { get; private set; } = false;
        
        public event EventHandler? LicenseValidated;

        public LicenseModal()
        {
            InitializeComponent();
            _licenseService = new LicenseService();
            
            // Display the current machine ID
            var machineId = _licenseService.GetCurrentMachineId();
            MachineIdTextBox.Text = machineId;
        }

        private void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ValidateButton.IsEnabled = false;
                StatusTextBlock.Text = "Validating license...";

                var licenseKey = LicenseKeyTextBox.Text.Trim();
                if (string.IsNullOrEmpty(licenseKey))
                {
                    StatusTextBlock.Text = "Please enter a license key.";
                    ValidateButton.IsEnabled = true;
                    return;
                }

                // Save and validate the license
                if (_licenseService.SaveLicenseKey(licenseKey))
                {
                    var result = _licenseService.ValidateLicense();
                    if (result.IsValid)
                    {
                        StatusTextBlock.Text = $"License validated successfully!\n{result.Message}";
                        IsLicenseValid = true;
                        
                        // Notify main window that license was validated
                        LicenseValidated?.Invoke(this, EventArgs.Empty);
                        
                        // Close the modal after a short delay
                        var timer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(2)
                        };
                        timer.Tick += (s, args) =>
                        {
                            timer.Stop();
                            Close();
                        };
                        timer.Start();
                    }
                    else
                    {
                        StatusTextBlock.Text = $"License validation failed: {result.Message}";
                        _licenseService.ClearLicense(); // Clear invalid license
                    }
                }
                else
                {
                    StatusTextBlock.Text = "Invalid license key format.";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error validating license: {ex.Message}";
            }
            finally
            {
                ValidateButton.IsEnabled = true;
            }
        }

        private void CopyMachineIdButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var machineId = MachineIdTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(machineId))
                {
                    System.Windows.Clipboard.SetText(machineId);
                    StatusTextBlock.Text = "Machine ID copied to clipboard! You can now use it in the Key Generator.";
                }
                else
                {
                    StatusTextBlock.Text = "No Machine ID to copy.";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error copying Machine ID: {ex.Message}";
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
} 