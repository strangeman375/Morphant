#nullable enable
#pragma warning disable CS1591
using System.Linq;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.QueryVariableNames.SiblingQueries
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
                .Construct(input => new(string.Join(",", from value in input.Values select value) + "/" + string.Join(",", from value in input.Values select value + 1)));
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)new Mapper();
            var source = new Source { Values = new[] { 1, 2, 2 } };
            var destination = mapper.Create(source, default);
            if (destination.InitialValue != "1,2,2/2,3,3" || destination.Value != "1,2,2/2,3,3")
                throw new global::System.InvalidOperationException("Create changed the written computation.");

            source.Values = new[] { 3 };
            var updated = mapper.Update(source, destination, default);
            if (!ReferenceEquals(updated, destination) ||
                updated.InitialValue != "1,2,2/2,3,3" || updated.Value != "1,2,2/2,3,3")
                throw new global::System.InvalidOperationException("Update changed binding or result identity.");
        }
    }
}
