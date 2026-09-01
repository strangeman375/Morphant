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

    internal static string LimitWithStableHash(
        string readableName,
        string stableIdentity,
        int maxLength)
    {
        if (readableName.Length <= maxLength)
        {
            return readableName;
        }

        var hashSuffix = "__" + GetStableHash(stableIdentity);

        if (maxLength <= hashSuffix.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        }

        var prefixLength = maxLength - hashSuffix.Length;

        if (prefixLength > 0 &&
            prefixLength < readableName.Length &&
            char.IsHighSurrogate(readableName[prefixLength - 1]) &&
            char.IsLowSurrogate(readableName[prefixLength]))
        {
            prefixLength--;
        }

        var prefix = readableName
            .Substring(0, prefixLength)
            .TrimEnd('_');

        return prefix + hashSuffix;
    }
}

internal sealed class HintNamePartAllocator
{
    private readonly HashSet<string> _usedHintNameParts =
        new(StringComparer.OrdinalIgnoreCase);

    public string Allocate(string stableIdentity)
    {
        return Allocate(
            stableIdentity,
            HintNameHelper.ToHintNamePart(stableIdentity));
    }

    public string Allocate(
        string stableIdentity,
        string readableHintNamePart)
    {
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
