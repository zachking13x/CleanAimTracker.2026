using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CleanAimTracker.Services
{
    public static class AimTrainerStorage
    {
        private static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CleanAimTracker", "AimTrainer");

        private static readonly string FilePath = Path.Combine(Folder, "results.json");

        public static void Save(AimTrainerResult result)
        {
            try
            {
                Directory.CreateDirectory(Folder);

                var list = LoadAll();
                list.Add(result);

                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                string tmpPath = FilePath + ".tmp";
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, FilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                LogService.Error("AimTrainerStorage.Save failed — drill result not persisted", ex);
            }
        }

        public static List<AimTrainerResult> LoadAll()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new List<AimTrainerResult>();

                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<AimTrainerResult>>(json)
                       ?? new List<AimTrainerResult>();
            }
            catch
            {
                return new List<AimTrainerResult>();
            }
        }

        public static AimTrainerResult? LoadLast()
        {
            var all = LoadAll();
            if (all.Count == 0) return null;
            return all[^1];
        }
    }
}
