using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateExtensionActualizationTest;

namespace Morphant.Generator.UnitTests.TemplateExtensionTests.Actualization;

[TestFixture]
internal sealed class TemplateExtensionLifecycleTests
{
    [Test]
    public void Adds_deduplicates_and_removes_extensions_with_map_usages()
    {
        var generated = ExpectedGeneratedReferenceExtension(
            "TestCase_GeneratedDestination",
            "global::TestCase.GeneratedDestination",
            "global::TestCase.Morphant.Generated." +
            "GeneratedDestinationMorphantTemplate");

        var direct = ExpectedDirectExtension(
            "System_Int32",
            "int",
            "int");

        RunAndAssert(
            Step(
                "without usages",
                BuildUsageSource(
                    string.Empty,
                    string.Empty)),
            Step(
                "first generated usage added",
                BuildUsageSource(
                    "            builder.Map<Source, " +
                    "GeneratedDestination>();",
                    string.Empty),
                generated),
            Step(
                "duplicate generated and direct usages added",
                BuildUsageSource(
                    "            builder.Map<Source, " +
                    "GeneratedDestination>();",
                    "            builder.Map<AlternativeSource, " +
                    "GeneratedDestination>();\n" +
                    "            builder.Map<AlternativeSource, int>();"),
                generated,
                direct),
            Step(
                "first generated usage removed",
                BuildUsageSource(
                    string.Empty,
                    "            builder.Map<AlternativeSource, " +
                    "GeneratedDestination>();\n" +
                    "            builder.Map<AlternativeSource, int>();"),
                generated,
                direct),
            Step(
                "direct usage removed",
                BuildUsageSource(
                    string.Empty,
                    "            builder.Map<AlternativeSource, " +
                    "GeneratedDestination>();"),
                generated),
            Step(
                "last generated usage removed",
                BuildUsageSource(
                    string.Empty,
                    string.Empty)));
    }

    [Test]
    public void Tracks_mapper_discovery_and_usage_movement_between_files()
    {
        var expected = ExpectedGeneratedReferenceExtension(
            "TestCase_Destination",
            "global::TestCase.Destination",
            "global::TestCase.Morphant.Generated." +
            "DestinationMorphantTemplate");

        var models = SourceFile("Models.cs", ModelsSource);
        var undiscoveredMapper = SourceFile(
            "FirstMapper.cs",
            BuildMapperSource(
                "FirstMapper",
                string.Empty));
        var discoveredFirstMapper = SourceFile(
            "FirstMapper.cs",
            BuildMapperSource(
                "FirstMapper",
                "    [MorphantMapper]\n"));
        var discoveredSecondMapper = SourceFile(
            "SecondMapper.cs",
            BuildMapperSource(
                "SecondMapper",
                "    [MorphantMapper]\n"));

        RunAndAssert(
            Step(
                "mapper has no discovery attribute",
                new[]
                {
                    models,
                    undiscoveredMapper
                }),
            Step(
                "discovery attribute added",
                new[]
                {
                    models,
                    discoveredFirstMapper
                },
                expected),
            Step(
                "usage moved to another file",
                new[]
                {
                    models,
                    undiscoveredMapper,
                    discoveredSecondMapper
                },
                expected),
            Step(
                "file with last discovered mapper removed",
                new[]
                {
                    models,
                    undiscoveredMapper
                }));
    }

