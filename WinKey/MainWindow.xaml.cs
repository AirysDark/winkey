using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using WinKey.Services;

namespace WinKey;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private ComputerReport? _report;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshReport();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshReport();

    private void RefreshReport()
    {
        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;

            _report = SystemInfoService.GetReport();
            WindowsInfoBox.Text = _report.WindowsSection;
            HardwareInfoBox.Text = _report.HardwareSection;
            NetworkInfoBox.Text = _report.NetworkSection;
            ReportBox.Text = _report.FullText;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "WinKey error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Cursor = null;
        }
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        if (_report != null)
            Clipboard.SetText(_report.FullText);
    }

    private void ExportTxt_Click(object sender, RoutedEventArgs e)
    {
        if (_report == null)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "Text report (*.txt)|*.txt",
            FileName = $"WinKey-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog() == true)
            File.WriteAllText(dialog.FileName, _report.FullText, Encoding.UTF8);
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_report == null)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "JSON report (*.json)|*.json",
            FileName = $"WinKey-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            string json = JsonSerializer.Serialize(_report, JsonOptions);
            File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
        }
    }
}
