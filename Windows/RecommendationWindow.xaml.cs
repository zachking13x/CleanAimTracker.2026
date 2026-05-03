using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CleanAimTracker.Windows
{
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

        public bool HasAdsScopeSection => AdsScopeLines.Count > 0;

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
                AdsScopeLines = new();
                AdvancedLines = new();
                GameTipsLines = new();
                OnPropertyChanged(nameof(HasAdsScopeSection));
                return;
            }

            var view = SelectedGame.Translate(_rec);

            GameSettingsLines = view.GameSettingsLines;
            AdsScopeLines = view.AdsScopeLines;
            AdvancedLines = view.AdvancedLines;
            GameTipsLines = view.GameTipsLines;

            OnPropertyChanged(nameof(HasAdsScopeSection));
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
