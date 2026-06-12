using System.Windows;
using System.Windows.Input;

namespace CleanAimTracker.Windows
{
    /// <summary>
    /// TASK-4.2: styled post-session reminder prompt. ShowDialog() returns
    /// true when the user wants the tomorrow reminder scheduled.
    /// </summary>
    public partial class ReminderPromptWindow : Window
    {
        public ReminderPromptWindow(string title, string message)
        {
            InitializeComponent();
            TitleText.Text   = title;
            MessageText.Text = message;
            MouseLeftButtonDown += (_, e) => { if (e.ChangedButton == MouseButton.Left) DragMove(); };
        }

        private void RemindMe_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void NotNow_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
