// Compiled integration scenario: TypeMapperDeclarativeControlFlowTests/TransferredExpressionsTests::Executes_queries_and_deferred_local_functions_in_all_structured_surfaces
#nullable enable
#pragma warning disable CS1591

using System;
using System.Linq;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TransferredExpressions_a11ce003
{
    public sealed class Source
    {
        public int[] Values { get; set; } = Array.Empty<int>();

        public int Factor { get; set; }
    }

    public sealed class ConstructDestination
    {
        public ConstructDestination(
            int[] snapshot,
            Func<int[]> deferred)
        {
            Snapshot = snapshot;
            Deferred = deferred;
        }

        public int[] Snapshot { get; }

        public Func<int[]> Deferred { get; }
    }

    public sealed class ResolveDestination
    {
        public ResolveDestination(
            int[] snapshot,
            Func<int[]> deferred)
        {
            Snapshot = snapshot;
            Deferred = deferred;
        }

        public int[] Snapshot { get; }

        public Func<int[]> Deferred { get; }
    }

    public sealed class MembersDestination
    {
        public int[] Snapshot { get; set; } = Array.Empty<int>();

        public Func<int[]> Deferred { get; set; } =
            () => Array.Empty<int>();
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct(source => new(
                    (from context in source.Values
                     where context > 0
                     select context * source.Factor).ToArray(),
                    Value<Func<int[]>>(() =>
                    {
                        int Multiply(int value) =>
                            value * source.Factor;

                        return (from destination in source.Values
                                where destination > 0
                                select Multiply(destination)).ToArray();
                    })));

            builder.Map<Source, ResolveDestination>()
                .Resolve((source, _) => new(
                    (from context in source.Values
                     where context > 0
                     select context * source.Factor).ToArray(),
                    Value<Func<int[]>>(() =>
                    {
                        int Multiply(int value) =>
                            value * source.Factor;

                        return (from destination in source.Values
                                where destination > 0
                                select Multiply(destination)).ToArray();
                    })));

            builder.Map<Source, MembersDestination>()
                .Members(source => new()
                {
                    Snapshot =
                        (from context in source.Values
                         where context > 0
                         select context * source.Factor).ToArray(),
                    Deferred = Value<Func<int[]>>(() =>
                    {
                        int Multiply(int value) =>
                            value * source.Factor;

                        return (from destination in source.Values
                                where destination > 0
                                select Multiply(destination)).ToArray();
                    })
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                Values = new[] { -1, 2, 3 },
                Factor = 2
            };

            var construct =
                ((ITypeMapper<Source, ConstructDestination>)mapper)
                .Create(source, default(MappingContext));
            var resolve =
                ((ITypeMapper<Source, ResolveDestination>)mapper)
                .Create(source, default(MappingContext));
            var members =
                ((ITypeMapper<Source, MembersDestination>)mapper)
                .Create(source, default(MappingContext));

            AssertValues(construct.Snapshot, 4, 6);
            AssertValues(resolve.Snapshot, 4, 6);
            AssertValues(members.Snapshot, 4, 6);

            source.Values = new[] { -2, 4 };
            source.Factor = 3;

            AssertValues(construct.Deferred(), 12);
            AssertValues(resolve.Deferred(), 12);
            AssertValues(members.Deferred(), 12);
        }

        private static void AssertValues(
            int[] actual,
            params int[] expected)
        {
            if (!actual.SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    "A transferred query or local function changed semantics.");
            }
        }
    }
}
