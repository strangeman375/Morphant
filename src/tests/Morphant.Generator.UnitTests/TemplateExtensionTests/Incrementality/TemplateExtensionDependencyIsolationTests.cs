using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateExtensionIncrementalityTest;

namespace Morphant.Generator.UnitTests.TemplateExtensionTests.Incrementality;

[TestFixture]
internal sealed class TemplateExtensionDependencyIsolationTests
{
    private const string DestinationAHintName =
        "Morphant.TemplateExtensions.TestCase_DestinationA.g.cs";

    private const string DestinationBHintName =
        "Morphant.TemplateExtensions.TestCase_DestinationB.g.cs";

    private const string StableHintName =
        "Morphant.TemplateExtensions.TestCase_ZStableDestination.g.cs";

    [Test]
    public void Rebuilds_only_request_whose_surface_changes()
    {
        RunAndAssert(
            Step(
                "reference destinations",
                BuildSurfaceSourceFiles(
                    "public sealed class DestinationA",
                    "DestinationB"),
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "first destination becomes a value type",
                BuildSurfaceSourceFiles(
                    "public struct DestinationA",
                    "DestinationB"),
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.Modified),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.Unchanged)),
            Step(
                "second destination becomes nullable",
                BuildSurfaceSourceFiles(
                    "public struct DestinationA",
                    "DestinationB?"),
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.Modified)),
            Step(
                "second destination becomes non-nullable",
                BuildSurfaceSourceFiles(
                    "public struct DestinationA",
                    "DestinationB"),
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.Modified)));
    }

    [Test]
    public void Rebuilds_only_request_affected_by_global_alias_rebinding()
    {
        var stableFiles = new[]
        {
            SourceFile("Models.cs", AliasModelsSource),
            SourceFile("Mapper.cs", AliasMapperSource)
        };

        RunAndAssert(
            LanguageVersion.CSharp10,
            Step(
                "alias targets first destination",
                stableFiles.Append(
                        SourceFile(
                            "GlobalUsings.cs",
                            BuildGlobalAliasSource(
                                "FirstDestination")))
                    .ToArray(),
                Expected(
                    "Morphant.TemplateExtensions." +
                    "TestCase_FirstDestination.g.cs",
                    IncrementalStepRunReason.New),
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "alias targets second destination",
                stableFiles.Append(
                        SourceFile(
                            "GlobalUsings.cs",
                            BuildGlobalAliasSource(
                                "SecondDestination")))
                    .ToArray(),
                Expected(
                    "Morphant.TemplateExtensions." +
                    "TestCase_SecondDestination.g.cs",
                    IncrementalStepRunReason.Modified),
                Expected(
                    StableHintName,
                    IncrementalStepRunReason.Unchanged)));
    }

    [Test]
    public void Keeps_requests_cached_when_referenced_details_change()
    {
        var initialChangingReference = CreateReference(
            "ExternalChanging",
            BuildReferencedDestinationSource(
                "ExternalChanging",
                "public sealed class Destination\n" +
                "{\n" +
                "    public int Id { get; set; }\n" +
                "}"));

        var editedChangingReference = CreateReference(
            "ExternalChanging",
            BuildReferencedDestinationSource(
                "ExternalChanging",
                "/// <summary>Updated.</summary>\n" +
                "public sealed class Destination\n" +
                "{\n" +
                "    public string Name { get; init; } = string.Empty;\n" +
                "}"));

        var stableReference = CreateReference(
            "ExternalStable",
            BuildReferencedDestinationSource(
                "ExternalStable",
                "public sealed class Destination\n" +
                "{\n" +
                "}"));

        var sourceFiles = new[]
        {
            SourceFile("Mapper.cs", ReferencedMapperSource),
            SourceFile("Templates.cs", ReferencedTemplatesSource)
        };

        RunAndAssert(
            Step(
                "initial references",
                sourceFiles,
                new MetadataReference[]
                {
                    initialChangingReference,
                    stableReference
                },
                Expected(
                    "Morphant.TemplateExtensions." +
                    "ExternalChanging_Destination.g.cs",
                    IncrementalStepRunReason.New),
                Expected(
                    "Morphant.TemplateExtensions." +
                    "ExternalStable_Destination.g.cs",
                    IncrementalStepRunReason.New)),
            Step(
                "referenced details changed",
                sourceFiles,
                new MetadataReference[]
                {
                    editedChangingReference,
                    stableReference
                },
                Expected(
                    "Morphant.TemplateExtensions." +
                    "ExternalChanging_Destination.g.cs",
                    IncrementalStepRunReason.Cached),
                Expected(
                    "Morphant.TemplateExtensions." +
                    "ExternalStable_Destination.g.cs",
                    IncrementalStepRunReason.Cached)));
    }

    [Test]
    public void Rebuilds_only_request_whose_referenced_surface_changes()
    {
        var classReference = CreateReference(
            "ExternalChanging",
            BuildReferencedDestinationSource(
                "ExternalChanging",
                "public sealed class Destination\n" +
                "{\n" +
                "}"));

        var structReference = CreateReference(
            "ExternalChanging",
            BuildReferencedDestinationSource(
                "ExternalChanging",
                "public struct Destination\n" +
                "{\n" +
                "}"));

        var stableReference = CreateReference(
            "ExternalStable",
            BuildReferencedDestinationSource(
                "ExternalStable",
                "public sealed class Destination\n" +
                "{\n" +
                "}"));

        var sourceFiles = new[]
        {
            SourceFile("Mapper.cs", ReferencedMapperSource),
            SourceFile("Templates.cs", ReferencedTemplatesSource)
        };

        RunAndAssert(
            Step(
                "referenced class",
                sourceFiles,
                new MetadataReference[]
                {
                    classReference,
                    stableReference
                },
                Expected(
                    "Morphant.TemplateExtensions." +
                    "ExternalChanging_Destination.g.cs",
                    IncrementalStepRunReason.New),
                Expected(
                    "Morphant.TemplateExtensions." +
                    "ExternalStable_Destination.g.cs",
                    IncrementalStepRunReason.New)),
            Step(
                "referenced destination becomes a value type",
                sourceFiles,
                new MetadataReference[]
                {
                    structReference,
                    stableReference
                },
                Expected(
                    "Morphant.TemplateExtensions." +
                    "ExternalChanging_Destination.g.cs",
                    IncrementalStepRunReason.Modified),
                Expected(
                    "Morphant.TemplateExtensions." +
                    "ExternalStable_Destination.g.cs",
                    IncrementalStepRunReason.Unchanged)));
    }

    private static TemplateExtensionIncrementalitySourceFile[]
        BuildSurfaceSourceFiles(
            string destinationADeclaration,
            string destinationBUsage)
    {
        return new[]
        {
            SourceFile(
                "Models.cs",
                SurfaceModelsSourceTemplate.Replace(
                    "__DESTINATION_A_DECLARATION__",
                    destinationADeclaration)),
            SourceFile(
                "Mapper.cs",
                SurfaceMapperSourceTemplate.Replace(
                    "__DESTINATION_B_USAGE__",
                    destinationBUsage))
        };
    }

    private static string BuildGlobalAliasSource(string destinationName)
    {
        return GlobalAliasSourceTemplate.Replace(
            "__DESTINATION_NAME__",
            destinationName);
    }

    private static string BuildReferencedDestinationSource(
        string destinationNamespace,
        string destinationDeclaration)
    {
        return ReferencedDestinationSourceTemplate
            .Replace(
                "__DESTINATION_NAMESPACE__",
                destinationNamespace)
            .Replace(
                "__DESTINATION_DECLARATION__",
                destinationDeclaration);
    }

    // lang=c#
    private const string SurfaceModelsSourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

