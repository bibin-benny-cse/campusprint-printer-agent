using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace XeroxGo.PrinterAgent.Services
{
    public static class StartupManager
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "XeroxGoPrinterAgent";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to check registry startup status", ex);
                return false;
            }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Environment.ProcessPath ?? "";

                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\" --minimized");
                        Logger.Info($"Registered auto-start at: {exePath}");
                    }
                }
                else
                {
                    if (key.GetValue(AppName) != null)
                    {
                        key.DeleteValue(AppName);
                        Logger.Info("Removed auto-start from registry");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update registry auto-start", ex);
            }
        }
    }
}
