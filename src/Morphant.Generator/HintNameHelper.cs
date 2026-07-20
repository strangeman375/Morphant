using System.Text;

namespace Morphant.Generator;

public static class HintNameHelper
{
    public static string ToHintNamePart(string value)
    {
        var builder = new StringBuilder(value.Length + 10);
        var requiresDisambiguation = false;

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

        if (requiresDisambiguation)
        {
            // A double underscore cannot occur in the non-hashed form because
            // metadata names cannot contain empty namespace segments.
            builder.Append("__");
            builder.Append(GetStableHash(value));
        }

        return builder.ToString();
    }

    private static string GetStableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;

            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return hash.ToString("x8");
        }
    }
}
