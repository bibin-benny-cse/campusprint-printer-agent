using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XeroxGo.PrinterAgent.Models
{
    public class AppConfig
    {
        public string ApiUrl { get; set; } = "https://campusprint-backend-bl6p.onrender.com/api";
        public string AgentApiKey { get; set; } = "";
        public int PollIntervalSeconds { get; set; } = 3;
        public int HeartbeatIntervalSeconds { get; set; } = 5;
        public bool AutoStartWithWindows { get; set; } = true;
        public string TempDirectory { get; set; } = "";

        // Backward compatibility properties for deserializing legacy config files
        [JsonPropertyName("LogicalPrinterName")]
        public string? LegacyLogicalPrinterName { get; set; }

        [JsonPropertyName("PhysicalPrinterName")]
        public string? LegacyPhysicalPrinterName { get; set; }

        [JsonPropertyName("PrinterName")]
        public string? LegacyPrinterName { get; set; }

        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XeroxGo"
        );

        private static readonly string ConfigFilePath = Path.Combine(ConfigDir, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null)
                    {
                        config.EnsureDefaults();
                        return config;
                    }
                }
            }
            catch
            {
                // Fallback to defaults on corrupt config
            }

            var defaultConfig = new AppConfig();
            defaultConfig.EnsureDefaults();
            defaultConfig.Save();
            return defaultConfig;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                EnsureDefaults();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(ApiUrl))
                ApiUrl = "https://campusprint-backend-bl6p.onrender.com/api";

            if (PollIntervalSeconds < 1)
                PollIntervalSeconds = 3;

            if (HeartbeatIntervalSeconds < 1)
                HeartbeatIntervalSeconds = 5;

            if (string.IsNullOrWhiteSpace(TempDirectory))
            {
                TempDirectory = Path.Combine(ConfigDir, "temp");
            }

            if (!Directory.Exists(TempDirectory))
            {
                try
                {
                    Directory.CreateDirectory(TempDirectory);
                }
                catch { }
            }
        }
    }
}
