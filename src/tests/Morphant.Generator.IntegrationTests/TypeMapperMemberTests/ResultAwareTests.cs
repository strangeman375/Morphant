using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class ResultAwareTests
{
    [Test]
    public void Keeps_previous_and_selected_constructor_result_distinct()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;
using System.Diagnostics.CodeAnalysis;

namespace TestCase
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
    public partial class TestMapper : TypeMapper
    {
        public static int InitialCount { get; private set; }

        public static int MutableCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct((source, previous) =>
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
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp11,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Uses_the_selected_factory_and_direct_results_and_stops_on_null()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
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
                .Construct((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse)
                    {
                        return previous;
                    }

                    return new(ByFactory<FactoryDestination>(() =>
                        source.ReturnNull
                            ? null!
                            : new FactoryDestination(source.Seed)));
                })
                .Members((source, _, result) => new()
                {
                    Value = Assign(result.Value + source.Delta)
                });

            builder.Map<Source, IDirectDestination>()
                .Construct((source, previous) =>
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
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Provides_the_non_null_value_of_a_nullable_destination_as_result()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Seed { get; init; }

        public int Delta { get; init; }
    }

    public struct Destination
    {
        public Destination(int seed)
        {
            Seed = seed;
            Value = seed * 10;
        }

        public int Seed { get; }

        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination?>()
                .Construct(source => new(seed: source.Seed))
                .Members((source, _, result) => new()
                {
                    Value = result.Value + source.Delta
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination?>)
                new TestMapper();
            var context = default(MappingContext);
            var created = mapper.Create(
                new Source { Seed = 2, Delta = 3 },
                context);
            var updated = mapper.Update(
                new Source { Delta = 4 },
                new Destination(5),
                context);

            if (created is not { Seed: 2, Value: 23 } ||
                updated is not { Seed: 5, Value: 54 })
            {
                throw new InvalidOperationException(
                    "Nullable destination result was not normalized.");
            }
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
