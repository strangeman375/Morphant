// Compiled integration scenario: TypeMapperCSharpSemanticsTests::Rejects_untransferable_extension_binding_before_emission
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0030

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;
using Morphant.Generator.IntegrationTests.CSharp9.TransferPreflight_a11ce00d;

namespace Morphant.Generator.IntegrationTests.CSharp9.TransferPreflight_a11ce00d
{
    public sealed class Sequence
    {
        public int[] Values { get; set; } = Array.Empty<int>();
    }

    public struct SequenceEnumerator
    {
        private readonly int[] _values;
        private int _index;

        public SequenceEnumerator(int[] values)
        {
            _values = values;
            _index = -1;
        }

        public int Current => _values[_index];

        public bool MoveNext() => ++_index < _values.Length;
    }

    public static class TransferExtensions
    {
        public static int Measure(this string value) => value.Length;

        public static SequenceEnumerator GetEnumerator(
            this Sequence sequence) =>
            new SequenceEnumerator(sequence.Values);
    }
}

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TransferPreflight_a11ce00d
{
    public sealed class Source
    {
        public string Text { get; set; } = string.Empty;

        public Sequence Values { get; set; } = new Sequence();
    }

    public sealed class MethodGroupDestination
    {
        public MethodGroupDestination(Func<int> value) => Value = value;

        public Func<int> Value { get; }
    }

    public sealed class PatternDestination
    {
        public PatternDestination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, MethodGroupDestination>()
                .Construct(source =>
                    new(Value<Func<int>>(source.Text.Measure)));

            builder.Map<Source, PatternDestination>()
                .ConstructUsing(source =>
                {
                    var total = 0;

                    foreach (var value in source.Values)
                    {
                        total += value;
                    }

                    return new PatternDestination(total);
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
                Text = "abc",
                Values = new Sequence
                {
                    Values = new[] { 1, 2, 3 }
                }
            };

            ExpectUnsupported<MethodGroupDestination>(mapper, source);
            ExpectUnsupported<PatternDestination>(mapper, source);
        }

        private static void ExpectUnsupported<TDestination>(
            TestMapper mapper,
            Source source)
        {
            try
            {
                ((ITypeMapper<Source, TDestination>)mapper)
                    .Create(source, default(MappingContext));
            }
            catch (MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Untransferable extension binding escaped into generated code.");
        }
    }
}
