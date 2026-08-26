using System.Globalization;
using System.Text;

namespace Morphant.Generator;

internal static class HintNameHelper
{
    private const ulong Fnv1A64OffsetBasis = 14695981039346656037UL;
    private const ulong Fnv1A64Prime = 1099511628211UL;

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

    internal static string AppendStableHash(
        string hintNamePart,
        string value)
    {
        return hintNamePart + "__" + GetStableHash(value);
    }

    internal static string GetStableHash(string value)
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

internal sealed class HintNamePartAllocator
{
    private readonly HashSet<string> _usedHintNameParts =
        new(StringComparer.OrdinalIgnoreCase);

    public string Allocate(string stableIdentity)
    {
        var readableHintNamePart =
            HintNameHelper.ToHintNamePart(stableIdentity);

        if (_usedHintNameParts.Add(readableHintNamePart))
        {
            return readableHintNamePart;
        }

        var hintNamePart = HintNameHelper.AppendStableHash(
            readableHintNamePart,
            stableIdentity);

        var collisionIndex = 2;

        while (!_usedHintNameParts.Add(hintNamePart))
        {
            hintNamePart =
                HintNameHelper.AppendStableHash(
                    readableHintNamePart,
                    stableIdentity) +
                "_" +
                collisionIndex.ToString(
                    CultureInfo.InvariantCulture);

            collisionIndex++;
        }

        return hintNamePart;
    }
}
