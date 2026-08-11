using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class DuplicateRegistrationTests
{
    [Test]
    public void Reports_every_later_registration_against_the_first()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Destination>();
        builder.Map<Source, Destination>();
        builder.Map<Source, Destination>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0013", "MORPH0013" }));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.All.EqualTo(
                    "Mapping contract 'global::Morphant.ITypeMapper<" +
                    "global::TestCase.Source, " +
                    "global::TestCase.Destination>' is registered more than " +
                    "once in mapper 'global::TestCase.TestMapper'."));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MappingRegistrationGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.All.EqualTo("Map"));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MappingRegistrationGeneratorTest.SourceText(
                        diagnostic.AdditionalLocations.Single())),
                Is.All.EqualTo("Map"));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    diagnostic.Location.GetLineSpan().StartLinePosition.Line),
                Is.EqualTo(new[] { 15, 16 }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    diagnostic.AdditionalLocations.Single()
                        .GetLineSpan().StartLinePosition.Line),
                Is.All.EqualTo(14));
        });
    }

    [Test]
    public void Canonical_normalization_detects_all_duplicate_forms()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using System;
using Alias = System.Object;
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<dynamic, Destination>();
        builder.Map<Alias?, Destination?>();

        builder.Map<(int Id, string? Name), Destination>();
        builder.Map<(int, string), Destination?>();

        builder.Map<nint, Destination>();
        builder.Map<IntPtr, Destination>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0013",
                    "MORPH0013",
                    "MORPH0013"
                }));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "Mapping contract 'global::Morphant.ITypeMapper<" +
                    "global::System.IntPtr, " +
                    "global::TestCase.Destination>' is registered more than " +
                    "once in mapper 'global::TestCase.TestMapper'.",
                    "Mapping contract 'global::Morphant.ITypeMapper<" +
                    "global::System.ValueTuple<int, string>, " +
                    "global::TestCase.Destination>' is registered more than " +
                    "once in mapper 'global::TestCase.TestMapper'.",
                    "Mapping contract 'global::Morphant.ITypeMapper<object, " +
                    "global::TestCase.Destination>' is registered more than " +
                    "once in mapper 'global::TestCase.TestMapper'."
                }));
        });
    }

    [Test]
    public void Nullable_value_type_is_a_distinct_registration()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<int, Destination>();
        builder.Map<int?, Destination>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void Same_pair_in_different_or_inherited_mapper_levels_is_allowed()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

public abstract class BaseMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}

[MorphantMapper]
public partial class DerivedMapper : BaseMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<Source, Destination>();
    }
}

[MorphantMapper]
public partial class OtherMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Suppression_hides_the_duplicate_diagnostic()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class Destination
{
    public int Value { get; init; }
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Destination>()
            .Convert(source => new Destination { Value = 101 });
        builder.Map<Source, Destination>()
            .Convert(source => new Destination { Value = 202 });
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(
            source,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0013"] = ReportDiagnostic.Suppress
            });
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Duplicate_diagnostic_coexists_with_authoritative_pair_errors()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public partial class Container
{
    private sealed class Source { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, Destination>();
        }
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0011", "MORPH0013" }));
    }
}
