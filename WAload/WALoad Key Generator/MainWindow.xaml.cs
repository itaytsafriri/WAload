using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using System.Linq;

namespace WALoad_Key_Generator
{
    public partial class MainWindow : Window
    {
        private DateTime _expiryDate = DateTime.Now.AddYears(1);
        private string _machineId = string.Empty;
        private string _licenseKey = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            ExpiryDatePicker.SelectedDate = _expiryDate;
            
            // Add text changed event to update _machineId when user types/pastes
            MachineIdTextBox.TextChanged += (s, e) =>
            {
                _machineId = MachineIdTextBox.Text.Trim();
            };
            
            // Don't auto-generate Machine ID - let user paste from WAload
            // GenerateMachineId();
        }

        private void GenerateMachineIdButton_Click(object sender, RoutedEventArgs e)
        {
            GenerateMachineId();
        }

        private void GenerateMachineId()
        {
            try
            {
                _machineId = GetMachineId();
                MachineIdTextBox.Text = _machineId;
                OutputTextBox.AppendText($"Machine ID generated: {_machineId}\n");
            }
            catch (Exception ex)
            {
                OutputTextBox.AppendText($"Error generating Machine ID: {ex.Message}\n");
            }
        }

        private void GenerateLicenseKeyButton_Click(object sender, RoutedEventArgs e)
        {
            GenerateLicenseKey();
        }

        private void GenerateLicenseKey()
        {
            try
            {
                if (string.IsNullOrEmpty(_machineId))
                {
                    OutputTextBox.AppendText("Please generate a Machine ID first.\n");
                    return;
                }

                var licenseData = new LicenseData
                {
                    MachineId = _machineId,
                    ExpiryDate = _expiryDate,
                    Features = GetSelectedFeatures(),
                    GeneratedDate = DateTime.Now
                };

                _licenseKey = GenerateLicenseKeyFromData(licenseData);
                LicenseKeyTextBox.Text = _licenseKey;

                var jsonData = JsonConvert.SerializeObject(licenseData, Formatting.Indented);
                OutputTextBox.AppendText($"License Key generated successfully!\n\nLicense Data:\n{jsonData}\n\nLicense Key:\n{_licenseKey}\n");
            }
            catch (Exception ex)
            {
                OutputTextBox.AppendText($"Error generating License Key: {ex.Message}\n");
            }
        }

        private void SetExpiryButton_Click(object sender, RoutedEventArgs e)
        {
            if (ExpiryDatePicker.SelectedDate.HasValue)
            {
                _expiryDate = ExpiryDatePicker.SelectedDate.Value;
                OutputTextBox.AppendText($"Expiry date set to: {_expiryDate:yyyy-MM-dd}\n");
            }
        }

