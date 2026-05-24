using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CleanAimTracker.Services
{
    public static class SessionStorage
    {
        private static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "CleanAimTracker");

        private static readonly string FilePath =
            Path.Combine(Folder, "session_history.json");

        public static void Save(SessionSummary summary)
        {
            try
            {
                Directory.CreateDirectory(Folder);

                List<SessionSummary> history = LoadAll();
                history.Add(summary);

                string json    = JsonSerializer.Serialize(history);
                string tmpPath = FilePath + ".tmp";

                // Safe atomic write: write to .tmp then replace,
                // so a crash mid-write never corrupts the main file.
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, FilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                LogService.Error("SessionStorage.Save failed", ex);
            }
        }

        public static void SaveSession(SessionSummary summary)
        {
            Save(summary);
        }

        public static List<SessionSummary> LoadAll()
        {
            try
            {
                Directory.CreateDirectory(Folder);

                if (!File.Exists(FilePath))
                    return new List<SessionSummary>();

                return JsonSerializer.Deserialize<List<SessionSummary>>(
                    File.ReadAllText(FilePath)
                ) ?? new List<SessionSummary>();
            }
            catch (Exception ex)
            {
                LogService.Error("SessionStorage.LoadAll failed — returning empty list", ex);
                return new List<SessionSummary>();
            }
        }

        public static SessionSummary? LoadLast()
        {
            var all = LoadAll();
            if (all.Count == 0) return null;
            return all[^1];
        }

        // Internal use only — called by reset flows, not exposed to arbitrary callers.
        internal static void ClearAll()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string tmpPath = FilePath + ".tmp";
                File.WriteAllText(tmpPath, "[]");
                File.Move(tmpPath, FilePath, overwrite: true);
                LogService.Info("SessionStorage.ClearAll: session history wiped");
            }
            catch (Exception ex)
            {
                LogService.Error("SessionStorage.ClearAll failed", ex);
            }
        }
    }
}
