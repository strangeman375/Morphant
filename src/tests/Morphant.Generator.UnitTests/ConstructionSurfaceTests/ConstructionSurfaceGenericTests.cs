using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceGenericTests
{
    [Test]
    public async Task Reuses_one_constrained_generic_plan_for_closed_destinations()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Factory { public Factory() { } }

    public sealed class Outer<TOuter>
        where TOuter : class
    {
        public sealed class Destination<TValue, TFactory>
            where TValue : unmanaged
            where TFactory : class?, new()
        {
            public Destination(
                TOuter outer,
                TValue value,
                TFactory factory) { }
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Outer<string>.Destination<int, Factory>>();
            builder.Map<Source, Outer<object>.Destination<long, Factory>>();
        }
    }
}
""";

        // lang=c#
        const string destinationConstructor =
"""
/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="outer">Configures the <c>outer</c> constructor argument.</param>
/// <param name="value">Configures the <c>value</c> constructor argument.</param>
/// <param name="factory">Configures the <c>factory</c> constructor argument.</param>
public DestinationConstruction(
    global::Morphant.Members.ConstructorParameter<TOuter> outer,
    global::Morphant.Members.ConstructorParameter<TValue> value,
    global::Morphant.Members.ConstructorParameter<TFactory> factory)
{
}
""";

        const string typeDeclaration =
"""
internal sealed class DestinationConstruction<TOuter, TValue, TFactory>
    where TOuter : class
    where TValue : unmanaged
    where TFactory : class?, new()
""";
        const string parametersTypeDeclaration =
"""
internal sealed class DestinationConstructionConstructorParameters<TOuter, TValue, TFactory>
    where TOuter : class
    where TValue : unmanaged
    where TFactory : class?, new()
""";
        const string destinationCref =
            "global::TestCase.Outer&lt;TOuter&gt;." +
            "Destination&lt;TValue, TFactory&gt;";
        const string destinationType =
            "global::TestCase.Outer<TOuter>." +
            "Destination<TValue, TFactory>";
        var plan = BuildPlan(
            "TestCase.Morphant.Generated.Outer1Scope",
            typeDeclaration,
            parametersTypeDeclaration,
            "DestinationConstruction<TOuter, TValue, TFactory>",
            "DestinationConstructionConstructorParameters<TOuter, TValue, TFactory>",
            destinationCref,
            destinationType,
            destinationConstructor,
            (
                "outer",
                "public global::Morphant.Members.ConstructorParameter<TOuter> outer = null!;"
            ),
            (
                "value",
                "public global::Morphant.Members.ConstructorParameter<TValue> value = null!;"
            ),
            (
                "factory",
                "public global::Morphant.Members.ConstructorParameter<TFactory> factory = null!;"
            ));
        const string stringDestination =
            "global::TestCase.Outer<string>." +
            "Destination<int, global::TestCase.Factory>";
        const string objectDestination =
            "global::TestCase.Outer<object>." +
            "Destination<long, global::TestCase.Factory>";

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Outer_1_Destination_2.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Outer_System_Object__Destination_System_Int64__TestCase_Factory_.g.cs",
                BuildExtension(
                    "global::TestCase.Source",
                    objectDestination,
                    "global::TestCase.Morphant.Generated.Outer1Scope." +
                    "DestinationConstruction<object, long, global::TestCase.Factory>")
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Outer_System_String__Destination_System_Int32__TestCase_Factory_.g.cs",
                BuildExtension(
                    "global::TestCase.Source",
                    stringDestination,
                    "global::TestCase.Morphant.Generated.Outer1Scope." +
                    "DestinationConstruction<string, int, global::TestCase.Factory>")
            ));
    }

    [Test]
    public async Task Uses_only_definition_constraints_for_alpha_equivalent_pairs()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public interface IFirst { }
    public interface ISecond { }

    public sealed class Source<T>
        where T : class
    {
        public T Value { get; init; } = null!;
    }

    public sealed class Destination<T>
        where T : class
    {
        public Destination(T value) { }
    }

    [MorphantMapper]
    public partial class FirstMapper<T> : TypeMapper
        where T : class, IFirst
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Construct(source => new(source.Value));
    }

    [MorphantMapper]
    public partial class SecondMapper<U> : TypeMapper
        where U : class, ISecond
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<U>, Destination<U>>()
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
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<T> value)
{
}
""";

        const string typeDeclaration =
"""
internal sealed class DestinationConstruction<T>
    where T : class
""";
        const string parametersTypeDeclaration =