    [Test]
    public void Follows_destination_between_generated_direct_and_no_surface_outcomes()
    {
        const string hintNamePart =
            "TestCase_Models_Destination__84a940df8bb69798";
        const string destinationType =
            "global::TestCase.Models.Destination";

        var generated = ExpectedGeneratedReferenceExtension(
            hintNamePart,
            destinationType,
            "global::TestCase.Morphant.Generated.ModelsScope." +
            "DestinationMorphantTemplate");

        var direct = ExpectedDirectExtension(
            hintNamePart,
            destinationType,
            destinationType);

        RunAndAssert(
            Step(
                "public class uses generated template",
                BuildDestinationKindSource(
                    "        public sealed class Destination\n" +
                    "        {\n" +
                    "        }"),
                generated),
            Step(
                "public enum uses direct template",
                BuildDestinationKindSource(
                    "        public enum Destination\n" +
                    "        {\n" +
                    "            None\n" +
                    "        }"),
                direct),
            Step(
                "delegate has no template surface",
                BuildDestinationKindSource(
                    "        public delegate void Destination();")),
            Step(
                "private class cannot appear in generated extension",
                BuildDestinationKindSource(
                    "        private sealed class Destination\n" +
                    "        {\n" +
                    "        }")),
            Step(
                "public class surface restored",
                BuildDestinationKindSource(
                    "        public sealed class Destination\n" +
                    "        {\n" +
                    "        }"),
                generated));
    }

    [Test]
    public void Moves_extension_when_destination_is_renamed_and_moved()
    {
        var initialExpected = ExpectedGeneratedReferenceExtension(
            "OldModels_Destination",
            "global::OldModels.Destination",
            "global::OldModels.Morphant.Generated." +
            "DestinationMorphantTemplate");

        var updatedExpected = ExpectedGeneratedReferenceExtension(
            "NewModels_RenamedDestination",
            "global::NewModels.RenamedDestination",
            "global::NewModels.Morphant.Generated." +
            "RenamedDestinationMorphantTemplate");

        RunAndAssert(
            Step(
                "initial destination identity",
                BuildMovedDestinationSource(
                    "OldModels",
                    "Destination"),
                initialExpected),
            Step(
                "renamed and moved destination",
                BuildMovedDestinationSource(
                    "NewModels",
                    "RenamedDestination"),
                updatedExpected));
    }

    [Test]
    public void Updates_extension_when_destination_alias_target_changes()
    {
        var firstExpected = ExpectedGeneratedReferenceExtension(
            "TestCase_FirstDestination",
            "global::TestCase.FirstDestination",
            "global::TestCase.Morphant.Generated." +
            "FirstDestinationMorphantTemplate");

        var secondExpected = ExpectedGeneratedReferenceExtension(
            "TestCase_SecondDestination",
            "global::TestCase.SecondDestination",
            "global::TestCase.Morphant.Generated." +
            "SecondDestinationMorphantTemplate");

        RunAndAssert(
            Step(
                "alias targets first destination",
                BuildAliasTargetSource("FirstDestination"),
                firstExpected),
            Step(
                "alias targets second destination",
                BuildAliasTargetSource("SecondDestination"),
                secondExpected),
            Step(
                "first alias target restored",
                BuildAliasTargetSource("FirstDestination"),
                firstExpected));
    }

    [Test]
    public void Updates_extension_when_referenced_destination_kind_changes()
    {
        var generated = ExpectedGeneratedReferenceExtension(
            "ReferencedModels_Destination",
            "global::ReferencedModels.Destination",
            "global::ReferencedModels.Morphant.Generated." +
            "DestinationMorphantTemplate");

        var direct = ExpectedDirectExtension(
            "ReferencedModels_Destination",
            "global::ReferencedModels.Destination",
            "global::ReferencedModels.Destination");

        var classReference = CreateReference(
            "ReferencedModels",
            BuildReferencedDestination(
                "public sealed class Destination\n" +
                "    {\n" +
                "    }"));

        var enumReference = CreateReference(
            "ReferencedModels",
            BuildReferencedDestination(
                "public enum Destination\n" +
                "    {\n" +
                "        None\n" +
                "    }"));

        var editedClassReference = CreateReference(
            "ReferencedModels",
            BuildReferencedDestination(
                "/// <summary>\n" +
                "    /// Represents an edited destination.\n" +
                "    /// </summary>\n" +
                "    public sealed class Destination\n" +
                "    {\n" +
                "        public int Id { get; init; }\n" +
                "    }"));

        var delegateReference = CreateReference(
            "ReferencedModels",
            BuildReferencedDestination(
                "public delegate void Destination();"));

        RunAndAssert(
            Step(
                "referenced class",
                ReferencedUsageSource,
                new[] { classReference },
                generated),
            Step(
                "referenced class details changed",
                ReferencedUsageSource,
                new[] { editedClassReference },
                generated),
            Step(
                "referenced enum",
                ReferencedUsageSource,
                new[] { enumReference },
                direct),
            Step(
                "referenced delegate",
                ReferencedUsageSource,
                new[] { delegateReference }),
            Step(
                "referenced class restored",
                ReferencedUsageSource,
                new[] { classReference },
                generated));
    }

