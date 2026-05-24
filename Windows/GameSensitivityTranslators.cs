using System.Collections.Generic;
using CleanAimTracker.Models;

namespace CleanAimTracker.Windows
{
    public interface IGameSensitivityTranslator
    {
        string GameName { get; }
        GameSpecificView Translate(SensitivityRecommendation rec);
    }

    public class GameSpecificView
    {
        public List<string> GameSettingsLines { get; set; } = new();
        public List<string> AdsScopeLines { get; set; } = new();
        public List<string> AdvancedLines { get; set; } = new();
        public List<string> GameTipsLines { get; set; } = new();

        public bool HasAdsScopeSection => AdsScopeLines.Count > 0;
    }

    // ─────────────────────────────────────────────
    // FORTNITE
    // ─────────────────────────────────────────────
    public class FortniteTranslator : IGameSensitivityTranslator
    {
        public string GameName => "Fortnite";

        public GameSpecificView Translate(SensitivityRecommendation rec)
        {
            // RecommendedSensitivity is already in Fortnite percentage units
            // (e.g. 7.62 means 7.62%) — no scaling needed.
            double sliderPercent = rec.RecommendedSensitivity;

            var view = new GameSpecificView();

            // Field names match Fortnite's Mouse and Keyboard settings page exactly
            view.GameSettingsLines.Add($"X-Axis Sensitivity: {sliderPercent:F1}%");
            view.GameSettingsLines.Add($"Y-Axis Sensitivity: {sliderPercent:F1}%");
            view.GameSettingsLines.Add("Targeting Sensitivity: 70%");
            view.GameSettingsLines.Add("Scope Sensitivity: 70%");
            view.GameSettingsLines.Add("Building Sensitivity: 100%");
            view.GameSettingsLines.Add("Editing Sensitivity: 100%");
            view.GameSettingsLines.Add("First Person Mode: 100%");

            view.AdsScopeLines.Add("Keep Targeting and Scope around 70% for consistent ADS feel.");

            view.AdvancedLines.Add("Raw Input: On.");
            view.AdvancedLines.Add("Mouse Acceleration: Off.");
            view.AdvancedLines.Add("Polling Rate: 1000 Hz.");

            view.GameTipsLines.Add("Keep X and Y equal unless you have a specific reason not to.");
            view.GameTipsLines.Add("Avoid changing sensitivity frequently; give your muscle memory time to adapt.");

            return view;
        }
    }

    // ─────────────────────────────────────────────
    // VALORANT
    // ─────────────────────────────────────────────
    public class ValorantTranslator : IGameSensitivityTranslator
    {
        public string GameName => "Valorant";

        public GameSpecificView Translate(SensitivityRecommendation rec)
        {
            var view = new GameSpecificView();

            view.GameSettingsLines.Add($"Sensitivity: {rec.RecommendedSensitivity:F4}");
            view.GameSettingsLines.Add("Scoped Sensitivity Multiplier: 1.0");
            view.GameSettingsLines.Add("Raw Input Buffer: On");

            view.AdsScopeLines.Add("Keep scoped multiplier at 1.0 for consistent muscle memory.");

            view.AdvancedLines.Add("Mouse Acceleration: Off.");
            view.AdvancedLines.Add("Polling Rate: 1000 Hz.");

            view.GameTipsLines.Add("Avoid changing sensitivity mid‑season.");
            view.GameTipsLines.Add("Use the practice range to verify flick control.");

            return view;
        }
    }

    // ─────────────────────────────────────────────
    // APEX LEGENDS
    // ─────────────────────────────────────────────
    public class ApexTranslator : IGameSensitivityTranslator
    {
        public string GameName => "Apex Legends";

        public GameSpecificView Translate(SensitivityRecommendation rec)
        {
            var view = new GameSpecificView();

            view.GameSettingsLines.Add($"Mouse Sensitivity: {rec.RecommendedSensitivity:F4}");
            view.GameSettingsLines.Add("ADS Sensitivity Multiplier: 1.0");
            view.GameSettingsLines.Add("Per Optic ADS Sensitivity: Off");

            view.AdsScopeLines.Add("Match ADS multiplier to hipfire cm/360.");

            view.AdvancedLines.Add("Mouse Acceleration: Off.");
            view.AdvancedLines.Add("FOV: 90–110.");

            view.GameTipsLines.Add("Test both close‑range tracking and long‑range corrections.");

            return view;
        }
    }

    // ─────────────────────────────────────────────
    // CS2
    // ─────────────────────────────────────────────
    public class Cs2Translator : IGameSensitivityTranslator
    {
        public string GameName => "Counter-Strike 2";

        public GameSpecificView Translate(SensitivityRecommendation rec)
        {
            var view = new GameSpecificView();

            view.GameSettingsLines.Add($"sensitivity {rec.RecommendedSensitivity:F4}");
            view.GameSettingsLines.Add("zoom_sensitivity_ratio_mouse 1.0");
            view.GameSettingsLines.Add("m_rawinput 1");

            view.AdsScopeLines.Add("Keep zoom ratio at 1.0 for consistency.");

            view.AdvancedLines.Add("Disable mouse acceleration.");
            view.AdvancedLines.Add("Use consistent resolution/aspect ratio.");

            view.GameTipsLines.Add("Train micro‑adjustments and flicks.");

            return view;
        }
    }

    // ─────────────────────────────────────────────
    // OVERWATCH 2
    // ─────────────────────────────────────────────
    public class OverwatchTranslator : IGameSensitivityTranslator
    {
        public string GameName => "Overwatch 2";

