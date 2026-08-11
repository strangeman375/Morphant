namespace Morphant.Generator.UnitTests.InheritanceDiagnosticsTests;

[TestFixture]
internal sealed class BaseConfigurationTests
{
    [Test]
    public void Reports_each_extra_call_once_for_a_generic_source_level()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source<T>
    {
    }

    public sealed class Destination<T>
    {
    }

    public abstract class RootMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
        }
    }

    public abstract class DuplicateMapper<T> : RootMapper<T>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            base.Configure(builder);
            base.Configure(builder);
        }
    }

    [MorphantMapper]
    public partial class IntMapper : DuplicateMapper<int>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<int>, Destination<int>>();
        }
    }

    [MorphantMapper]
    public partial class StringMapper : DuplicateMapper<string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<string>, Destination<string>>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0024", "MORPH0024" }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.All.EqualTo(
                    "Base configuration is included more than once in " +
                    "Configure of mapper " +
                    "'global::TestCase.DuplicateMapper<T>'."));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Has.All.EqualTo("Configure"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations.Count),
                Has.All.EqualTo(1));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.AdditionalLocations[0])),
                Has.All.EqualTo("Configure"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Accepts_one_direct_call_on_each_connected_level()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class Destination
    {
    }

    public abstract class FarMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
        }
    }

    public abstract class NearMapper : FarMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            base.Configure(builder);
    }

    [MorphantMapper]
    public partial class TestMapper : NearMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder!);
            builder.Map<Source, Destination>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Keeps_an_unavailable_base_diagnostic_with_the_visible_duplicate()
    {
        // lang=c#
        const string metadataSource =
"""
#nullable enable

using Morphant;

namespace Shared;

public abstract class ExternalBase : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
    }
}
""";

        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Shared;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : ExternalBase
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            base.Configure(builder);
            builder.Map<Source, Destination>();
        }
    }
}
""";

        var reference = InheritanceDiagnosticsGeneratorTest.CompileReference(
            "InheritanceDiagnosticsMetadataBase",
            metadataSource);
        var result = InheritanceDiagnosticsGeneratorTest.Run(
            source,
            additionalReferences: [reference]);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0016", "MORPH0024" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
