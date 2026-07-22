using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateExtensionIncrementalityTest;

namespace Morphant.Generator.UnitTests.TemplateExtensionTests.Incrementality;

[TestFixture]
internal sealed class TemplateExtensionIncrementalLifecycleTests
{
    private const string GeneratedHintName =
        "Morphant.Generated.TemplateExtension.TestCase_GeneratedDestination.g.cs";

    private const string StableHintName =
        "Morphant.Generated.TemplateExtension.TestCase_StableDestination.g.cs";

    private const string DirectHintName =
        "Morphant.Generated.TemplateExtension.TestCase_ZDirectDestination.g.cs";

    [Test]
    public void Adds_and_removes_only_affected_requests()
    {
        var models = SourceFile("Models.cs", ModelsSource);

        RunAndAssert(
            Step(
                "generated destination",
                WithMapper(
                    models,
                    "builder.Map<Source, GeneratedDestination>();"),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "stable destination added",
                WithMapper(
                    models,
                    "builder.Map<Source, GeneratedDestination>();",
                    "builder.Map<Source, StableDestination>();"),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "direct destination added",
                WithMapper(
                    models,
                    "builder.Map<Source, GeneratedDestination>();",
                    "builder.Map<Source, StableDestination>();",
                    "builder.Map<Source, ZDirectDestination>();"),
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.Unchanged)),
            Step(
                "direct destination removed",
                WithMapper(
                    models,
                    "builder.Map<Source, GeneratedDestination>();",
                    "builder.Map<Source, StableDestination>();"),
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.Removed),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.Unchanged)),
            Step(
                "stable destination removed",
                WithMapper(
                    models,
                    "builder.Map<Source, GeneratedDestination>();"),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.Removed)),
            Step(
                "last destination removed",
                WithMapper(models),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Removed)));
    }

    [Test]
    public void Tracks_generated_direct_and_no_surface_transitions()
    {
        var stable = SourceFile(
            "Stable.cs",
            StableDestinationSource);

        RunAndAssert(
            Step(
                "generated destination",
                new[]
                {
                    stable,
                    SourceFile(
                        "Changing.cs",
                        BuildChangingDestinationSource(
                            "public sealed class ZChangingDestination\n" +
                            "    {\n" +
                            "    }")),
                    SourceFile("Mapper.cs", MapperSource)
                },
                Expected(
                    "Morphant.Generated.TemplateExtension." +
                    "TestCase_ZChangingDestination.g.cs",
                    IncrementalStepRunReason.New),
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "direct destination",
                new[]
                {
                    stable,
                    SourceFile(
                        "Changing.cs",
                        BuildChangingDestinationSource(
                            "public enum ZChangingDestination\n" +
                            "    {\n" +
                            "        None\n" +
                            "    }")),
                    SourceFile("Mapper.cs", MapperSource)
                },
                Expected(
                    "Morphant.Generated.TemplateExtension." +
                    "TestCase_ZChangingDestination.g.cs",
                    IncrementalStepRunReason.Modified),
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.Unchanged)),
            Step(
                "unsupported destination",
                new[]
                {
                    stable,
                    SourceFile(
                        "Changing.cs",
                        BuildChangingDestinationSource(
                            "public delegate void " +
                            "ZChangingDestination();")),
                    SourceFile("Mapper.cs", MapperSource)
                },
                Expected(
                    "Morphant.Generated.TemplateExtension." +
                    "TestCase_ZChangingDestination.g.cs",
                    IncrementalStepRunReason.Removed),
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.Unchanged)),
            Step(
                "generated destination restored",
                new[]
                {
                    stable,
                    SourceFile(
                        "Changing.cs",
                        BuildChangingDestinationSource(
                            "public sealed class ZChangingDestination\n" +
                            "    {\n" +
                            "    }")),
                    SourceFile("Mapper.cs", MapperSource)
                },
                Expected(
                    "Morphant.Generated.TemplateExtension." +
                    "TestCase_ZChangingDestination.g.cs",
                    IncrementalStepRunReason.New),
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.Unchanged)));
    }

    [Test]
    public void Tracks_mapper_discovery_and_usage_movement()
    {
        var models = SourceFile(
            "DiscoveryModels.cs",
            DiscoveryModelsSource);
        var stableMapper = SourceFile(
            "StableMapper.cs",
            StableMapperSource);
        var undiscoveredFirstMapper = SourceFile(
            "FirstMapper.cs",
            BuildDiscoveryMapperSource(
                "FirstMapper",
                string.Empty));
        var discoveredFirstMapper = SourceFile(
            "FirstMapper.cs",
            BuildDiscoveryMapperSource(
                "FirstMapper",
                "    [MorphantMapper]\n"));
        var undiscoveredSecondMapper = SourceFile(
            "SecondMapper.cs",
            BuildDiscoveryMapperSource(
                "SecondMapper",
                string.Empty));
        var discoveredSecondMapper = SourceFile(
            "SecondMapper.cs",
            BuildDiscoveryMapperSource(
                "SecondMapper",
                "    [MorphantMapper]\n"));

        const string discoveredHintName =
            "Morphant.Generated.TemplateExtension." +
            "TestCase_ZDiscoveredDestination.g.cs";

        RunAndAssert(
            Step(
                "second mapper is not discovered",
                new[]
                {
                    models,
                    stableMapper,
                    undiscoveredFirstMapper,
                    undiscoveredSecondMapper
                },
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "first mapper is discovered",
                new[]
                {
                    models,
                    stableMapper,
                    discoveredFirstMapper,
                    undiscoveredSecondMapper
                },
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    discoveredHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "usage moves to another discovered mapper",
                new[]
                {
                    models,
                    stableMapper,
                    undiscoveredFirstMapper,
                    discoveredSecondMapper
                },
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    discoveredHintName,
                    IncrementalStepRunReason.Cached)),
            Step(
                "last mapper is no longer discovered",
                new[]
                {
                    models,
                    stableMapper,
                    undiscoveredFirstMapper,
                    undiscoveredSecondMapper
                },
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    discoveredHintName,
                    IncrementalStepRunReason.Removed)));
    }

    [Test]
    public void Adds_and_removes_request_as_generic_usage_closes_and_opens()
    {
        const string closedHintName =
            "Morphant.Generated.TemplateExtension." +
            "TestCase_GenericDestination_1_int___" +
            "b7900cafc9c698ba.g.cs";

        RunAndAssert(
            Step(
                "open generic usage",
                new[]
                {
                    SourceFile(
                        "TestCase.cs",
                        BuildGenericUsageSource("T"))
                }),
            Step(
                "closed generic usage",
                new[]
                {
                    SourceFile(
                        "TestCase.cs",
                        BuildGenericUsageSource("int"))
                },
                Expected(
                    closedHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "open generic usage restored",
                new[]
                {
                    SourceFile(
                        "TestCase.cs",
                        BuildGenericUsageSource("T"))
                },
                Expected(
                    closedHintName,
                    IncrementalStepRunReason.Removed)));
    }

    [Test]
    public void Removes_and_restores_request_as_destination_becomes_file_local()
    {
        const string hintName =
            "Morphant.Generated.TemplateExtension.TestCase_Destination.g.cs";

        RunAndAssert(
            LanguageVersion.CSharp11,
            Step(
                "internal destination",
                new[]
                {
                    SourceFile(
                        "TestCase.cs",
                        BuildFileLocalDestinationSource("internal"))
                },
                Expected(
                    hintName,
                    IncrementalStepRunReason.New)),
            Step(
                "file-local destination",
                new[]
                {
                    SourceFile(
                        "TestCase.cs",
                        BuildFileLocalDestinationSource("file"))
                },
                Expected(
                    hintName,
                    IncrementalStepRunReason.Removed)),
            Step(
                "internal destination restored",
                new[]
                {
                    SourceFile(
                        "TestCase.cs",
                        BuildFileLocalDestinationSource("internal"))
                },
                Expected(
                    hintName,
                    IncrementalStepRunReason.New)));
    }

    private static TemplateExtensionIncrementalitySourceFile[] WithMapper(
        TemplateExtensionIncrementalitySourceFile models,
        params string[] mapStatements)
    {
        return new[]
        {
            models,
            SourceFile(
                "Mapper.cs",
                BuildMapperSource(mapStatements))
        };
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

    private static string BuildChangingDestinationSource(
        string declaration)
    {
        return ChangingDestinationSourceTemplate.Replace(
            "__DECLARATION__",
            declaration);
    }

    private static string BuildDiscoveryMapperSource(
        string mapperName,
        string mapperAttribute)
    {
        return DiscoveryMapperSourceTemplate
            .Replace("__MAPPER_ATTRIBUTE__", mapperAttribute)
            .Replace("__MAPPER_NAME__", mapperName);
    }

    private static string BuildGenericUsageSource(string typeArgument)
    {
        return GenericUsageSourceTemplate.Replace(
            "__TYPE_ARGUMENT__",
            typeArgument);
    }

    private static string BuildFileLocalDestinationSource(
        string accessibility)
    {
        return FileLocalDestinationSourceTemplate.Replace(
            "__ACCESSIBILITY__",
            accessibility);
    }

    // lang=c#
    private const string ModelsSource =
"""
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class GeneratedDestination
    {
    }

    public sealed class StableDestination
    {
    }

    public enum ZDirectDestination
    {
        None
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record GeneratedDestinationMorphantTemplate;

    internal sealed record StableDestinationMorphantTemplate;
}
""";

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
    private const string StableDestinationSource =
"""
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class StableDestination
    {
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record StableDestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string ChangingDestinationSourceTemplate =
"""
#pragma warning disable CS1591

namespace TestCase
{
    __DECLARATION__
}

namespace TestCase.Morphant.Generated
{
    internal sealed record ZChangingDestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string MapperSource =
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
            builder.Map<Source, ZChangingDestination>();
            builder.Map<Source, StableDestination>();
        }
    }
}
""";

    // lang=c#
    private const string DiscoveryModelsSource =
"""
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class StableDestination
    {
    }

    public sealed class ZDiscoveredDestination
    {
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record StableDestinationMorphantTemplate;

    internal sealed record ZDiscoveredDestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string StableMapperSource =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class StableMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, StableDestination>();
        }
    }
}
""";

    // lang=c#
    private const string DiscoveryMapperSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
__MAPPER_ATTRIBUTE__    public partial class __MAPPER_NAME__ : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ZDiscoveredDestination>();
        }
    }
}
""";

    // lang=c#
    private const string GenericUsageSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class GenericDestination<TValue>
    {
    }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, GenericDestination<__TYPE_ARGUMENT__>>();
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record GenericDestinationMorphantTemplate<TValue>;
}
""";

    // lang=c#
    private const string FileLocalDestinationSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    __ACCESSIBILITY__ sealed class Destination
    {
    }

    public sealed class Source
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}
""";
}
