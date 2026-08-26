using Microsoft.Win32;
using System.Management;

namespace WinKey.Services;

public static class ProductKeyService
{
    private const string WindowsLicensingApplicationId = "55c92734-d682-4d71-983e-d6ec3f16059f";

    public static string GetInstalledProductKey()
    {
        try
        {
            ProductKeyDecodeResult result = DecodeInstalledProductKeyCandidates();
            string activePartial = GetActiveWindowsPartialProductKey();

            if (!string.IsNullOrWhiteSpace(activePartial))
            {
                if (result.ModernKeyValid && result.ModernKey.EndsWith(activePartial, StringComparison.OrdinalIgnoreCase))
                    return result.ModernKey;

                if (result.LegacyKeyValid && result.LegacyKey.EndsWith(activePartial, StringComparison.OrdinalIgnoreCase))
                    return result.LegacyKey;
            }

            return result.ModernKeyValid ? result.ModernKey
                : result.LegacyKeyValid ? result.LegacyKey
                : "Not recoverable";
        }
        catch
        {
            return "Unavailable";
        }
    }

    // Returns both decoder candidates without silently replacing either result.
    // This is used for debugging and lets the UI/backup flow explicitly choose
    // which decoder output should be displayed or saved.
    public static ProductKeyDecodeResult DecodeInstalledProductKeyCandidates()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        if (key?.GetValue("DigitalProductId") is not byte[] digitalProductId)
            return new ProductKeyDecodeResult(string.Empty, string.Empty, false, false);

        return ProductKeyDecoder.Decode(digitalProductId);
    }

    // Backup must save this raw decoder output, not a displayed or cached key.
    // The modern candidate is intentionally returned directly so there is no
    // licence-verification or UI substitution in this path.
    public static string GetDecodedInstalledProductKey()
    {
        try
        {
            ProductKeyDecodeResult result = DecodeInstalledProductKeyCandidates();
            return result.ModernKeyValid ? result.ModernKey : "Not recoverable";
        }
        catch
        {
            return "Unavailable";
        }
    }

    public static string GetOemProductKey()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT OA3xOriginalProductKey FROM SoftwareLicensingService");
            foreach (ManagementObject item in searcher.Get())
            {
                string? value = item["OA3xOriginalProductKey"]?.ToString()?.Trim();
                if (ProductKeyDecoder.IsProductKey(value))
                    return value!;
            }
        }
        catch { }

        return "No embedded OEM/UEFI key found";
    }

    public static string GetActiveWindowsPartialProductKey()
    {
        try
        {
            const string query = "SELECT ApplicationID, LicenseStatus, PartialProductKey FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL";
            using var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject item in searcher.Get())
            {
                string applicationId = item["ApplicationID"]?.ToString() ?? string.Empty;
                int licenseStatus = item["LicenseStatus"] is null ? 0 : Convert.ToInt32(item["LicenseStatus"]);
                string partialKey = item["PartialProductKey"]?.ToString()?.Trim() ?? string.Empty;

                if (applicationId.Equals(WindowsLicensingApplicationId, StringComparison.OrdinalIgnoreCase) &&
                    licenseStatus == 1 && partialKey.Length == 5)
                {
                    return partialKey;
                }
            }
        }
        catch { }

        return string.Empty;
    }
}
