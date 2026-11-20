// Compiled integration scenario: TypeMapperConfigurationFailureTests::Reports_an_invalid_pair_without_hiding_independent_contracts
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0014
#pragma warning disable MORPH0019

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationFailure_f27b94e1
{
    public sealed class Destination { }

    public sealed class Envelope<T> { }

    public sealed class Result<T> { }

    public sealed class Source { }

    public sealed class IndependentDestination { }

    [MorphantMapper]
    public partial class InvalidPlanMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .ConstructUsing(_ => new Destination())
                .ResolveUsing((_, __) => new Destination());
        }
    }

    [MorphantMapper]
    public partial class GenericMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Envelope<T>, Result<string>>();
            builder.Map<Envelope<string>, Result<T>>();
            builder.Map<Source, IndependentDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            ExpectConfigurationFailure(new InvalidPlanMapper());

            var generic = new GenericMapper<int>();
            var contract =
                (ITypeMapper<Source, IndependentDestination>)generic;
            var result = contract.Create(
                new Source());

            if (result is null ||
                generic is ITypeMapper<Envelope<int>, Result<string>> ||
                generic is ITypeMapper<Envelope<string>, Result<int>>)
            {
                throw new InvalidOperationException(
                    "Generic contract isolation was not preserved.");
            }
        }

        private static void ExpectConfigurationFailure(
            InvalidPlanMapper mapper)
        {
            var contract = (ITypeMapper<Source, Destination>)mapper;

            try
            {
                contract.Create(new Source(), default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException
                   exception)
                when (exception.Operation == MappingOperation.Create &&
                      exception.SourceType == typeof(Source) &&
                      exception.DestinationType == typeof(Destination) &&
                      exception.Reason ==
                      "The mapping configuration is invalid: Construct or " +
                      "Resolve is configured more than once.")
            {
                return;
            }

            throw new InvalidOperationException(
                "The invalid plan did not expose a structured failure " +
                "contract.");
        }
    }
}
