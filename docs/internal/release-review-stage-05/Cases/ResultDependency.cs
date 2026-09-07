// Characterizes the existing result-dependent behavior for the pending design decision.
using Morphant;

namespace Stage05Audit.Cases
{
    public sealed class Source { public int Value { get; set; } = 7; }
    public sealed class Destination
    {
        private int value;
        public Destination(int value) { ConstructorValue = value; Value = value; }
        public int ConstructorValue { get; }
        public int Writes { get; private set; }
        public int Value { get => value; set { this.value = value; Writes++; } }
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((_, _, result) => new() { Value = result.Value + 10 });
    }

    public static class Scenario
    {
        public static void Run()
        {
            ITypeMapper<Source, Destination> mapper = new Mapper();
            var result = mapper.Create(new Source());
            Check.Equal("observed constructor argument", 7, result.ConstructorValue);
            Check.Equal("observed result-dependent member value", 17, result.Value);
            Check.Equal("observed assignments", 2, result.Writes);
        }
    }
}
