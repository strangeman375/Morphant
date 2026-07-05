using System.Text;

namespace Morphant.Generator;

public static class HintNameHelper
{
    public static string ToHintNamePart(string value)
    {
        var builder = new StringBuilder(value.Length + 9);

        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        builder.Append('_');
        builder.Append(GetStableHash(value));

        return builder.ToString();
    }

    private static string GetStableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;

            foreach (var ch in value)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return hash.ToString("x8");
        }
    }
}
