namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

[TestFixture]
internal sealed class ReachabilityTests
{
    [Test]
    public void Missing_construction_uses_only_enabled_no_previous_paths()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public interface ACreate { }
    public interface BUpdateCreate { }
    public interface CUpdateThrow { }
    public interface DBothThrow { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ACreate>(MappingMode.Create);
            builder.Map<Source, BUpdateCreate>(MappingMode.Update)
                .NullDestinationHandling(NullDestinationHandling.Create);
            builder.Map<Source, CUpdateThrow>(MappingMode.Update)
                .NullDestinationHandling(NullDestinationHandling.Throw);
            builder.Map<Source, DBothThrow>(MappingMode.CreateAndUpdate)
                .NullDestinationHandling(NullDestinationHandling.Throw);
        }
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.ConstructionDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0035",
                    "MORPH0035",
                    "MORPH0035"
                }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    MissingMessage("ACreate", "Create"),
                    MissingMessage(
                        "BUpdateCreate",
                        "Update without an existing destination"),
                    MissingMessage("DBothThrow", "Create")
                }));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    ConstructionDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.All.EqualTo("Map"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Runtime_manual_and_existing_only_policies_are_not_analyzed()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public interface IRuntimeDestination { }

    public sealed class RuntimeDestination : IRuntimeDestination
    {
        public static RuntimeDestination Instance { get; } = new();
    }

    public sealed class StructuredDestination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, IRuntimeDestination>()
                .ConstructUsing(source => RuntimeDestination.Instance);
            builder.Map<Source, RuntimeDestination>()
                .ResolveUsing((source, previous) => null!);
            builder.Map<Source, object>()
                .Convert(source => null!);
            builder.Map<Source, StructuredDestination>();
            builder.Map<Source, IRuntimeDestination>(MappingMode.Update)
                .NullDestinationHandling(NullDestinationHandling.Throw);
        }
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.ConstructionDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static string MissingMessage(
        string destination,
        string paths)
    {
        return $"Mapping 'TestCase.Source -> TestCase.{destination}' " +
            $"cannot create a destination. Affected cases: {paths}.";
    }
}
