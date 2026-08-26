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
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key?.GetValue("DigitalProductId") is not byte[] digitalProductId)
                return "Not recoverable";

            string decodedKey = DecodeProductKey(digitalProductId);
            if (!IsProductKey(decodedKey))
                return "Not recoverable";

            // DigitalProductId can decode to a generic/default Windows key that is
            // not the key currently licensed on this PC. Never present that as a
            // usable backup unless it matches the active Windows licence.
            string activePartialKey = GetActiveWindowsPartialProductKey();
            if (string.IsNullOrWhiteSpace(activePartialKey))
                return "Not recoverable";

            return decodedKey.EndsWith(activePartialKey, StringComparison.OrdinalIgnoreCase)
                ? decodedKey
                : "Not recoverable";
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
                var value = item["OA3xOriginalProductKey"]?.ToString()?.Trim();
                if (IsProductKey(value))
                    return value!;
            }
        }
        catch
        {
        }

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
                    licenseStatus == 1 &&
                    partialKey.Length == 5)
                {
                    return partialKey;
                }
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static bool IsProductKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        System.Text.RegularExpressions.Regex.IsMatch(key.Trim(), @"(?i)^[A-Z0-9]{5}(?:-[A-Z0-9]{5}){4}$");

    private static string DecodeProductKey(byte[] digitalProductId)
    {
        const string chars = "BCDFGHJKMPQRTVWXY2346789";
        const int keyStart = 52;
        const int keyLength = 15;

        if (digitalProductId.Length < keyStart + keyLength || digitalProductId.Length <= 66)
            return "Not found";

        var keyBytes = digitalProductId.Skip(keyStart).Take(keyLength).ToArray();
        bool isWin8OrNewer = ((digitalProductId[66] / 6) & 1) != 0;

        if (isWin8OrNewer)
            digitalProductId[66] = (byte)((digitalProductId[66] & 0xF7) | ((2 & 4) * 4));

        var decodedCharacters = new char[25];
        int last = 0;

        for (int i = 24; i >= 0; i--)
        {
            int current = 0;

            for (int j = 14; j >= 0; j--)
            {
                current = current * 256 + keyBytes[j];
                keyBytes[j] = (byte)(current / 24);
                current %= 24;
            }

            decodedCharacters[i] = chars[current];
            last = current;
        }

        string decoded = new(decodedCharacters);

        if (isWin8OrNewer)
            decoded = decoded.Remove(0, 1).Insert(Math.Clamp(last, 0, 24), "N");

        if (decoded.Length != 25)
            return "Not found";

        return string.Join("-", Enumerable.Range(0, 5)
            .Select(group => decoded.Substring(group * 5, 5)));
    }
}
