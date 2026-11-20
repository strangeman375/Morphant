// Compiled integration scenario: TypeMapperMemberTests/ResultAwareTests::Uses_selected_runtime_callback_results_and_stops_on_null
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ResultAware_f6b07787
{
    public sealed class Source
    {
        public int Seed { get; init; }

        public int Delta { get; init; }

        public bool Reuse { get; init; }

        public bool ReturnNull { get; init; }
    }

    public sealed class FactoryDestination
    {
        public FactoryDestination(int seed)
        {
            Seed = seed;
            Value = seed * 10;
        }

        public int Seed { get; }

        public int Value { get; set; }
    }

    public interface IDirectDestination
    {
        int Seed { get; }

        int Value { get; set; }
    }

    public sealed class DirectDestination : IDirectDestination
    {
        public DirectDestination(int seed)
        {
            Seed = seed;
            Value = seed * 100;
        }

        public int Seed { get; }

        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int AssignmentCount { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, FactoryDestination>()
                .ResolveUsing((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse)
                    {
                        return previous.Value;
                    }

                    return source.ReturnNull
                        ? null!
                        : new FactoryDestination(source.Seed);
                })
                .Members((source, _, result) => new()
                {
                    Value = Assign(result.Value + source.Delta)
                });

            builder.Map<Source, IDirectDestination>()
                .ResolveUsing((source, previous) =>
                    source.ReturnNull
                        ? null!
                        : previous.HasValue && source.Reuse
                            ? previous.Value
                            : new DirectDestination(source.Seed))
                .Members((source, _, result) => new()
                {
                    Value = Assign(result.Value + source.Delta)
                });
        }

        private static int Assign(int value)
        {
            AssignmentCount++;
            return value;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var factoryMapper =
                (ITypeMapper<Source, FactoryDestination>)mapper;
            var directMapper =
                (ITypeMapper<Source, IDirectDestination>)mapper;
            var factoryPrevious = new FactoryDestination(7);
            var directPrevious = new DirectDestination(8);

            var factoryCreated = factoryMapper.Create(
                new Source { Seed = 2, Delta = 3 },
                context);
            var factoryReused = factoryMapper.Update(
                new Source { Delta = 4, Reuse = true },
                factoryPrevious,
                context);
            var factoryReplacement = factoryMapper.Update(
                new Source { Seed = 5, Delta = 6 },
                factoryPrevious,
                context);
            var factoryNull = factoryMapper.Create(
                new Source { ReturnNull = true },
                context);

            var directCreated = directMapper.Create(
                new Source { Seed = 3, Delta = 7 },
                context);
            var directReused = directMapper.Update(
                new Source { Delta = 8, Reuse = true },
                directPrevious,
                context);
            var directReplacement = directMapper.Update(
                new Source { Seed = 4, Delta = 9 },
                directPrevious,
                context);
            var directNull = directMapper.Create(
                new Source { ReturnNull = true },
                context);

            if (factoryCreated.Value != 23 ||
                !ReferenceEquals(factoryPrevious, factoryReused) ||
                factoryReused.Value != 74 ||
                ReferenceEquals(factoryPrevious, factoryReplacement) ||
                factoryReplacement.Value != 56 ||
                factoryNull is not null ||
                directCreated.Value != 307 ||
                !ReferenceEquals(directPrevious, directReused) ||
                directReused.Value != 808 ||
                ReferenceEquals(directPrevious, directReplacement) ||
                directReplacement.Value != 409 ||
                directNull is not null ||
                TestMapper.AssignmentCount != 6)
            {
                throw new InvalidOperationException(
                    "Selected-result or terminal-null semantics changed.");
            }
        }
    }
}
