using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.IncludeMembersTests;

internal static class IncludeMembersGeneratorTest
{
    public static IncludeMembersGeneratorResult Run(
        string source,
        LanguageVersion languageVersion = LanguageVersion.CSharp9,
        GeneratorDriver? driver = null) =>
        new(GeneratorTestDriver.Run(
            "IncludeMembersConsumer",
            source,
            languageVersion,
            driver: driver));

    public static string SourceText(Location location) =>
        GeneratorTestDriver.GetSourceText(location);
}

internal sealed record IncludeMembersGeneratorResult :
    GeneratorTestDriverResult
{
    public IncludeMembersGeneratorResult(GeneratorTestDriverResult result)
        : base(result)
    {
    }

    public ImmutableArray<Diagnostic> IncludeMembersDiagnostics =>
        EffectiveDiagnostics
            .Where(static diagnostic => diagnostic.Id is
                "MORPH0049" or "MORPH0050")
            .ToImmutableArray();
}
