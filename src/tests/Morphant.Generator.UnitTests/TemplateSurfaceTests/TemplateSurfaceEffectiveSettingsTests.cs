using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateSurfaceTests;

[TestFixture]
internal sealed class TemplateSurfaceEffectiveSettingsTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("Default")]
    [TestCase("default")]
    public async Task Uses_Full_when_all_configuration_levels_inherit(
        string? assemblyTemplateSurface)
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
            builder.TemplateSurface(TemplateSurface.Default);

            builder.Map<Source, Destination>()
                .TemplateSurface(TemplateSurface.Default);
        }
    }
}
""";

        var expectedSources = new[]
        {
            (
                "Morphant.Generated.TemplateType." +
                "TestCase_Destination.g.cs",
                TemplateSurfaceExpectedSources.EmptyTemplateType(
                    "Destination")
            ),
            (
                "Morphant.Generated.TemplateExtension." +
                "TestCase_Destination.g.cs",
                TemplateSurfaceExpectedSources.GenericExtension(
                    "global::TestCase.Destination",
                    "global::TestCase.Destination?",
                    "global::TestCase.Morphant.Generated." +
                    "DestinationMorphantTemplate")
            )
        };

        if (assemblyTemplateSurface is null)
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

              build_property.TemplateSurface = {{assemblyTemplateSurface}}
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

    public sealed class NoneDestination
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
                .TemplateSurface(TemplateSurface.Direct);

            builder.Map<Source, NoneDestination>()
                .TemplateSurface(TemplateSurface.None);

            builder.TemplateSurface(TemplateSurface.Full);
        }
    }
}
""";

        await TemplateSurfaceGeneratorTest.RunAndAssertWithAnalyzerConfig(
            LanguageVersion.CSharp9,
            source,
            """
is_global = true

build_property.TemplateSurface = direct
""",
            (
                "Morphant.Generated.TemplateType." +
                "TestCase_RootDestination.g.cs",
                TemplateSurfaceExpectedSources.EmptyTemplateType(
                    "RootDestination")
            ),
            (
                "Morphant.Generated.TemplateExtension." +
                "TestCase_AssemblyDestination.g.cs",
                TemplateSurfaceExpectedSources.GenericExtension(
                    "global::TestCase.AssemblyDestination",
                    "global::TestCase.AssemblyDestination?",
                    "global::TestCase.AssemblyDestination")
            ),
            (
                "Morphant.Generated.TemplateExtension." +
                "TestCase_MappingDestination.g.cs",
                TemplateSurfaceExpectedSources.GenericExtension(
                    "global::TestCase.MappingDestination",
                    "global::TestCase.MappingDestination?",
                    "global::TestCase.MappingDestination")
            ),
            (
                "Morphant.Generated.TemplateExtension." +
                "TestCase_RootDestination.g.cs",
                TemplateSurfaceExpectedSources.GenericExtension(
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

    public sealed class ExplicitFullDestination
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
            builder.TemplateSurface((TemplateSurface)42);

            builder.Map<Source, InvalidRootDestination>();

            builder.Map<Source, ExplicitFullDestination>()
                .TemplateSurface(TemplateSurface.Full);

            builder.Map<Source, InvalidMappingDestination>()
                .TemplateSurface((TemplateSurface)42);
        }
    }
}
""";

        await TemplateSurfaceGeneratorTest.RunAndAssertWithAnalyzerConfig(
            LanguageVersion.CSharp9,
            source,
            """
is_global = true

build_property.TemplateSurface = invalid
""",
            (
                "Morphant.Generated.TemplateType." +
                "TestCase_ExplicitFullDestination.g.cs",
                TemplateSurfaceExpectedSources.EmptyTemplateType(
                    "ExplicitFullDestination")
            ),
            (
                "Morphant.Generated.TemplateExtension." +
                "TestCase_ExplicitFullDestination.g.cs",
                TemplateSurfaceExpectedSources.GenericExtension(
                    "global::TestCase.ExplicitFullDestination",
                    "global::TestCase.ExplicitFullDestination?",
                    "global::TestCase.Morphant.Generated." +
                    "ExplicitFullDestinationMorphantTemplate")
            ));
    }

    [Test]
    public async Task Keeps_direct_only_destinations_direct_under_Full()
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
                .TemplateSurface(TemplateSurface.Full);
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
                TemplateSurfaceExpectedSources.GenericExtension(
                    "int",
                    "int",
                    "int")
            ));
    }

    [Test]
    public async Task Keeps_convention_mapping_when_surface_is_None()
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
            builder.Map<Source, Destination>()
                .TemplateSurface(TemplateSurface.None);
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

            return destination;
        }

        private global::TestCase.Destination? MapNewImpl(
            global::TestCase.Source source,
            global::Morphant.MappingContext context)
        {
            return new global::TestCase.Destination();
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
