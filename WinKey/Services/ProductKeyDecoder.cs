namespace WinKey.Services;

public sealed record ProductKeyDecodeResult(
    string ModernKey,
    string LegacyKey,
    bool ModernKeyValid,
    bool LegacyKeyValid);

public static class ProductKeyDecoder
{
    private const string KeyChars = "BCDFGHJKMPQRTVWXY2346789";
    private const int KeyStart = 52;
    private const int KeyLength = 15;

    public static ProductKeyDecodeResult Decode(byte[] digitalProductId)
    {
        if (digitalProductId is null)
            throw new ArgumentNullException(nameof(digitalProductId));

        string modernKey = DecodeProductKey(digitalProductId, useWindows8Algorithm: true);
        string legacyKey = DecodeProductKey(digitalProductId, useWindows8Algorithm: false);

        return new ProductKeyDecodeResult(
            modernKey,
            legacyKey,
            IsProductKey(modernKey),
            IsProductKey(legacyKey));
    }

    public static bool IsProductKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        System.Text.RegularExpressions.Regex.IsMatch(
            key.Trim(),
            @"(?i)^[A-Z0-9]{5}(?:-[A-Z0-9]{5}){4}$");

    private static string DecodeProductKey(byte[] digitalProductId, bool useWindows8Algorithm)
    {
        if (digitalProductId.Length < KeyStart + KeyLength || digitalProductId.Length <= 66)
            return string.Empty;

        byte[] keyBytes = digitalProductId
            .Skip(KeyStart)
            .Take(KeyLength)
            .ToArray();

        int last = 0;
        char[] decoded = new char[25];

        for (int i = 24; i >= 0; i--)
        {
            int current = 0;

            for (int j = KeyLength - 1; j >= 0; j--)
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
            int insertIndex = Math.Clamp(last, 0, 24);
            result = result.Remove(0, 1).Insert(insertIndex, "N");
        }

        if (result.Length != 25)
            return string.Empty;

        return string.Join("-", Enumerable.Range(0, 5)
            .Select(group => result.Substring(group * 5, 5)));
    }
}
