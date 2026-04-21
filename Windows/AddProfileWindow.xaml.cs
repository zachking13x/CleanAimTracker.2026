using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class AddProfileWindow : Window
    {
        // ⭐ REQUIRED by MainWindow
        public bool ProfileSaved { get; private set; }

        public AddProfileWindow()
        {
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Add your real save logic here later

            // ⭐ REQUIRED by MainWindow
            ProfileSaved = true;

            this.Close();
        }
    }
}
