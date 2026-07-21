using Microsoft.CodeAnalysis;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeIncrementalityTest;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Incrementality;

[TestFixture]
internal sealed class TemplateTypeIncrementalLifecycleTests
{
    private const string DestinationAHintName =
        "Morphant.TemplateType.TestCase_DestinationA.g.cs";

    private const string DestinationBHintName =
        "Morphant.TemplateType.TestCase_DestinationB.g.cs";

    [Test]
    public void Adds_and_removes_only_affected_model_and_request()
    {
        var destinationFiles = new[]
        {
            SourceFile(
                "DestinationA.cs",
                BuildDestinationSource("DestinationA")),
            SourceFile(
                "DestinationB.cs",
                BuildDestinationSource("DestinationB"))
        };

        RunAndAssert(
            Step(
                "first destination added",
                WithMapper(
                    destinationFiles,
                    "builder.Map<Source, DestinationA>();"),
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "second destination added",
                WithMapper(
                    destinationFiles,
                    "builder.Map<Source, DestinationA>();",
                    "builder.Map<Source, DestinationB>();"),
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "second destination removed",
                WithMapper(
                    destinationFiles,
                    "builder.Map<Source, DestinationA>();"),
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.Removed)),
            Step(
                "last destination removed",
                WithMapper(destinationFiles),
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.Removed)));
    }

    [Test]
    public void Removes_and_restores_model_when_destination_becomes_direct()
    {
        var destinationFile = SourceFile(
            "Destination.cs",
            BuildDestinationSource("Destination"));

        RunAndAssert(
            Step(
                "generated destination",
                WithMapper(
                    new[] { destinationFile },
                    "builder.Map<Source, Destination>();"),
                Expected(
                    "Morphant.TemplateType.TestCase_Destination.g.cs",
                    IncrementalStepRunReason.New)),
            Step(
                "direct destination",
                WithMapper(
                    new[] { destinationFile },
                    "builder.Map<Source, int>();"),
                Expected(
                    "Morphant.TemplateType.TestCase_Destination.g.cs",
                    IncrementalStepRunReason.Removed)),
            Step(
                "generated destination restored",
                WithMapper(
                    new[] { destinationFile },
                    "builder.Map<Source, Destination>();"),
                Expected(
                    "Morphant.TemplateType.TestCase_Destination.g.cs",
                    IncrementalStepRunReason.New)));
    }

    private static TemplateTypeIncrementalitySourceFile[] WithMapper(
        IEnumerable<TemplateTypeIncrementalitySourceFile> destinationFiles,
        params string[] mapStatements)
    {
        return destinationFiles.Prepend(
                SourceFile(
                    "Mapper.cs",
                    BuildMapperSource(mapStatements)))
            .ToArray();
    }

    private static string BuildMapperSource(
        IReadOnlyCollection<string> mapStatements)
    {
        return MapperSourceTemplate.Replace(
            "__MAP_STATEMENTS__",
            string.Join(
                "\n",
                mapStatements.Select(static statement =>
                    "            " + statement)));
    }

    private static string BuildDestinationSource(string typeName)
    {
        return DestinationSourceTemplate.Replace(
            "__TYPE_NAME__",
            typeName);
    }

    // lang=c#
    private const string MapperSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
__MAP_STATEMENTS__
        }
    }
}
""";

    // lang=c#
    private const string DestinationSourceTemplate =
"""
namespace TestCase
{
    public sealed class __TYPE_NAME__
    {
        public int Id { get; set; }
    }
}
""";
}
