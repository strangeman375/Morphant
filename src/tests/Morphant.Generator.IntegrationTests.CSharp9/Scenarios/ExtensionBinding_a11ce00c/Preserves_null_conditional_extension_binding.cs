// Compiled integration scenario: TypeMapperCSharpSemanticsTests::Preserves_null_conditional_extension_binding
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;
using Morphant.Generator.IntegrationTests.CSharp9.ExtensionBinding_a11ce00c;

namespace Morphant.Generator.IntegrationTests.CSharp9.ExtensionBinding_a11ce00c
{
    public static class TextExtensions
    {
        public static int Measure(this string value, int offset) =>
            value.Length + offset;

        public static void AddLength(
            this string value,
            ICollection<int> values) =>
            values.Add(value.Length);
    }
}

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionBinding_a11ce00c
{
    public sealed class Source
    {
        public string? Text { get; set; }

        public int Reads { get; private set; }

        public string? ReadText()
        {
            Reads++;
            return Text;
        }
    }

    public interface IValueDestination
    {
        int? Value { get; }
    }

    public sealed class ConstructDestination : IValueDestination
    {
        public ConstructDestination(int? value) => Value = value;

        public int? Value { get; }
    }

    public sealed class ResolveDestination : IValueDestination
    {
        public ResolveDestination(int? value) => Value = value;

        public int? Value { get; }
    }

    public sealed class MembersDestination : IValueDestination
    {
        public int? Value { get; set; }
    }

    public sealed class ConstructUsingDestination : IValueDestination
    {
        public ConstructUsingDestination(int? value) => Value = value;

        public int? Value { get; }
    }

    public sealed class ResolveUsingDestination : IValueDestination
    {
        public ResolveUsingDestination(int? value) => Value = value;

        public int? Value { get; }
    }

    public sealed class ConvertDestination : IValueDestination
    {
        public ConvertDestination(int? value) => Value = value;

        public int? Value { get; }
    }

    public sealed class VoidDestination : IValueDestination
    {
        public VoidDestination(int? value) => Value = value;

        public int? Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct(source =>
                    new(source.ReadText()?.Measure(1)));

            builder.Map<Source, ResolveDestination>()
                .Resolve((source, _) =>
                    new(source.ReadText()?.Measure(2)));

            builder.Map<Source, MembersDestination>()
                .Members(source => new()
                {
                    Value = source.ReadText()?.Measure(3)
                });

            builder.Map<Source, ConstructUsingDestination>()
                .ConstructUsing(source =>
                    new ConstructUsingDestination(
                        source.ReadText()?.Measure(4)));

            builder.Map<Source, ResolveUsingDestination>()
                .ResolveUsing((source, _) =>
                    new ResolveUsingDestination(
                        source.ReadText()?.Measure(5)));

            builder.Map<Source, ConvertDestination>()
                .Convert(source =>
                    new ConvertDestination(
                        source!.ReadText()?.Measure(6)));

            builder.Map<Source, VoidDestination>()
                .ConstructUsing(source =>
                {
                    var values = new List<int>();
                    source.ReadText()?.AddLength(values);
                    return new VoidDestination(
                        values.Count == 0 ? (int?)null : values[0]);
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();

            AssertValue<ConstructDestination>(mapper, 4);
            AssertValue<ResolveDestination>(mapper, 5);
            AssertValue<MembersDestination>(mapper, 6);
            AssertValue<ConstructUsingDestination>(mapper, 7);
            AssertValue<ResolveUsingDestination>(mapper, 8);
            AssertValue<ConvertDestination>(mapper, 9);
            AssertValue<VoidDestination>(mapper, 3);

            var nullSource = new Source { Text = null };
            var nullResult =
                ((ITypeMapper<Source, ConstructDestination>)mapper)
                .Create(nullSource, default(MappingContext));

            if (nullResult.Value is not null || nullSource.Reads != 1)
            {
                throw new InvalidOperationException(
                    "Null propagation or receiver evaluation changed.");
            }
        }

        private static void AssertValue<TDestination>(
            TestMapper mapper,
            int expected)
            where TDestination : IValueDestination
        {
            var source = new Source { Text = "abc" };
            var destination =
                ((ITypeMapper<Source, TDestination>)mapper)
                .Create(source, default(MappingContext));

            if (destination.Value != expected || source.Reads != 1)
            {
                throw new InvalidOperationException(
                    "A conditional extension call changed binding, " +
                    "null propagation, or evaluation count.");
            }
        }
    }
}
