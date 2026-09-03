// Compiled integration scenario: TypeMapperMemberTests/ResultAwareTests::Keeps_previous_and_selected_constructor_result_distinct
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ResultAware_3aa73f8a
{
    public sealed class Source
    {
        public int Id { get; init; }

        public int Delta { get; init; }

        public bool Reuse { get; init; }
    }

    public sealed class Destination
    {
        [SetsRequiredMembers]
        public Destination(int seed)
        {
            Seed = seed;
            Mutable = 100 + seed;
            Field = 200 + seed;
            RequiredInitial = "constructor";
            RequiredPost = "constructor";
        }

        public int Seed { get; }

        public int Initial { get; init; }

        public string ResultParameterName { get; init; } = "constructor";

        public required string RequiredInitial { get; set; }

        public required string RequiredPost { get; set; }

        public int Mutable { get; set; }

        public int Field;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int InitialCount { get; private set; }

        public static int MutableCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse)
                    {
                        return previous;
                    }

                    return new(seed: source.Id);
                })
                .Members((source, previous, result) => new()
                {
                    Initial = MapInitial(
                        previous.HasValue
                            ? previous.Value.Seed
                            : -1),
                    ResultParameterName = nameof(result),
                    RequiredInitial = previous.HasValue
                        ? "previous-" + previous.Value.Seed
                        : "create",
                    RequiredPost = "result-" + result.Seed,
                    Mutable = MapMutable(
                        result.Mutable + source.Delta),
                    Field = result.Field + source.Delta
                });

        private static int MapInitial(int value)
        {
            InitialCount++;
            return value;
        }

        private static int MapMutable(int value)
        {
            MutableCount++;
            return value;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var created = mapper.Create(
                new Source { Id = 1, Delta = 3 },
                context);

            if (created.Seed != 1 ||
                created.Initial != -1 ||
                created.ResultParameterName != "result" ||
                created.RequiredInitial != "create" ||
                created.RequiredPost != "result-1" ||
                created.Mutable != 104 ||
                created.Field != 204)
            {
                throw new InvalidOperationException(
                    "Create did not split member phases correctly.");
            }

            var previous = new Destination(7)
            {
                Initial = 70
            };
            var reused = mapper.Update(
                new Source
                {
                    Id = 2,
                    Delta = 5,
                    Reuse = true
                },
                previous,
                context);

            if (!ReferenceEquals(previous, reused) ||
                reused.Initial != 70 ||
                reused.ResultParameterName != "constructor" ||
                reused.RequiredInitial != "previous-7" ||
                reused.RequiredPost != "result-7" ||
                reused.Mutable != 112 ||
                reused.Field != 212)
            {
                throw new InvalidOperationException(
                    "The explicit previous result was not preserved.");
            }

            var replacement = mapper.Update(
                new Source { Id = 9, Delta = 2 },
                previous,
                context);

            if (ReferenceEquals(previous, replacement) ||
                replacement.Seed != 9 ||
                replacement.Initial != 7 ||
                replacement.ResultParameterName != "result" ||
                replacement.RequiredInitial != "previous-7" ||
                replacement.RequiredPost != "result-9" ||
                replacement.Mutable != 111 ||
                replacement.Field != 211 ||
                TestMapper.InitialCount != 2 ||
                TestMapper.MutableCount != 3)
            {
                throw new InvalidOperationException(
                    "Previous and replacement result were conflated.");
            }
        }
    }
}
