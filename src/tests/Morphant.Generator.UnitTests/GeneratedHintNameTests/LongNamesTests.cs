using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.GeneratedHintNameTests;

[TestFixture]
internal sealed partial class LongNamesTests
{
    [TestCase(false, "1.0.0.0", "src/Mapper.cs")]
    [TestCase(false, "2.0.0.0", "renamed/Mapper.cs")]
    [TestCase(true, "1.0.0.0", "src/Mapper.cs")]
    [TestCase(true, "2.0.0.0", "renamed/Mapper.cs")]
    public void Long_labels_remain_distinct_and_stable_across_paths_and_versions(
        bool referencedModels,
        string version,
        string sourcePath)
    {
        // The two ASCII names used to collide after filename truncation.
        // The mapper and third destination exercise two- and three-byte letters
        // in every generated artifact kind, with equal readable pair prefixes.
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
public sealed class Source { public int Value { get; set; } }
public sealed class AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA { public int Value { get; set; } }
public sealed class AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA__8aa1c15210409ca2 { public int Value { get; set; } }
public sealed class 漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢 { public int Value { get; set; } }

[MorphantMapper]
public partial class ЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖ : TypeMapper<ЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖЖ>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA>();
        builder.Map<Source, AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA__8aa1c15210409ca2>();
        builder.Map<Source, 漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢漢>();
    }
}
""";
        var assemblyVersion =
            "[assembly: System.Reflection.AssemblyVersion(\"" + version + "\")]\n";
        var mapperStart = source.IndexOf("[MorphantMapper]", StringComparison.Ordinal);
        var input = source.Replace("using Morphant;", "using Morphant;\n" + assemblyVersion);
        MetadataReference[] references = [];

        if (referencedModels)
        {
            references =
            [
                GeneratorTestDriver.CompileReference(
                    "ExternalModels", source[..mapperStart].Replace(
                        "using Morphant;", "using Morphant;\n" + assemblyVersion))
            ];
            input = "#nullable enable\n#pragma warning disable CS1591\nusing Morphant;\n" +
                    assemblyVersion + source[mapperStart..];
        }

        var result = GeneratorTestDriver.Run(
            "PortableHintNames",
            [new GeneratorTestSourceFile(sourcePath, input)],
            LanguageVersion.CSharp9,
            additionalReferences: references);
        Assert.That(result.EffectiveDiagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        using var binary = new MemoryStream();
        var emission = result.OutputCompilation.Emit(binary);
        Assert.That(emission.Success, Is.True,
            string.Join(Environment.NewLine, emission.Diagnostics));
        Assert.That(emission.Diagnostics.Where(diagnostic =>
            diagnostic.Severity >= DiagnosticSeverity.Warning), Is.Empty);
        var actual = result.GeneratedSources
            .Select(item => (item.HintName, Source: item.SourceText.ToString()))
            .OrderBy(item => item.HintName, StringComparer.Ordinal).ToArray();
        Assert.That(actual, Is.EqualTo(ExpectedSources.Select(item =>
                (item.HintName, Source: GeneratedSourceText.Normalize(item.Source)))
            .OrderBy(item => item.HintName, StringComparer.Ordinal).ToArray()));
        Assert.That(actual.Select(item => item.HintName), Is.Unique.IgnoreCase);

        var root = Path.Combine(Path.GetTempPath(), nameof(LongNamesTests), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            foreach (var (hintName, content) in actual)
            {
                Assert.That(Encoding.UTF8.GetByteCount(hintName), Is.LessThanOrEqualTo(220));
                Assert.That(hintName, Does.Match(@"__[0-9a-f]{32}\.g\.cs$"));
                var path = Path.Combine(root, hintName);
                File.WriteAllText(path, content);
                Assert.That(File.ReadAllText(path), Is.EqualTo(content));
            }
            Assert.That(Directory.GetFiles(root).Select(Path.GetFileName),
                Is.EquivalentTo(actual.Select(item => item.HintName)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
