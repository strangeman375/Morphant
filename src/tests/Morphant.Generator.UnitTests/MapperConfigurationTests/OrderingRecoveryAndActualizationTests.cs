using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests.MapperConfigurationTests;

[TestFixture]
internal sealed class OrderingRecoveryAndActualizationTests
{
    [TestCase("MORPH0015")]
    [TestCase("MORPH0016")]
    [TestCase("MORPH0017")]
    [TestCase("MORPH0018")]
    public void Suppression_and_severity_change_only_presentation(string id)
    {
        var testCase = BuildCase(id);
        var visible = MapperConfigurationGeneratorTest.Run(
            testCase.Source,
            additionalReferences: testCase.References);
        var suppressed = MapperConfigurationGeneratorTest.Run(
            testCase.Source,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                [id] = ReportDiagnostic.Suppress
            },
            additionalReferences: testCase.References);
        var warning = MapperConfigurationGeneratorTest.Run(
            testCase.Source,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                [id] = ReportDiagnostic.Warn
            },
            additionalReferences: testCase.References);

        Assert.Multiple(() =>
        {
            Assert.That(
                visible.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { id }));
            Assert.That(suppressed.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                warning.EffectiveDiagnostics.Single().Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(
                Artifacts(suppressed),
                Is.EqualTo(Artifacts(visible)));
            Assert.That(
                Artifacts(warning),
                Is.EqualTo(Artifacts(visible)));
            Assert.That(visible.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(suppressed.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(warning.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Publication_order_is_by_id_before_mapper_and_source_order()
    {
        var metadataBase = BuildMetadataBase();

        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;
using SharedConfiguration;

#pragma warning disable CS1591

namespace TestCase;

public sealed class SourceA { }
public sealed class DestinationA { }
public sealed class SourceB { }
public sealed class DestinationB { }
public sealed class SourceC { }
public sealed class DestinationC { }

[MorphantMapper]
public partial class PairFlowMapper : TypeMapper<PairFlowMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        var mapping = builder.Map<SourceC, DestinationC>();
        _ = mapping;
    }
}

[MorphantMapper]
public partial class RootFlowMapper : TypeMapper<RootFlowMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        var alias = builder;
        alias.Map<SourceB, DestinationB>();
    }
}

[MorphantMapper]
public partial class UnavailableMapper : MetadataBaseMapper<UnavailableMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<SourceA, DestinationA>();
    }
}