namespace TestCase
{
    __DESTINATION_A_DECLARATION__
    {
    }

    public sealed class DestinationB
    {
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationAMorphantTemplate;

    internal sealed record DestinationBMorphantTemplate;
}
""";

    // lang=c#
    private const string SurfaceMapperSourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

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
            builder.Map<Source, DestinationA>();
            builder.Map<Source, __DESTINATION_B_USAGE__>();
        }
    }
}
""";

    // lang=c#
    private const string AliasModelsSource =
"""
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class FirstDestination
    {
    }

    public sealed class SecondDestination
    {
    }

    public sealed class ZStableDestination
    {
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record FirstDestinationMorphantTemplate;

    internal sealed record SecondDestinationMorphantTemplate;

    internal sealed record ZStableDestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string AliasMapperSource =
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
            builder.Map<Source, DestinationAlias>();
            builder.Map<Source, ZStableDestination>();
        }
    }
}
""";

    // lang=c#
    private const string GlobalAliasSourceTemplate =
"""
global using DestinationAlias = TestCase.__DESTINATION_NAME__;
""";

    // lang=c#
    private const string ReferencedDestinationSourceTemplate =
"""
#nullable enable

namespace __DESTINATION_NAMESPACE__
{
    __DESTINATION_DECLARATION__
}
""";

    // lang=c#
    private const string ReferencedMapperSource =
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
            builder.Map<Source, ExternalChanging.Destination>();
            builder.Map<Source, ExternalStable.Destination>();
        }
    }
}
""";

    // lang=c#
    private const string ReferencedTemplatesSource =
"""
#pragma warning disable CS1591

namespace ExternalChanging.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}

namespace ExternalStable.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}
""";
}
