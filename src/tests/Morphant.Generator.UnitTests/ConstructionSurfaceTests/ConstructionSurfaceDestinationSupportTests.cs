using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceDestinationSupportTests
{
    [Test]
    public async Task Generates_only_the_surface_allowed_by_each_destination_capability()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using System.Collections.Generic;
using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class EmptyClass { }
    public struct CustomStruct { }
    public interface IDestination { }
    public abstract class AbstractDestination { }

    public sealed class FactoryOnly
    {
        private FactoryOnly() { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, EmptyClass>();
            builder.Map<Source, CustomStruct>();
            builder.Map<Source, IDestination>();
            builder.Map<Source, AbstractDestination>();
            builder.Map<Source, FactoryOnly>();
            builder.Map<Source, int>();
            builder.Map<Source, List<int>>();
        }
    }
}
""";

        var emptyClassType = "global::TestCase.EmptyClass";
        var emptyClassConstruction =
            "global::TestCase.Morphant.Generated.EmptyClassConstruction";
        var customStructType = "global::TestCase.CustomStruct";
        var customStructConstruction =
            "global::TestCase.Morphant.Generated.CustomStructConstruction";
        var interfaceType = "global::TestCase.IDestination";
        var abstractType = "global::TestCase.AbstractDestination";
        var factoryOnlyType = "global::TestCase.FactoryOnly";

        (string FileName, string Content)[] expectedSources =
        {
            (
                "Morphant.Generated.Construction.TestCase_CustomStruct.g.cs",
                BuildParameterlessPlan(
                    "CustomStructConstruction",
                    customStructType)
            ),
            (
                "Morphant.Generated.Construction.TestCase_EmptyClass.g.cs",
                BuildParameterlessPlan(
                    "EmptyClassConstruction",
                    emptyClassType)
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__System_Int32.g.cs",
                BuildExtension("int", "int", "int", "int")
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_AbstractDestination.g.cs",
                BuildExtension(
                    abstractType,
                    abstractType,
                    abstractType,
                    abstractType)
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_CustomStruct.g.cs",
                BuildExtension(
                    customStructType,
                    customStructType,
                    customStructType,
                    customStructConstruction)
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_EmptyClass.g.cs",
                BuildExtension(
                    emptyClassType,
                    emptyClassType,
                    emptyClassType,
                    emptyClassConstruction)
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_FactoryOnly.g.cs",
                BuildExtension(
                    factoryOnlyType,
                    factoryOnlyType,
                    factoryOnlyType,
                    factoryOnlyType)
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_IDestination.g.cs",
                BuildExtension(
                    interfaceType,
                    interfaceType,
                    interfaceType,
                    interfaceType)
            )
        };

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            expectedSources);
    }

    [Test]
    public async Task Uses_only_constructors_visible_from_the_common_generated_context()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        private Destination(byte value) { }
        protected Destination(short value) { }
        private protected Destination(long value) { }
        internal Destination(int value) { }
        protected internal Destination(uint value) { }
        public Destination(string value) { }

        [MorphantMapper]
        public partial class TestMapper : TypeMapper
        {
            protected override void Configure(MapperBuilder builder) =>
                builder.Map<Source, Destination>();
        }
    }
}
""";

        // lang=c#
        const string destinationConstructors =
"""
/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="value">Configures the <c>value</c> constructor argument.</param>
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<int> value)
{
}

/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="value">Configures the <c>value</c> constructor argument.</param>
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<uint> value)
{
}

/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="value">Configures the <c>value</c> constructor argument.</param>
#nullable disable annotations
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<string> value)
{
}
#nullable enable annotations
""";

        var destinationType = "global::TestCase.Destination";
        var plan = ConstructionSurfaceExpectedSource.Plan(
            "TestCase.Morphant.Generated",
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .FallbackPlanDocumentation(destinationType),
                "internal sealed class DestinationConstruction",
                "DestinationConstruction",
                "DestinationConstruction",
                destinationType,
                destinationConstructors,
                "DestinationConstructionConstructorParameters"),
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                "internal sealed class DestinationConstructionConstructorParameters",
                destinationType,
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "value",
                    "public global::Morphant.Members.ConstructorParameter<int> valueInt = null!;"),
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "value",
                    "public global::Morphant.Members.ConstructorParameter<uint> valueUint = null!;"),
                """
#nullable disable annotations
/// <summary>
/// Configures the <c>value</c> constructor argument.
/// </summary>
public global::Morphant.Members.ConstructorParameter<string> valueString = null!;
#nullable enable annotations
"""));
        var extension = BuildExtension(
            destinationType,
            destinationType,
            destinationType,
            "global::TestCase.Morphant.Generated.DestinationConstruction");

        await ConstructionSurfaceGeneratorTest
            .RunAndAssertAllowingCompilerWarnings(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Destination.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination.g.cs",
                extension
            ));
    }

    [Test]
    public async Task Reuses_one_plan_for_nullable_and_non_nullable_custom_structs()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public struct Destination
    {
        public Destination(int value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, Destination?>();
        }
    }
}
""";

        // lang=c#
        const string destinationConstructors =
"""
/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="value">Configures the <c>value</c> constructor argument.</param>
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<int> value)
{
}

/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
public DestinationConstruction()
{
}
""";

        var destinationType = "global::TestCase.Destination";
        var constructionType =
            "global::TestCase.Morphant.Generated.DestinationConstruction";
        var plan = ConstructionSurfaceExpectedSource.Plan(
            "TestCase.Morphant.Generated",
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .FallbackPlanDocumentation(destinationType),
                "internal sealed class DestinationConstruction",
                "DestinationConstruction",
                "DestinationConstruction",
                destinationType,
                destinationConstructors,
                "DestinationConstructionConstructorParameters"),
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                "internal sealed class DestinationConstructionConstructorParameters",
                destinationType,
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "value",
                    "public global::Morphant.Members.ConstructorParameter<int> value = null!;")));

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Destination.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__System_Nullable_TestCase_Destination_.g.cs",
                BuildExtension(
                    destinationType + "?",
                    destinationType + "?",
                    destinationType,
                    constructionType)
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination.g.cs",
                BuildExtension(
                    destinationType,
                    destinationType,
                    destinationType,
                    constructionType)
            ));
    }

    private static string BuildParameterlessPlan(
        string constructionTypeName,
        string destinationType)
    {
        // lang=c#
        var destinationConstructor =
$$"""
/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
public {{constructionTypeName}}()
{
}
""";

        return ConstructionSurfaceExpectedSource.Plan(
            "TestCase.Morphant.Generated",
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .FallbackPlanDocumentation(destinationType),
                $"internal sealed class {constructionTypeName}",
                constructionTypeName,
                constructionTypeName,
                destinationType,
                destinationConstructor));
    }

    private static string BuildExtension(
        string builderDestinationType,
        string destinationType,
        string previousDestinationType,
        string constructionResultType)
    {
        var builderType =
            "global::Morphant.MapperBuilder<global::TestCase.Source, " +
            builderDestinationType + ">";

        return ConstructionSurfaceExpectedSource.MappingExtension(
            builderType,
            "global::TestCase.Source",
            "global::TestCase.Source?",
            destinationType,
            previousDestinationType,
            constructionResultType);
    }
}
