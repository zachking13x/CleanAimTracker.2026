using System;
using System.IO;
using System.Text.Json;
using CleanAimTracker.Models;
using System.Collections.Generic;

namespace CleanAimTracker.Services
{
    public static class SessionStorage
    {
        private static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "CleanAimTracker", "Sessions");

        public static void SaveSession(SessionSummary session)
        {
            Directory.CreateDirectory(Folder);

            string file = Path.Combine(Folder,
                $"session_{DateTime.Now:yyyyMMdd_HHmmss}.json");

            File.WriteAllText(file,
                JsonSerializer.Serialize(session, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }

        public static List<SessionSummary> LoadAll()
        {
            Directory.CreateDirectory(Folder);

            var list = new List<SessionSummary>();

            foreach (var file in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var session = JsonSerializer.Deserialize<SessionSummary>(json);
                    if (session != null)
                        list.Add(session);
                }
                catch
                {
                    // ignore corrupted or unreadable files
                }
            }

            return list;
        }

        public static void ClearAll()
        {
            Directory.CreateDirectory(Folder);

            foreach (var file in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // ignore locked or corrupted files
                }
            }
        }
    }
}
