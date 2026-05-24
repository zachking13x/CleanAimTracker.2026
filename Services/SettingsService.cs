using CleanAimTracker.Models;
using System.IO;
using System.Text.Json;

namespace CleanAimTracker.Services
{
    public static class SettingsService
    {
        private static readonly string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CleanAimTracker", "settings.json");

        public static UserSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new UserSettings();

                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
            catch
            {
                return new UserSettings();
            }
        }

        public static void Save(UserSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Atomic write: write to .tmp then replace so a crash mid-write
            // never corrupts the main settings file.
            string tmpPath = FilePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, FilePath, overwrite: true);
        }
    }
}
