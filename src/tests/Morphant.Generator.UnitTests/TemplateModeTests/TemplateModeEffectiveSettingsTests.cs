using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateModeTests;

[TestFixture]
internal sealed class TemplateModeEffectiveSettingsTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("Default")]
    [TestCase("default")]
    public async Task Uses_Dsl_when_all_configuration_levels_inherit(
        string? assemblyTemplateMode)
    {
        // lang=c#
        const string source =
"""
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

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.TemplateMode(TemplateMode.Default);

            builder.Map<Source, Destination>()
                .TemplateMode(TemplateMode.Default);
        }
    }
}
""";

        var expectedSources = new[]
        {
            (
                "Morphant.Generated.TemplateType." +
                "TestCase_Destination.g.cs",
                TemplateModeExpectedSources.EmptyTemplateType(
                    "Destination")
            ),
            (
                "Morphant.Generated.TemplateExtension." +
                "TestCase_Destination.g.cs",
                TemplateModeExpectedSources.GenericExtension(
                    "global::TestCase.Destination",
                    "global::TestCase.Destination?",
                    "global::TestCase.Morphant.Generated." +
                    "DestinationMorphantTemplate")
            )
        };

        if (assemblyTemplateMode is null)
        {
            await TemplateSurfaceGeneratorTest.RunAndAssert(
                LanguageVersion.CSharp9,
                source,
                expectedSources);
            return;
        }

        await TemplateSurfaceGeneratorTest.RunAndAssertWithAnalyzerConfig(
            LanguageVersion.CSharp9,
            source,
            $$"""
              is_global = true

              build_property.MorphantTemplateMode = {{assemblyTemplateMode}}
              """,
            expectedSources);
    }

    [Test]
    public async Task Resolves_mapping_then_mapper_then_assembly_precedence()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class AssemblyDestination
    {
    }

    public sealed class RootDestination
    {
    }

    public sealed class MappingDestination
    {
    }

    [MorphantMapper]
    public partial class AssemblyMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, AssemblyDestination>();
        }
    }

    [MorphantMapper]
    public partial class ConfiguredMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, RootDestination>();

            builder.Map<Source, MappingDestination>()
                .TemplateMode(TemplateMode.Raw);

            builder.TemplateMode(TemplateMode.Dsl);
        }
    }
}
""";

        await TemplateSurfaceGeneratorTest.RunAndAssertWithAnalyzerConfig(
            LanguageVersion.CSharp9,
            source,
            """
is_global = true

build_property.MorphantTemplateMode = raw
""",
            (
                "Morphant.Generated.TemplateType." +
                "TestCase_RootDestination.g.cs",
                TemplateModeExpectedSources.EmptyTemplateType(
                    "RootDestination")
            ),
            (
                "Morphant.Generated.TemplateExtension." +
                "TestCase_AssemblyDestination.g.cs",
                TemplateModeExpectedSources.GenericExtension(
                    "global::TestCase.AssemblyDestination",
                    "global::TestCase.AssemblyDestination?",
                    "global::TestCase.AssemblyDestination")
            ),
            (
                "Morphant.Generated.TemplateExtension." +
                "TestCase_MappingDestination.g.cs",
                TemplateModeExpectedSources.GenericExtension(
                    "global::TestCase.MappingDestination",
                    "global::TestCase.MappingDestination?",
                    "global::TestCase.MappingDestination")
            ),
            (
                "Morphant.Generated.TemplateExtension." +
                "TestCase_RootDestination.g.cs",
                TemplateModeExpectedSources.GenericExtension(
                    "global::TestCase.RootDestination",
                    "global::TestCase.RootDestination?",
                    "global::TestCase.Morphant.Generated." +
                    "RootDestinationMorphantTemplate")
            ));
    }

    [Test]
    public async Task Skips_invalid_effective_values_and_allows_specific_override()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class AssemblyInheritedDestination
    {
    }

    public sealed class InvalidRootDestination
    {
    }

    public sealed class ExplicitDslDestination
    {
    }

    public sealed class InvalidMappingDestination
    {
    }

    [MorphantMapper]
    public partial class AssemblyMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, AssemblyInheritedDestination>();
        }
    }

    [MorphantMapper]
    public partial class ConfiguredMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.TemplateMode((TemplateMode)42);

            builder.Map<Source, InvalidRootDestination>();

            builder.Map<Source, ExplicitDslDestination>()
                .TemplateMode(TemplateMode.Dsl);

            builder.Map<Source, InvalidMappingDestination>()
                .TemplateMode((TemplateMode)42);
        }
    }
}
""";

        await TemplateSurfaceGeneratorTest.RunAndAssertWithAnalyzerConfig(
            LanguageVersion.CSharp9,
            source,
            """
is_global = true

build_property.MorphantTemplateMode = invalid
""",
            (
                "Morphant.Generated.TemplateType." +
                "TestCase_ExplicitDslDestination.g.cs",
                TemplateModeExpectedSources.EmptyTemplateType(
                    "ExplicitDslDestination")
            ),
            (
                "Morphant.Generated.TemplateExtension." +
                "TestCase_ExplicitDslDestination.g.cs",
                TemplateModeExpectedSources.GenericExtension(
                    "global::TestCase.ExplicitDslDestination",
                    "global::TestCase.ExplicitDslDestination?",
                    "global::TestCase.Morphant.Generated." +
                    "ExplicitDslDestinationMorphantTemplate")
            ));
    }

    [Test]
    public async Task Keeps_direct_only_destinations_direct_under_Dsl()
    {
        // lang=c#
        const string source =
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
            builder.Map<Source, int>()
                .TemplateMode(TemplateMode.Dsl);
        }
    }
}
""";

        await TemplateSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TemplateExtension." +
                "System_Int32.g.cs",
                TemplateModeExpectedSources.GenericExtension(
                    "int",
                    "int",
                    "int")
            ));
    }

    [Test]
    public async Task Keeps_convention_mapping_when_Raw_has_no_template()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .TemplateMode(TemplateMode.Raw);
        }
    }
}
""";

        // lang=c#
        const string expected =
"""
// <auto-generated />
#nullable enable

namespace TestCase
{
    public partial class TestMapper :
        global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>
    {
        /// <inheritdoc/>
        global::TestCase.Destination? global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Map(
            global::TestCase.Source? source,
            global::Morphant.MappingContext context)
        {
            if (source is null)
            {
                return default;
            }

            return MapNewImpl(source, context);
        }

        /// <inheritdoc/>
        global::TestCase.Destination? global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Map(
            global::TestCase.Source? source,
            global::TestCase.Destination? destination,
            global::Morphant.MappingContext context)
        {
            if (source is null)
            {
                return default;
            }

            if (destination is null)
            {
                return MapNewImpl(source, context);
            }

            destination.Value = source.Value;

            return destination;
        }

        private global::TestCase.Destination? MapNewImpl(
            global::TestCase.Source source,
            global::Morphant.MappingContext context)
        {
            return new global::TestCase.Destination()
            {
                Value = source.Value
            };
        }
    }
}
""";

        await TypeMapperGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TypeMapper." +
                "TestCase_TestMapper.g.cs",
                expected
            ));
    }
}
