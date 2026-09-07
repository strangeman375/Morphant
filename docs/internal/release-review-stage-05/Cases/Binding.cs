using Morphant;

namespace Stage05Audit.Cases
{
    public sealed class Source { public int Value { get; set; } }
    public sealed class Destination
    {
        public Destination(int value) { Value = value; }
        public int Value { get; set; }
    }

    public static class Foreign
    {
        public static int Auto(int value) => value + 1;
        public static int Ignore(int value) => value + 2;
        public static int Map(int value) => value + 3;
        public static int Value(int value) => value + 4;
        public static MappingBuilder<Mapper, Source, Destination> Members(
            this MappingBuilder<Mapper, Source, Destination> builder,
            string value) => builder;
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
#if CAPTURE
            int offset = 5;
            builder.Map<Source, Destination>()
                .Members(source => new() { Value = source.Value + offset });
#elif RUNTIME_CAPTURE
            int offset = 5;
            builder.Map<Source, Destination>()
                .Convert(source => new(source.Value + offset));
#elif LOOP
            builder.Map<Source, Destination>().Members(source =>
            {
                for (int index = 0; index < source.Value; index++) { }
                return new() { Value = source.Value };
            });
#elif MUTATION
            builder.Map<Source, Destination>().Members((source, previous, result) =>
            {
                result.Value++;
                return new() { Value = source.Value };
            });
#elif FOREIGN_CALLBACK
            builder.Map<Source, Destination>().Members("foreign");
#else
            builder.Map<Source, Destination>().Members(source => new()
            {
                Value = Foreign.Value(Foreign.Map(Foreign.Ignore(Foreign.Auto(source.Value))))
            });
#endif
        }
    }

    public static class Scenario
    {
        public static void Run()
        {
            var mapper = (ITypeMapper<Source, Destination>)new Mapper();
            Check.Equal("foreign marker names are ordinary methods", 17,
                mapper.Create(new Source { Value = 7 }).Value);
        }
    }
}
