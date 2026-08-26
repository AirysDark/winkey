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

    public KeyChoice SelectedChoice { get; private set; } = KeyChoice.Installed;

    public KeyBackupChoiceWindow()
    {
        InitializeComponent();
        InstalledKeyOption.IsEnabled = true;
        OemKeyOption.IsEnabled = true;
        InstalledKeyOption.IsChecked = true;
    }

    private void InstalledKeyOption_Click(object sender, RoutedEventArgs e)
    {
        InstalledKeyOption.IsChecked = true;
        OemKeyOption.IsChecked = false;
    }

    private void OemKeyOption_Click(object sender, RoutedEventArgs e)
    {
        OemKeyOption.IsChecked = true;
        InstalledKeyOption.IsChecked = false;
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
