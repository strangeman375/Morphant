using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceNamingTests
{
    [Test]
    public async Task Uses_destination_relative_namespaces_and_nested_type_scopes()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

public sealed class GlobalSource { }
public sealed class GlobalDestination { }

namespace First
{
    public sealed class Destination { }
}

namespace Second
{
    public sealed class Destination { }
}

namespace TestCase
{
    public sealed class Outer<T>
    {
        public sealed class Destination<U>
        {
            public Destination(T outer, U value) { }
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<GlobalSource, GlobalDestination>();
            builder.Map<GlobalSource, First.Destination>();
            builder.Map<GlobalSource, Second.Destination>();
            builder.Map<GlobalSource, Outer<string>.Destination<int>>();
        }
    }
}
""";

        // lang=c#
        const string nestedDestinationConstructor =
"""
/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="outer">Configures the <c>outer</c> constructor argument.</param>
/// <param name="value">Configures the <c>value</c> constructor argument.</param>
public DestinationConstruction(
    global::Morphant.Members.ConstructorParameter<T> outer,
    global::Morphant.Members.ConstructorParameter<U> value)
{
}
""";

        const string nestedDestinationCref =
            "global::TestCase.Outer&lt;T&gt;.Destination&lt;U&gt;";
        var nestedPlan = ConstructionSurfaceExpectedSource.Plan(
            "TestCase.Morphant.Generated.Outer1Scope",
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .FallbackPlanDocumentation(nestedDestinationCref),
                "internal sealed class DestinationConstruction<T, U>",
                "DestinationConstruction",
                "DestinationConstruction<T, U>",
                "global::TestCase.Outer<T>.Destination<U>",
                nestedDestinationConstructor,
                "DestinationConstructionConstructorParameters<T, U>"),
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                "internal sealed class DestinationConstructionConstructorParameters<T, U>",
                nestedDestinationCref,
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "outer",
                    "public global::Morphant.Members.ConstructorParameter<T> outer = null!;"),
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "value",
                    "public global::Morphant.Members.ConstructorParameter<U> value = null!;")));
        var nestedDestinationType =
            "global::TestCase.Outer<string>.Destination<int>";

        (string FileName, string Content)[] expectedSources =
        {
            (
                "Morphant.Generated.Construction.GlobalDestination.g.cs",
                BuildParameterlessPlan(
                    "Morphant.Generated",
                    "GlobalDestinationConstruction",
                    "global::GlobalDestination")
            ),
            (
                "Morphant.Generated.Construction.First_Destination.g.cs",
                BuildParameterlessPlan(
                    "First.Morphant.Generated",
                    "DestinationConstruction",
                    "global::First.Destination")
            ),
            (
                "Morphant.Generated.Construction.Second_Destination.g.cs",
                BuildParameterlessPlan(
                    "Second.Morphant.Generated",
                    "DestinationConstruction",
                    "global::Second.Destination")
            ),
            (
                "Morphant.Generated.Construction.TestCase_Outer_1_Destination_1.g.cs",
                nestedPlan
            ),
            (
                "Morphant.Generated.MappingExtension.GlobalSource__First_Destination.g.cs",
                BuildExtension(
                    "global::GlobalSource",
                    "global::First.Destination",
                    "global::First.Morphant.Generated.DestinationConstruction")
            ),
            (
                "Morphant.Generated.MappingExtension.GlobalSource__GlobalDestination.g.cs",
                BuildExtension(
                    "global::GlobalSource",
                    "global::GlobalDestination",
                    "global::Morphant.Generated.GlobalDestinationConstruction")
            ),
            (
                "Morphant.Generated.MappingExtension.GlobalSource__Second_Destination.g.cs",
                BuildExtension(
                    "global::GlobalSource",
                    "global::Second.Destination",
                    "global::Second.Morphant.Generated.DestinationConstruction")
            ),
            (
                "Morphant.Generated.MappingExtension.GlobalSource__TestCase_Outer_System_String__Destination_System_Int32_.g.cs",
                BuildExtension(
                    "global::GlobalSource",
                    nestedDestinationType,
                    "global::TestCase.Morphant.Generated.Outer1Scope." +
                    "DestinationConstruction<string, int>")
            )
        };

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            expectedSources);
    }

    [Test]
    public async Task Adds_a_hash_only_for_real_case_insensitive_hint_collisions()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class URL { }
    public sealed class Url { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, URL>();
            builder.Map<Source, Url>();
        }
    }
}
""";

        var upperType = "global::TestCase.URL";
        var lowerType = "global::TestCase.Url";

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_URL.g.cs",
                BuildParameterlessPlan(
                    "TestCase.Morphant.Generated",
                    "URLConstruction",
                    upperType)
            ),
            (
                "Morphant.Generated.Construction.TestCase_Url__e9fae35bfd70d886.g.cs",
                BuildParameterlessPlan(
                    "TestCase.Morphant.Generated",
                    "UrlConstruction",
                    lowerType)
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_URL.g.cs",
                BuildExtension(
                    "global::TestCase.Source",
                    upperType,
                    "global::TestCase.Morphant.Generated.URLConstruction")
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Url__df20b2fbed6d104d.g.cs",
                BuildExtension(
                    "global::TestCase.Source",
                    lowerType,
                    "global::TestCase.Morphant.Generated.UrlConstruction")
            ));
    }

    [Test]
    public async Task Escapes_keyword_type_parameters_in_every_generated_usage()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source<@class>
        where @class : class
    {
        public @class Value { get; init; } = null!;
    }

    public sealed class Destination<@class>
        where @class : class
    {
        public Destination(@class value) { }
    }

    [MorphantMapper]
    public partial class TestMapper<@class> : TypeMapper
        where @class : class
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<@class>, Destination<@class>>()
                .Construct(source => new(source.Value));
    }
}
""";

        // lang=c#
        const string destinationConstructor =
"""
/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="value">Configures the <c>value</c> constructor argument.</param>
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<@class> value)
{
}
""";

        const string destinationCref =
            "global::TestCase.Destination&lt;@class&gt;";
        const string constraints = "where @class : class";
        var plan = ConstructionSurfaceExpectedSource.Plan(
            "TestCase.Morphant.Generated",
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .FallbackPlanDocumentation(destinationCref),
                "internal sealed class DestinationConstruction<@class>\n" +
                "    where @class : class",
                "DestinationConstruction",
                "DestinationConstruction<@class>",
                "global::TestCase.Destination<@class>",
                destinationConstructor,
                "DestinationConstructionConstructorParameters<@class>"),
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                "internal sealed class DestinationConstructionConstructorParameters<@class>\n" +
                "    where @class : class",
                destinationCref,
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "value",
                    "public global::Morphant.Members.ConstructorParameter<@class> value = null!;")));
        var sourceType = "global::TestCase.Source<@class>";
        var destinationType = "global::TestCase.Destination<@class>";
        var builderType =
            $"global::Morphant.MapperBuilder<{sourceType}, {destinationType}>";
        var extension = ConstructionSurfaceExpectedSource.MappingExtension(
            builderType,
            sourceType,
            sourceType + "?",
            destinationType,
            destinationType,
            "global::TestCase.Morphant.Generated.DestinationConstruction<@class>",
            "<@class>",
            "/// <typeparam name=\"class\">A type used by the mapping pair.</typeparam>",
            constraints);

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Destination_1.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source_class___TestCase_Destination_class_.g.cs",
                extension
            ));
    }

    [Test]
    public async Task Deduplicates_canonical_pair_representations_across_mappers()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source<T> { }

    public sealed class Destination<T>
    {
        public Destination(T value) { }
    }

    [MorphantMapper]
    public partial class NullableMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<string?>, Destination<string?>>();
    }

    [MorphantMapper]
    public partial class NonNullableMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<string>, Destination<string>>();
    }

    [MorphantMapper]
    public partial class DynamicMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<dynamic>, Destination<dynamic>>();
    }

    [MorphantMapper]
    public partial class ObjectMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<object>, Destination<object>>();
    }
}
""";

        // lang=c#
        const string destinationConstructor =
