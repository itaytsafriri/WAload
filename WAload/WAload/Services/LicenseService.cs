using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace WAload.Services
{
    public class LicenseService
    {
        private const string LicenseFileName = "sys_config_3a7b9c.dat";
        private const string MachineIdFileName = "app_state_7f2e8d.bin";
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SystemConfig", "AppCache");
        private static readonly string LicenseFilePath = Path.Combine(AppDataFolder, LicenseFileName);
        private static readonly string MachineIdFilePath = Path.Combine(AppDataFolder, MachineIdFileName);

        public class LicenseData
        {
            public string MachineId { get; set; } = string.Empty;
            public DateTime ExpiryDate { get; set; }
            public string[] Features { get; set; } = Array.Empty<string>();
            public DateTime GeneratedDate { get; set; }
        }

        public class LicenseValidationResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; } = string.Empty;
            public LicenseData? LicenseData { get; set; }
            public string[] AvailableFeatures { get; set; } = Array.Empty<string>();
        }

        private void EnsureAppDataDirectoryExists()
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
                // Set directory as hidden
                var dirInfo = new DirectoryInfo(AppDataFolder);
                dirInfo.Attributes |= FileAttributes.Hidden;
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Created hidden license directory: {AppDataFolder}");
            }
        }

        private byte[] ConvertToBinaryFormat(string text)
        {
            var textBytes = Encoding.UTF8.GetBytes(text);
            var random = new Random();
            var padding = new byte[16];
            random.NextBytes(padding);
            
            // XOR encode with simple pattern
            for (int i = 0; i < textBytes.Length; i++)
            {
                textBytes[i] ^= (byte)(0xAA ^ (i % 256));
            }
            
            // Combine padding + encoded text
            var result = new byte[padding.Length + textBytes.Length];
            Array.Copy(padding, 0, result, 0, padding.Length);
            Array.Copy(textBytes, 0, result, padding.Length, textBytes.Length);
            
            return result;
        }

        private string ConvertFromBinaryFormat(byte[] binaryData)
        {
            if (binaryData.Length <= 16) return string.Empty;
            
            // Extract encoded text (skip padding)
            var textBytes = new byte[binaryData.Length - 16];
            Array.Copy(binaryData, 16, textBytes, 0, textBytes.Length);
            
            // XOR decode with same pattern
            for (int i = 0; i < textBytes.Length; i++)
            {
                textBytes[i] ^= (byte)(0xAA ^ (i % 256));
            }
            
            return Encoding.UTF8.GetString(textBytes);
        }

        public LicenseValidationResult ValidateLicense()
        {
            try
            {
                // Add timeout to prevent hanging
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                var validationTask = Task.Run(() => ValidateLicenseInternal());
                
                if (Task.WhenAny(validationTask, timeoutTask).Result == timeoutTask)
                {
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        Message = "License validation timed out. Please try again."
                    };
                }
                
                return validationTask.Result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error validating license: {ex.Message}");
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = $"License validation error: {ex.Message}"
                };
            }
        }

        private LicenseValidationResult ValidateLicenseInternal()
        {
            EnsureAppDataDirectoryExists();
            
            // Check if license file exists
            if (!File.Exists(LicenseFilePath))
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = "License file not found. Please enter your license key."
                };
            }

            // Read license key from binary format
            string licenseKey;
            try
            {
                var binaryData = File.ReadAllBytes(LicenseFilePath);
                licenseKey = ConvertFromBinaryFormat(binaryData).Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error reading license file: {ex.Message}");
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = "Failed to read license file. Please re-enter your license key."
                };
            }
            
            if (string.IsNullOrEmpty(licenseKey))
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = "License file is empty. Please enter a valid license key."
                };
            }

            // Decrypt and validate license
            var licenseData = DecryptLicenseKey(licenseKey);
            if (licenseData == null)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = "Invalid license key format."
                };
            }

            // Check if license has expired
            if (DateTime.Now > licenseData.ExpiryDate)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = $"License has expired on {licenseData.ExpiryDate:yyyy-MM-dd}."
                };
            }

            // Validate machine ID
            var currentMachineId = GetCurrentMachineId();
            if (licenseData.MachineId != currentMachineId)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = "License is not valid for this machine."
                };
            }

            return new LicenseValidationResult
            {
                IsValid = true,
                Message = $"License valid until {licenseData.ExpiryDate:yyyy-MM-dd}",
                LicenseData = licenseData,
                AvailableFeatures = licenseData.Features
            };
        }

        public bool SaveLicenseKey(string licenseKey)
        {
            try
            {
                // Validate the license key first
                var licenseData = DecryptLicenseKey(licenseKey);
                if (licenseData == null)
                {
                    return false;
                }

                EnsureAppDataDirectoryExists();
                
                // Save the license key in binary format
                var binaryData = ConvertToBinaryFormat(licenseKey);
                File.WriteAllBytes(LicenseFilePath, binaryData);
                System.Diagnostics.Debug.WriteLine("[LicenseService] License key saved successfully in binary format.");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error saving license key: {ex.Message}");
                return false;
            }
        }

        public string GetCurrentMachineId()
        {
            try
            {
                // Add timeout to prevent hanging
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
                var machineIdTask = Task.Run(() => GetCurrentMachineIdInternal());
                
                if (Task.WhenAny(machineIdTask, timeoutTask).Result == timeoutTask)
                {
                    System.Diagnostics.Debug.WriteLine("[LicenseService] Machine ID generation timed out, using fallback");
                    return "TIMEOUT_MACHINE_ID";
                }
                
                return machineIdTask.Result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error getting machine ID: {ex.Message}");
                return "UNKNOWN_MACHINE";
            }
        }

        private string GetCurrentMachineIdInternal()
        {
            EnsureAppDataDirectoryExists();
            
            // Check if we have a cached machine ID
            if (File.Exists(MachineIdFilePath))
            {
                try
                {
                    // Read machine ID as plain text
                    var cachedMachineId = File.ReadAllText(MachineIdFilePath).Trim();
                    
                    // Validate that the machine ID contains only safe characters
                    if (!string.IsNullOrEmpty(cachedMachineId) && IsValidMachineId(cachedMachineId))
                    {
                        return cachedMachineId;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LicenseService] Corrupted machine ID detected, regenerating...");
                        // Delete corrupted file
                        File.Delete(MachineIdFilePath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LicenseService] Error reading machine ID file: {ex.Message}");
                    try
                    {
                        File.Delete(MachineIdFilePath);
                    }
                    catch { }
                }
            }

            // Generate new machine ID
            var machineId = GenerateMachineId();
            // Save as plain text for clean display
            File.WriteAllText(MachineIdFilePath, machineId, Encoding.UTF8);
            System.Diagnostics.Debug.WriteLine($"[LicenseService] New machine ID generated and saved: {machineId}");
            return machineId;
        }

        private bool IsValidMachineId(string machineId)
        {
            // Check if machine ID contains only letters and numbers (no special characters)
            if (string.IsNullOrEmpty(machineId) || machineId.Length < 16)
                return false;
            
            foreach (char c in machineId)
            {
                if (!char.IsLetterOrDigit(c))
                    return false;
            }
            
            return true;
        }

        private string GenerateMachineId()
        {
            var sb = new StringBuilder();
            
            // Use more reliable hardware identifiers that don't require WMI
            try
            {
                // Get computer name
                sb.Append(Environment.MachineName ?? "UNKNOWN");
                sb.Append("-");
                
                // Get user name
                sb.Append(Environment.UserName ?? "USER");
                sb.Append("-");
                
                // Get OS version
                sb.Append(Environment.OSVersion?.ToString() ?? "OS");
                sb.Append("-");
                
                // Get processor count
                sb.Append(Environment.ProcessorCount.ToString());
                sb.Append("-");
                
                // Get system directory
                sb.Append(Environment.SystemDirectory ?? "SYS");
                
                // Add timestamp for uniqueness
                sb.Append(DateTime.Now.Ticks.ToString());
            }
            catch
            {
                sb.Clear();
                sb.Append($"FALLBACK-ID-{DateTime.Now.Ticks}");
            }

            // Create a hash of the combined system info and ensure clean output
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                
                // Convert to hex instead of base64 for cleaner output
                var hex = BitConverter.ToString(hash).Replace("-", "");
                
                // Take first 32 characters and ensure they're all valid
                var machineId = hex.Substring(0, Math.Min(32, hex.Length));
                
                // Ensure all characters are safe for display
                var cleanId = new StringBuilder();
                foreach (char c in machineId)
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        cleanId.Append(c);
                    }
                }
                
                // If somehow we don't have enough characters, pad with safe characters
                while (cleanId.Length < 32)
                {
                    cleanId.Append((char)('A' + (cleanId.Length % 26)));
                }
                
                return cleanId.ToString().Substring(0, 32);
            }
        }

        private LicenseData? DecryptLicenseKey(string licenseKey)
        {
            try
            {
                // Restore base64 characters
                var encryptedData = licenseKey.Replace("_", "/").Replace("-", "+");
                var encryptedBytes = Convert.FromBase64String(encryptedData);

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

                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(encryptedBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs))
                    {
                        var json = sr.ReadToEnd();
                        return JsonConvert.DeserializeObject<LicenseData>(json);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error decrypting license key: {ex.Message}");
                return null;
            }
        }

        public bool HasFeature(string feature)
        {
            var result = ValidateLicense();
            if (!result.IsValid)
                return false;

            return result.AvailableFeatures.Any(f => string.Equals(f, feature, StringComparison.OrdinalIgnoreCase));
        }

        public void ClearLicense()
        {
            try
            {
                if (File.Exists(LicenseFilePath))
                {
                    File.Delete(LicenseFilePath);
                }
                System.Diagnostics.Debug.WriteLine("[LicenseService] License cleared.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Error clearing license: {ex.Message}");
            }
        }
    }
} 