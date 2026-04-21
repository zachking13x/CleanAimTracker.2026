using System.Diagnostics;
using System.Windows;

namespace CleanAimTracker
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            // Show assembly version if available
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = ver != null ? $"Version {ver.Major}.{ver.Minor}.{ver.Build}" : "Version 1.0.0";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
