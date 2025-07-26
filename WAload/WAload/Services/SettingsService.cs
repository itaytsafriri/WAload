using System;
using System.IO;
using WAload.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WAload.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        public SettingsService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "WAload");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            
            _settingsFilePath = Path.Combine(appFolder, "settings.yaml");
            
            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
                
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var yamlContent = File.ReadAllText(_settingsFilePath);
                    var settings = _deserializer.Deserialize<AppSettings>(yamlContent);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
            
            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var yamlContent = _serializer.Serialize(settings);
                File.WriteAllText(_settingsFilePath, yamlContent);
                System.Diagnostics.Debug.WriteLine($"Settings saved to: {_settingsFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
} 