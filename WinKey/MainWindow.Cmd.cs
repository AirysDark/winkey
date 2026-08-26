using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinKey;

public partial class MainWindow
{
    private bool _cmdButtonAdded;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (_cmdButtonAdded) return;

        StackPanel? actionsPanel = FindActionsPanel(this);
        if (actionsPanel == null) return;

        var cmdButton = new Button
        {
            Content = "CMD",
            Width = 145,
            MinHeight = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 0),
            ToolTip = "Run MAS_AIO.cmd"
        };

        if (TryFindResource("ADButton") is Style style)
        {
            cmdButton.Style = style;
        }

        cmdButton.Click += Cmd_Click;
        actionsPanel.Children.Add(cmdButton);
        _cmdButtonAdded = true;
    }

    private static StackPanel? FindActionsPanel(DependencyObject root)
    {
        if (root is StackPanel panel && panel.Children.Count > 0 && panel.Children[0] is TextBlock title && title.Text == "Actions")
        {
            return panel;
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            StackPanel? found = FindActionsPanel(VisualTreeHelper.GetChild(root, i));
            if (found != null) return found;
        }

        return null;
    }

    private void Cmd_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string scriptPath = Path.Combine(AppContext.BaseDirectory, "MAS_AIO.cmd");

            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("MAS_AIO.cmd was not found next to WinKey.exe.", scriptPath);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                Arguments = $"/c call \"{scriptPath}\"",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "CMD Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
