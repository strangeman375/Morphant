using System.Text;

namespace Morphant.Generator;

internal static class GeneratedSourceHintName
{
    // Keep the file component below the common 255-byte filesystem limit and
    // leave room for tooling that decorates a generated filename.
    private const int MaxHintNameUtf8Bytes = 220;

    public static string Create(
        string artifactKind,
        string stableIdentity)
    {
        var prefix = "Morphant.Generated." + artifactKind + ".";
        var candidate = prefix + stableIdentity + ".g.cs";

        if (Encoding.UTF8.GetByteCount(candidate) <= MaxHintNameUtf8Bytes)
        {
            return candidate;
        }

        var suffix = "__" +
                     HintNameHelper.GetStableHash(candidate) +
                     ".g.cs";
        var identityByteBudget = MaxHintNameUtf8Bytes -
                                 Encoding.UTF8.GetByteCount(prefix) -
                                 Encoding.UTF8.GetByteCount(suffix);
        var readablePrefix = TakeUtf8Prefix(
                stableIdentity,
                identityByteBudget)
            .TrimEnd('_');

        return prefix + readablePrefix + suffix;
    }

    private static string TakeUtf8Prefix(string value, int maxByteCount)
    {
        var byteCount = 0;
        var length = 0;

        while (length < value.Length)
        {
            var characterCount =
                char.IsHighSurrogate(value[length]) &&
                length + 1 < value.Length &&
                char.IsLowSurrogate(value[length + 1])
                    ? 2
                    : 1;
            var characterByteCount = GetUtf8ByteCount(
                value[length],
                characterCount);

            if (byteCount + characterByteCount > maxByteCount)
            {
                break;
            }

            byteCount += characterByteCount;
            length += characterCount;
        }

        return value.Substring(0, length);
    }

    private static int GetUtf8ByteCount(char character, int characterCount)
    {
        if (characterCount == 2)
        {
            return 4;
        }

        if (character <= '\u007F')
        {
            return 1;
        }

        return character <= '\u07FF'
            ? 2
            : 3;
    }
}