"""
internal sealed class DestinationConstructionConstructorParameters<T>
    where T : class
""";
        const string destinationCref =
            "global::TestCase.Destination&lt;T&gt;";
        var plan = BuildPlan(
            "TestCase.Morphant.Generated",
            typeDeclaration,
            parametersTypeDeclaration,
            "DestinationConstruction<T>",
            "DestinationConstructionConstructorParameters<T>",
            destinationCref,
            "global::TestCase.Destination<T>",
            destinationConstructor,
            (
                "value",
                "public global::Morphant.Members.ConstructorParameter<T> value = null!;"
            ));
        const string sourceType = "global::TestCase.Source<T>";
        const string destinationType = "global::TestCase.Destination<T>";
        var extension = BuildExtension(
            sourceType,
            destinationType,
            "global::TestCase.Morphant.Generated.DestinationConstruction<T>",
            "<T>",
            "/// <typeparam name=\"T\">A type used by the mapping pair.</typeparam>",
            "where T : class");

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Destination_1.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source_T___TestCase_Destination_T_.g.cs",
                extension
            ));
    }

    [Test]
    public async Task Omits_different_mapper_constraints_from_a_shared_extension()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source<T>
    {
        public T Value { get; init; } = default!;
    }

    public sealed class Destination<T>
    {
        public Destination(T value) { }
    }

    [MorphantMapper]
    public partial class ReferenceMapper<T> : TypeMapper
        where T : class
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Construct(source => new(source.Value));
    }

    [MorphantMapper]
    public partial class ValueMapper<T> : TypeMapper
        where T : struct
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
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
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<T> value)
{
}
""";

        const string destinationCref =
            "global::TestCase.Destination&lt;T&gt;";
        var plan = BuildPlan(
            "TestCase.Morphant.Generated",
            "internal sealed class DestinationConstruction<T>",
            "internal sealed class DestinationConstructionConstructorParameters<T>",
            "DestinationConstruction<T>",
            "DestinationConstructionConstructorParameters<T>",
            destinationCref,
            "global::TestCase.Destination<T>",
            destinationConstructor,
            (
                "value",
                "public global::Morphant.Members.ConstructorParameter<T> value = null!;"
            ));
        const string sourceType = "global::TestCase.Source<T>";
        const string destinationType = "global::TestCase.Destination<T>";
        var extension = BuildExtension(
            sourceType,
            destinationType,
            "global::TestCase.Morphant.Generated.DestinationConstruction<T>",
            "<T>",
            "/// <typeparam name=\"T\">A type used by the mapping pair.</typeparam>");

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Destination_1.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source_T___TestCase_Destination_T_.g.cs",
                extension
            ));
    }

    [Test]
    public async Task Merges_and_substitutes_constraints_from_pair_definitions()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public interface IMarker<T> { }

    public sealed class Source<TValue, TDependency>
        where TValue : class?, IMarker<TDependency>?
        where TDependency : class, new()
    {
        public TValue Value { get; init; } = default!;
    }

    public sealed class Destination<TValue, TDependency>
        where TValue : notnull, IMarker<TDependency>
        where TDependency : class?, new()
    {
        public Destination(TValue value, TDependency dependency) { }
    }

    [MorphantMapper]
    public partial class TestMapper<TValue, TDependency> : TypeMapper
        where TValue : class, IMarker<TDependency>
        where TDependency : class, new()
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<
                    Source<TValue, TDependency>,
                    Destination<TValue, TDependency>>()
                .Construct(source => new(
                    source.Value,
                    new TDependency()));
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
/// <param name="dependency">Configures the <c>dependency</c> constructor argument.</param>
public DestinationConstruction(
    global::Morphant.Members.ConstructorParameter<TValue> value,
    global::Morphant.Members.ConstructorParameter<TDependency> dependency)
{
}
""";

        const string typeDeclaration =
"""
internal sealed class DestinationConstruction<TValue, TDependency>
    where TValue : notnull, global::TestCase.IMarker<TDependency>
    where TDependency : class?, new()
""";
        const string parametersTypeDeclaration =
"""
internal sealed class DestinationConstructionConstructorParameters<TValue, TDependency>
    where TValue : notnull, global::TestCase.IMarker<TDependency>
    where TDependency : class?, new()
""";
        const string destinationCref =
            "global::TestCase.Destination&lt;TValue, TDependency&gt;";
        var plan = BuildPlan(
            "TestCase.Morphant.Generated",
            typeDeclaration,
            parametersTypeDeclaration,
            "DestinationConstruction<TValue, TDependency>",
            "DestinationConstructionConstructorParameters<TValue, TDependency>",
            destinationCref,
            "global::TestCase.Destination<TValue, TDependency>",
            destinationConstructor,
            (
                "value",
                "public global::Morphant.Members.ConstructorParameter<TValue> value = null!;"
            ),
            (
                "dependency",
                "public global::Morphant.Members.ConstructorParameter<TDependency> dependency = null!;"
            ));
        const string sourceType =
            "global::TestCase.Source<TValue, TDependency>";
        const string destinationType =
            "global::TestCase.Destination<TValue, TDependency>";
        const string typeParameterDocumentation =
