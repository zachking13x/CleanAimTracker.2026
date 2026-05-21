using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CleanAimTracker.Windows
{
    /// <summary>
    /// A parsed setting row — label on the left, value on the right.
    /// Built from translator strings like "X-Axis Sensitivity: 40.0%".
    /// </summary>
    public sealed class SettingRow
    {
        public string Label    { get; }
        public string Value    { get; }
        public bool   HasValue => !string.IsNullOrEmpty(Value);

        public SettingRow(string line)
        {
            int idx = line.IndexOf(": ");
            if (idx >= 0 && idx < line.Length - 2)
            {
                Label = line[..idx];
                Value = line[(idx + 2)..];
            }
            else
            {
                Label = line.TrimStart('•', ' ', '→');
                Value = string.Empty;
            }
        }
    }

    public partial class RecommendationWindow : Window, INotifyPropertyChanged
    {
        private readonly SensitivityRecommendation _rec;

        public ObservableCollection<IGameSensitivityTranslator> GameOptions { get; }
            = new ObservableCollection<IGameSensitivityTranslator>();

        private IGameSensitivityTranslator _selectedGame;
        public IGameSensitivityTranslator SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (_selectedGame != value)
                {
                    _selectedGame = value;
                    OnPropertyChanged(nameof(SelectedGame));
                    UpdateGameView();
                }
            }
        }

        private List<string> _gameSettingsLines = new();
        public List<string> GameSettingsLines
        {
            get => _gameSettingsLines;
            private set { _gameSettingsLines = value; OnPropertyChanged(nameof(GameSettingsLines)); }
        }

        private List<string> _adsScopeLines = new();
        public List<string> AdsScopeLines
        {
            get => _adsScopeLines;
            private set { _adsScopeLines = value; OnPropertyChanged(nameof(AdsScopeLines)); }
        }

        private List<string> _advancedLines = new();
        public List<string> AdvancedLines
        {
            get => _advancedLines;
            private set { _advancedLines = value; OnPropertyChanged(nameof(AdvancedLines)); }
        }

        private List<string> _gameTipsLines = new();
        public List<string> GameTipsLines
        {
            get => _gameTipsLines;
            private set { _gameTipsLines = value; OnPropertyChanged(nameof(GameTipsLines)); }
        }

        // ── Parsed row collections for styled label/value display ──────────
        private List<SettingRow> _gameSettingRows = new();
        public List<SettingRow> GameSettingRows
        {
            get => _gameSettingRows;
            private set { _gameSettingRows = value; OnPropertyChanged(nameof(GameSettingRows)); }
        }

        private List<SettingRow> _adsScopeRows = new();
        public List<SettingRow> AdsScopeRows
        {
            get => _adsScopeRows;
            private set { _adsScopeRows = value; OnPropertyChanged(nameof(AdsScopeRows)); }
        }

        private List<SettingRow> _advancedRows = new();
        public List<SettingRow> AdvancedRows
        {
            get => _advancedRows;
            private set { _advancedRows = value; OnPropertyChanged(nameof(AdvancedRows)); }
        }

        public bool HasAdsScopeSection => AdsScopeRows.Count > 0;

        public double ConfidenceRatio => _rec.Confidence / 100.0;

        public string RecommendedSensitivityRange =>
            $"Range: {_rec.RecommendedSensitivityMin:F4} – {_rec.RecommendedSensitivityMax:F4}";

        /// <summary>
        /// The single plain instruction shown directly below the hero number.
        /// Updates whenever the selected game changes.
        /// Example: "In Fortnite, change your sensitivity from 0.5000 to 0.4252."
        /// </summary>
        public string PlainActionLine
        {
            get
            {
                string game = _selectedGame?.GameName ?? "your game";
                return $"In {game}, change your sensitivity from " +
                       $"{_rec.CurrentSensitivity:F4} to {_rec.RecommendedSensitivity:F4}.";
            }
        }

        /// <summary>
        /// Plain-English description of what the cm/360 value means to the user,
        /// without percentages or directional jargon.
        /// Example: "Your mouse will travel 38.4 cm per full turn — up from 32.7 cm.
        ///           This feels slower but gives you more precision."
        /// </summary>
        public string Cm360PlainDesc
        {
            get
            {
                double rec = _rec.RecommendedCm360;
                double cur = _rec.CurrentCm360;

                if (Math.Abs(rec - cur) < 0.1)
                    return $"Your mouse will travel {rec:F1} cm per full turn — " +
                            "the same as your current setup.";

                bool bigger        = rec > cur;
                string direction   = bigger ? "up"      : "down";
                string feel        = bigger ? "slower"  : "snappier";
                string tradeoff    = bigger ? "more precision" : "a faster feel";

                return $"Your mouse will travel {rec:F1} cm per full turn — " +
                       $"{direction} from {cur:F1} cm. " +
                       $"This feels {feel} but gives you {tradeoff}.";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public RecommendationWindow(SensitivityRecommendation rec)
        {
            InitializeComponent();
            _rec = rec ?? throw new ArgumentNullException(nameof(rec));

            DataContext = rec;

            GameOptions.Add(new FortniteTranslator());
            GameOptions.Add(new ValorantTranslator());
            GameOptions.Add(new ApexTranslator());
            GameOptions.Add(new Cs2Translator());
            GameOptions.Add(new OverwatchTranslator());
            GameOptions.Add(new R6Translator());
            GameOptions.Add(new WarzoneTranslator());
            GameOptions.Add(new PubgTranslator());
            GameOptions.Add(new HaloTranslator());
            GameOptions.Add(new TarkovTranslator());

            GameCombo.SelectedIndex = 0;
            SelectedGame = GameOptions[0];
        }

        private void GameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameCombo.SelectedItem is IGameSensitivityTranslator translator)
                SelectedGame = translator;
        }

        private void UpdateGameView()
        {
            if (SelectedGame == null)
            {
                GameSettingsLines = new();
                AdsScopeLines     = new();
                AdvancedLines     = new();
                GameTipsLines     = new();
                GameSettingRows   = new();
                AdsScopeRows      = new();
                AdvancedRows      = new();
                OnPropertyChanged(nameof(HasAdsScopeSection));
                return;
            }

            var view = SelectedGame.Translate(_rec);

            // Keep raw lists for backward-compat bindings
            GameSettingsLines = view.GameSettingsLines;
            AdsScopeLines     = view.AdsScopeLines;
            AdvancedLines     = view.AdvancedLines;
            GameTipsLines     = view.GameTipsLines;

            // Build parsed row collections for styled label/value display
            GameSettingRows = view.GameSettingsLines
                .Select(l => new SettingRow(l)).ToList();
            AdsScopeRows    = view.AdsScopeLines
                .Select(l => new SettingRow(l)).ToList();
            AdvancedRows    = view.AdvancedLines
                .Select(l => new SettingRow(l)).ToList();

            OnPropertyChanged(nameof(HasAdsScopeSection));
            OnPropertyChanged(nameof(PlainActionLine));
        }

        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                return;

            DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        private void CopySettings_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();

            sb.AppendLine("🎯 Recommended Settings");
            sb.AppendLine($"• DPI: {_rec.RecommendedDPI}");
            sb.AppendLine($"• Sensitivity: {_rec.RecommendedSensitivity:F4}");
            sb.AppendLine($"• Sensitivity Range: {_rec.RecommendedSensitivityMin:F4} – {_rec.RecommendedSensitivityMax:F4}");
            sb.AppendLine($"• cm/360: {_rec.RecommendedCm360:F2}");
            sb.AppendLine();
            sb.AppendLine("Confidence: " + _rec.Confidence + "%");
            sb.AppendLine();
            sb.AppendLine("Explanation:");
            sb.AppendLine(_rec.Explanation);

            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Settings copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CopyComparison_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();

            sb.AppendLine("📊 Current vs Recommended");
            sb.AppendLine();
            sb.AppendLine("Current:");
            sb.AppendLine($"• DPI: {_rec.CurrentDPI}");
            sb.AppendLine($"• Sensitivity: {_rec.CurrentSensitivity:F4}");
            sb.AppendLine($"• cm/360: {_rec.CurrentCm360:F2}");
            sb.AppendLine();
            sb.AppendLine("Recommended:");
            sb.AppendLine($"• DPI: {_rec.RecommendedDPI}");
            sb.AppendLine($"• Sensitivity: {_rec.RecommendedSensitivity:F4}");
            sb.AppendLine($"• cm/360: {_rec.RecommendedCm360:F2}");
            sb.AppendLine();
            sb.AppendLine("Verdicts:");
            sb.AppendLine($"• DPI: {_rec.DpiVerdict}");
            sb.AppendLine($"• Sensitivity: {_rec.SensVerdict}");
            sb.AppendLine($"• cm/360: {_rec.Cm360Verdict}");
            sb.AppendLine();
            sb.AppendLine("Overall:");
            sb.AppendLine(_rec.OverallVerdict);

            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Comparison copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
