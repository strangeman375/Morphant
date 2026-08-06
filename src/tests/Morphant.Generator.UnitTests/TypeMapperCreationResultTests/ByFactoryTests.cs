using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperCreationResultTests;

[TestFixture]
internal sealed class ByFactoryTests
{
    [Test]
    public void Executes_lambda_block_method_group_and_delegate_forms_once()
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

        public string Name { get; init; } = string.Empty;
    }

    public sealed class BlockDestination
    {
        public BlockDestination(int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }

        public string Name { get; set; } = string.Empty;
    }

    public sealed class MethodGroupDestination
    {
        public int Value { get; set; }
    }

    public sealed class DelegateDestination
    {
        public int Value { get; set; }
    }

    public sealed class Provider
    {
        public int MethodGroupCount { get; private set; }

        public MethodGroupDestination Create()
        {
            MethodGroupCount++;
            return new MethodGroupDestination();
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        private readonly Func<DelegateDestination> _factory;

        public TestMapper()
        {
            _factory = () =>
            {
                DelegateCount++;
                return new DelegateDestination();
            };
        }

        public static int BlockCount { get; private set; }

        public static int DelegateCount { get; private set; }

        public Provider Provider { get; } = new();

        protected override void Configure(MapperBuilder builder)
        {
            const int Offset = 10;

            builder.Map<Source, BlockDestination>()
                .Construct(source => new(ByFactory(() =>
                {
                    static int Normalize(int value) =>
                        value < 0 ? -value : value;

                    BlockCount++;
                    var seed = Normalize(source.Value) + Offset;

                    for (var index = 0; index < 2; index++)
                    {
                        seed++;
                    }

                    return new BlockDestination(seed);
                })));

            builder.Map<Source, MethodGroupDestination>()
                .Construct(_ => new(ByFactory(Provider.Create)));

            builder.Map<Source, DelegateDestination>()
                .Construct(_ => new(ByFactory(_factory)));
        }

        private static BlockDestination __CreateByFactory() =>
            new(-1);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var source = new Source
            {
                Value = 4,
                Name = "mapped"
            };
            var blockMapper =
                (ITypeMapper<Source, BlockDestination>)mapper;
            var created = blockMapper.Create(source, context);
            var createdByUpdate = blockMapper.Update(source, null, context);
            var previous = new BlockDestination(41);
            var updated = blockMapper.Update(source, previous, context);

            if (created.Seed != 16 || created.Name != "mapped" ||
                createdByUpdate.Seed != 16 ||
                createdByUpdate.Name != "mapped" ||
                !ReferenceEquals(previous, updated) ||
                updated.Name != "mapped" ||
                TestMapper.BlockCount != 2)
            {
                throw new InvalidOperationException(
                    "Block factory did not preserve its lifecycle.");
            }

            var methodGroup =
                ((ITypeMapper<Source, MethodGroupDestination>)mapper)
                .Create(source, context);
            var delegated =
                ((ITypeMapper<Source, DelegateDestination>)mapper)
                .Create(source, context);

            if (methodGroup.Value != 4 ||
                delegated.Value != 4 ||
                mapper.Provider.MethodGroupCount != 1 ||
                TestMapper.DelegateCount != 1)
            {
                throw new InvalidOperationException(
                    "Factory delegate form was not invoked exactly once.");
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
    public void Supports_previous_aware_replacement_and_terminal_null()
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
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public bool Reuse { get; init; }

        public bool ReturnNull { get; init; }
    }

    public sealed class Destination
    {
        private string _name = string.Empty;

        public Destination(int id, bool replacedPrevious)
        {
            Id = id;
            ReplacedPrevious = replacedPrevious;
        }

        public static int AssignmentCount { get; private set; }

        public int Id { get; }

        public bool ReplacedPrevious { get; }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                AssignmentCount++;
            }
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int FactoryCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse)
                    {
                        return previous;
                    }

                    return new(ByFactory<Destination>(() =>
                        Create(
                            source.Id,
                            previous.HasValue,
                            source.ReturnNull)));
                });

        private static Destination Create(
            int id,
            bool replacedPrevious,
            bool returnNull)
        {
            FactoryCount++;

            return returnNull
                ? null!
                : new Destination(id, replacedPrevious);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var context = default(MappingContext);
            var created = mapper.Create(
                new Source { Id = 1, Name = "created" },
                context);
            var previous = new Destination(7, false);
            var reused = mapper.Update(
                new Source
                {
                    Id = 2,
                    Name = "reused",
                    Reuse = true
                },
                previous,
                context);
            var replaced = mapper.Update(
                new Source { Id = 3, Name = "replaced" },
                previous,
                context);
            var nullResult = mapper.Create(
                new Source
                {
                    Id = 4,
                    Name = "must not assign",
                    ReturnNull = true
                },
                context);

            if (created.Id != 1 || created.ReplacedPrevious ||
                created.Name != "created" ||
                !ReferenceEquals(previous, reused) ||
                reused.Name != "reused" ||
                ReferenceEquals(previous, replaced) ||
                replaced.Id != 3 || !replaced.ReplacedPrevious ||
                replaced.Name != "replaced" ||
                nullResult is not null ||
                TestMapper.FactoryCount != 3 ||
                Destination.AssignmentCount != 3)
            {
                throw new InvalidOperationException(
                    "Factory replacement or terminal null semantics changed.");
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
