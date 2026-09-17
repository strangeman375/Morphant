#nullable enable
#pragma warning disable CS1591
using System.Linq;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.QueryVariableNames.Nested
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
                    let nested = (from other in input.Values
                                  where other > value
                                  select other).Sum()
                    select value + nested into total
                    select total)))
                .Members(input => new()
                {
                    Value = string.Join(",", input.Values.Select(value =>
                        (from item in input.Values select item + value).Sum()))
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)new Mapper();
            var source = new Source { Values = new[] { 1, 2, 2 } };
            var destination = mapper.Create(source, default);
            if (destination.InitialValue != "5,2,2" ||
                destination.Value != "8,11,11")
            {
                throw new global::System.InvalidOperationException(
                    "Query variable binding changed during Create.");
            }

            source.Values = new[] { 3 };
            var updated = mapper.Update(source, destination, default);
            if (!ReferenceEquals(updated, destination) ||
                destination.InitialValue != "5,2,2" ||
                destination.Value != "6")
            {
                throw new global::System.InvalidOperationException(
                    "Query variable binding or destination reuse changed during Update.");
            }
        }
    }
}
