namespace Morphant.Build.Tasks;

internal static class PortablePath
{
    private static readonly char[] UnsafeComponentCharacters =
        ['<', '>', ':', '"', '/', '\\', '|', '?', '*', ';', '[', ']'];

    private static readonly HashSet<string> ReservedWindowsNames = new(
        [
            "CON",
            "PRN",
            "AUX",
            "NUL",
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "LPT1",
            "LPT2",
            "LPT3",
            "LPT4",
            "LPT5",
            "LPT6",
            "LPT7",
            "LPT8",
            "LPT9"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsSafeComponent(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value is "." or ".." ||
            value.IndexOfAny(UnsafeComponentCharacters) >= 0 ||
            value.Any(char.IsControl) ||
            value.EndsWith(".", StringComparison.Ordinal) ||
            value.EndsWith(" ", StringComparison.Ordinal) ||
            value.IndexOf("$(", StringComparison.Ordinal) >= 0 ||
            value.IndexOf("@(", StringComparison.Ordinal) >= 0 ||
            value.IndexOf("%(", StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        var deviceName = value.Split(['.'], 2)[0];
        return !ReservedWindowsNames.Contains(deviceName);
    }
}
