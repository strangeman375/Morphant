// Compiled integration scenario: TypeMapperObservableFailureTests::Preserves_observable_failures_and_independent_contracts
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    public partial class UnsupportedRootMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<List<int>, Destination>();
            builder.Map<int[], Destination>();
            builder.Map<(int Left, int Right), Destination>();
            builder.Map<Func<int>, Destination>();
            builder.Map<Task<int>, Destination>();
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
            var unsupported =
                (ITypeMapper<List<int>, Destination>)
                new UnsupportedRootMapper();

            ExpectConfigurationFailure(
                () => unsupported.Create(
                    new List<int>(),
                    default(MappingContext)),
                "collection or buffer root");
            ExpectConfigurationFailure(
                () => unsupported.Update(
                    new List<int>(),
                    new Destination(),
                    default(MappingContext)),
                "collection or buffer root");
            ExpectConfigurationFailure(
                () => ((ITypeMapper<int[], Destination>)
                    new UnsupportedRootMapper()).Create(
                        Array.Empty<int>(),
                        default(MappingContext)),
                "array root");
            ExpectConfigurationFailure(
                () => ((ITypeMapper<(int Left, int Right), Destination>)
                    new UnsupportedRootMapper()).Create(
                        (1, 2),
                        default(MappingContext)),
                "tuple root");
            ExpectConfigurationFailure(
                () => ((ITypeMapper<Func<int>, Destination>)
                    new UnsupportedRootMapper()).Create(
                        static () => 1,
                        default(MappingContext)),
                "delegate root");
            ExpectConfigurationFailure(
                () => ((ITypeMapper<Task<int>, Destination>)
                    new UnsupportedRootMapper()).Create(
                        Task.FromResult(1),
                        default(MappingContext)),
                "deferred or async root");

            var generic = new GenericMapper<int>();
            var independent =
                (ITypeMapper<Source, IndependentDestination>)generic;
            var result = independent.Create(
                new Source(),
                default(MappingContext));

            if (result is null ||
                generic is ITypeMapper<Envelope<int>, Result<string>> ||
                generic is ITypeMapper<Envelope<string>, Result<int>>)
            {
                throw new InvalidOperationException(
                    "Generic contract isolation was not preserved.");
            }
        }

        private static void ExpectConfigurationFailure(
            Action action,
            string expectedReason)
        {
            try
            {
                action();
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException
                   exception)
                when (exception.Message.Contains(
                    expectedReason,
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "The unsupported root did not expose a complete failure " +
                "contract.");
        }
    }
}
