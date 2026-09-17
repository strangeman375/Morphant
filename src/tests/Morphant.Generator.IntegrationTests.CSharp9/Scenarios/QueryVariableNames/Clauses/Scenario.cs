#nullable enable
#pragma warning disable CS1591
using System.Linq;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.QueryVariableNames.Clauses
{
    public sealed class Source
    {
        public int[] Values { get; set; } = new int[0];
    }
    public sealed class Destination
    {
        public Destination(string value)
        {
            InitialValue = value;
            Value = value;
        }
        public string InitialValue { get; }
        public string Value { get; set; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(input => new(string.Join(",",
                    from value in input.Values
                    let doubled = value * 2
                    join match in input.Values on value equals match into matches
                    from matched in matches
                    orderby doubled descending, matched
                    group doubled by matched into grouped
                    select grouped.Key + ":" + grouped.Sum())));
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)new Mapper();
            var source = new Source { Values = new[] { 1, 2, 2 } };
            var destination = mapper.Create(source, default);
            if (destination.InitialValue != "2:16,1:2" ||
                destination.Value != "2:16,1:2")
            {
                throw new global::System.InvalidOperationException(
                    "Query variable binding changed during Create.");
            }

            source.Values = new[] { 3 };
            var updated = mapper.Update(source, destination, default);
            if (!ReferenceEquals(updated, destination) ||
                destination.InitialValue != "2:16,1:2" ||
                destination.Value != "2:16,1:2")
            {
                throw new global::System.InvalidOperationException(
                    "Query variable binding or destination reuse changed during Update.");
            }
        }
    }
}
