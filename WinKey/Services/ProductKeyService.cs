using Microsoft.Win32;
using System.Management;

namespace WinKey.Services;

public static class ProductKeyService
{
    private const string WindowsLicensingApplicationId = "55c92734-d682-4d71-983e-d6ec3f16059f";
    private const string KeyChars = "BCDFGHJKMPQRTVWXY2346789";

    public static string GetInstalledProductKey()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key?.GetValue("DigitalProductId") is not byte[] digitalProductId)
                return "Not recoverable";

            string modernKey = DecodeProductKey(digitalProductId, true);
            string legacyKey = DecodeProductKey(digitalProductId, false);
            string activePartial = GetActiveWindowsPartialProductKey();

            if (!string.IsNullOrWhiteSpace(activePartial))
            {
                if (modernKey.EndsWith(activePartial, StringComparison.OrdinalIgnoreCase))
                    return modernKey;
                if (legacyKey.EndsWith(activePartial, StringComparison.OrdinalIgnoreCase))
                    return legacyKey;
            }

            return IsProductKey(modernKey) ? modernKey
                : IsProductKey(legacyKey) ? legacyKey
                : "Not recoverable";
        }
        catch
        {
            return "Unavailable";
        }
    }

    // This is intentionally separate from GetInstalledProductKey().
    // Backup must save the decoder output itself, not the displayed/cached
    // installed key and not a value selected by licence verification logic.
    public static string GetDecodedInstalledProductKey()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key?.GetValue("DigitalProductId") is not byte[] digitalProductId)
                return "Not recoverable";

            string decodedKey = DecodeProductKey(digitalProductId, true);
            if (!IsProductKey(decodedKey))
                decodedKey = DecodeProductKey(digitalProductId, false);

            return IsProductKey(decodedKey) ? decodedKey : "Not recoverable";
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
                if (IsProductKey(value))
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

    private static bool IsProductKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        System.Text.RegularExpressions.Regex.IsMatch(key.Trim(), @"(?i)^[A-Z0-9]{5}(?:-[A-Z0-9]{5}){4}$");

    private static string DecodeProductKey(byte[] digitalProductId, bool useWindows8Algorithm)
    {
        const int keyStart = 52;
        const int keyLength = 15;

        if (digitalProductId.Length < keyStart + keyLength || digitalProductId.Length <= 66)
            return string.Empty;

        byte[] keyBytes = digitalProductId.Skip(keyStart).Take(keyLength).ToArray();
        int last = 0;
        char[] decoded = new char[25];

        for (int i = 24; i >= 0; i--)
        {
            int current = 0;
            for (int j = 14; j >= 0; j--)
            {
                current = current * 256 + keyBytes[j];
                keyBytes[j] = (byte)(current / 24);
                current %= 24;
            }

            decoded[i] = KeyChars[current];
            last = current;
        }

        string result = new(decoded);

        if (useWindows8Algorithm && ((digitalProductId[66] / 6) & 1) == 1)
        {
            result = result.Remove(0, 1).Insert(Math.Clamp(last, 0, 24), "N");
        }

        if (result.Length != 25)
            return string.Empty;

        return string.Join("-", Enumerable.Range(0, 5)
            .Select(group => result.Substring(group * 5, 5)));
    }
}
