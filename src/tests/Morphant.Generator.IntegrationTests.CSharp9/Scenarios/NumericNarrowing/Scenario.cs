#nullable enable
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NumericNarrowing
{
    public sealed class Source
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
    }
    public sealed class Destination
    {
        public int Id { get; set; } = 19;
        public string Name { get; set; } = "initial";
    }
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>();
    }
    public static class Scenario
    {
        public static void Verify(string operation)
        {
            ITypeMapper<Source, Destination> mapper = new TestMapper();
            var source = new Source { Id = 42, Name = "mapped" };
            var existing = new Destination { Id = 73 };
            var result = operation switch
            {
                "Create" => mapper.Create(source),
                "UpdateNull" => mapper.Update(source, null),
                "UpdateExisting" => mapper.Update(source, existing),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            if (result.Id != (operation == "UpdateExisting" ? 73 : 19) || result.Name != "mapped" ||
                (operation == "UpdateExisting" && !ReferenceEquals(result, existing)))
            {
                throw new InvalidOperationException("Narrowing must leave Id untouched while compatible members map normally.");
            }
        }
    }
}
