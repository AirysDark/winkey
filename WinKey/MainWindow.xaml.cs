using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WinKey.Services;

namespace WinKey;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string[] DriverInstallers =
    [
        "HP Privacy Settings.exe",
        "HP Power Manager.exe",
        "HP MAC Address Manager.exe",
        "System Default Settings.exe",
        "HP Connection Optimizer.exe",
        "Realtek PCIe Media Card Reader Driver.exe",
        "Intel Bluetooth Driver.exe",
        "Intel WLAN Driver.exe",
        "HP Hotkey Support - UWP.exe",
        "Synaptics (Validity) Fingerprint Sensor Driver.exe",
        "HP Universal Camera Driver.exe",
        "Synaptics Mouse Driver.exe",
        "Intel Video Driver and Control Panel.exe",
        "Intel Chipset Installation Utility and Driver.exe",
        "Intel Management Engine Driver.exe",
        "Intel Serial IO Driver.exe",
        "Intel Dynamic Platform and Thermal Framework Driver.exe",
        "Conexant HD Audio Driver - Coffee Lake.exe",
        "HP USB-C Dock G5 - Firmware.exe",
        "HP USB-C Dock G5 - Audio Driver.exe",
        "HP Elite USB-C Docking Station Driver.exe",
        "HP USB-C Mini Dock - Driver Pack.exe",
        "HP USB-C Universal Docking Station Driver.exe",
        "HP USB 3.0 Port Replicator and USB Travel Dock Driver.exe",
        "Remote HP PC Hardware Diagnostics UEFI.exe",
        "HP Windows Hardware Diagnostics.exe",
        "HP PC Hardware Diagnostics UEFI.exe",
        "HP Firmware Pack (Q85).exe"
    ];

    private ComputerReport? _report;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshReport();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if (e.ClickCount == 2)
                ToggleMaximizeRestore();
            else
                DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshReport();

    private void RefreshReport()
    {
        try
        {
            Cursor = Cursors.Wait;
            _report = SystemInfoService.GetReport();

            WindowsInfoBox.Text = _report.WindowsSection;
            HardwareInfoBox.Text = _report.HardwareSection;
            DriversInfoBox.Text = _report.DriversSection;
            NetworkInfoBox.Text = _report.NetworkSection;
            ReportBox.Text = _report.FullText;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "WinKey error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Cursor = null;
        }
    }

    private async void InstallDrivers_Click(object sender, RoutedEventArgs e)
    {
        string driverFolder = Path.Combine(AppContext.BaseDirectory, "hp-drivers");

        if (!Directory.Exists(driverFolder))
        {
            MessageBox.Show(
                $"The driver folder was not found:\n\n{driverFolder}\n\nCreate an hp-drivers folder next to WinKey.exe and place the HP driver installers inside it.",
                "WinKey - Driver Folder Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        int foundCount = DriverInstallers.Count(name => File.Exists(Path.Combine(driverFolder, name)));
        int missingCount = DriverInstallers.Length - foundCount;

        if (foundCount == 0)
        {
            MessageBox.Show(
                "No matching HP driver installers were found in the hp-drivers folder.",
                "WinKey - No Drivers Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            $"WinKey found {foundCount} driver installer(s).\n" +
            $"{missingCount} listed installer(s) are missing and will be skipped.\n\n" +
            "Each installer will start one at a time. WinKey will wait until the current installer completely exits before starting the next one.\n\n" +
            "Do you want to begin?",
            "Install HP Drivers",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        InstallDriversButton.IsEnabled = false;
        DriverInstallStatus.Text = "Preparing driver installation...";

        var log = new StringBuilder();
        log.AppendLine($"HP driver installation started: {DateTime.Now:G}");
        log.AppendLine($"Driver folder: {driverFolder}");
        log.AppendLine();

        try
        {
            for (int i = 0; i < DriverInstallers.Length; i++)
            {
                string installerName = DriverInstallers[i];
                string installerPath = Path.Combine(driverFolder, installerName);

                if (!File.Exists(installerPath))
                {
                    log.AppendLine($"SKIPPED - Missing: {installerName}");
                    continue;
                }

                DriverInstallStatus.Text = $"Installing {i + 1}/{DriverInstallers.Length}: {installerName}";
                log.AppendLine($"STARTING - {installerName}");

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = installerPath,
                        WorkingDirectory = driverFolder,
                        UseShellExecute = true
                    };

                    using Process? process = Process.Start(startInfo);
                    if (process == null)
                    {
                        log.AppendLine($"FAILED - Could not start: {installerName}");
                        continue;
                    }

                    await process.WaitForExitAsync();
                    log.AppendLine($"FINISHED - {installerName} (Exit code: {process.ExitCode})");
                }
                catch (Exception ex)
                {
                    log.AppendLine($"FAILED - {installerName}: {ex.Message}");
                }
            }

            log.AppendLine();
            log.AppendLine($"Driver installation sequence finished: {DateTime.Now:G}");
            DriversInfoBox.Text = log + Environment.NewLine + DriversInfoBox.Text;
            DriverInstallStatus.Text = "Driver installation sequence finished.";

            MessageBox.Show(
                "The driver installation sequence has finished. Check the Drivers tab for the installation log.",
                "WinKey - Drivers Finished",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            InstallDriversButton.IsEnabled = true;
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