[MorphantMapper]
public abstract partial class MissingMapper : TypeMapper<MissingMapper>
{
}
""";

        var result = MapperConfigurationGeneratorTest.Run(
            source,
            additionalReferences: [metadataBase]);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0015",
                    "MORPH0016",
                    "MORPH0017",
                    "MORPH0018"
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Preserves_independent_flow_reasons_and_discards_duplicate_flow()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class SourceA { }
public sealed class DestinationA { }
public sealed class SourceB { }
public sealed class DestinationB { }

[MorphantMapper]
public partial class IndependentReasonsMapper : TypeMapper<IndependentReasonsMapper>
{
    private static void Observe(MapperBuilder builder) { }

    protected override void Configure(MapperBuilder builder)
    {
        Observe(builder);
        var mapping = builder.Map<SourceA, DestinationA>();
        _ = mapping;
    }
}

[MorphantMapper]
public partial class DuplicateMapper : TypeMapper<DuplicateMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<SourceB, DestinationB>();
        var duplicate = builder.Map<SourceB, DestinationB>();
        _ = duplicate;
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0013",
                    "MORPH0017",
                    "MORPH0018"
                }));
            Assert.That(
                result.Diagnostics.Count(static diagnostic =>
                    diagnostic.Id == "MORPH0018"),
                Is.EqualTo(1));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Pair_structure_does_not_hide_an_independent_pair_flow_break()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Destination { }

public partial class Container
{
    private sealed class HiddenSource { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var mapping = builder.Map<HiddenSource, Destination>();
            _ = mapping;
        }
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0011", "MORPH0018" }));
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void One_driver_actualizes_every_configuration_gate_and_restores()
    {
        var missing = MapperConfigurationGeneratorTest.Run(MissingSource);
        var rootFlow = MapperConfigurationGeneratorTest.Run(
            RootFlowSource,
            driver: missing.Driver);
        var pairFlow = MapperConfigurationGeneratorTest.Run(
            PairFlowSource,
            driver: rootFlow.Driver);
        var unavailableBase = MapperConfigurationGeneratorTest.Run(
            UnavailableBaseSource,
            driver: pairFlow.Driver,
            additionalReferences: [BuildMetadataBase()]);
        var restored = MapperConfigurationGeneratorTest.Run(
            ValidSource,
            driver: unavailableBase.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(Ids(missing), Is.EqualTo(new[] { "MORPH0015" }));
            Assert.That(Ids(unavailableBase),
                Is.EqualTo(new[] { "MORPH0016" }));
            Assert.That(Ids(rootFlow), Is.EqualTo(new[] { "MORPH0017" }));
            Assert.That(Ids(pairFlow), Is.EqualTo(new[] { "MORPH0018" }));
            Assert.That(restored.Diagnostics, Is.Empty);
            Assert.That(restored.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static string[] Ids(MapperConfigurationGeneratorResult result)
    {
        return result.Diagnostics
            .Select(static diagnostic => diagnostic.Id)
            .ToArray();
    }

    private static (string HintName, string Source)[] Artifacts(
        MapperConfigurationGeneratorResult result)
    {
        return result.GeneratedSources
            .Select(static generated =>
                (generated.HintName, generated.SourceText.ToString()))
            .ToArray();
    }

    private static ConfigurationCase BuildCase(string id)
    {
        return id switch
        {
            "MORPH0015" => new ConfigurationCase(MissingSource, []),
            "MORPH0016" => new ConfigurationCase(
                UnavailableBaseSource,
                [BuildMetadataBase()]),
            "MORPH0017" => new ConfigurationCase(RootFlowSource, []),
            "MORPH0018" => new ConfigurationCase(PairFlowSource, []),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
    }

    private static MetadataReference BuildMetadataBase()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

namespace SharedConfiguration;

public abstract class MetadataBaseMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : MetadataBaseMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.MappingMode(MappingMode.Create);
    }
}
""";

        return MapperConfigurationGeneratorTest.CompileReference(
            "MapperConfigurationMetadataBase",
            source);
    }

    // lang=c#
    private const string MissingSource =
"""
#nullable enable
using Morphant;
#pragma warning disable CS1591
namespace TestCase;
[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper> { }
""";

    // lang=c#
    private const string RootFlowSource =
"""
#nullable enable
using Morphant;
#pragma warning disable CS1591
namespace TestCase;
public sealed class Source { }
public sealed class Destination { }
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        var alias = builder;
        alias.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string PairFlowSource =
"""
#nullable enable
using Morphant;
#pragma warning disable CS1591
namespace TestCase;
public sealed class Source { }
public sealed class Destination { }
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        var mapping = builder.Map<Source, Destination>();
        _ = mapping;
    }
}
""";

    // lang=c#
    private const string UnavailableBaseSource =
"""
#nullable enable
using Morphant;
using SharedConfiguration;
#pragma warning disable CS1591
namespace TestCase;
public sealed class Source { }
public sealed class Destination { }
[MorphantMapper]
public partial class TestMapper : MetadataBaseMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string ValidSource =
"""
#nullable enable
using Morphant;
#pragma warning disable CS1591
namespace TestCase;
public sealed class Source { public int Value { get; set; } }
public sealed class Destination { public int Value { get; set; } }
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

    private sealed record ConfigurationCase(
        string Source,
        IReadOnlyCollection<MetadataReference> References);
}
