using System.Globalization;
using System.Text;

namespace Morphant.Generator;

public static class HintNameHelper
{
    private const ulong Fnv1A64OffsetBasis = 14695981039346656037UL;
    private const ulong Fnv1A64Prime = 1099511628211UL;

    public static string ToHintNamePart(string value)
    {
        var hintNamePart = ToReadableHintNamePart(
            value,
            out var requiresDisambiguation);

        return requiresDisambiguation
            ? AppendStableHash(hintNamePart, value)
            : hintNamePart;
    }

    internal static string ToReadableHintNamePart(string value)
    {
        return ToReadableHintNamePart(value, out _);
    }

    internal static string AppendStableHash(
        string hintNamePart,
        string value)
    {
        return hintNamePart + "__" + GetStableHash(value);
    }

    private static string ToReadableHintNamePart(
        string value,
        out bool requiresDisambiguation)
    {
        var builder = new StringBuilder(value.Length);
        requiresDisambiguation = false;

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                continue;
            }

            builder.Append('_');

            // Dots are unambiguous separators in a top-level metadata name.
            // Other characters can collide with dots or with each other after
            // replacement, for example A.B_C and A_B.C.
            requiresDisambiguation |= character != '.';
        }

        return builder.ToString();
    }

    private static string GetStableHash(string value)
    {
        unchecked
        {
            var hash = Fnv1A64OffsetBasis;

            foreach (var character in value)
            {
                hash ^= character;
                hash *= Fnv1A64Prime;
            }

            return hash.ToString(
                "x16",
                CultureInfo.InvariantCulture);
        }
    }
}
