using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class UnsupportedRootTests
{
    [Test]
    public void Reports_both_root_type_parameter_roles_in_role_order()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class TestMapper<TSource, TDestination> : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<TSource, TDestination>();
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0012", "MORPH0012" }));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "The source type 'TSource' cannot be used in Map because " +
                    "it is a root type parameter.",
                    "The destination type 'TDestination' cannot be used in " +
                    "Map because it is a root type parameter."
                }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MappingRegistrationGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "TSource", "TDestination" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Nullable_value_wrapper_does_not_make_a_root_parameter_eligible()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper<T> : TypeMapper
    where T : struct
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<T?, Destination>();
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0012"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain("source type 'T?'"));
            Assert.That(
                MappingRegistrationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("T?"));
        });
    }

    [Test]
    public void Type_parameter_inside_a_nominal_root_is_eligible()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper<T> : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Envelope<T>, Destination>();
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
    public void Opaque_root_families_are_eligible()
    {
        // lang=c#
        const string source =
"""
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<(int Id, string Name), Destination>();
        builder.Map<int[], Destination>();
        builder.Map<List<int>, Destination>();
        builder.Map<Func<int>, Destination>();
        builder.Map<Expression<Func<int>>, Destination>();
        builder.Map<Task<int>, Destination>();
        builder.Map<IObservable<int>, Destination>();
        builder.Map<string, int>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);
        var diagnostics = result.Diagnostics
            .Where(static diagnostic => diagnostic.Id is
                "MORPH0011" or
                "MORPH0012" or
                "MORPH0013" or
                "MORPH0014")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Suppression_hides_the_diagnostic()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper<T> : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<T, Destination>();
}
""";

        var suppressed = MappingRegistrationGeneratorTest.Run(
            source,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0012"] = ReportDiagnostic.Suppress
            });

        Assert.Multiple(() =>
        {
            Assert.That(suppressed.EffectiveDiagnostics, Is.Empty);
            Assert.That(suppressed.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
