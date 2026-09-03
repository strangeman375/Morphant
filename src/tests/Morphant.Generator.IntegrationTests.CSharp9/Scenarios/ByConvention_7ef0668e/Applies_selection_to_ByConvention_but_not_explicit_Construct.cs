// Compiled integration scenario: TypeMapperConstructorSelectionTests/ByConventionTests::Applies_selection_to_ByConvention_but_not_explicit_Construct
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0036

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByConvention_7ef0668e
{
    public sealed class Source
    {
        public int Id { get; init; }
    }

    public sealed class ParameterlessDestination
    {
        public ParameterlessDestination()
        {
            Kind = "parameterless";
        }

        public ParameterlessDestination(int id)
        {
            Kind = id.ToString();
        }

        public string Kind { get; }
    }

    public sealed class ExplicitByConventionDestination
    {
        public ExplicitByConventionDestination()
        {
        }

        public ExplicitByConventionDestination(int id)
        {
        }
    }

    public sealed class ExplicitConstructDestination
    {
        public ExplicitConstructDestination()
        {
            Kind = "parameterless";
        }

        public ExplicitConstructDestination(int id)
        {
            Kind = id.ToString();
        }

        public string Kind { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ParameterlessDestination>()
                .ConstructorSelection(
                    ConstructorSelection.Parameterless)
                .Construct(_ => new(ByConvention()));
            builder.Map<Source, ExplicitByConventionDestination>()
                .ConstructorSelection(ConstructorSelection.Explicit)
                .Construct(_ => new(ByConvention()));
            builder.Map<Source, ExplicitConstructDestination>()
                .ConstructorSelection(ConstructorSelection.Explicit)
                .Construct(source => new(source.Id));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Id = 17 };
            var context = default(MappingContext);
            var parameterless =
                ((ITypeMapper<Source, ParameterlessDestination>)mapper)
                    .Create(source, context);
            var explicitConstruct =
                ((ITypeMapper<Source, ExplicitConstructDestination>)mapper)
                    .Create(source, context);

            if (parameterless.Kind != "parameterless" ||
                explicitConstruct.Kind != "17")
            {
                throw new InvalidOperationException(
                    "ConstructorSelection crossed its applicability boundary.");
            }

            try
            {
                ((ITypeMapper<Source, ExplicitByConventionDestination>)mapper)
                    .Create(source, context);
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Explicit allowed ByConvention construction.");
        }
    }
}
