using Microsoft.Win32;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;

namespace WinKey.Services;

public sealed record ComputerReport(
    string GeneratedAt,
    string ComputerName,
    string WindowsEdition,
    string WindowsVersion,
    string WindowsBuild,
    string ActivationStatus,
    string ProductKey,
    string OemKey,
    string WindowsSection,
    string HardwareSection,
    string NetworkSection,
    string FullText);

public sealed class SystemInfoService
{
    private const string CurrentVersionPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    private static readonly string[] ComputerSystemFields =
        ["Manufacturer", "Model", "TotalPhysicalMemory"];
    private static readonly string[] ProcessorFields =
        ["Name", "NumberOfCores", "NumberOfLogicalProcessors"];
    private static readonly string[] BaseBoardFields =
        ["Manufacturer", "Product", "SerialNumber"];
    private static readonly string[] BiosFields =
        ["Manufacturer", "SMBIOSBIOSVersion", "SerialNumber"];
    private static readonly string[] VideoControllerFields =
        ["Name", "DriverVersion"];
    private static readonly string[] DiskDriveFields =
        ["Model", "Size", "SerialNumber"];

    public static ComputerReport GetReport()
    {
        string computerName = Environment.MachineName;
        string edition = ReadRegistry(CurrentVersionPath, "ProductName");
        string version = ReadRegistry(CurrentVersionPath, "DisplayVersion");
        string build = ReadRegistry(CurrentVersionPath, "CurrentBuild") + "." + ReadRegistry(CurrentVersionPath, "UBR");
        string productKey = ProductKeyService.GetInstalledProductKey();
        string oemKey = ProductKeyService.GetOemProductKey();
        string activation = GetActivationStatus();

        var windows = new StringBuilder();
        windows.AppendLine($"Computer Name : {computerName}");
        windows.AppendLine($"Windows       : {edition}");
        windows.AppendLine($"Version       : {version}");
        windows.AppendLine($"Build         : {build}");
        windows.AppendLine($"Activation    : {activation}");
        windows.AppendLine($"Installed Key : {productKey}");
        windows.AppendLine($"OEM/UEFI Key  : {oemKey}");
        windows.AppendLine("Product ID    : " + ReadRegistry(CurrentVersionPath, "ProductId"));
        windows.AppendLine($"Install Date  : {GetInstallDate()}");

        string hardware = GetHardwareInfo();
        string network = GetNetworkInfo();
        string full = string.Join(Environment.NewLine,
            "WinKey Report",
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            string.Empty,
            "=== WINDOWS & LICENCE ===",
            windows.ToString(),
            "=== HARDWARE ===",
            hardware,
            "=== NETWORK ===",
            network);

        return new ComputerReport(
            DateTime.Now.ToString("O"),
            computerName,
            edition,
            version,
            build,
            activation,
            productKey,
            oemKey,
            windows.ToString(),
            hardware,
            network,
            full);
    }

    private static string GetHardwareInfo()
    {
        var sb = new StringBuilder();

        AppendWmi(sb, "Computer System",
            "SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem",
            ComputerSystemFields);
        AppendWmi(sb, "CPU",
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor",
            ProcessorFields);
        AppendWmi(sb, "Motherboard",
            "SELECT Manufacturer, Product, SerialNumber FROM Win32_BaseBoard",
            BaseBoardFields);
        AppendWmi(sb, "BIOS",
            "SELECT Manufacturer, SMBIOSBIOSVersion, SerialNumber FROM Win32_BIOS",
            BiosFields);
        AppendWmi(sb, "GPU",
            "SELECT Name, DriverVersion FROM Win32_VideoController",
            VideoControllerFields);
        AppendWmi(sb, "Disk",
            "SELECT Model, Size, SerialNumber FROM Win32_DiskDrive",
            DiskDriveFields);

        return sb.ToString();
    }

    private static string GetNetworkInfo()
    {
        var sb = new StringBuilder();

        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()
                     .Where(x => x.OperationalStatus == OperationalStatus.Up))
        {
            sb.AppendLine(adapter.Name);
            sb.AppendLine($"  Type: {adapter.NetworkInterfaceType}");
            sb.AppendLine($"  MAC : {adapter.GetPhysicalAddress()}");

            foreach (var ip in adapter.GetIPProperties().UnicastAddresses)
                sb.AppendLine($"  IP  : {ip.Address}");
        }

        return sb.ToString();
    }

    private static string GetActivationStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL");

            var statuses = new List<int>();

            foreach (ManagementBaseObject item in searcher.Get())
            {
                statuses.Add(Convert.ToInt32(item["LicenseStatus"] ?? 0));
            }

            return statuses.Contains(1)
                ? "Activated"
                : "Not activated or activation state unavailable";
        }
        catch
        {
            return "Unable to determine";
        }
    }

    private static string GetInstallDate()
    {
        var raw = ReadRegistry(CurrentVersionPath, "InstallDate");

        return long.TryParse(raw, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime.ToString()
            : raw;
    }

    private static string ReadRegistry(string path, string name)
    {
        try
        {
            string registryPath = string.Concat("HKEY_LOCAL_MACHINE", "\\", path);
            return Registry.GetValue(registryPath, name, "Unknown")?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unavailable";
        }
    }

    private static void AppendWmi(StringBuilder sb, string title, string query, IReadOnlyList<string> fields)
    {
        sb.AppendLine($"[{title}]");

        try
        {
            using var searcher = new ManagementObjectSearcher(query);

            foreach (ManagementBaseObject item in searcher.Get())
            {
                foreach (string field in fields)
                {
                    string value = item[field]?.ToString() ?? "Unknown";

                    if (field is "TotalPhysicalMemory" or "Size" &&
                        long.TryParse(value, out long bytes))
                    {
                        value = $"{bytes / 1024d / 1024d / 1024d:N2} GB";
                    }

                    sb.AppendLine($"{field}: {value}");
                }

                sb.AppendLine();
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Unavailable: {ex.Message}");
        }
    }
}
