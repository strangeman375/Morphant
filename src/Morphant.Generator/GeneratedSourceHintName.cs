using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator;

internal static class GeneratedSourceHintName
{
    // Keep the file component below the common 255-byte filesystem limit and
    // leave room for tooling that decorates a generated filename.
    private const int MaxHintNameUtf8Bytes = 220;

    public static string Create(
        string artifactKind,
        string readableLabel,
        string identity)
    {
        var prefix = "Morphant.Generated." + artifactKind + ".";
        var suffix = "__" + identity + ".g.cs";

        if (Encoding.UTF8.GetByteCount(prefix + readableLabel + suffix) <=
            MaxHintNameUtf8Bytes)
        {
            return prefix + readableLabel + suffix;
        }

        var identityByteBudget = MaxHintNameUtf8Bytes -
                                 Encoding.UTF8.GetByteCount(prefix) -
                                 Encoding.UTF8.GetByteCount(suffix);
        var readablePrefix = TakeUtf8Prefix(
                readableLabel,
                identityByteBudget)
            .TrimEnd('_', '.');

        return prefix + readablePrefix + suffix;
    }

    public static string ForDestination(
        string artifactKind,
        INamedTypeSymbol destination,
        Compilation compilation)
    {
        var tuple = BclTupleShapePolicy.TryCreate(destination);
        var identity = tuple is null
            ? GeneratedEntityIdentity.ForTypeDefinition(destination, compilation)
            : BclTuplePlanNaming.BuildIdentity(tuple, compilation);

        return Create(artifactKind, BuildTypeLabel(destination), identity);
    }

    public static string BuildTypeLabel(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return BuildTypeLabel(array.ElementType) + "Array" +
                   array.Rank.ToString(CultureInfo.InvariantCulture);
        }

        if (BclTupleShapePolicy.TryCreate(type) is { } tuple)
        {
            return (tuple.Kind == BclTupleKind.ValueTuple
                       ? "ValueTuple"
                       : "SystemTuple") +
                   tuple.Elements.Length.ToString(CultureInfo.InvariantCulture);
        }

        return HintNameHelper.ToHintNamePart(type.Name);
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
