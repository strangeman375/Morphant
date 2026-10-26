using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class GeneratedSourceText
{
    private const string NewLine = "\r\n";
    private static readonly SourceHashAlgorithm GeneratorStringChecksumAlgorithm =
        GetGeneratorStringChecksumAlgorithm();

    public static SourceText CreateForGenerator(string hintName, string source)
    {
        // Mapper output uses AddSource(string); declaration surfaces supply
        // SourceText explicitly. New hosts give these overloads different defaults.
        var checksumAlgorithm = hintName.StartsWith(
            "Morphant.Generated.TypeMapper.", StringComparison.Ordinal)
            ? GeneratorStringChecksumAlgorithm
            : SourceHashAlgorithm.Sha1;
        return SourceText.From(Normalize(source), Encoding.UTF8,
            checksumAlgorithm);
    }

    private static SourceHashAlgorithm GetGeneratorStringChecksumAlgorithm()
    {
        // Observe only Roslyn's metadata default, independently of production
        // generation. Expected filenames and complete source remain literal.
        var driver = CSharpGeneratorDriver.Create(
                new ChecksumGenerator().AsSourceGenerator())
            .RunGenerators(CSharpCompilation.Create("Checksum"));
        return driver.GetRunResult().Results.Single().GeneratedSources.Single()
            .SourceText.ChecksumAlgorithm;
    }

    private sealed class ChecksumGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.CompilationProvider,
                static (productionContext, _) =>
                    productionContext.AddSource("Checksum.cs", ""));
        }
    }

    public static string Normalize(string source)
    {
        var normalized = source
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\n", NewLine);

        return normalized.EndsWith(NewLine, StringComparison.Ordinal)
            ? normalized
            : normalized + NewLine;
    }
}
