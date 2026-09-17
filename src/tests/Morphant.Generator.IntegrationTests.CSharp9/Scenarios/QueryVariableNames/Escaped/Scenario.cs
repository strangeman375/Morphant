#nullable enable
#pragma warning disable CS1591
using System.Linq;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.QueryVariableNames.Escaped
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
                .Resolve((input, previous) =>
                {
                    if (previous.HasValue) return previous.Value;
                    return new(string.Join(",", from @class in input.Values
                        let @select = @class + 1
                        select nameof(@class) + ":" + @select));
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)new Mapper();
            var source = new Source { Values = new[] { 1, 2, 2 } };
            var destination = mapper.Create(source, default);
            if (destination.InitialValue != "class:2,class:3,class:3" ||
                destination.Value != "class:2,class:3,class:3")
            {
                throw new global::System.InvalidOperationException(
                    "Query variable binding changed during Create.");
            }

            source.Values = new[] { 3 };
            var updated = mapper.Update(source, destination, default);
            if (!ReferenceEquals(updated, destination) ||
                destination.InitialValue != "class:2,class:3,class:3" ||
                destination.Value != "class:2,class:3,class:3")
            {
                throw new global::System.InvalidOperationException(
                    "Query variable binding or destination reuse changed during Update.");
            }
        }
    }
}
