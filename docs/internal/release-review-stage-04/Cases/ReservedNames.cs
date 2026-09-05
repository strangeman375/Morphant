using Morphant;
#if RENAME_DESTINATION
using SelectedDestination = Stage04Audit.Cases.RenamedDestination;
#else
using SelectedDestination = Stage04Audit.Cases.Destination;
#endif

namespace Stage04Audit.Cases
{
    public sealed class Source
    {
        public int Value { get; set; }
        public int Clone { get; set; }
        public int EqualityContract { get; set; }
        public int DestinationMembers { get; set; }
    }

#if RENAME_DESTINATION
    public sealed class RenamedDestination
#else
    public sealed class Destination
#endif
    {
        public int Value { get; set; }
        public int Clone { get; set; }
        public int EqualityContract { get; set; }
        public int DestinationMembers { get; set; }
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, SelectedDestination>()
#if STRICT
            .UnmappedMemberValidation(UnmappedMemberValidation.Destination)
#endif
#if EXPLICIT_MEMBERS
            .Members(source => new()
            {
                Clone = source.Clone,
                EqualityContract = source.EqualityContract,
                DestinationMembers = source.DestinationMembers
            })
#endif
            ;
    }

    internal static class Scenario
    {
        internal static void Run()
        {
            var mapper = (ITypeMapper<Source, SelectedDestination>)new Mapper();
            var source = new Source { Value = 11, Clone = 13, EqualityContract = 17, DestinationMembers = 19 };
            var created = mapper.Create(source);
            var updated = mapper.Update(source, new SelectedDestination());
            Check.Equal("ordinary-property-create", 11, created.Value);
            Check.Equal("clone-property-create", 13, created.Clone);
            Check.Equal("equality-contract-property-create", 17, created.EqualityContract);
            Check.Equal("plan-name-property-create", 19, created.DestinationMembers);
            Check.Equal("ordinary-property-update", 11, updated.Value);
            Check.Equal("clone-property-update", 13, updated.Clone);
            Check.Equal("equality-contract-property-update", 17, updated.EqualityContract);
            Check.Equal("plan-name-property-update", 19, updated.DestinationMembers);
        }
    }
}
