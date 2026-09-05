using Morphant;
using Morphant.Generated.Types.A_Audit_002EStage04.N_Stage04Audit.N_Cases.Plans;

namespace Stage04Audit.Cases
{
    public sealed class Source { public int Id { get; set; } }
    public sealed class Destination
    {
        public Destination(int id) => Id = id;
        public int Id { get; }
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
#if NESTED_TRYGET
                .Resolve((source, previous) =>
                {
                    if (previous.TryGetValue(out var destination))
                    {
                        if (destination.Id == source.Id)
                            return previous;
                    }
                    return new(source.Id);
                });
#elif BLOCK_BODY || HAS_VALUE_BLOCK
                .Resolve((source, previous) =>
                {
#if HAS_VALUE_BLOCK
                    if (previous.HasValue && previous.Value.Id == source.Id)
#else
                    if (previous.TryGetValue(out var destination) && destination.Id == source.Id)
#endif
                        return previous;
                    return new(source.Id);
                });
#else
                .Resolve((source, previous) =>
#if HAS_VALUE || HAS_VALUE_TARGET_TYPED
                    previous.HasValue && previous.Value.Id == source.Id
#else
                    previous.TryGetValue(out var destination) && destination.Id == source.Id
#endif
                        ? previous
#if EXPLICIT_NAME || HAS_VALUE
                        : new DestinationConstruction(source.Id));
#else
                        : new(source.Id));
#endif
#endif
    }

    internal static class Scenario
    {
        internal static void Run()
        {
            var mapper = (ITypeMapper<Source, Destination>)new Mapper();
            var source = new Source { Id = 11 };
            Check.Equal("resolve-conditional-create", 11, mapper.Create(source).Id);
            var previous = new Destination(11);
            Check.Equal("resolve-conditional-reuse", true,
                ReferenceEquals(previous, mapper.Update(source, previous)));
            Check.Equal("resolve-conditional-null-update", 11, mapper.Update(source, null).Id);
            var different = new Destination(19);
            var replacement = mapper.Update(source, different);
            Check.Equal("resolve-conditional-replacement", true,
                !ReferenceEquals(different, replacement) && replacement.Id == 11);
        }
    }
}
