namespace Morphant.Generator.UnitTests.MapperConfigurationTests;

[TestFixture]
internal sealed class ConfigureDeclarationTests
{
    [Test]
    public void Accepts_block_expression_and_empty_source_bodies()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class BlockMapper : TypeMapper
{
    protected override void Configure(MapperBuilder configuration)
    {
        var unrelated = 1;
        _ = unrelated;
    }
}

[MorphantMapper]
public partial class ExpressionMapper : TypeMapper
{
    protected override void Configure(MapperBuilder configuration) =>
        _ = 1;
}

[MorphantMapper]
public partial class EmptyMapper : TypeMapper
{
    protected override void Configure(MapperBuilder configuration)
    {
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.GeneratedSources, Is.Empty);
        });
    }

    [Test]
    public void Reports_missing_override_at_the_mapper_identifier()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public abstract partial class MissingMapper : TypeMapper
{
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0015"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Mapper 'TestCase.MissingMapper' must override " +
                    "'Configure(Morphant.MapperBuilder)' with a readable " +
                    "method body."));
            Assert.That(
                MapperConfigurationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("MissingMapper"));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_plain_absence_even_when_the_compiler_requires_override()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class MissingMapper : TypeMapper
{
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0015" }));
            Assert.That(
                result.CompilerWarningsAndErrors.Select(static diagnostic =>
                    diagnostic.Id),
                Does.Contain("CS0534"));
            Assert.That(result.GeneratedSources, Is.Empty);
        });
    }

    [Test]
    public void Inherited_concrete_override_does_not_replace_an_own_override()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public abstract class BaseMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
    }
}

[MorphantMapper]
public partial class DerivedMapper : BaseMapper
{
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0015" }));
            Assert.That(
                MapperConfigurationGeneratorTest.SourceText(
                    result.Diagnostics.Single().Location),
                Is.EqualTo("DerivedMapper"));
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_a_bodyless_exact_override_at_Configure()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public abstract partial class AbstractMapper : TypeMapper
{
    protected abstract override void Configure(MapperBuilder builder);
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0015"));
            Assert.That(
                MapperConfigurationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Configure"));
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Leaves_malformed_override_attempts_to_the_compiler()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public abstract partial class WrongReturn : TypeMapper
{
    protected override int Configure(MapperBuilder builder) => 0;
}

[MorphantMapper]
public abstract partial class WrongParameter : TypeMapper
{
    protected override void Configure(string builder)
    {
    }
}

[MorphantMapper]
public abstract partial class StaticOverride : TypeMapper
{
    protected static override void Configure(MapperBuilder builder)
    {
    }
}

[MorphantMapper]
public abstract partial class UnresolvedParameter : TypeMapper
{
    protected override void Configure(MissingBuilder builder)
    {
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Not.Empty);
        });
    }

    [Test]
    public void Unrelated_Configure_body_error_does_not_hide_missing_override()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper
{
    private void Configure()
    {
        MissingMethod();
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0015" }));
            Assert.That(
                result.CompilerWarningsAndErrors.Select(static diagnostic =>
                    diagnostic.Id),
                Does.Contain("CS0103"));
            Assert.That(result.GeneratedSources, Is.Empty);
        });
    }

    [Test]
    public void Mapper_structural_gate_suppresses_missing_Configure()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public abstract class InvalidMapper : TypeMapper
{
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0006" }));
    }
}
