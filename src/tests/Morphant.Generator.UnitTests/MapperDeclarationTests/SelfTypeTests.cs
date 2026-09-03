using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests.MapperDeclarationTests;

[TestFixture]
internal sealed class SelfTypeTests
{
    private const string MapperFile =
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs";

    [Test]
    public void Unrelated_concrete_self_type_reports_MORPH0058()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public abstract class OtherMapper : TypeMapper<OtherMapper>
{
}

[MorphantMapper]
public partial class TestMapper : TypeMapper<OtherMapper>
{
    protected override void Configure(MapperBuilder builder) { }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0058"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Mapper 'TestCase.TestMapper' must close " +
                    "'Morphant.TypeMapper<TMapper>' with its own type or " +
                    "a correctly constrained CRTP self type instead of " +
                    "'TestCase.OtherMapper'."));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("OtherMapper"));
            Assert.That(result.HasGeneratedFile(MapperFile), Is.False);
            Assert.That(result.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void Loose_unattributed_configuration_base_reports_MORPH0058()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Destination
{
    public int X { get; set; }
    public int Y { get; set; }
}

public abstract class LooseMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : TypeMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int X, int Y), Destination>();
}

[MorphantMapper]
public partial class TestMapper : LooseMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        base.Configure(builder);
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0058"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Mapper 'TestCase.LooseMapper<TMapper>' must close " +
                    "'Morphant.TypeMapper<TMapper>' with its own type or " +
                    "a correctly constrained CRTP self type instead of " +
                    "'TMapper'."));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("TMapper"));
            Assert.That(result.HasGeneratedFile(MapperFile), Is.False);
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void Correctly_constrained_unattributed_configuration_base_is_valid()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Destination
{
    public int X { get; set; }
    public int Y { get; set; }
}

public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : CommonMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int X, int Y), Destination>()
            .Members(source => new()
            {
                X = source.X,
                Y = source.Y
            });
}

[MorphantMapper]
public partial class TestMapper : CommonMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        base.Configure(builder);
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerErrors, Is.Empty);
            Assert.That(result.GeneratedSources, Is.Not.Empty);
        });
    }

    [Test]
    public void Nullable_CRTP_constraint_is_compared_nominally()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591, CS8631

using Morphant;

namespace TestCase;

public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : CommonMapper<TMapper>?
{
    protected override void Configure(MapperBuilder builder) { }
}

[MorphantMapper]
public partial class TestMapper : CommonMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<int, string>().Convert(value => value.ToString());
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.HasGeneratedFile(MapperFile), Is.True);
        });
    }

    [Test]
    public void Concrete_intermediate_base_bound_to_the_final_mapper_is_valid()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : CommonMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder) { }
}

public abstract class TestMapperFeature : CommonMapper<TestMapper>
{
}

[MorphantMapper]
public partial class TestMapper : TestMapperFeature
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<Source, Destination>();
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerErrors, Is.Empty);
            Assert.That(result.HasGeneratedFile(MapperFile), Is.True);
        });
    }

    [Test]
    public void Concrete_intermediate_base_can_configure_a_scoped_tuple_surface()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;
}

public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : CommonMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder) { }
}

public abstract class TestMapperFeature : CommonMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<Source, (int Id, string Name)>()
            .Members(value => new()
            {
                Id = value.Id,
                Name = value.Name
            });
    }
}

[MorphantMapper]
public partial class TestMapper : TestMapperFeature
{
    protected override void Configure(MapperBuilder builder) =>
        base.Configure(builder);
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerErrors, Is.Empty);
            Assert.That(
                result.HasGeneratedFile(
                    "Morphant.Generated.MemberExtension." +
                    "TestCase_Source__" +
                    "System_ValueTuple_System_Int32__System_String___" +
                    "TestCase_TestMapper.g.cs"),
                Is.True);
        });
    }

    [Test]
    public void Shared_invalid_configuration_base_reports_once()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public abstract class LooseMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : TypeMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder) { }
}

[MorphantMapper]
public partial class FirstMapper : LooseMapper<FirstMapper>
{
}

[MorphantMapper]
public partial class SecondMapper : LooseMapper<SecondMapper>
{
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0058"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.StartWith("Mapper 'TestCase.LooseMapper<TMapper>'"));
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void Invalid_intermediate_configuration_family_reports_MORPH0058()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public abstract class RootMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : RootMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder) { }
}

public abstract class LooseFeatureMapper<TMapper> : RootMapper<TMapper>
    where TMapper : RootMapper<TMapper>
{
}

[MorphantMapper]
public partial class TestMapper : LooseFeatureMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        base.Configure(builder);
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0058"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.StartWith(
                    "Mapper 'TestCase.LooseFeatureMapper<TMapper>'"));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("TMapper"));
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void Suppressing_MORPH0058_does_not_restore_unsafe_generation()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public abstract class LooseMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : TypeMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder) { }
}

[MorphantMapper]
public partial class TestMapper : LooseMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        base.Configure(builder);
}
""";

        var result = MapperDeclarationGeneratorTest.Run(
            source,
            new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0058"] = ReportDiagnostic.Suppress
            });
        var warning = MapperDeclarationGeneratorTest.Run(
            source,
            new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0058"] = ReportDiagnostic.Warn
            });

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerErrors, Is.Empty);
            Assert.That(
                warning.EffectiveDiagnostics.Single().Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(warning.GeneratedSources, Is.Empty);
            Assert.That(warning.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void Correcting_base_constraint_removes_MORPH0058()
    {
        // lang=c#
        const string invalidSource =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : TypeMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder) { }
}

[MorphantMapper]
public partial class TestMapper : CommonMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<Source, Destination>();
    }
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

public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : CommonMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder) { }
}

[MorphantMapper]
public partial class TestMapper : CommonMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<Source, Destination>();
    }
}
""";
        var valid = MapperDeclarationGeneratorTest.Run(
            validSource,
            driver: invalid.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(
                invalid.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0058" }));
            Assert.That(invalid.GeneratedSources, Is.Empty);
            Assert.That(valid.Diagnostics, Is.Empty);
            Assert.That(valid.CompilerErrors, Is.Empty);
            Assert.That(valid.HasGeneratedFile(MapperFile), Is.True);
        });
    }
}
