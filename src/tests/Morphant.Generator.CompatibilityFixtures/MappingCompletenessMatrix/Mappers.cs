#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace MappingCompletenessMatrix
{
    public sealed class AssemblySource
    {
        public int AssemblyUsed { get; init; }

        public int AssemblyUnused { get; init; }
    }

    public sealed class AssemblyDestination
    {
        public int AssemblyUsed { get; set; }

        public int AssemblyUnmapped { get; set; }
    }

    public sealed class RootSource
    {
        public int RootUsed { get; init; }

        public int RootUnused { get; init; }
    }

    public sealed class RootDestination
    {
        public int RootUsed { get; set; }

        public int RootUnmapped { get; set; }
    }

    public sealed class PairSource
    {
        public int PairUsed { get; init; }

        public int PairUnused { get; init; }
    }

    public sealed class PairDestination
    {
        public int PairUsed { get; set; }

        public int PairUnmapped { get; set; }
    }

    public sealed class NoneSource
    {
        public int NoneUsed { get; init; }

        public int NoneUnused { get; init; }
    }

    public sealed class NoneDestination
    {
        public int NoneUsed { get; set; }

        public int NoneUnmapped { get; set; }
    }

    public sealed class DefaultSource
    {
        public int DefaultUsed { get; init; }

        public int DefaultUnused { get; init; }
    }

    public sealed class DefaultDestination
    {
        public int DefaultUsed { get; set; }

        public int DefaultUnmapped { get; set; }
    }

    public sealed class IncludedSource
    {
        public int IncludedUsed { get; init; }

        public int IncludedUnused { get; init; }
    }

    public sealed class IncludedDestination
    {
        public int IncludedUsed { get; set; }

        public int IncludedUnmapped { get; set; }
    }

    public sealed class ConnectedSource
    {
        public int ConnectedUsed { get; init; }

        public int ConnectedUnused { get; init; }
    }

    public sealed class ConnectedDestination
    {
        public int ConnectedUsed { get; set; }

        public int BaseUnmapped { get; set; }
    }

    [MorphantMapper]
    public partial class AssemblyMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<AssemblySource, AssemblyDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    AssemblyUsed = source.AssemblyUsed
                });
    }

    [MorphantMapper]
    public partial class ValidationMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Destination);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Source);

            builder.Map<RootSource, RootDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    RootUsed = source.RootUsed
                });

            builder.Map<PairSource, PairDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Destination)
                .Members(source => new()
                {
                    PairUsed = source.PairUsed
                });

            builder.Map<NoneSource, NoneDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.None)
                .Members(source => new()
                {
                    NoneUsed = source.NoneUsed
                });

            builder.Map<DefaultSource, DefaultDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Default)
                .Members(source => new()
                {
                    DefaultUsed = source.DefaultUsed
                });
        }
    }

    public abstract class IncludedBaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IncludedSource, IncludedDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Destination)
                .Members(source => new()
                {
                    IncludedUsed = source.IncludedUsed
                });
    }

    [MorphantMapper]
    public partial class IncludedMapper : IncludedBaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Source);

            builder.Map<IncludedSource, IncludedDestination>()
                .IncludeBase<IncludedSource, IncludedDestination>();
        }
    }

    public abstract class ConnectedBaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Destination);
    }

    [MorphantMapper]
    public partial class ConnectedMapper : ConnectedBaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<ConnectedSource, ConnectedDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    ConnectedUsed = source.ConnectedUsed
                });
        }
    }
}
