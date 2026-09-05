using Morphant;

namespace Stage04Audit.Cases
{
#if NON_STATIC
    public class Models
#else
    public static class Models
#endif
    {
        public sealed class Source { public int Value { get; set; } }
        public sealed class Destination { public int Value { get; set; } }
    }

    public sealed class Source { public int Value { get; set; } }
    public sealed class Destination { public int Value { get; set; } }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Models.Source, Destination>();
            builder.Map<Source, Models.Destination>();
        }
    }

    internal static class Scenario
    {
        internal static void Run()
        {
            object mapper = new Mapper();
            Check.Equal("control-contract", true, mapper is ITypeMapper<Source, Destination>);
            Check.Equal("nested-source-contract", true, mapper is ITypeMapper<Models.Source, Destination>);
            Check.Equal("nested-destination-contract", true, mapper is ITypeMapper<Source, Models.Destination>);
            Check.Equal("ordinary-pair-remains-working", 11,
                ((ITypeMapper<Source, Destination>)mapper).Create(new Source { Value = 11 }).Value);
            if (mapper is ITypeMapper<Models.Source, Destination> nestedSource)
                Check.Equal("nested-source-mapping", 13, nestedSource.Create(new Models.Source { Value = 13 }).Value);
            if (mapper is ITypeMapper<Source, Models.Destination> nestedDestination)
                Check.Equal("nested-destination-mapping", 17, nestedDestination.Create(new Source { Value = 17 }).Value);
        }
    }
}
