using Microsoft.Win32;
using System;
using System.Windows;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Manages Dark / System theme switching.
    /// Swaps merged resource dictionaries at the App level at runtime.
    /// </summary>
    public static class ThemeService
    {
        private const string DarkThemePath = "Themes/DarkTheme.xaml";
        private const string LightThemePath = "Themes/LightTheme.xaml";

        /// <summary>Apply the saved theme on startup.</summary>
        public static void Initialize()
        {
            var settings = SettingsService.Load();
            ApplyTheme(settings.ThemeMode);
        }

        /// <summary>
        /// Apply a theme mode: "dark" or "system".
        /// System mode reads the Windows personalization registry key.
        /// </summary>
        public static void Apply(string mode) => ApplyTheme(mode);

        public static void ApplyTheme(string mode)
        {
            bool useDark;

            switch (mode?.ToLower())
            {
                case "light":
                    useDark = false;
                    break;
                case "system":
                    useDark = !IsWindowsLightTheme();
                    break;
                default: // "dark" and anything else
                    useDark = true;
                    break;
            }

            string themePath = useDark ? DarkThemePath : LightThemePath;

            try
            {
                var dict = new ResourceDictionary
                {
                    Source = new Uri(themePath, UriKind.Relative)
                };

                var app = Application.Current;
                if (app == null) return;

                // Remove existing theme dictionary (always index 0)
                if (app.Resources.MergedDictionaries.Count > 0)
                    app.Resources.MergedDictionaries.RemoveAt(0);

                // Insert new theme at index 0
                app.Resources.MergedDictionaries.Insert(0, dict);

                LogService.Info($"Theme applied: {mode} -> {(useDark ? "dark" : "light")}");
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to apply theme", ex);
            }
        }

        /// <summary>Toggle between dark and system mode.</summary>
        public static void Toggle()
        {
            var settings = SettingsService.Load();
            settings.ThemeMode = settings.ThemeMode == "dark" ? "system" : "dark";
            SettingsService.Save(settings);
            ApplyTheme(settings.ThemeMode);
        }

        /// <summary>Get the current theme mode string.</summary>
        public static string GetCurrentMode()
        {
            return SettingsService.Load().ThemeMode;
        }

        /// <summary>
        /// Reads the Windows registry to determine if the system is using light theme.
        /// Key: HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize
        /// Value: AppsUseLightTheme (1 = light, 0 = dark)
        /// </summary>
        private static bool IsWindowsLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                return value is int i && i == 1;
            }
            catch
            {
                return false; // default to dark if registry read fails
            }
        }
    }
}