        public GameSpecificView Translate(SensitivityRecommendation rec)
        {
            var view = new GameSpecificView();

            view.GameSettingsLines.Add($"Mouse Sensitivity: {rec.RecommendedSensitivity * 100:F1}");
            view.GameSettingsLines.Add("Scoped Sensitivity: 100%");
            view.GameSettingsLines.Add("Relative Aim Sensitivity While Zoomed: 100%");

            view.AdsScopeLines.Add("Keep zoom sensitivity at 100%.");

            view.AdvancedLines.Add("Aim Smoothing: 0.");
            view.AdvancedLines.Add("Aim Ease In: 0–20.");

            view.GameTipsLines.Add("Test on both hitscan and projectile heroes.");

            return view;
        }
    }

    // ─────────────────────────────────────────────
    // RAINBOW SIX SIEGE
    // ─────────────────────────────────────────────
    public class R6Translator : IGameSensitivityTranslator
    {
        public string GameName => "Rainbow Six Siege";

        public GameSpecificView Translate(SensitivityRecommendation rec)
        {
            var view = new GameSpecificView();

            view.GameSettingsLines.Add($"Mouse Sensitivity: {rec.RecommendedSensitivity * 10:F1}");
            view.GameSettingsLines.Add("ADS Sensitivity: 50");

            view.AdsScopeLines.Add("Tune per‑zoom around baseline.");

            view.AdvancedLines.Add("FOV: 80–90.");
            view.AdvancedLines.Add("Mouse Acceleration: Off.");

            view.GameTipsLines.Add("R6 rewards precise micro‑adjustments.");

            return view;
        }
    }

    // ─────────────────────────────────────────────
    // WARZONE / MW2 / MW3
    // ─────────────────────────────────────────────
    public class WarzoneTranslator : IGameSensitivityTranslator
    {
        public string GameName => "Call of Duty: MW / Warzone";

        public GameSpecificView Translate(SensitivityRecommendation rec)
        {
            var view = new GameSpecificView();

            view.GameSettingsLines.Add($"Horizontal Sensitivity: {rec.RecommendedSensitivity * 10:F1}");
            view.GameSettingsLines.Add($"Vertical Sensitivity: {rec.RecommendedSensitivity * 10:F1}");
            view.GameSettingsLines.Add("ADS Sensitivity Multiplier: 1.0");

            view.AdsScopeLines.Add("Use 1.0 ADS multiplier.");

            view.AdvancedLines.Add("Aim Response Curve: Standard or Dynamic.");
            view.AdvancedLines.Add("Deadzone: As low as possible.");

            view.GameTipsLines.Add("Avoid very high sensitivities; recoil control matters.");

            return view;
        }
    }

    // ─────────────────────────────────────────────
    // PUBG
    // ─────────────────────────────────────────────
    public class PubgTranslator : IGameSensitivityTranslator
    {
        public string GameName => "PUBG";

        public GameSpecificView Translate(SensitivityRecommendation rec)
        {
            var view = new GameSpecificView();

            view.GameSettingsLines.Add($"General Sensitivity: {rec.RecommendedSensitivity * 50:F1}");
            view.GameSettingsLines.Add("Vertical Sensitivity Multiplier: 1.0");

            view.AdsScopeLines.Add("Align scope sensitivities around baseline.");

            view.AdvancedLines.Add("Disable mouse acceleration.");
            view.AdvancedLines.Add("Use consistent FOV.");

            view.GameTipsLines.Add("PUBG favors stability over speed.");

            return view;
        }
    }

    // ─────────────────────────────────────────────
    // HALO INFINITE
    // ─────────────────────────────────────────────
    public class HaloTranslator : IGameSensitivityTranslator
    {
        public string GameName => "Halo Infinite";

        public GameSpecificView Translate(SensitivityRecommendation rec)
        {
            var view = new GameSpecificView();

            view.GameSettingsLines.Add($"Look Sensitivity (Horizontal): {rec.RecommendedSensitivity * 5:F1}");
            view.GameSettingsLines.Add($"Look Sensitivity (Vertical): {rec.RecommendedSensitivity * 5:F1}");

            view.AdsScopeLines.Add("Keep zoom sensitivity close to hipfire.");

            view.AdvancedLines.Add("Deadzones: As low as possible.");
            view.AdvancedLines.Add("Acceleration: Off.");

            view.GameTipsLines.Add("Halo rewards strong crosshair placement.");

            return view;
        }
    }

    // ─────────────────────────────────────────────
    // ESCAPE FROM TARKOV
    // ─────────────────────────────────────────────
    public class TarkovTranslator : IGameSensitivityTranslator
    {
        public string GameName => "Escape From Tarkov";

        public GameSpecificView Translate(SensitivityRecommendation rec)
        {
            var view = new GameSpecificView();

            view.GameSettingsLines.Add($"Mouse Sensitivity: {rec.RecommendedSensitivity * 50:F1}");
            view.GameSettingsLines.Add("Aiming Sensitivity: 50");

            view.AdsScopeLines.Add("Align scope sensitivities with hipfire cm/360.");

            view.AdvancedLines.Add("FOV: 60–75.");
            view.AdvancedLines.Add("Mouse Acceleration: Off.");

            view.GameTipsLines.Add("Tarkov punishes over‑corrections; prioritize control.");

            return view;
        }
    }
}
