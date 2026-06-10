using CleanAimTracker.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CleanAimTracker.Windows
{
    public partial class OnboardingCalibrationWindow : Window
    {
        private int _page = 0;
        private const int PageCount = 5;

        private readonly StackPanel[] _pages;
        private readonly System.Windows.Shapes.Ellipse[] _dots;

        private static readonly string[] ButtonLabels =
        {
            "Get started",      // 0 → 1
            "Begin calibration", // 1 → 2
            "Start calibration", // 2 → 3 (marks complete, closes)
            "See my drill",     // 3 → 4
            "Let's go"          // 4 → close
        };

        public OnboardingCalibrationWindow()
        {
            InitializeComponent();
            _pages = new[] { PageWelcome, PageCalibrationBrief, PageCalibrationRunning, PageFirstInsight, PageFirstDrill };
            _dots  = new[] { Dot0, Dot1, Dot2, Dot3, Dot4 };
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var settings = SettingsService.Load();
            ThemeService.ApplyTheme(settings.ThemeMode ?? "Dark");
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_page == 2)
            {
                // CalibrationRunning → mark calibration acknowledged and close
                MarkCalibrationAcknowledged();
                Close();
                return;
            }

            if (_page >= PageCount - 1)
            {
                MarkCalibrationComplete();
                Close();
                return;
            }

            _page++;
            RefreshPage();
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsService.Load();
            settings.OnboardingSkipped = true;
            SettingsService.Save(settings);
            Close();
        }

        private void RefreshPage()
        {
            for (int i = 0; i < PageCount; i++)
            {
                _pages[i].Visibility = i == _page ? Visibility.Visible : Visibility.Collapsed;
                _dots[i].Fill = i == _page
                    ? (Brush)FindResource("AccentBrush")
                    : (Brush)FindResource("MutedText");
            }

            NextBtn.Content = ButtonLabels[_page];

            // Hide skip button on final pages
            SkipBtn.Visibility = _page >= 3 ? Visibility.Collapsed : Visibility.Visible;
        }

        private static void MarkCalibrationAcknowledged()
        {
            // User has been shown calibration instructions — mark skip so we don't repeat
            var settings = SettingsService.Load();
            settings.OnboardingSkipped = true;
            SettingsService.Save(settings);
        }

        private static void MarkCalibrationComplete()
        {
            var settings = SettingsService.Load();
            settings.CalibrationComplete = true;
            SettingsService.Save(settings);
        }
    }
}