"""
/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="value">Configures the <c>value</c> constructor argument.</param>
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<T> value)
{
}
""";

        const string destinationCref =
            "global::TestCase.Destination&lt;T&gt;";
        var plan = ConstructionSurfaceExpectedSource.Plan(
            "TestCase.Morphant.Generated",
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .FallbackPlanDocumentation(destinationCref),
                "internal sealed class DestinationConstruction<T>",
                "DestinationConstruction",
                "DestinationConstruction<T>",
                "global::TestCase.Destination<T>",
                destinationConstructor,
                "DestinationConstructionConstructorParameters<T>"),
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                "internal sealed class DestinationConstructionConstructorParameters<T>",
                destinationCref,
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "value",
                    "public global::Morphant.Members.ConstructorParameter<T> value = null!;")));

        const string stringSource = "global::TestCase.Source<string>";
        const string stringDestination =
            "global::TestCase.Destination<string>";
        const string objectSource = "global::TestCase.Source<object>";
        const string objectDestination =
            "global::TestCase.Destination<object>";

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Destination_1.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source_System_Object___TestCase_Destination_System_Object_.g.cs",
                BuildExtension(
                    objectSource,
                    objectDestination,
                    "global::TestCase.Morphant.Generated.DestinationConstruction<object>")
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source_System_String___TestCase_Destination_System_String_.g.cs",
                BuildExtension(
                    stringSource,
                    stringDestination,
                    "global::TestCase.Morphant.Generated.DestinationConstruction<string>")
            ));
    }

    private static string BuildParameterlessPlan(
        string @namespace,
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
            @namespace,
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
        string sourceType,
        string destinationType,
        string constructionResultType)
    {
        var builderType =
            $"global::Morphant.MapperBuilder<{sourceType}, {destinationType}>";

        return ConstructionSurfaceExpectedSource.MappingExtension(
            builderType,
            sourceType,
            sourceType + "?",
            destinationType,
            destinationType,
            constructionResultType);
    }
}
