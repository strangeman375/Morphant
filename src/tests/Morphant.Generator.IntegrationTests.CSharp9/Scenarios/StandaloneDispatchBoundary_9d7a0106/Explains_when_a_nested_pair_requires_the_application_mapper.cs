// Compiled integration scenario: TypeMapperStandaloneDispatchTests::Explains_when_a_nested_pair_requires_the_application_mapper
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.StandaloneDispatchBoundary_9d7a0106
{
    public sealed class MissingSource
    {
    }

    public sealed class MissingDestination
    {
    }

    public sealed class Source
    {
    }

    public sealed class Destination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Convert((_, __, context) =>
                    context.Mapper.Map<
                        MissingSource,
                        MissingDestination>(new MissingSource()) is null
                        ? new Destination()
                        : new Destination());
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var generated = new TestMapper();
            var contract = (ITypeMapper<Source, Destination>)generated;
            ITypeMapper<Source, Destination>? missing = null;

            ExpectNullMapper(() => missing!.Create(new Source()));
            ExpectNullMapper(() => missing!.Update(
                new Source(),
                new Destination()));

            try
            {
                contract.Create(new Source());
            }
            catch (MappingNotFoundException exception)
                when (exception.Operation == MappingOperation.Create &&
                      exception.SourceType == typeof(MissingSource) &&
                      exception.DestinationType ==
                      typeof(MissingDestination) &&
                      exception.Message.Contains("Use IMapper"))
            {
                return;
            }

            throw new InvalidOperationException(
                "Standalone dispatch did not explain its exact-pair " +
                    "boundary.");
        }

        private static void ExpectNullMapper(Action action)
        {
            try
            {
                action();
            }
            catch (ArgumentNullException exception)
                when (exception.ParamName == "mapper")
            {
                return;
            }

            throw new InvalidOperationException(
                "A context-free mapping extension accepted a null mapper.");
        }
    }
}
