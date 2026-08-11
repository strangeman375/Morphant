using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class OrderingAndActualizationTests
{
    [Test]
    public void Publication_order_is_by_id_before_source_order()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }
public sealed class Destination { }

public partial class Container
{
    private sealed class Hidden { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Envelope<T>, Destination>();
            builder.Map<Envelope<int>, Destination>();
            builder.Map<int, Destination>();
            builder.Map<int, Destination>();
            builder.Map<T, Destination>();
            builder.Map<Hidden, Destination>();
        }
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);
        var ids = result.Diagnostics
            .Select(static diagnostic => diagnostic.Id)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(ids.First(), Is.EqualTo("MORPH0011"));
            Assert.That(ids.SkipWhile(static id => id == "MORPH0011").First(),
                Is.EqualTo("MORPH0012"));
            Assert.That(ids, Does.Contain("MORPH0013"));
            Assert.That(ids.Last(), Is.EqualTo("MORPH0014"));
            Assert.That(
                ids.Select(static id => int.Parse(id[5..])),
                Is.Ordered);
        });
    }

    [Test]
    public void One_driver_actualizes_every_registration_diagnostic_and_restores()
    {
        // lang=c#
        const string validSource =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }
public sealed class Destination { }

public partial class Container
{
    public sealed class Source { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";
        var valid = MappingRegistrationGeneratorTest.Run(validSource);

        // lang=c#
        const string unavailableSource =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }
public sealed class Destination { }

public partial class Container
{
    private sealed class Source { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";
        var unavailable = MappingRegistrationGeneratorTest.Run(
            unavailableSource,
            driver: valid.Driver);

        // lang=c#
        const string unsupportedSource =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }
public sealed class Destination { }

public partial class Container
{
    public sealed class Source { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<T, Destination>();
    }
}
""";
        var unsupported = MappingRegistrationGeneratorTest.Run(
            unsupportedSource,
            driver: unavailable.Driver);

        // lang=c#
        const string duplicateSource =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }
public sealed class Destination { }

public partial class Container
{
    public sealed class Source { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, Destination>();
        }
    }
}
""";
        var duplicate = MappingRegistrationGeneratorTest.Run(
            duplicateSource,
            driver: unsupported.Driver);

        // lang=c#
        const string unifiableSource =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }
public sealed class Destination { }

public partial class Container
{
    public sealed class Source { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Envelope<T>, Destination>();
            builder.Map<Envelope<int>, Destination>();
        }
    }
}
""";
        var unifiable = MappingRegistrationGeneratorTest.Run(
            unifiableSource,
            driver: duplicate.Driver);
        var restored = MappingRegistrationGeneratorTest.Run(
            validSource,
            driver: unifiable.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(valid.Diagnostics, Is.Empty);
            Assert.That(
                unavailable.Diagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0011" }));
            Assert.That(
                unsupported.Diagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0012" }));
            Assert.That(
                duplicate.Diagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0013" }));
            Assert.That(
                unifiable.Diagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0014" }));
            Assert.That(restored.Diagnostics, Is.Empty);
            Assert.That(restored.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [TestCase("MORPH0011")]
    [TestCase("MORPH0013")]
    [TestCase("MORPH0014")]
    public void Suppression_and_severity_are_effective(string id)
    {
        var source = SourceFor(id);
        var visible = MappingRegistrationGeneratorTest.Run(source);
        var suppressed = MappingRegistrationGeneratorTest.Run(
            source,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                [id] = ReportDiagnostic.Suppress
            });
        var warning = MappingRegistrationGeneratorTest.Run(
            source,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                [id] = ReportDiagnostic.Warn
            });

        Assert.Multiple(() =>
        {
            Assert.That(
                visible.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Does.Contain(id));
            Assert.That(suppressed.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                warning.EffectiveDiagnostics.Single().Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
        });
    }

    private static string SourceFor(string id)
    {
        return id switch
        {
            "MORPH0011" =>
"""
using Morphant;
#pragma warning disable CS1591
namespace TestCase;
public sealed class Destination { }
public partial class Container
{
    private sealed class Source { }
    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""",
            "MORPH0013" =>
"""
using Morphant;
#pragma warning disable CS1591
namespace TestCase;
public sealed class Source { }
public sealed class Destination { }
public partial class Container
{
    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, Destination>();
        }
    }
}
""",
            "MORPH0014" =>
"""
using Morphant;
#pragma warning disable CS1591
namespace TestCase;
public sealed class Envelope<T> { }
public sealed class Destination { }
public partial class Container
{
    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Envelope<T>, Destination>();
            builder.Map<Envelope<int>, Destination>();
        }
    }
}
""",
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
    }
}
