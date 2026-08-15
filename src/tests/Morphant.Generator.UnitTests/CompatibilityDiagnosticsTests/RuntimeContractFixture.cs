using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests.CompatibilityDiagnosticsTests;

internal sealed class RuntimeContractFixture
{
    private const string ContractMetadata =
        "Morphant.GeneratorContractVersion";
    private const string ResourceSuffix =
        "CompatibilityDiagnosticsTests.Fixtures.CompatibleRuntime.cs.txt";

    private static int _assemblyIndex;
    private string _source = LoadSource();
    private string _revision = "1";

    private RuntimeContractFixture()
    {
    }

    public static RuntimeContractFixture Compatible()
    {
        return new RuntimeContractFixture();
    }

    public RuntimeContractFixture WithRevision(string revision)
    {
        _revision = revision;
        return this;
    }

    public RuntimeContractFixture WithDuplicateRevision(string revision)
    {
        const string namespaceMarker = "namespace Morphant";
        var insertionIndex = _source.IndexOf(
            namespaceMarker,
            StringComparison.Ordinal);

        if (insertionIndex < 0)
        {
            throw new InvalidOperationException(
                "The runtime fixture does not contain its root namespace.");
        }

        _source = _source.Insert(
            insertionIndex,
            $"[assembly: AssemblyMetadata(\"{ContractMetadata}\", " +
            $"\"{revision}\")]\n\n");
        return this;
    }

    public RuntimeContractFixture With(
        params RuntimeContractDefect[] defects)
    {
        foreach (var defect in defects)
        {
            Apply(defect);
        }

        return this;
    }

    public PortableExecutableReference CreateReference(
        string? assemblyName = null)
    {
        var source = _source.Replace(
            "%%REVISION%%",
            _revision,
            StringComparison.Ordinal);

        return CompatibilityGeneratorTest.CreateReference(
            assemblyName ??
            "CompatibleRuntime" + Interlocked.Increment(ref _assemblyIndex),
            source);
    }

    private void Apply(RuntimeContractDefect defect)
    {
        if (defect == RuntimeContractDefect.MissingMapperAttribute)
        {
            _source = ReplaceExactlyOnce(
                _source,
                "public sealed class MorphantMapperAttribute : Attribute",
                "public sealed class MissingMorphantMapperAttribute : " +
                "Attribute",
                defect);
            _source = ReplaceExactlyOnce(
                _source,
                "public MorphantMapperAttribute()",
                "public MissingMorphantMapperAttribute()",
                defect);
            return;
        }

        var replacement = defect switch
        {
            RuntimeContractDefect.InternalMapperAttribute => new Replacement(
                "public sealed class MorphantMapperAttribute : Attribute",
                "internal sealed class MorphantMapperAttribute : Attribute"),
            RuntimeContractDefect.ConfigureReturnsInt => new Replacement(
                "protected abstract void Configure(MapperBuilder builder);",
                "protected abstract int Configure(MapperBuilder builder);"),
            RuntimeContractDefect.MapUsesIntInsteadOfMappingMode =>
                new Replacement(
                    "MappingMode value = Morphant.MappingMode.Default",
                    "int value = 0"),
            RuntimeContractDefect.InvalidMappingModeValues => new Replacement(
                "Create = 1,\n        Update = 2,\n        CreateAndUpdate = 3",
                "Create = 4,\n        Update = 2,\n        CreateAndUpdate = 6"),
            RuntimeContractDefect.InvariantConstructSource => new Replacement(
                "public delegate TResult Construct<in TSource, out TResult>",
                "public delegate TResult Construct<TSource, out TResult>"),
            RuntimeContractDefect.AutoMarkerWithoutMemberMarker =>
                new Replacement(
                    "public sealed class AutoMarker : MemberMarker",
                    "public sealed class AutoMarker"),
            RuntimeContractDefect.ConstructorParameterConvertsFromString =>
                new Replacement(
                    "implicit operator ConstructorParameter<T>(T value)",
                    "implicit operator ConstructorParameter<T>(string value)"),
            RuntimeContractDefect.MappingConfigurationReasonIsObject =>
                new Replacement(
                    "Type destinationType,\n            string reason)",
                    "Type destinationType,\n            object reason)"),
            RuntimeContractDefect.MutableMappingContext => new Replacement(
                "public readonly struct MappingContext",
                "public struct MappingContext"),
            RuntimeContractDefect.InvariantConstructUsingSource =>
                new Replacement(
                    "public delegate TResult ConstructUsing<in TSource, out TResult>",
                    "public delegate TResult ConstructUsing<TSource, out TResult>"),
            _ => throw new ArgumentOutOfRangeException(nameof(defect), defect, null)
        };

        _source = ReplaceExactlyOnce(
            _source,
            replacement.Original,
            replacement.Changed,
            defect);
    }

    private static string ReplaceExactlyOnce(
        string source,
        string original,
        string changed,
        RuntimeContractDefect defect)
    {
        var first = source.IndexOf(original, StringComparison.Ordinal);
        var second = first < 0
            ? -1
            : source.IndexOf(
                original,
                first + original.Length,
                StringComparison.Ordinal);

        if (first < 0 || second >= 0)
        {
            throw new InvalidOperationException(
                $"Defect '{defect}' expected exactly one matching runtime " +
                "fixture fragment.");
        }

        return source.Remove(first, original.Length).Insert(first, changed);
    }

    private static string LoadSource()
    {
        var assembly = typeof(RuntimeContractFixture).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(
            name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException(
                $"Embedded runtime fixture '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }

    private sealed record Replacement(string Original, string Changed);
}

internal enum RuntimeContractDefect
{
    MissingMapperAttribute,
    InternalMapperAttribute,
    ConfigureReturnsInt,
    MapUsesIntInsteadOfMappingMode,
    InvalidMappingModeValues,
    InvariantConstructSource,
    AutoMarkerWithoutMemberMarker,
    ConstructorParameterConvertsFromString,
    MappingConfigurationReasonIsObject,
    MutableMappingContext,
    InvariantConstructUsingSource
}
