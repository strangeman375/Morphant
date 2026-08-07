using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperCreationResultTests;

[TestFixture]
internal sealed class DestinationKindTests
{
    [Test]
    public void Maps_nullable_value_and_constructed_generic_factory_results()
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
    public readonly struct Source
    {
        public int Value { get; init; }
    }

    public struct ValueDestination
    {
        public ValueDestination(int seed)
        {
            Seed = seed;
            Value = -1;
        }

        public int Seed { get; }

        public int Value { get; set; }
    }

    public sealed class GenericDestination<T>
    {
        public GenericDestination(T seed)
        {
            Seed = seed;
        }

        public T Seed { get; }

        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int ValueFactoryCount { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ValueDestination?>()
                .Construct(source => new(ByFactory(() =>
                {
                    ValueFactoryCount++;
                    return new ValueDestination(source.Value + 1);
                })));

            builder.Map<Source, GenericDestination<int>>()
                .Construct(source => new(ByFactory(() =>
                    new GenericDestination<int>(source.Value + 2))));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 5 };
            var context = default(MappingContext);
            var valueMapper =
                (ITypeMapper<Source, ValueDestination?>)mapper;
            var created = valueMapper.Create(source, context);
            var createdByUpdate = valueMapper.Update(source, null, context);
            var previous = new ValueDestination(40);
            var updated = valueMapper.Update(source, previous, context);

            if (!created.HasValue || created.Value.Seed != 6 ||
                created.Value.Value != 5 ||
                !createdByUpdate.HasValue ||
                createdByUpdate.Value.Seed != 6 ||
                createdByUpdate.Value.Value != 5 ||
                !updated.HasValue || updated.Value.Seed != 40 ||
                updated.Value.Value != 5 ||
                TestMapper.ValueFactoryCount != 2)
            {
                throw new InvalidOperationException(
                    "Nullable value factory lifecycle changed.");
            }

            var generic =
                ((ITypeMapper<Source, GenericDestination<int>>)mapper)
                .Create(source, context);

            if (generic.Seed != 7 || generic.Value != 5)
            {
                throw new InvalidOperationException(
                    "Constructed generic factory result was not mapped.");
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
