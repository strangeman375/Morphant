using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Morphant.Generator;

internal static class HintNameHelper
{
    public static string ToHintNamePart(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(
                char.IsLetterOrDigit(character)
                    ? character
                    : '_');
        }

        return builder.ToString();
    }

    internal static string GetStableHash128(string value)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
        var result = new StringBuilder(32);

        for (var index = 0; index < 16; index++)
        {
            result.Append(hash[index].ToString(
                "x2",
                CultureInfo.InvariantCulture));
        }

        return result.ToString();
    }
}
