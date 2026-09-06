#nullable enable
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MemberAliases
{
    public sealed class Source
    {
        public int Clone { get; set; }
        public int Clone_ { get; set; }
        public int EqualityContract { get; set; }
        public int DestinationMembers { get; set; }
    }

    public class BaseDestination { public int Clone_ { get; set; } }
    public sealed class Destination : BaseDestination
    {
        public int Clone { get; set; }
        public int EqualityContract { get; set; }
        public int DestinationMembers { get; set; }
    }

    [MorphantMapper]
    public sealed partial class ConventionMapper : TypeMapper<ConventionMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Destination);
    }

    [MorphantMapper]
    public sealed partial class ExplicitMapper : TypeMapper<ExplicitMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Destination)
                .Members(s => new global::Morphant.Generated.N_71837e14fa33c4bbdecc13b801c4539b.DestinationMembers()
                {
                    Clone__ = s.Clone + 1,
                    Clone_ = s.Clone_,
                    EqualityContract_ = Auto(),
                    DestinationMembers_ = Ignore()
                } with { Clone__ = s.Clone + 2 });
            builder.Map<Source, (int Clone, int Clone_)>()
                .Construct(s => new(s.Clone, s.Clone_))
                .Members(s => new() { Clone__ = s.Clone + 3, Clone_ = s.Clone_ + 4 });
        }
    }

    public sealed class ChildSource { public int Value { get; set; } }
    public sealed class ChildDestination { public int Value { get; set; } }
    public sealed class ReadOnlySource { public ChildSource Clone { get; set; } = new(); }
    public sealed class ReadOnlyDestination { public ChildDestination Clone { get; } = new(); }

    [MorphantMapper]
    public sealed partial class ReadOnlyMapper : TypeMapper<ReadOnlyMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>();
            builder.Map<ReadOnlySource, ReadOnlyDestination>()
                .Members(s =>
                {
                    var members = new global::Morphant.Generated.N_3abddc791276c41a1b2979f4a2333137.ReadOnlyDestinationMembers();
                    Update(s.Clone, members.Clone_);
                    return members;
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var source = new Source { Clone = 13, Clone_ = 17, EqualityContract = 19, DestinationMembers = 23 };
            ITypeMapper<Source, Destination> convention = new ConventionMapper();
            VerifyValues(convention.Create(source), 13, 17, 19, 23);
            var existing = new Destination();
            if (!ReferenceEquals(convention.Update(source, existing), existing))
                throw new InvalidOperationException("Convention Update replaced destination.");
            VerifyValues(existing, 13, 17, 19, 23);

            ITypeMapper<Source, Destination> explicitMapper = new ExplicitMapper();
            VerifyValues(explicitMapper.Create(source), 15, 17, 19, 0);
            existing.DestinationMembers = 31;
            VerifyValues(explicitMapper.Update(source, existing), 15, 17, 19, 31);
            ITypeMapper<Source, (int Clone, int Clone_)> tupleMapper = new ExplicitMapper();
            if (tupleMapper.Create(source) != (16, 21) ||
                tupleMapper.Update(source, (0, 0)) != (16, 21))
                throw new InvalidOperationException("Tuple aliases selected the wrong elements.");

            ITypeMapper<ReadOnlySource, ReadOnlyDestination> readOnlyMapper = new ReadOnlyMapper();
            var readOnly = new ReadOnlyDestination();
            var child = readOnly.Clone;
            var updated = readOnlyMapper.Update(new ReadOnlySource { Clone = new ChildSource { Value = 41 } }, readOnly);
            if (!ReferenceEquals(readOnly, updated) || !ReferenceEquals(child, updated.Clone) || child.Value != 41)
                throw new InvalidOperationException("Read-only alias lost its destination member.");
        }

        private static void VerifyValues(Destination result, int clone, int underscored, int equality, int planName)
        {
            if (result.Clone != clone || result.Clone_ != underscored ||
                result.EqualityContract != equality || result.DestinationMembers != planName)
                throw new InvalidOperationException("Member aliases or conventions lost values.");
        }
    }
}
