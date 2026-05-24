using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CleanAimTracker.Models;


namespace CleanAimTracker.Services
{
    public static class ProfileStorage
    {
        private static readonly string ProfilesFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CleanAimTracker", "Profiles");

        private static readonly string ProfilesFile =
            Path.Combine(ProfilesFolder, "profiles.json");

        static ProfileStorage()
        {
            if (!Directory.Exists(ProfilesFolder))
                Directory.CreateDirectory(ProfilesFolder);

            if (!File.Exists(ProfilesFile))
                File.WriteAllText(ProfilesFile, "[]");
        }

        public static List<AimProfile> LoadProfiles()
        {
            try
            {
                string json = File.ReadAllText(ProfilesFile);
                return JsonSerializer.Deserialize<List<AimProfile>>(json) ?? new List<AimProfile>();
            }
            catch
            {
                return new List<AimProfile>();
            }
        }

        public static void SaveProfiles(List<AimProfile> profiles)
        {
            try
            {
                Directory.CreateDirectory(ProfilesFolder);

                string json    = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
                string tmpPath = ProfilesFile + ".tmp";

                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, ProfilesFile, overwrite: true);
            }
            catch (Exception ex)
            {
                LogService.Error("ProfileStorage.SaveProfiles failed", ex);
            }
        }
    }
}
