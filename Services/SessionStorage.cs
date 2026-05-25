using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // Oldest excess sessions are moved to the archive file instead of being discarded.
        private const int MaxSessions = 1000;
        private static readonly string ArchivePath = FilePath + ".archive.json";

        public static void Save(SessionSummary summary)
        {
            try
            {
                Directory.CreateDirectory(Folder);

                List<SessionSummary> history = LoadAll();
                history.Add(summary);

                if (history.Count > MaxSessions)
                {
                    // Move the oldest excess entries to the archive before trimming.
                    var excess = history.Take(history.Count - MaxSessions).ToList();
                    AppendToArchive(excess);
                    history = history.Skip(history.Count - MaxSessions).ToList();
                }

                // Atomic write: write to .tmp then replace,
                // so a crash mid-write never corrupts the main file.
                string json    = JsonSerializer.Serialize(history);
                string tmpPath = FilePath + ".tmp";
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, FilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                LogService.Error("SessionStorage.Save failed", ex);
            }
        }

        [Obsolete("Use Save() directly. SaveSession() will be removed in a future version.")]
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

        private static void AppendToArchive(List<SessionSummary> sessions)
        {
            try
            {
                Directory.CreateDirectory(Folder);

                List<SessionSummary> existing = new();
                if (File.Exists(ArchivePath))
                {
                    try
                    {
                        existing = JsonSerializer.Deserialize<List<SessionSummary>>(
                            File.ReadAllText(ArchivePath)) ?? new();
                    }
                    catch { /* corrupt archive — start fresh */ }
                }

                existing.AddRange(sessions);

                string json    = JsonSerializer.Serialize(existing);
                string tmpPath = ArchivePath + ".tmp";
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, ArchivePath, overwrite: true);
            }
            catch (Exception ex)
            {
                LogService.Error("SessionStorage.AppendToArchive failed", ex);
            }
        }
    }
}
