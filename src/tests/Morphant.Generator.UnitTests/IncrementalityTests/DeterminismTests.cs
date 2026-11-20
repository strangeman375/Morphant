using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class DeterminismTests
{
    [Test]
    public void Produces_the_same_result_for_any_source_tree_order()
    {
        var files = new[]
        {
            new GeneratorTestSourceFile("Models.cs", ModelsSource),
            new GeneratorTestSourceFile("UpperMapper.cs", UpperMapperSource),
            new GeneratorTestSourceFile("TitleMapper.cs", TitleMapperSource),
            new GeneratorTestSourceFile("StableMapper.cs", StableMapperSource)
        };

        var forward = GeneratorTestDriver.Run(
            "MorphantDeterminism",
            files,
            LanguageVersion.CSharp9);
        var reverse = GeneratorTestDriver.Run(
            "MorphantDeterminism",
            files.Reverse().ToArray(),
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(
                GeneratedSources(forward),
                Is.EqualTo(GeneratedSources(reverse)));
            Assert.That(
                Diagnostics(forward),
                Is.EqualTo(Diagnostics(reverse)));
            Assert.That(forward.Diagnostics, Is.Not.Empty);
            Assert.That(forward.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(reverse.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static DeterministicGeneratedSource[] GeneratedSources(
        GeneratorTestDriverResult result)
    {
        return result.GeneratedSources
            .Select(static source =>
                new DeterministicGeneratedSource(
                    source.HintName,
                    source.SourceText.ToString()))
            .OrderBy(static source => source.HintName, StringComparer.Ordinal)
            .ToArray();
    }

    private static DeterministicDiagnostic[] Diagnostics(
        GeneratorTestDriverResult result)
    {
        return result.Diagnostics
            .Select(static diagnostic =>
                new DeterministicDiagnostic(
                    diagnostic.Id,
                    diagnostic.Severity,
                    diagnostic.GetMessage(),
                    diagnostic.Location.SourceTree?.FilePath,
                    diagnostic.Location.IsInSource
                        ? diagnostic.Location.SourceSpan.Start
                        : -1,
                    diagnostic.Location.IsInSource
                        ? diagnostic.Location.SourceSpan.Length
                        : 0,
                    string.Join(
                        "|",
                        diagnostic.AdditionalLocations.Select(LocationKey)),
                    string.Join(
                        "|",
                        diagnostic.Properties
                            .OrderBy(
                                static property => property.Key,
                                StringComparer.Ordinal)
                            .Select(static property =>
                                property.Key + "=" + property.Value)),
                    diagnostic.IsSuppressed))
            .OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ThenBy(
                static diagnostic => diagnostic.Path,
                StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Start)
            .ToArray();
    }

    private static string LocationKey(Location location)
    {
        return (location.SourceTree?.FilePath ?? string.Empty) + ":" +
               (location.IsInSource
                   ? location.SourceSpan.Start + ":" +
                     location.SourceSpan.Length
                   : "none");
    }

    // lang=c#
    private const string ModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source { }

    public sealed class URL
    {
        public int Value { get; set; }
    }

    public sealed class Url
    {
        public int Value { get; set; }
    }

    public sealed class StableSource { }

    public sealed class StableDestination { }
}
""";

    // lang=c#
    private const string UpperMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class URLMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, URL>();
    }
}
""";

    // lang=c#
    private const string TitleMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class UrlMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Url>();
    }
}
""";

    // lang=c#
    private const string StableMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class StableMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<StableSource, StableDestination>();
            builder.Map<StableSource, StableDestination>();
        }
    }
}
""";
}

internal sealed record DeterministicGeneratedSource(
    string HintName,
    string Source);

internal sealed record DeterministicDiagnostic(
    string Id,
    DiagnosticSeverity Severity,
    string Message,
    string? Path,
    int Start,
    int Length,
    string AdditionalLocations,
    string Properties,
    bool IsSuppressed);