        private void CopyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_licenseKey))
                {
                    System.Windows.Clipboard.SetText(_licenseKey);
                    OutputTextBox.AppendText("License key copied to clipboard!\n");
                }
                else
                {
                    OutputTextBox.AppendText("No license key to copy.\n");
                }
            }
            catch (Exception ex)
            {
                OutputTextBox.AppendText($"Error copying to clipboard: {ex.Message}\n");
            }
        }

        private void CopyMachineIdButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_machineId))
                {
                    System.Windows.Clipboard.SetText(_machineId);
                    OutputTextBox.AppendText("Machine ID copied to clipboard!\n");
                }
                else
                {
                    OutputTextBox.AppendText("No Machine ID to copy.\n");
                }
            }
            catch (Exception ex)
            {
                OutputTextBox.AppendText($"Error copying Machine ID: {ex.Message}\n");
            }
        }

        private void PasteMachineIdButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var clipboardText = System.Windows.Clipboard.GetText();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    _machineId = clipboardText.Trim();
                    MachineIdTextBox.Text = _machineId;
                    OutputTextBox.AppendText($"Machine ID pasted from clipboard: {_machineId}\n");
                }
                else
                {
                    OutputTextBox.AppendText("No text found in clipboard.\n");
                }
            }
            catch (Exception ex)
            {
                OutputTextBox.AppendText($"Error pasting Machine ID: {ex.Message}\n");
            }
        }

        private void CopyLicenseKeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_licenseKey))
                {
                    System.Windows.Clipboard.SetText(_licenseKey);
                    OutputTextBox.AppendText("License key copied to clipboard!\n");
                }
                else
                {
                    OutputTextBox.AppendText("No license key to copy.\n");
                }
            }
            catch (Exception ex)
            {
                OutputTextBox.AppendText($"Error copying license key: {ex.Message}\n");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            OutputTextBox.Clear();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ExpiryDatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ExpiryDatePicker.SelectedDate.HasValue)
            {
                _expiryDate = ExpiryDatePicker.SelectedDate.Value;
            }
        }

        private string GetMachineId()
        {
            var sb = new StringBuilder();
            
            // Use more reliable hardware identifiers that don't require WMI
            try
            {
                // Get computer name
                sb.Append(Environment.MachineName);
                
                // Get user name
                sb.Append(Environment.UserName);
                
                // Get OS version
                sb.Append(Environment.OSVersion.ToString());
                
                // Get processor count
                sb.Append(Environment.ProcessorCount.ToString());
                
                // Get system directory
                sb.Append(Environment.SystemDirectory);
                
                // Get working set (memory)
                sb.Append(Environment.WorkingSet.ToString());
                
                // Get current directory
                sb.Append(Environment.CurrentDirectory);
            }
            catch
            {
                sb.Append("FALLBACK_ID");
            }

            // Create a hash of the combined system info
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return Convert.ToBase64String(hash).Substring(0, 32).Replace("/", "_").Replace("+", "-");
            }
        }

        private string[] GetSelectedFeatures()
        {
            var features = new System.Collections.Generic.List<string>();
            
            if (FeatureBasicCheckBox.IsChecked == true)
                features.Add("Basic");
            
            if (FeatureMediaProcessingCheckBox.IsChecked == true)
                features.Add("MediaProcessing");
            
            if (FeatureAdvancedCheckBox.IsChecked == true)
                features.Add("Advanced");
            
            if (FeatureUnlimitedCheckBox.IsChecked == true)
                features.Add("Unlimited");
            
            return features.ToArray();
        }

        private string GenerateLicenseKeyFromData(LicenseData data)
        {
            try
            {
                // Serialize the license data to JSON
                var json = JsonConvert.SerializeObject(data);
                
                // Encrypt the JSON data
                using (var aes = Aes.Create())
                {
                    // Ensure exactly 16 bytes for AES-128
                    var keyBytes = Encoding.UTF8.GetBytes("WALoadKeyGen2024!!");
                    if (keyBytes.Length < 16)
                    {
                        // Pad with zeros if too short
                        var paddedKey = new byte[16];
                        Array.Copy(keyBytes, paddedKey, keyBytes.Length);
                        keyBytes = paddedKey;
                    }
                    else if (keyBytes.Length > 16)
                    {
                        // Truncate if too long
                        keyBytes = keyBytes.Take(16).ToArray();
                    }
                    
                    aes.Key = keyBytes;
                    aes.IV = new byte[16]; // Zero IV for simplicity

                    using (var encryptor = aes.CreateEncryptor())
                    using (var ms = new System.IO.MemoryStream())
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new System.IO.StreamWriter(cs))
                    {
                        sw.Write(json);
                        sw.Flush();
                        cs.FlushFinalBlock();
                        
                        // Convert to base64 and make URL-safe
                        var encryptedData = Convert.ToBase64String(ms.ToArray());
                        return encryptedData.Replace("/", "_").Replace("+", "-");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating license key: {ex.Message}");
            }
        }
    }

    public class LicenseData
    {
        public string MachineId { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public string[] Features { get; set; } = Array.Empty<string>();
        public DateTime GeneratedDate { get; set; }
    }
} 