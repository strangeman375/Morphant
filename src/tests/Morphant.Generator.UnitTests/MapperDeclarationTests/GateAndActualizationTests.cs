using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Morphant.Generator.UnitTests.MapperDeclarationTests;

[TestFixture]
internal sealed class GateAndActualizationTests
{
    private const string MapperFile =
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs";

    [Test]
    public void Suppressing_MORPH0006_does_not_make_generation_possible()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(
            source,
            new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0006"] = ReportDiagnostic.Suppress
            });

        Assert.Multiple(() =>
        {
            Assert.That(
                CompilationWithAnalyzers.GetEffectiveDiagnostics(
                    result.Diagnostics,
                    result.OutputCompilation),
                Is.Empty);
            Assert.That(result.HasGeneratedFile(MapperFile), Is.False);
            Assert.That(result.GeneratedSources, Is.Not.Empty);
        });
    }

    [Test]
    public void Changing_MORPH0006_to_warning_changes_only_presentation()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(
            source,
            new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0006"] = ReportDiagnostic.Warn
            });
        var diagnostic = CompilationWithAnalyzers.GetEffectiveDiagnostics(
                result.Diagnostics,
                result.OutputCompilation)
            .Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostic.Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(result.HasGeneratedFile(MapperFile), Is.False);
        });
    }

    [Test]
    public void Non_partial_gate_suppresses_exact_contract_analysis()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public abstract class TestMapper : TypeMapper<TestMapper>,
    ITypeMapper<Source, Destination>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0006" }));
    }

    [Test]
    public void Suppressing_MORPH0009_does_not_restore_the_conflicting_pair()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper>,
    ITypeMapper<Source, Destination>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(
            source,
            new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0009"] = ReportDiagnostic.Suppress
            });

        Assert.Multiple(() =>
        {
            Assert.That(
                CompilationWithAnalyzers.GetEffectiveDiagnostics(
                    result.Diagnostics,
                    result.OutputCompilation),
                Is.Empty);
            Assert.That(result.HasGeneratedFile(MapperFile), Is.False);
            Assert.That(result.GeneratedSources, Is.Not.Empty);
        });
    }

    [Test]
    public void Adding_partial_removes_MORPH0006_and_restores_the_mapper()
    {
        // lang=c#
        const string invalidSource =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";
        var invalid = MapperDeclarationGeneratorTest.Run(invalidSource);

        // lang=c#
        const string validSource =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";
        var valid = MapperDeclarationGeneratorTest.Run(
            validSource,
            driver: invalid.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(
                invalid.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0006" }));
            Assert.That(invalid.HasGeneratedFile(MapperFile), Is.False);
            Assert.That(valid.Diagnostics, Is.Empty);
            Assert.That(valid.HasGeneratedFile(MapperFile), Is.True);
            Assert.That(valid.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void Removing_direct_interface_restores_the_excluded_pair()
    {
        // lang=c#
        const string invalidSource =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper>,
    ITypeMapper<Source, Destination>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";
        var invalid = MapperDeclarationGeneratorTest.Run(invalidSource);

        // lang=c#
        const string validSource =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";
        var valid = MapperDeclarationGeneratorTest.Run(
            validSource,
            driver: invalid.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(
                invalid.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0009" }));
            Assert.That(invalid.HasGeneratedFile(MapperFile), Is.False);
            Assert.That(valid.Diagnostics, Is.Empty);
            Assert.That(valid.HasGeneratedFile(MapperFile), Is.True);
        });
    }

    [Test]
    public void Same_mapper_name_in_different_namespaces_is_not_deduplicated()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace First
{
    [MorphantMapper]
    public class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) { }
    }
}

namespace Second
{
    [MorphantMapper]
    public class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) { }
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(diagnostic => diagnostic.GetMessage()),
            Is.EqualTo(new[]
            {
                "Mapper 'First.TestMapper' must be declared partial.",
                "Mapper 'Second.TestMapper' must be declared partial."
            }));
    }
}
