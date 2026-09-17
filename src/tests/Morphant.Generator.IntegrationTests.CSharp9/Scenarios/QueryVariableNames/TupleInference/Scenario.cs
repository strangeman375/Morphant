#nullable enable
#pragma warning disable CS1591
using System.Linq;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.QueryVariableNames.TupleInference
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
                    from context in input.Values
                    select (context, context) into pair
                    select pair.Item1 + pair.Item2)))
                .Members(input => new()
                {
                    Value = string.Join(",", from Item1 in input.Values
                        select (0, Item1) into pair
                        select pair.Item2)
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)new Mapper();
            var source = new Source { Values = new[] { 1, 2, 2 } };
            var destination = mapper.Create(source, default);
            if (destination.InitialValue != "2,4,4" ||
                destination.Value != "1,2,2")
            {
                throw new global::System.InvalidOperationException(
                    "Query variable binding changed during Create.");
            }

            source.Values = new[] { 3 };
            var updated = mapper.Update(source, destination, default);
            if (!ReferenceEquals(updated, destination) ||
                destination.InitialValue != "2,4,4" ||
                destination.Value != "3")
            {
                throw new global::System.InvalidOperationException(
                    "Query variable binding or destination reuse changed during Update.");
            }
        }
    }
}
