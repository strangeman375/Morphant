using Microsoft.CodeAnalysis;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateExtensionIncrementalityTest;

namespace Morphant.Generator.UnitTests.TemplateExtensionTests.Incrementality;

[TestFixture]
internal sealed class TemplateExtensionCachingTests
{
    private const string GeneratedHintName =
        "Morphant.Generated.TemplateExtension.TestCase_GeneratedDestination.g.cs";

    private const string DirectHintName =
        "Morphant.Generated.TemplateExtension.TestCase_DirectDestination.g.cs";

    [Test]
    public void Caches_generated_and_direct_requests_when_inputs_are_unchanged()
    {
        var sourceFiles = BuildSourceFiles(
            BuildModelsSource(
                "        public int Id { get; set; }",
                "        First"),
            BuildTemplateSource(
                "        public int Id { get; init; }"),
            BuildMapperSource(
                "builder.Map<SourceA, GeneratedDestination>();",
                "builder.Map<SourceB, DirectDestination>();"),
            BuildUnrelatedSource(1));

        RunAndAssert(
            Step(
                "initial generation",
                sourceFiles,
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "unchanged inputs",
                sourceFiles,
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Cached)));
    }

    [Test]
    public void Keeps_requests_cached_when_unrelated_file_changes()
    {
        var models = SourceFile(
            "Models.cs",
            BuildModelsSource(
                "        public int Id { get; set; }",
                "        First"));
        var template = SourceFile(
            "Template.cs",
            BuildTemplateSource(
                "        public int Id { get; init; }"));
        var mapper = SourceFile(
            "Mapper.cs",
            BuildMapperSource(
                "builder.Map<SourceA, GeneratedDestination>();",
                "builder.Map<SourceB, DirectDestination>();"));

        RunAndAssert(
            Step(
                "initial unrelated declaration",
                new[]
                {
                    models,
                    template,
                    mapper,
                    SourceFile(
                        "Unrelated.cs",
                        BuildUnrelatedSource(1))
                },
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "unrelated declaration changed",
                new[]
                {
                    models,
                    template,
                    mapper,
                    SourceFile(
                        "Unrelated.cs",
                        BuildUnrelatedSource(2))
                },
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Cached)));
    }

    [Test]
    public void Keeps_requests_cached_when_destination_details_change()
    {
        var mapper = SourceFile(
            "Mapper.cs",
            BuildMapperSource(
                "builder.Map<SourceA, GeneratedDestination>();",
                "builder.Map<SourceB, DirectDestination>();"));

        RunAndAssert(
            Step(
                "initial destination details",
                new[]
                {
                    mapper,
                    SourceFile(
                        "Models.cs",
                        BuildModelsSource(
                            "        public int Id { get; set; }",
                            "        First")),
                    SourceFile(
                        "Template.cs",
                        BuildTemplateSource(
                            "        public int Id { get; init; }"))
                },
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "generated destination details changed",
                new[]
                {
                    mapper,
                    SourceFile(
                        "Models.cs",
                        BuildModelsSource(
                            "        /// <summary>Updated.</summary>\n" +
                            "        public long Value { get; init; }",
                            "        First")),
                    SourceFile(
                        "Template.cs",
                        BuildTemplateSource(
                            "        public int Id { get; init; }"))
                },
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Cached)),
            Step(
                "template declaration details changed",
                new[]
                {
                    mapper,
                    SourceFile(
                        "Models.cs",
                        BuildModelsSource(
                            "        /// <summary>Updated.</summary>\n" +
                            "        public long Value { get; init; }",
                            "        First")),
                    SourceFile(
                        "Template.cs",
                        BuildTemplateSource(
                            "        public long Value { get; init; }"))
                },
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Cached)),
            Step(
                "direct destination details changed",
                new[]
                {
                    mapper,
                    SourceFile(
                        "Models.cs",
                        BuildModelsSource(
                            "        /// <summary>Updated.</summary>\n" +
                            "        public long Value { get; init; }",
                            "        /// <summary>Added.</summary>\n" +
                            "        Second")),
                    SourceFile(
                        "Template.cs",
                        BuildTemplateSource(
                            "        public long Value { get; init; }"))
                },
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Cached)));
    }

    [Test]
    public void Keeps_requests_cached_when_only_map_usage_details_change()
    {
        var models = SourceFile(
            "Models.cs",
            BuildModelsSource(
                "        public int Id { get; set; }",
                "        First"));
        var template = SourceFile(
            "Template.cs",
            BuildTemplateSource(string.Empty));

        RunAndAssert(
            Step(
                "initial usages",
                new[]
                {
                    models,
                    template,
                    SourceFile(
                        "Mapper.cs",
                        BuildMapperSource(
                            "builder.Map<SourceA, " +
                            "GeneratedDestination>();",
                            "builder.Map<SourceB, " +
                            "DirectDestination>();"))
                },
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "sources modes order and duplicates changed",
                new[]
                {
                    models,
                    template,
                    SourceFile(
                        "Mapper.cs",
                        BuildMapperSource(
                            "builder.Map<SourceC, DirectDestination>(" +
                            "MappingMode.MapExisting);",
                            "builder.Map<SourceB, GeneratedDestination>(" +
                            "MappingMode.MapNew);",
                            "builder.Map<SourceA, DirectDestination>();",
                            "builder.Map<SourceC, " +
                            "GeneratedDestination>();"))
                },
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Cached)));
    }

    [Test]
    public void Keeps_requests_cached_when_nullable_project_setting_changes()
    {
        var sourceFiles = BuildSourceFiles(
            BuildModelsSource(
                "        public string Name { get; set; }",
                "        First"),
            BuildTemplateSource(string.Empty),
            BuildMapperSource(
                "builder.Map<SourceA, GeneratedDestination>();",
                "builder.Map<SourceB, DirectDestination>();"),
            BuildUnrelatedSource(1));

        RunAndAssert(
            Step(
                "nullable disabled",
                sourceFiles,
                NullableContextOptions.Disable,
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "nullable enabled",
                sourceFiles,
                NullableContextOptions.Enable,
                Expected(
                    DirectHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    GeneratedHintName,
                    IncrementalStepRunReason.Cached)));
    }

    private static TemplateExtensionIncrementalitySourceFile[]
        BuildSourceFiles(
            string models,
            string template,
            string mapper,
            string unrelated)
    {
        return new[]
        {
            SourceFile("Models.cs", models),
            SourceFile("Template.cs", template),
            SourceFile("Mapper.cs", mapper),
            SourceFile("Unrelated.cs", unrelated)
        };
    }

    private static string BuildModelsSource(
        string generatedDestinationBody,
        string directDestinationBody)
    {
        return ModelsSourceTemplate
            .Replace(
                "__GENERATED_DESTINATION_BODY__",
                generatedDestinationBody)
            .Replace(
                "__DIRECT_DESTINATION_BODY__",
                directDestinationBody);
    }

    private static string BuildTemplateSource(string templateBody)
    {
        return TemplateSourceTemplate.Replace(
            "__TEMPLATE_BODY__",
            templateBody);
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

    private static string BuildUnrelatedSource(int version)
    {
        return UnrelatedSourceTemplate.Replace(
            "__VERSION__",
            version.ToString());
    }

    // lang=c#
    private const string ModelsSourceTemplate =
"""
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class GeneratedDestination
    {
__GENERATED_DESTINATION_BODY__
    }

    public enum DirectDestination
    {
__DIRECT_DESTINATION_BODY__
    }
}
""";

    // lang=c#
    private const string TemplateSourceTemplate =
"""
#pragma warning disable CS1591

namespace TestCase.Morphant.Generated
{
    internal sealed record GeneratedDestinationMorphantTemplate
    {
__TEMPLATE_BODY__
    }
}
""";

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
