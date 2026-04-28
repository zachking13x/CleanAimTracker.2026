using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class GlossaryWindow : Window
    {
        public GlossaryWindow()
        {
            InitializeComponent();

            GlossaryList.ItemsSource = new[]
            {
                new { Term="cm/360", Definition="How far you must move your mouse to rotate 360° in-game." },
                new { Term="DPI", Definition="Mouse hardware sensitivity (counts per inch)." },
                new { Term="Sensitivity", Definition="In-game multiplier applied to DPI." },
                new { Term="Flick Count", Definition="Number of rapid, high-speed directional movements." },
                new { Term="Small Flicks", Definition="Short, controlled flicks — usually good." },
                new { Term="Large Flicks", Definition="Big, fast flicks — often inaccurate." },
                new { Term="Smoothness", Definition="How stable your movement angles are." },
                new { Term="Movement Consistency", Definition="How similar your movement distances are." },
                new { Term="Correction Sharpness", Definition="How aggressively you change speed to correct aim." },
                new { Term="Jitter", Definition="Tiny unintended micro-movements." },
                new { Term="Idle Percentage", Definition="How much of the session you weren’t moving the mouse." },
                new { Term="Peak Velocity", Definition="Fastest movement speed recorded." },
                new { Term="Peak Acceleration", Definition="How quickly your speed changes." },
                new { Term="Overall Quality Score", Definition="Combined score of smoothness, consistency, and corrections." }
            };
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
