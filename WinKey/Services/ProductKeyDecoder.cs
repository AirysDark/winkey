namespace WinKey.Services;

public sealed record ProductKeyDecodeResult(string InstalledKey, bool InstalledKeyValid);

/// <summary>
/// Decodes the Windows product key stored in DigitalProductId.
/// The Windows 8/10/11 algorithm is implemented here using the standard
/// base-24 conversion plus the Windows 8+ N-insertion transformation.
/// </summary>
public static class ProductKeyDecoder
{
    private const string KeyChars = "BCDFGHJKMPQRTVWXY2346789";
    private const int KeyStart = 52;
    private const int KeyLength = 15;

    public static ProductKeyDecodeResult Decode(byte[] digitalProductId)
    {
        if (digitalProductId is null)
            throw new ArgumentNullException(nameof(digitalProductId));

        string key = DecodeWindows8AndLater(digitalProductId);
        return new ProductKeyDecodeResult(key, IsProductKey(key));
    }

    public static bool IsProductKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        System.Text.RegularExpressions.Regex.IsMatch(
            key.Trim(),
            @"(?i)^[A-Z0-9]{5}(?:-[A-Z0-9]{5}){4}$");

    private static string DecodeWindows8AndLater(byte[] digitalProductId)
    {
        if (digitalProductId.Length < KeyStart + KeyLength || digitalProductId.Length <= 66)
            return string.Empty;

        // Work on a private copy. The Windows 8+ algorithm modifies byte 66
        // before decoding and must not mutate the registry buffer owned by the caller.
        byte[] buffer = (byte[])digitalProductId.Clone();
        int isWindows8OrLater = (buffer[66] / 6) & 1;
        buffer[66] = (byte)((buffer[66] & 0xF7) | ((isWindows8OrLater & 2) * 4));

        byte[] keyBytes = buffer.Skip(KeyStart).Take(KeyLength).ToArray();
        char[] decoded = new char[25];
        int last = 0;

        for (int i = 24; i >= 0; i--)
        {
            int current = 0;
            for (int j = KeyLength - 1; j >= 0; j--)
            {
                current = (current * 256) + keyBytes[j];
                keyBytes[j] = (byte)(current / 24);
                current %= 24;
            }

            decoded[i] = KeyChars[current];
            last = current;
        }

        string key = new(decoded);

        // Canonical Windows 8+ transformation: remove the first character,
        // insert N at the final base-24 remainder position, then format 5x5.
        if (isWindows8OrLater == 1)
            key = key.Substring(1, last) + "N" + key.Substring(last + 1);

        if (key.Length != 25)
            return string.Empty;

        return string.Join("-", Enumerable.Range(0, 5)
            .Select(group => key.Substring(group * 5, 5)));
    }
}
