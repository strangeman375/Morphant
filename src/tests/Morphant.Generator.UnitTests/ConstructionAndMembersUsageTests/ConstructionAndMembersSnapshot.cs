using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionAndMembersUsageTests;

internal static class ConstructionAndMembersSnapshot
{
    // Complete readable examples of construction and member composition.
    public static void Verify(
        string source,
        (string HintName, string Source)[] expectedMappers,
        (string HintName, string Source)[] expectedSurfaces,
        string expectedDiagnostics = "",
        LanguageVersion languageVersion = LanguageVersion.CSharp9)
    {
        var result = GeneratorTestDriver.Run(
            "ConstructionAndMembersReview", source, languageVersion);
        var expected = expectedMappers.Concat(expectedSurfaces).ToArray();
        var actual = result.GeneratedSources.ToDictionary(
            item => item.HintName, item => item.SourceText.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Select(item => item.HintName)),
                "Every generated file must have a complete snapshot.");

            foreach (var (hintName, content) in expected)
            {
                if (actual.TryGetValue(hintName, out var generated))
                    Assert.That(generated, Is.EqualTo(GeneratedSourceText.Normalize(content)), hintName);
            }

            var diagnostics = result.EffectiveDiagnostics
                .Concat(result.CompilerWarningsAndErrors)
                .Select(DescribeDiagnostic)
                .OrderBy(item => item, StringComparer.Ordinal);
            Assert.That(string.Join("\n", diagnostics),
                Is.EqualTo(expectedDiagnostics.Replace("\r\n", "\n").Trim()));
        });
    }

    private static string DescribeDiagnostic(Diagnostic diagnostic)
    {
        var locations = new[] { diagnostic.Location }.Concat(diagnostic.AdditionalLocations);
        var spans = locations.Select(location =>
        {
            if (!location.IsInSource)
                return "<no source>";

            var span = location.GetLineSpan();
            return $"{span.Path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}" +
                   $"-{span.EndLinePosition.Line + 1},{span.EndLinePosition.Character + 1})";
        });
        return $"{diagnostic.Id} {diagnostic.Severity}: {diagnostic.GetMessage()}\n" +
               $"  at {string.Join("; ", spans)}";
    }
}
