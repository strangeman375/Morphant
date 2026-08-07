// Compiled integration scenario: TypeMapperCreationResultTests/ByFactoryTests::Executes_lambda_block_method_group_and_delegate_forms_once
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByFactory_baa540b5
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
