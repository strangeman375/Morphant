// Compiled integration scenario: TypeMapperMemberTests/MemberSelectionTests::Resolves_included_current_and_connected_mapper_precedence
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MemberSelectionHierarchy_9d7a0310
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public abstract class Destination
    {
        public int Value { get; set; } = -1;
    }

    public sealed class IncludedDestination : Destination { }

    public sealed class LocalDestination : Destination { }

    public sealed class BaseRootDestination : Destination { }

    public sealed class CurrentRootDestination : Destination { }

    public abstract class IncludedBaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, IncludedDestination>()
                .MemberSelection(MemberSelection.Auto);
            builder.Map<Source, LocalDestination>()
                .MemberSelection(MemberSelection.Auto);
        }
    }

    [MorphantMapper]
    public partial class IncludedMapper : IncludedBaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.MemberSelection(MemberSelection.Explicit);

            builder.Map<Source, IncludedDestination>()
                .IncludeBase<Source, IncludedDestination>();
            builder.Map<Source, LocalDestination>()
                .IncludeBase<Source, LocalDestination>()
                .MemberSelection(MemberSelection.Explicit);
        }
    }

    public abstract class RootBaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MemberSelection(MemberSelection.Auto);
            builder.MemberSelection(MemberSelection.Explicit);
        }
    }

    [MorphantMapper]
    public partial class BaseRootMapper : RootBaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, BaseRootDestination>();
        }
    }

    [MorphantMapper]
    public partial class CurrentRootMapper : RootBaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.MemberSelection(MemberSelection.Auto);
            builder.Map<Source, CurrentRootDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var source = new Source { Value = 37 };
            var includedMapper = new IncludedMapper();
            var included = ((ITypeMapper<Source, IncludedDestination>)
                includedMapper).Create(source);
            var local = ((ITypeMapper<Source, LocalDestination>)
                includedMapper).Create(source);
            var fromBase = ((ITypeMapper<Source, BaseRootDestination>)
                new BaseRootMapper()).Create(source);
            var current = ((ITypeMapper<Source, CurrentRootDestination>)
                new CurrentRootMapper()).Create(source);

            if (included.Value != 37 ||
                local.Value != -1 ||
                fromBase.Value != -1 ||
                current.Value != 37)
            {
                throw new InvalidOperationException(
                    "MemberSelection did not follow mapping, included " +
                    "mapping, current mapper, and connected base precedence.");
            }
        }
    }
}