"""
/// <typeparam name="TValue">A type used by the mapping pair.</typeparam>
/// <typeparam name="TDependency">A type used by the mapping pair.</typeparam>
""";
        const string extensionConstraints =
"""
where TValue : class, global::TestCase.IMarker<TDependency>
where TDependency : class, new()
""";
        var extension = BuildExtension(
            sourceType,
            destinationType,
            "global::TestCase.Morphant.Generated." +
            "DestinationConstruction<TValue, TDependency>",
            "<TValue, TDependency>",
            typeParameterDocumentation,
            extensionConstraints);

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Destination_2.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source_TValue__TDependency___TestCase_Destination_TValue__TDependency_.g.cs",
                extension
            ));
    }

    [Test]
    public async Task Preserves_containing_definition_constraints_in_open_pairs()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Outer<TOuter>
        where TOuter : class
    {
        public sealed class Source<TValue>
            where TValue : TOuter
        {
            public TValue Value { get; init; } = default!;
        }

        public sealed class Destination<TValue>
            where TValue : TOuter
        {
            public Destination(TValue value) { }
        }
    }

    [MorphantMapper]
    public partial class TestMapper<TOuter, TValue> : TypeMapper
        where TOuter : class
        where TValue : TOuter
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<
                    Outer<TOuter>.Source<TValue>,
                    Outer<TOuter>.Destination<TValue>>()
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
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<TValue> value)
{
}
""";

        const string typeDeclaration =
"""
internal sealed class DestinationConstruction<TOuter, TValue>
    where TOuter : class
    where TValue : TOuter
""";
        const string parametersTypeDeclaration =
"""
internal sealed class DestinationConstructionConstructorParameters<TOuter, TValue>
    where TOuter : class
    where TValue : TOuter
""";
        const string destinationCref =
            "global::TestCase.Outer&lt;TOuter&gt;." +
            "Destination&lt;TValue&gt;";
        var plan = BuildPlan(
            "TestCase.Morphant.Generated.Outer1Scope",
            typeDeclaration,
            parametersTypeDeclaration,
            "DestinationConstruction<TOuter, TValue>",
            "DestinationConstructionConstructorParameters<TOuter, TValue>",
            destinationCref,
            "global::TestCase.Outer<TOuter>.Destination<TValue>",
            destinationConstructor,
            (
                "value",
                "public global::Morphant.Members.ConstructorParameter<TValue> value = null!;"
            ));
        const string sourceType =
            "global::TestCase.Outer<TOuter>.Source<TValue>";
        const string destinationType =
            "global::TestCase.Outer<TOuter>.Destination<TValue>";
        const string typeParameterDocumentation =
"""
/// <typeparam name="TOuter">A type used by the mapping pair.</typeparam>
/// <typeparam name="TValue">A type used by the mapping pair.</typeparam>
""";
        const string constraints =
"""
where TOuter : class
where TValue : TOuter
""";
        var extension = BuildExtension(
            sourceType,
            destinationType,
            "global::TestCase.Morphant.Generated.Outer1Scope." +
            "DestinationConstruction<TOuter, TValue>",
            "<TOuter, TValue>",
            typeParameterDocumentation,
            constraints);

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Outer_1_Destination_1.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Outer_TOuter__Source_TValue___TestCase_Outer_TOuter__Destination_TValue_.g.cs",
                extension
            ));
    }

    [Test]
    public async Task Substitutes_closed_types_in_definition_constraints()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public interface IMarker<T> { }

    public sealed class Source<TValue, TMarker>
        where TValue : IMarker<TMarker>
    {
        public TValue Value { get; init; } = default!;
    }

    public sealed class Destination<TValue, TMarker>
        where TValue : IMarker<TMarker>
    {
        public Destination(TValue value) { }
    }

    [MorphantMapper]
    public partial class TestMapper<TValue> : TypeMapper
        where TValue : IMarker<int>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<
                    Source<TValue, int>,
                    Destination<TValue, int>>()
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
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<TValue> value)
{
}
""";

        const string typeDeclaration =
"""
internal sealed class DestinationConstruction<TValue, TMarker>
    where TValue : global::TestCase.IMarker<TMarker>
""";
        const string parametersTypeDeclaration =
