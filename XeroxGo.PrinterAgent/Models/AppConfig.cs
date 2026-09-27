using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XeroxGo.PrinterAgent.Models
{
    public class AppConfig
    {
        public string ApiUrl { get; set; } = "http://localhost:3001/api";
        public string LogicalPrinterName { get; set; } = "Printer 1";
        public string PhysicalPrinterName { get; set; } = "Auto";
        public string AgentApiKey { get; set; } = "";
        
        // Backward compatibility alias for PrinterName
        [JsonIgnore]
        public string PrinterName
        {
            get => LogicalPrinterName;
            set => LogicalPrinterName = value;
        }

        public int PollIntervalSeconds { get; set; } = 3;
        public int HeartbeatIntervalSeconds { get; set; } = 5;
        public bool AutoStartWithWindows { get; set; } = true;
        public string TempDirectory { get; set; } = "";

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
                ApiUrl = "http://localhost:3001/api";

            if (string.IsNullOrWhiteSpace(LogicalPrinterName))
                LogicalPrinterName = "Printer 1";

            if (string.IsNullOrWhiteSpace(PhysicalPrinterName))
                PhysicalPrinterName = "Auto";

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
