using System.IO;
using System.Windows;

namespace WAload
{
    public partial class RenameDialog : Window
    {
        public string NewFileName { get; private set; } = string.Empty;
        public bool WasRenamed { get; private set; } = false;

        public RenameDialog(string currentFileName)
        {
            InitializeComponent();
            
            // Display current filename
            CurrentFileNameText.Text = currentFileName;
            
            // Pre-fill the text box with current name (without extension for easy editing)
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(currentFileName);
            var extension = Path.GetExtension(currentFileName);
            
            NewFileNameTextBox.Text = nameWithoutExtension;
            
            // Select all text for easy replacement
            NewFileNameTextBox.Focus();
            NewFileNameTextBox.SelectAll();
            
            // Store the extension for later use
            Tag = extension;
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            var newNameWithoutExtension = NewFileNameTextBox.Text?.Trim();
            
            if (string.IsNullOrWhiteSpace(newNameWithoutExtension))
            {
                System.Windows.MessageBox.Show(
                    "Please enter a valid filename.",
                    "Invalid Name",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                NewFileNameTextBox.Focus();
                return;
            }
            
            // Check for invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                if (newNameWithoutExtension.Contains(invalidChar))
                {
                    System.Windows.MessageBox.Show(
                        $"The filename contains invalid character: '{invalidChar}'\n\nPlease remove invalid characters and try again.",
                        "Invalid Characters",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    NewFileNameTextBox.Focus();
                    return;
                }
            }
            
            // Add back the extension
            var extension = Tag as string ?? "";
            NewFileName = newNameWithoutExtension + extension;
            WasRenamed = true;
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            WasRenamed = false;
            DialogResult = false;
            Close();
        }

        // Handle Enter key in textbox
        private void NewFileNameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                RenameButton_Click(sender, e);
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                CancelButton_Click(sender, e);
            }
        }

        // Override to handle key events
        protected override void OnSourceInitialized(System.EventArgs e)
        {
            base.OnSourceInitialized(e);
            NewFileNameTextBox.KeyDown += NewFileNameTextBox_KeyDown;
        }
    }
}
