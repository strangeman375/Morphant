// Compiled integration scenario: TypeMapperExpressionTransferTests::Preserves_local_warning_and_nullable_context
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.LexicalContext_a11ce00b
{
    public sealed class Source
    {
        public string? Text { get; set; }
    }

    public sealed class PragmaDestination
    {
        public PragmaDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class NullableDestination
    {
        public int Value { get; set; }
    }

    public sealed class ConvertDestination
    {
        public ConvertDestination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
#pragma warning disable CS8602
            builder.Map<Source, PragmaDestination>()
                .Construct(source => new(source.Text.Length));

            builder.Map<Source, ConvertDestination>()
                .Convert(source =>
                    new ConvertDestination(source!.Text.Length));
#pragma warning restore CS8602

#nullable disable warnings
            builder.Map<Source, NullableDestination>()
                .Members(source => new()
                {
                    Value = source.Text.Length
                });
#nullable enable warnings
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Text = "Morphant" };

            AssertValue<PragmaDestination>(
                mapper,
                source,
                static destination => destination.Value);
            AssertValue<NullableDestination>(
                mapper,
                source,
                static destination => destination.Value);
            AssertValue<ConvertDestination>(
                mapper,
                source,
                static destination => destination.Value);
        }

        private static void AssertValue<TDestination>(
            TestMapper mapper,
            Source source,
            Func<TDestination, int> read)
        {
            var destination =
                ((ITypeMapper<Source, TDestination>)mapper)
                .Create(source, default(MappingContext));

            if (read(destination) != 8)
            {
                throw new InvalidOperationException(
                    "A local warning or nullable context was lost.");
            }
        }
    }
}
