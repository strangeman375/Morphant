#nullable enable
#pragma warning disable CS1591
using System.Linq;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.QueryVariableNames.MultilineRaw
{
    public sealed class Source
    {
        public int[] Values { get; set; } = new int[0];
    }
    public sealed class Destination
    {
        public Destination(string value) => Value = value;
        public string Value { get; set; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(input => { var context = input.Values[0]; return new($$"""
value {{context,3:000}}
{{string.Join(",", from value in input.Values select value)}}
tail
"""); });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)new Mapper();
            var source = new Source { Values = new[] { 1, 2, 2 } };
            var destination = mapper.Create(source, default);
            var expected = $@"value {1,3:000}
1,2,2
tail";
            if (destination.Value != expected)
                throw new global::System.InvalidOperationException("Interpolation changed binding or literal line endings.");
            source.Values = new[] { 3 };
            var updated = mapper.Update(source, destination, default);
            if (!ReferenceEquals(updated, destination) || updated.Value != expected)
                throw new global::System.InvalidOperationException("Update changed the existing value.");
        }
    }
}
