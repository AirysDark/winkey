using System.Windows;

namespace WinKey;

public partial class KeyBackupChoiceWindow : Window
{
    public enum KeyChoice
    {
        None,
        Installed,
        Oem
    }

    public KeyChoice SelectedChoice { get; private set; } = KeyChoice.None;

    public KeyBackupChoiceWindow()
    {
        InitializeComponent();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        SelectedChoice = OemKeyOption.IsChecked == true
            ? KeyChoice.Oem
            : KeyChoice.Installed;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedChoice = KeyChoice.None;
        DialogResult = false;
    }
}