    private static string BuildUsageSource(
        string firstMapperStatements,
        string secondMapperStatements)
    {
        return UsageSourceTemplate
            .Replace(
                "__FIRST_MAPPER_STATEMENTS__",
                firstMapperStatements)
            .Replace(
                "__SECOND_MAPPER_STATEMENTS__",
                secondMapperStatements);
    }

    private static string BuildMapperSource(
        string mapperName,
        string mapperAttribute)
    {
        return MapperSourceTemplate
            .Replace("__MAPPER_ATTRIBUTE__", mapperAttribute)
            .Replace("__MAPPER_NAME__", mapperName);
    }

    private static string BuildDestinationKindSource(
        string destinationDeclaration)
    {
        return DestinationKindSourceTemplate.Replace(
            "__DESTINATION_DECLARATION__",
            destinationDeclaration);
    }

    private static string BuildMovedDestinationSource(
        string destinationNamespace,
        string destinationName)
    {
        return MovedDestinationSourceTemplate
            .Replace(
                "__DESTINATION_NAMESPACE__",
                destinationNamespace)
            .Replace(
                "__DESTINATION_NAME__",
                destinationName);
    }

    private static string BuildAliasTargetSource(string aliasTarget)
    {
        return AliasTargetSourceTemplate.Replace(
            "__ALIAS_TARGET__",
            aliasTarget);
    }

    private static string BuildReferencedDestination(
        string declaration)
    {
        return ReferencedDestinationTemplate.Replace(
            "__DESTINATION_DECLARATION__",
            declaration);
    }

    // lang=c#
    private const string UsageSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class AlternativeSource
    {
    }

    public sealed class GeneratedDestination
    {
    }

    [MorphantMapper]
    public partial class FirstMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
__FIRST_MAPPER_STATEMENTS__
        }
    }

    [MorphantMapper]
    public partial class SecondMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
__SECOND_MAPPER_STATEMENTS__
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record GeneratedDestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string ModelsSource =
"""
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class Destination
    {
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string MapperSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
__MAPPER_ATTRIBUTE__    public partial class __MAPPER_NAME__ : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }
}
""";

    // lang=c#
    private const string DestinationKindSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public static class Models
    {
__DESTINATION_DECLARATION__

        [MorphantMapper]
        public partial class TestMapper : TypeMapper
        {
            protected override void Configure(MapperBuilder builder)
            {
                builder.Map<Source, Destination>();
            }
        }
    }
}

namespace TestCase.Morphant.Generated.ModelsScope
{
    internal sealed record DestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string MovedDestinationSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

public sealed class Source
{
}

namespace __DESTINATION_NAMESPACE__
{
    public sealed class __DESTINATION_NAME__
    {
    }
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, __DESTINATION_NAMESPACE__.__DESTINATION_NAME__>();
    }
}

namespace __DESTINATION_NAMESPACE__.Morphant.Generated
{
    internal sealed record __DESTINATION_NAME__MorphantTemplate;
}
""";

    // lang=c#
    private const string AliasTargetSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;
using DestinationAlias = TestCase.__ALIAS_TARGET__;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class FirstDestination
    {
    }

    public sealed class SecondDestination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, DestinationAlias>();
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record FirstDestinationMorphantTemplate;

    internal sealed record SecondDestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string ReferencedUsageSource =
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
            builder.Map<Source, ReferencedModels.Destination>();
        }
    }
}

namespace ReferencedModels.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string ReferencedDestinationTemplate =
"""
#pragma warning disable CS1591

namespace ReferencedModels
{
    __DESTINATION_DECLARATION__
}
""";
}
