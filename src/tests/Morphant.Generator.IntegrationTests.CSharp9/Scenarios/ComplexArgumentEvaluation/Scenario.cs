#nullable enable
#pragma warning disable CS1591
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ComplexArgumentEvaluation
{
    public sealed class Source { public int Value { get; set; } }

    public sealed class Destination
    {
        public Destination(Source original, int value) { Original = original; Value = value; }
        public Source Original { get; }
        public int Value { get; }
    }

    [MorphantMapper]
    public partial class RefMapper : TypeMapper<RefMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(source,
                    ReplaceSourceForConstructorValueSelection(ref source)
                        ? source.Value + 10
                        : source.Value + 20));

        private static bool ReplaceSourceForConstructorValueSelection(ref Source source)
        {
            source = new Source { Value = 100 };
            return true;
        }
    }

    [MorphantMapper]
    public partial class ClosureMapper : TypeMapper<ClosureMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source =>
                {
                    Func<bool> replaceSourceForConstructorValueSelection = () =>
                    {
                        source = new Source { Value = 100 };
                        return true;
                    };
                    return new(source,
                        replaceSourceForConstructorValueSelection()
                            ? ReadPreferredConstructorValue(source)
                            : ReadFallbackConstructorValue(source));
                });

        private static int ReadPreferredConstructorValue(Source source) => source.Value + 10;
        private static int ReadFallbackConstructorValue(Source source) => source.Value + 20;
    }

    public struct ValueSource
    {
        public int Value { get; set; }
        public bool AdvanceAndSelectConstructorValue() { Value++; return true; }
        public int ReadPreferredConstructorValue() => Value + 10;
        public int ReadFallbackConstructorValue() => Value + 20;
    }

    public sealed class ValueDestination
    {
        public ValueDestination(ValueSource original, int value) { Original = original; Value = value; }
        public ValueSource Original { get; }
        public int Value { get; }
    }

    [MorphantMapper]
    public partial class ValueMapper : TypeMapper<ValueMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<ValueSource, ValueDestination>()
                .Construct(source => new(source,
                    source.AdvanceAndSelectConstructorValue()
                        ? source.ReadPreferredConstructorValue()
                        : source.ReadFallbackConstructorValue()));
    }

    public static class Scenario
    {
        public static void VerifyReference(bool closure)
        {
            ITypeMapper<Source, Destination> mapper = closure ? new ClosureMapper() : new RefMapper();
            var source = new Source { Value = 5 };
            var created = mapper.Create(source);
            var replaced = mapper.Update(source, null);
            if (!ReferenceEquals(created.Original, source) || !ReferenceEquals(replaced.Original, source) ||
                created.Value != 110 || replaced.Value != 110 || source.Value != 5)
                throw new InvalidOperationException("The earlier argument must keep the original source reference.");
        }

        public static void VerifyValue()
        {
            ITypeMapper<ValueSource, ValueDestination> mapper = new ValueMapper();
            var source = new ValueSource { Value = 5 };
            var created = mapper.Create(source);
            var replaced = mapper.Update(source, null);
            if (created.Original.Value != 5 || replaced.Original.Value != 5 ||
                created.Value != 16 || replaced.Value != 16 || source.Value != 5)
                throw new InvalidOperationException("The earlier argument must copy the struct before its mutation.");
        }
    }
}