"""
internal sealed class DestinationConstructionConstructorParameters<TValue, TMarker>
    where TValue : global::TestCase.IMarker<TMarker>
""";
        const string destinationCref =
            "global::TestCase.Destination&lt;TValue, TMarker&gt;";
        var plan = BuildPlan(
            "TestCase.Morphant.Generated",
            typeDeclaration,
            parametersTypeDeclaration,
            "DestinationConstruction<TValue, TMarker>",
            "DestinationConstructionConstructorParameters<TValue, TMarker>",
            destinationCref,
            "global::TestCase.Destination<TValue, TMarker>",
            destinationConstructor,
            (
                "value",
                "public global::Morphant.Members.ConstructorParameter<TValue> value = null!;"
            ));
        const string sourceType = "global::TestCase.Source<TValue, int>";
        const string destinationType =
            "global::TestCase.Destination<TValue, int>";
        var extension = BuildExtension(
            sourceType,
            destinationType,
            "global::TestCase.Morphant.Generated." +
            "DestinationConstruction<TValue, int>",
            "<TValue>",
            "/// <typeparam name=\"TValue\">A type used by the mapping pair.</typeparam>",
            "where TValue : global::TestCase.IMarker<int>");

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Destination_2.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source_TValue__System_Int32___TestCase_Destination_TValue__System_Int32_.g.cs",
                extension
            ));
    }

    [Test]
    public async Task Renames_shadowed_containing_type_parameters_in_the_plan()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591, CS0693
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Outer<T>
    {
        public sealed class Destination<T>
        {
            public Destination(
                Outer<T> outer,
                T value) { }
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Outer<string>.Destination<int>>();
    }
}
""";

        // lang=c#
        const string destinationConstructor =
"""
/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="outer">Configures the <c>outer</c> constructor argument.</param>
/// <param name="value">Configures the <c>value</c> constructor argument.</param>
public DestinationConstruction(
    global::Morphant.Members.ConstructorParameter<global::TestCase.Outer<T2>> outer,
    global::Morphant.Members.ConstructorParameter<T2> value)
{
}
""";

        const string destinationCref =
            "global::TestCase.Outer&lt;T&gt;.Destination&lt;T&gt;";
        var plan = BuildPlan(
            "TestCase.Morphant.Generated.Outer1Scope",
            "internal sealed class DestinationConstruction<T, T2>",
            "internal sealed class DestinationConstructionConstructorParameters<T, T2>",
            "DestinationConstruction<T, T2>",
            "DestinationConstructionConstructorParameters<T, T2>",
            destinationCref,
            "global::TestCase.Outer<T>.Destination<T2>",
            destinationConstructor,
            (
                "outer",
                "public global::Morphant.Members.ConstructorParameter<global::TestCase.Outer<T2>> outer = null!;"
            ),
            (
                "value",
                "public global::Morphant.Members.ConstructorParameter<T2> value = null!;"
            ));
        const string destinationType =
            "global::TestCase.Outer<string>.Destination<int>";
        var extension = BuildExtension(
            "global::TestCase.Source",
            destinationType,
            "global::TestCase.Morphant.Generated.Outer1Scope." +
            "DestinationConstruction<string, int>");

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Outer_1_Destination_1.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Outer_System_String__Destination_System_Int32_.g.cs",
                extension
            ));
    }

    private static string BuildPlan(
        string @namespace,
        string typeDeclaration,
        string parametersTypeDeclaration,
        string constructionTypeReference,
        string parametersTypeReference,
        string destinationCref,
        string destinationType,
        string destinationConstructor,
        params (string ParameterName, string Declaration)[] fields)
    {
        var parameterFields = fields
            .Select(static field =>
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    field.ParameterName,
                    field.Declaration))
            .ToArray();
        var parametersType =
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                parametersTypeDeclaration,
                destinationCref,
                parameterFields);
        var constructionType =
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .FallbackPlanDocumentation(destinationCref),
                typeDeclaration,
                "DestinationConstruction",
                constructionTypeReference,
                destinationType,
                destinationConstructor,
                parametersTypeReference);

        return ConstructionSurfaceExpectedSource.Plan(
            @namespace,
            constructionType,
            parametersType);
    }

    private static string BuildExtension(
        string sourceType,
        string destinationType,
        string constructionResultType,
        string methodTypeParameterList = "",
        string typeParameterDocumentation = "",
        string constraints = "")
    {
        var builderType =
            $"global::Morphant.MapperBuilder<{sourceType}, {destinationType}>";

        return ConstructionSurfaceExpectedSource.MappingExtension(
            builderType,
            sourceType,
            sourceType + "?",
            destinationType,
            destinationType,
            constructionResultType,
            methodTypeParameterList,
            typeParameterDocumentation,
            constraints);
    }
}
