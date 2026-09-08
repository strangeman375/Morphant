#nullable enable
using System;
using Morphant;
using Tuple8 = System.Tuple<int, int, int, int, int, int, int, System.Tuple<int>>;
using Tuple15 = System.Tuple<int, int, int, int, int, int, int,
    System.Tuple<int, int, int, int, int, int, int, System.Tuple<int>>>;
using Value15 = System.ValueTuple<int, int, int, int, int, int, int,
    System.ValueTuple<int, int, int, int, int, int, int, System.ValueTuple<int>>>;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.LongTupleRules
{
    public sealed class Source { public int Value { get; set; } }
    public sealed class Snapshot
    {
        public int First { get; set; }
        public int Middle { get; set; }
        public int Last { get; set; }
    }

    [MorphantMapper]
    public partial class TailIgnoreMapper : TypeMapper<TailIgnoreMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Tuple8>().Members(source => new()
            {
                Item1 = source.Value,
                Item2 = 2, Item3 = 3, Item4 = 4, Item5 = 5, Item6 = 6, Item7 = 7,
                Item8 = Ignore()
            });
            builder.Map<Source, Tuple15>().Members(source => new()
            {
                Item1 = source.Value,
                Item2 = 2, Item3 = 3, Item4 = 4, Item5 = 5, Item6 = 6, Item7 = 7,
                Item8 = source.Value + 7,
                Item9 = 9, Item10 = 10, Item11 = 11, Item12 = 12, Item13 = 13, Item14 = 14,
                Item15 = Ignore()
            });
        }
    }

    [MorphantMapper]
    public partial class HeadIgnoreMapper : TypeMapper<HeadIgnoreMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Tuple8>().Members(source => new()
            {
                Item1 = Ignore(),
                Item2 = 2, Item3 = 3, Item4 = 4, Item5 = 5, Item6 = 6, Item7 = 7,
                Item8 = source.Value
            });
            builder.Map<Source, Tuple15>().Members(source => new()
            {
                Item1 = Ignore(),
                Item2 = 2, Item3 = 3, Item4 = 4, Item5 = 5, Item6 = 6, Item7 = 7,
                Item8 = Ignore(),
                Item9 = 9, Item10 = 10, Item11 = 11, Item12 = 12, Item13 = 13, Item14 = 14,
                Item15 = source.Value
            });
        }
    }

    [MorphantMapper]
    public partial class ValueMapper : TypeMapper<ValueMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Value15>()
                .Construct(source => new(source.Value, 2, 3, 4, 5, 6, 7, -8, 9, 10, 11, 12, 13, 14, -15))
                .Members(source => new() { Item8 = source.Value + 7, Item15 = source.Value + 14 })
                .MemberSelection(MemberSelection.Explicit);
            builder.Map<Source, Value15?>()
                .Construct(source => new(source.Value, 2, 3, 4, 5, 6, 7, -8, 9, 10, 11, 12, 13, 14, -15))
                .Members(source => new() { Item8 = source.Value + 7, Item15 = source.Value + 14 })
                .MemberSelection(MemberSelection.Explicit);
            builder.Map<Value15, Snapshot>().Members(source => new()
            {
                First = source.Item1,
                Middle = source.Rest.Item1,
                Last = source.Rest.Rest.Item1
            });
        }
    }

    public static class Scenario
    {
        public static void VerifyIgnoredHead(int arity, bool update)
        {
            var concrete = new HeadIgnoreMapper();
            var source = new Source { Value = 31 };
            if (arity == 8)
            {
                var mapper = (ITypeMapper<Source, Tuple8>)concrete;
                var result = update ? mapper.Update(source, null) : mapper.Create(source);
                if (result.Item1 != 0 || result.Rest.Item1 != 31)
                    throw new InvalidOperationException("Item8 must not replace the Ignore rule for Item1.");
            }
            else if (arity == 15)
            {
                var mapper = (ITypeMapper<Source, Tuple15>)concrete;
                var result = update ? mapper.Update(source, null) : mapper.Create(source);
                if (result.Item1 != 0 || result.Rest.Item1 != 0 || result.Rest.Rest.Item1 != 31)
                    throw new InvalidOperationException("Item15 must not replace the Ignore rules for Item1 and Item8.");
            }
            else throw new ArgumentOutOfRangeException(nameof(arity));
        }

        public static void VerifyValueTuple(bool update)
        {
            ITypeMapper<Source, Value15> mapper = new ValueMapper();
            var source = new Source { Value = 31 };
            Value15 previous = (101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115);
            var result = update ? mapper.Update(source, previous) : mapper.Create(source);
            if (result.Item1 != (update ? 101 : 31) || result.Rest.Item1 != 38 ||
                result.Rest.Item7 != (update ? 114 : 14) || result.Rest.Rest.Item1 != 45 ||
                previous.Rest.Item1 != 108 || previous.Rest.Rest.Item1 != 115)
                throw new InvalidOperationException("Long ValueTuple rules must update only selected logical fields in the returned value.");
        }

        public static void VerifyNullableValueTuple(bool present)
        {
            ITypeMapper<Source, Value15?> mapper = new ValueMapper();
            Value15? previous = present ? default(Value15) : null;
            var result = mapper.Update(new Source { Value = 31 }, previous);
            if (!result.HasValue || result.Value.Item1 != (present ? 0 : 31) ||
                result.Value.Rest.Item1 != 38 || result.Value.Rest.Rest.Item1 != 45)
                throw new InvalidOperationException("A null long tuple requires construction; a present default tuple requires field updates.");
        }

        public static void VerifyPhysicalSourcePaths(bool update)
        {
            ITypeMapper<Value15, Snapshot> mapper = new ValueMapper();
            Value15 source = (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);
            var previous = new Snapshot();
            var result = update ? mapper.Update(source, previous) : mapper.Create(source);
            if (result.First != 1 || result.Middle != 8 || result.Last != 15 ||
                (update && !ReferenceEquals(result, previous)))
                throw new InvalidOperationException("Source paths through repeated Rest must read the corresponding logical element.");
        }

        public static void VerifyIgnoredTail(int arity, bool update)
        {
            var concrete = new TailIgnoreMapper();
            var source = new Source { Value = 31 };
            if (arity == 8)
            {
                var mapper = (ITypeMapper<Source, Tuple8>)concrete;
                var result = update ? mapper.Update(source, null) : mapper.Create(source);
                if (result.Item1 != 31 || result.Item7 != 7 || result.Rest.Item1 != 0)
                    throw new InvalidOperationException(
                        $"Ignoring Item8 changed another element: Item1={result.Item1}, Item7={result.Item7}, Item8={result.Rest.Item1}.");
            }
            else if (arity == 15)
            {
                var mapper = (ITypeMapper<Source, Tuple15>)concrete;
                var result = update ? mapper.Update(source, null) : mapper.Create(source);
                if (result.Item1 != 31 || result.Rest.Item1 != 38 || result.Rest.Item7 != 14 || result.Rest.Rest.Item1 != 0)
                    throw new InvalidOperationException(
                        $"Ignoring Item15 changed another element: Item1={result.Item1}, Item8={result.Rest.Item1}, Item15={result.Rest.Rest.Item1}.");
            }
            else throw new ArgumentOutOfRangeException(nameof(arity));
        }
    }
}
