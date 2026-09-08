#nullable enable
using System;
using Morphant;
using Tuple8 = System.Tuple<int, int, int, int, int, int, int, System.Tuple<int>>;
using Tuple15 = System.Tuple<int, int, int, int, int, int, int,
    System.Tuple<int, int, int, int, int, int, int, System.Tuple<int>>>;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.LongTupleRules
{
    public sealed class Source { public int Value { get; set; } }

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

    public static class Scenario
    {
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
