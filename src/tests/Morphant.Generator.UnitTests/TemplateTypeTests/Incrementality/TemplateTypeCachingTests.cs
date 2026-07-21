using Microsoft.CodeAnalysis;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeIncrementalityTest;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Incrementality;

[TestFixture]
internal sealed class TemplateTypeCachingTests
{
    private const string DestinationHintName =
        "Morphant.TemplateType.TestCase_Destination.g.cs";

    private const string DestinationAHintName =
        "Morphant.TemplateType.TestCase_DestinationA.g.cs";

    private const string DestinationBHintName =
        "Morphant.TemplateType.TestCase_DestinationB.g.cs";

    [Test]
    public void Caches_model_and_request_when_inputs_are_unchanged()
    {
        var sourceFiles = new[]
        {
            SourceFile(
                "Mapper.cs",
                BuildMapperSource(
                    "builder.Map<SourceA, Destination>();")),
            SourceFile("Destination.cs", DestinationSource)
        };

        RunAndAssert(
            Step(
                "initial generation",
                sourceFiles,
                Expected(
                    DestinationHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "unchanged inputs",
                sourceFiles,
                Expected(
                    DestinationHintName,
                    IncrementalStepRunReason.Cached)));
    }

    [Test]
    public void Keeps_model_and_request_cached_when_unrelated_file_changes()
    {
        var initialFiles = new[]
        {
            SourceFile(
                "Mapper.cs",
                BuildMapperSource(
                    "builder.Map<SourceA, Destination>();")),
            SourceFile("Destination.cs", DestinationSource),
            SourceFile(
                "Unrelated.cs",
                BuildUnrelatedSource(1))
        };

        var updatedFiles = new[]
        {
            initialFiles[0],
            initialFiles[1],
            SourceFile(
                "Unrelated.cs",
                BuildUnrelatedSource(2))
        };

        RunAndAssert(
            Step(
                "initial generation",
                initialFiles,
                Expected(
                    DestinationHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "unrelated file changed",
                updatedFiles,
                Expected(
                    DestinationHintName,
                    IncrementalStepRunReason.Cached)));
    }

    [Test]
    public void Keeps_request_cached_when_rebuilt_model_is_equal()
    {
        // lang=c#
        const string initialDestination =
"""
#nullable enable

namespace TestCase
{
    public sealed class Destination
    {
        /// <summary>Initial wording.</summary>
        public int Id { get; set; }
    }
}
""";

        // lang=c#
        const string updatedDestination =
"""
#nullable enable

namespace TestCase
{
    public sealed class Destination
    {
        /// <summary>Updated wording.</summary>
        public int Id { get; set; }
    }
}
""";

        var mapperFile = SourceFile(
            "Mapper.cs",
            BuildMapperSource(
                "builder.Map<SourceA, Destination>();"));

        RunAndAssert(
            Step(
                "initial documentation",
                new[]
                {
                    mapperFile,
                    SourceFile("Destination.cs", initialDestination)
                },
                Expected(
                    DestinationHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "documentation wording changed",
                new[]
                {
                    mapperFile,
                    SourceFile("Destination.cs", updatedDestination)
                },
                Expected(
                    DestinationHintName,
                    IncrementalStepRunReason.Unchanged,
                    IncrementalStepRunReason.Cached)));
    }

    [Test]
    public void Keeps_models_and_requests_cached_when_only_map_usages_change()
    {
        var destinationFiles = new[]
        {
            SourceFile(
                "DestinationA.cs",
                BuildDestinationSource("DestinationA", "int Id")),
            SourceFile(
                "DestinationB.cs",
                BuildDestinationSource("DestinationB", "string Name"))
        };

        RunAndAssert(
            Step(
                "initial map usages",
                destinationFiles.Prepend(
                    SourceFile(
                        "Mapper.cs",
                        BuildMapperSource(
                            "builder.Map<SourceA, DestinationA>();",
                            "builder.Map<SourceB, DestinationB>();")))
                    .ToArray(),
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "map usages reordered",
                destinationFiles.Prepend(
                    SourceFile(
                        "Mapper.cs",
                        BuildMapperSource(
                            "builder.Map<SourceB, DestinationB>();",
                            "builder.Map<SourceA, DestinationA>();")))
                    .ToArray(),
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.Cached)),
            Step(
                "source and duplicate usages changed",
                destinationFiles.Prepend(
                    SourceFile(
                        "Mapper.cs",
                        BuildMapperSource(
                            "builder.Map<SourceC, DestinationA>();",
                            "builder.Map<SourceA, DestinationB>();",
                            "builder.Map<SourceB, DestinationB>();")))
                    .ToArray(),
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.Cached)));
    }

    private static string BuildMapperSource(
        params string[] mapStatements)
    {
        return MapperSourceTemplate.Replace(
            "__MAP_STATEMENTS__",
            string.Join(
                "\n",
                mapStatements.Select(static statement =>
                    "            " + statement)));
    }

    private static string BuildDestinationSource(
        string typeName,
        string member)
    {
        return DestinationSourceTemplate
            .Replace("__TYPE_NAME__", typeName)
            .Replace("__MEMBER__", member);
    }

    private static string BuildUnrelatedSource(int version)
    {
        return UnrelatedSourceTemplate.Replace(
            "__VERSION__",
            version.ToString());
    }

    // lang=c#
    private const string MapperSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class SourceA
    {
    }

    public sealed class SourceB
    {
    }

    public sealed class SourceC
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
    private const string DestinationSource =
"""
#nullable enable

namespace TestCase
{
    public sealed class Destination
    {
        public int Id { get; set; }
    }
}
""";

    // lang=c#
    private const string DestinationSourceTemplate =
"""
#nullable enable

namespace TestCase
{
    public sealed class __TYPE_NAME__
    {
        public __MEMBER__ { get; set; } = default!;
    }
}
""";

    // lang=c#
    private const string UnrelatedSourceTemplate =
"""
namespace Unrelated
{
    internal static class VersionInfo
    {
        public const int Version = __VERSION__;
    }
}
""";
}
