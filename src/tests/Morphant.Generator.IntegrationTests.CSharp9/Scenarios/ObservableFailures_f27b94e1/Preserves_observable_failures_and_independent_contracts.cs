// Compiled integration scenario: TypeMapperObservableFailureTests::Preserves_observable_failures_and_independent_contracts
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ObservableFailures_f27b94e1
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
            var result = generic.Create<Source, IndependentDestination>(
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
            try
            {
                mapper.Create(new Source());
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException
                   exception)
                when (exception.Operation == MappingOperation.Create &&
                      exception.SourceType == typeof(Source) &&
                      exception.DestinationType == typeof(Destination) &&
                      exception.Reason ==
                      "The configured mapping plan is invalid: more than " +
                      "one result callback is configured.")
            {
                return;
            }

            throw new InvalidOperationException(
                "The invalid plan did not expose a structured failure " +
                "contract.");
        }
    }
}
