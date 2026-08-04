using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperCreationResultTests;

[TestFixture]
internal sealed class DirectConstructTests
{
    [Test]
    public void Executes_expression_method_group_and_full_block_forms()
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
        public int Value { get; init; }

        public bool Reuse { get; init; }

        public bool ReturnNull { get; init; }

        public bool Fail { get; init; }
    }

    public interface IDestination
    {
        int Seed { get; }

        int Value { get; set; }
    }

    public sealed class Destination : IDestination
    {
        private int _value;

        public Destination(int seed)
        {
            Seed = seed;
        }

        public static int AssignmentCount { get; private set; }

        public int Seed { get; }

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                AssignmentCount++;
            }
        }
    }

    public abstract class AbstractDestination
    {
        public int Value { get; set; }
    }

    public sealed class ConcreteDestination : AbstractDestination
    {
    }

    public sealed class FactoryOnly
    {
        private FactoryOnly(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public static FactoryOnly Create(int value) => new(value);
    }

    public enum Level
    {
        None,
        One,
        Two
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int BlockCount { get; private set; }

        public static int ParseCount { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, IDestination>()
                .Construct((source, previous) =>
                {
                    BlockCount++;

                    if (source.Fail)
                    {
                        throw new InvalidOperationException("failed");
                    }

                    if (source.ReturnNull)
                    {
                        return null!;
                    }

                    if (previous.HasValue && source.Reuse)
                    {
                        return previous.Value;
                    }

                    static int Normalize(int value) =>
                        value < 0 ? -value : value;

                    var seed = 0;

                    for (var index = 0; index < 2; index++)
                    {
                        seed += Normalize(source.Value) + index;
                    }

                    return new Destination(seed);
                });

            builder.Map<string, Guid>()
                .Construct(ParseGuid);

            builder.Map<Source, Level>()
                .Construct(source => (Level)source.Value);

            builder.Map<Source, AbstractDestination>()
                .Construct(source => new ConcreteDestination
                {
                    Value = source.Value + 1
                });

            builder.Map<Source, FactoryOnly>()
                .Construct(source => FactoryOnly.Create(source.Value));
        }

        private static Guid ParseGuid(string source)
        {
            ParseCount++;
            return Guid.Parse(source);
        }

        private static IDestination ConstructDestination() =>
            new Destination(-1);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var typed = (ITypeMapper<Source, IDestination>)mapper;
            var source = new Source { Value = 3 };
            var created = typed.Map(source, context);
            var createdByUpdate = typed.Map(source, null, context);
            var previous = new Destination(41);
            var reused = typed.Map(
                new Source { Value = 5, Reuse = true },
                previous,
                context);
            var replaced = typed.Map(
                new Source { Value = 4 },
                previous,
                context);
            var nullResult = typed.Map(
                new Source { ReturnNull = true },
                context);

            if (created.Seed != 7 || created.Value != 3 ||
                createdByUpdate.Seed != 7 || createdByUpdate.Value != 3 ||
                !ReferenceEquals(previous, reused) || reused.Value != 5 ||
                ReferenceEquals(previous, replaced) ||
                replaced.Seed != 9 || replaced.Value != 4 ||
                nullResult is not null ||
                Destination.AssignmentCount != 4 ||
                TestMapper.BlockCount != 5)
            {
                throw new InvalidOperationException(
                    "Direct block lifecycle was not preserved.");
            }

            try
            {
                typed.Map(new Source { Fail = true }, context);
                throw new InvalidOperationException(
                    "Direct block exception was swallowed.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "failed")
            {
            }

            var guidMapper = (ITypeMapper<string, Guid>)mapper;
            var parsed = guidMapper.Map(
                "00112233-4455-6677-8899-aabbccddeeff",
                context);
            var existingGuid = Guid.NewGuid();
            var preservedGuid = guidMapper.Map(
                "ffffffff-ffff-ffff-ffff-ffffffffffff",
                existingGuid,
                context);

            if (parsed != Guid.Parse(
                    "00112233-4455-6677-8899-aabbccddeeff") ||
                preservedGuid != existingGuid ||
                TestMapper.ParseCount != 1)
            {
                throw new InvalidOperationException(
                    "Direct method group lifecycle was not preserved.");
            }

            var level = ((ITypeMapper<Source, Level>)mapper)
                .Map(new Source { Value = 2 }, context);
            var abstractResult =
                ((ITypeMapper<Source, AbstractDestination>)mapper)
                .Map(new Source { Value = 8 }, context);
            var factoryOnly =
                ((ITypeMapper<Source, FactoryOnly>)mapper)
                .Map(new Source { Value = 9 }, context);

            if (level != Level.Two ||
                abstractResult is not ConcreteDestination ||
                abstractResult.Value != 8 ||
                factoryOnly.Value != 9)
            {
                throw new InvalidOperationException(
                    "A direct destination kind used the wrong result.");
            }
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Keeps_source_only_Construct_inactive_for_existing_destination()
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
        public int Value { get; init; }
    }

    public interface IDestination
    {
        int Value { get; set; }
    }

    public sealed class Destination : IDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int ConstructionCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, IDestination>()
                .Construct(source => Create(source.Value));

        private static IDestination Create(int value)
        {
            ConstructionCount++;
            return new Destination { Value = value + 10 };
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, IDestination>)new TestMapper();
            var context = default(MappingContext);
            var source = new Source { Value = 7 };
            var created = mapper.Map(source, context);
            var previous = new Destination();
            var updated = mapper.Map(source, previous, context);

            if (created.Value != 7 ||
                !ReferenceEquals(previous, updated) ||
                updated.Value != 7 ||
                TestMapper.ConstructionCount != 1)
            {
                throw new InvalidOperationException(
                    "Source-only direct Construct ran for existing destination.");
            }
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
