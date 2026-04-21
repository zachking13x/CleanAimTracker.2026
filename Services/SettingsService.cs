using System;
using System.IO;
using System.Text.Json;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Persists user settings (DPI, sensitivity, units, theme, performance mode)
    /// to a local JSON file so they survive across app restarts.
    /// </summary>
    public class UserSettings
    {
        public double DPI { get; set; } = 800;
        public double Sensitivity { get; set; } = 1.0;
        public string Units { get; set; } = "cm";           // "cm" or "in"
        public string ThemeMode { get; set; } = "dark";      // "dark" or "system"
        public bool PerformanceMode { get; set; } = false;
        public bool FirstLaunchComplete { get; set; } = false;
        public string SelectedProfile { get; set; } = "Tactical Shooter A";
        public DateTime FirstLaunchDate { get; set; } = DateTime.MinValue;
    }

    public static class SettingsService
    {
        private static readonly string _folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "CleanAimTracker");

        private static readonly string _filePath =
            Path.Combine(_folder, "settings.json");

        private static UserSettings? _cached;

        // ── Load ──────────────────────────────────────────────────
        public static UserSettings Load()
        {
            if (_cached != null) return _cached;

            if (!File.Exists(_filePath))
            {
                _cached = new UserSettings();
                return _cached;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                _cached = JsonSerializer.Deserialize<UserSettings>(json)
                          ?? new UserSettings();
                return _cached;
            }
            catch
            {
                _cached = new UserSettings();
                return _cached;
            }
        }

        // ── Save ──────────────────────────────────────────────────
        public static void Save(UserSettings settings)
        {
            try
            {
                _cached = settings;
                Directory.CreateDirectory(_folder);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to save settings", ex);
            }
        }

        // ── Reset to defaults ─────────────────────────────────────
        public static UserSettings ResetToDefaults()
        {
            var defaults = new UserSettings();
            Save(defaults);
            return defaults;
        }

        // ── Quick helpers ─────────────────────────────────────────
        public static void UpdateDpi(double dpi)
        {
            var s = Load();
            s.DPI = dpi;
            Save(s);
        }

        public static void UpdateSensitivity(double sens)
        {
            var s = Load();
            s.Sensitivity = sens;
            Save(s);
        }

        public static void UpdateTheme(string mode)
        {
            var s = Load();
            s.ThemeMode = mode;
            Save(s);
        }

        public static void MarkFirstLaunchComplete()
        {
            var s = Load();
            s.FirstLaunchComplete = true;
            if (s.FirstLaunchDate == DateTime.MinValue)
                s.FirstLaunchDate = DateTime.Now;
            Save(s);
        }
    }
}
