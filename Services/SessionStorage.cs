using CleanAimTracker.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CleanAimTracker.Services
{
    public static class SessionStorage
    {
        private static readonly string FilePath = "session_history.json";

        public static void Save(SessionSummary summary)
        {
            List<SessionSummary> history = LoadAll();
            history.Add(summary);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(history));
        }

        public static void SaveSession(SessionSummary summary)
        {
            Save(summary);
        }

        public static List<SessionSummary> LoadAll()
        {
            if (!File.Exists(FilePath))
                return new List<SessionSummary>();

            return JsonSerializer.Deserialize<List<SessionSummary>>(
                File.ReadAllText(FilePath)
            );
        }

        public static SessionSummary LoadLast()
        {
            var all = LoadAll();
            if (all.Count == 0) return null;
            return all[^1];
        }

        public static void ClearAll()
        {
            if (File.Exists(FilePath))
                File.WriteAllText(FilePath, "[]");
        }
    }
}
