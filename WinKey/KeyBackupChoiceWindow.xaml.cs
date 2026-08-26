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

    public KeyBackupChoiceWindow(bool installedKeyAvailable, bool oemKeyAvailable)
    {
        InitializeComponent();

        InstalledKeyOption.IsEnabled = installedKeyAvailable;
        OemKeyOption.IsEnabled = oemKeyAvailable;

        if (installedKeyAvailable)
        {
            InstalledKeyOption.IsChecked = true;
        }
        else if (oemKeyAvailable)
        {
            OemKeyOption.IsChecked = true;
        }
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (OemKeyOption.IsChecked == true && OemKeyOption.IsEnabled)
        {
            SelectedChoice = KeyChoice.Oem;
        }
        else if (InstalledKeyOption.IsChecked == true && InstalledKeyOption.IsEnabled)
        {
            SelectedChoice = KeyChoice.Installed;
        }
        else
        {
            SelectedChoice = KeyChoice.None;
        }

        DialogResult = SelectedChoice != KeyChoice.None;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedChoice = KeyChoice.None;
        DialogResult = false;
    }
}
