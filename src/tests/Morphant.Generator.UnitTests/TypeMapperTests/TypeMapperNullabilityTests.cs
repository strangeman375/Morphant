using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperTests;

[TestFixture]
internal sealed class TypeMapperNullabilityTests
{
    [Test]
    public void Declares_nullable_interface_inputs_and_results()
    {
        var methods = typeof(ITypeMapper<,>)
            .GetMethods()
            .OrderBy(static method => method.GetParameters().Length)
            .ToArray();
        var context = new NullabilityInfoContext();

        Assert.Multiple(() =>
        {
            Assert.That(methods, Has.Length.EqualTo(2));

            AssertMethodNullability(
                context,
                methods[0],
                NullabilityState.Nullable,
                NullabilityState.NotNull);

            AssertMethodNullability(
                context,
                methods[1],
                NullabilityState.Nullable,
                NullabilityState.Nullable,
                NullabilityState.NotNull);
        });
    }

    [Test]
    public async Task Annotates_reference_mapping_inputs_and_results()
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

    public sealed record Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        await TypeMapperGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs",
                BuildExpectedCreatableMapper(
                    "global::TestCase.Source",
                    "global::TestCase.Source?",
                    "global::TestCase.Destination",
                    "global::TestCase.Destination?",
                    canMapExisting: true)
            ));
    }

    [Test]
    public async Task Keeps_non_nullable_value_mapping_types_unlifted()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<int, long>();
    }
}
""";

        await TypeMapperGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs",
                BuildExpectedMapper(
                    "int",
                    "int",
                    "long",
                    "long")
            ));
    }

    [Test]
    public async Task Preserves_nullable_value_mapping_types()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<int?, long?>();
    }
}
""";

        await TypeMapperGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs",
                BuildExpectedMapper(
                    "int?",
                    "int?",
                    "long?",
                    "long?")
            ));
    }

    [Test]
    public async Task Annotates_unconstrained_mapping_type_parameters()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper<TSource, TDestination> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<TSource, TDestination>();
    }
}
""";

        await TypeMapperGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper_2.g.cs",
                BuildExpectedMapper(
                    "TSource",
                    "TSource?",
                    "TDestination",
                    "TDestination?",
                    "<TSource, TDestination>")
            ));
    }

    [Test]
    public async Task Keeps_value_constrained_mapping_type_parameters_unlifted()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper<TSource, TDestination> : TypeMapper
        where TSource : struct
        where TDestination : unmanaged
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<TSource, TDestination>();
    }
}
""";

        await TypeMapperGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper_2.g.cs",
                BuildExpectedCreatableMapper(
                    "TSource",
                    "TSource",
                    "TDestination",
                    "TDestination",
                    "<TSource, TDestination>",
                    canMapExisting: false)
            ));
    }

    private static void AssertMethodNullability(
        NullabilityInfoContext context,
        MethodInfo method,
        params NullabilityState[] parameterStates)
    {
        Assert.That(
            context.Create(method.ReturnParameter).ReadState,
            Is.EqualTo(NullabilityState.Nullable));

        var actualParameterStates = method
            .GetParameters()
            .Select(parameter => context.Create(parameter).ReadState);

        Assert.That(actualParameterStates, Is.EqualTo(parameterStates));
    }

    private static string BuildExpectedMapper(
        string sourceType,
        string sourceParameterType,
        string destinationType,
        string destinationMethodType,
        string typeParameterList = "")
    {
        // lang=c#
        return $$"""
                 // <auto-generated />
                 #nullable enable

                 namespace TestCase
                 {
                     public partial class TestMapper{{typeParameterList}} :
                         global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>
                     {
                         /// <inheritdoc/>
                         {{destinationMethodType}} global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>.Map(
                             {{sourceParameterType}} source,
                             global::Morphant.MappingContext context)
                             => throw new global::System.NotImplementedException();

                         /// <inheritdoc/>
                         {{destinationMethodType}} global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>.Map(
                             {{sourceParameterType}} source,
                             {{destinationMethodType}} destination,
                             global::Morphant.MappingContext context)
                             => throw new global::System.NotImplementedException();
                     }
                 }
                 """;
    }

    private static string BuildExpectedCreatableMapper(
        string sourceType,
        string sourceParameterType,
        string destinationType,
        string destinationMethodType,
        string typeParameterList = "",
        bool canMapExisting = false)
    {
        var mapExistingImplementation = canMapExisting
            ? "=> destination;"
            : "=> throw new global::System.NotImplementedException();";

        // lang=c#
        return $$"""
                 // <auto-generated />
                 #nullable enable

                 namespace TestCase
                 {
                     public partial class TestMapper{{typeParameterList}} :
                         global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>
                     {
                         /// <inheritdoc/>
                         {{destinationMethodType}} global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>.Map(
                             {{sourceParameterType}} source,
                             global::Morphant.MappingContext context)
                         {
                             return new {{destinationType}}();
                         }

                         /// <inheritdoc/>
                         {{destinationMethodType}} global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>.Map(
                             {{sourceParameterType}} source,
                             {{destinationMethodType}} destination,
                             global::Morphant.MappingContext context)
                             {{mapExistingImplementation}}
                     }
                 }
                 """;
    }
}
