using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class GeneratedMetadataLimitTests
{
    [Test]
    public void Compiler_reports_an_overlong_generated_metadata_name()
    {
        var destinationName = new string('D', 960);
        const string preamble = "#nullable enable\n#pragma warning disable CS1591\n";

        // lang=c#
        var declarations =
$$"""
public class {{destinationName}} { public int Value { get; set; } }
public class Source { public int Value { get; set; } }
""";
        // lang=c#
        var mapper =
$$"""
[MorphantMapper]
public partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, {{destinationName}}>();
}
""";

        // The user type itself is legal. Adding generated names can exceed
        // the compiler's metadata limit even when parsing and binding succeed.
        var modelOnly = GeneratorTestDriver.Run(
            "NamingReview", preamble + declarations, LanguageVersion.CSharp9);
        Assert.That(modelOnly.EffectiveDiagnostics, Is.Empty);
        Assert.That(modelOnly.CompilerWarningsAndErrors, Is.Empty);
        Assert.That(modelOnly.GeneratedSources, Is.Empty);
        using var modelBinary = new MemoryStream();
        var modelEmission = modelOnly.OutputCompilation.Emit(modelBinary);
        Assert.That(modelEmission.Success, Is.True);
        Assert.That(modelEmission.Diagnostics, Is.Empty);

        var mapping = GeneratorTestDriver.Run(
            "NamingReview",
            preamble + "using Morphant;\n" + declarations + "\n" + mapper,
            LanguageVersion.CSharp9);
        Assert.That(mapping.EffectiveDiagnostics, Is.Empty);
        Assert.That(mapping.CompilerWarningsAndErrors, Is.Empty);
        using var mappingBinary = new MemoryStream();
        var mappingEmission = mapping.OutputCompilation.Emit(mappingBinary);
        Assert.That(mappingEmission.Success, Is.False);
        Assert.That(mappingEmission.Diagnostics, Has.Length.EqualTo(1));

        var diagnostic = mappingEmission.Diagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("CS7013"));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture),
                Is.EqualTo(
                    "Name 'Morphant.Generated.N_e36095653d54ae462de4322457ac4c31." +
                    destinationName +
                    "Construction' exceeds the maximum length allowed in metadata."));
            Assert.That(GeneratorTestDriver.GetSourceText(diagnostic.Location),
                Is.EqualTo(destinationName + "Construction"));
        });
    }
}
