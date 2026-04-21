using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Exports session data to CSV or JSON format.
    /// </summary>
    public static class ExportService
    {
        /// <summary>Export a single session to JSON file. Returns the file path.</summary>
        public static string ExportToJson(SessionSummary session, string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(session, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(filePath, json);
                LogService.Info($"Exported session to JSON: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                LogService.Error("JSON export failed", ex);
                throw;
            }
        }

        /// <summary>Export a single session to CSV file. Returns the file path.</summary>
        public static string ExportToCsv(SessionSummary session, string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Metric,Value");
                sb.AppendLine($"Timestamp,{session.Timestamp:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Duration,{session.Duration:mm\\:ss}");
                sb.AppendLine($"Total Samples,{session.TotalSamples}");
                sb.AppendLine($"DPI,{session.DPI}");
                sb.AppendLine($"Sensitivity,{session.Sensitivity}");
                sb.AppendLine($"cm/360,{session.CmPer360:F2}");
                sb.AppendLine($"Average Velocity (cm/s),{session.AverageVelocity:F2}");
                sb.AppendLine($"Peak Velocity (cm/s),{session.PeakVelocity:F2}");
                sb.AppendLine($"Total Distance (cm),{session.TotalDistanceCm:F2}");
                sb.AppendLine($"Avg Distance/Event,{session.AvgDistPerEvent:F2}");
                sb.AppendLine($"Peak Distance/Event,{session.PeakDistPerEvent:F2}");
                sb.AppendLine($"Peak Acceleration (cm/s2),{session.PeakAcceleration:F2}");
                sb.AppendLine($"Avg Acceleration (cm/s2),{session.AvgAcceleration:F2}");
                sb.AppendLine($"Flick Count,{session.FlickCount}");
                sb.AppendLine($"Small Flicks,{session.SmallFlickCount}");
                sb.AppendLine($"Large Flicks,{session.LargeFlickCount}");
                sb.AppendLine($"Idle Bursts,{session.IdleBurstCount}");
                sb.AppendLine($"Smoothness Score,{session.SmoothnessScore:F0}");
                sb.AppendLine($"Correction Sharpness,{session.CorrectionSharpness:F0}");
                sb.AppendLine($"Movement Consistency,{session.MovementConsistency:F0}");
                sb.AppendLine($"Overall Quality,{session.OverallQualityScore:F0}");
                sb.AppendLine($"Idle Percentage,{session.IdlePercentage:F1}");
                sb.AppendLine($"Jitter Events,{session.JitterAmount:F0}");

                File.WriteAllText(filePath, sb.ToString());
                LogService.Info($"Exported session to CSV: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                LogService.Error("CSV export failed", ex);
                throw;
            }
        }

        /// <summary>Export all sessions to a single CSV file.</summary>
        public static string ExportAllToCsv(List<SessionSummary> sessions, string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Timestamp,Duration,Samples,DPI,Sensitivity,cm360,AvgVelocity,PeakVelocity,TotalDistance,Flicks,SmallFlicks,LargeFlicks,Smoothness,Consistency,OverallQuality,IdlePct,Jitter");

                foreach (var s in sessions)
                {
                    sb.AppendLine(string.Join(",",
                        s.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        s.Duration.ToString(@"mm\:ss"),
                        s.TotalSamples,
                        s.DPI,
                        s.Sensitivity,
                        s.CmPer360.ToString("F2"),
                        s.AverageVelocity.ToString("F2"),
                        s.PeakVelocity.ToString("F2"),
                        s.TotalDistanceCm.ToString("F2"),
                        s.FlickCount,
                        s.SmallFlickCount,
                        s.LargeFlickCount,
                        s.SmoothnessScore.ToString("F0"),
                        s.MovementConsistency.ToString("F0"),
                        s.OverallQualityScore.ToString("F0"),
                        s.IdlePercentage.ToString("F1"),
                        s.JitterAmount.ToString("F0")
                    ));
                }

                File.WriteAllText(filePath, sb.ToString());
                LogService.Info($"Exported {sessions.Count} sessions to CSV: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                LogService.Error("Bulk CSV export failed", ex);
                throw;
            }
        }

        /// <summary>Returns the default export folder (Documents/CleanAimTracker).</summary>
        public static string GetDefaultExportFolder()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "CleanAimTracker", "Exports");
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Simple wrapper used by MainWindow. Exports a session to JSON in the default folder.
        /// </summary>
        public static void ExportSummary(SessionSummary session)
        {
            string folder = GetDefaultExportFolder();
            string file = Path.Combine(folder, $"Session_{DateTime.Now:yyyyMMdd_HHmmss}.json");

            ExportToJson(session, file);
        }

    }
}
