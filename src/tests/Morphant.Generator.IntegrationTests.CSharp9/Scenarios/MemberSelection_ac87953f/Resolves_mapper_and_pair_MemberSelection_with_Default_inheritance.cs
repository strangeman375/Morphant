// Compiled integration scenario: TypeMapperMemberTests/MemberSelectionTests::Resolves_mapper_and_pair_MemberSelection_with_Default_inheritance
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MemberSelection_ac87953f
{
    public sealed class Source
    {
        public int Explicit { get; init; }

        public int Convention { get; init; }
    }

    public sealed class ExplicitDestination
    {
        public int Explicit { get; set; }

        public int Convention { get; set; } = -1;
    }

    public sealed class AutoDestination
    {
        public int Explicit { get; set; }

        public int Convention { get; set; } = -1;
    }

    public sealed class DefaultDestination
    {
        public int Explicit { get; set; }

        public int Convention { get; set; } = -1;
    }

    public sealed class LibraryDefaultDestination
    {
        public int Explicit { get; set; }

        public int Convention { get; set; } = -1;
    }

    [MorphantMapper]
    public partial class ExplicitMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder
                .MemberSelection(MemberSelection.Explicit)
                .Map<Source, ExplicitDestination>()
                .Members((source, _) => new()
                {
                    Explicit = source.Explicit + 1
                });

            builder.Map<Source, AutoDestination>()
                .MemberSelection(MemberSelection.Auto)
                .Members((source, _) => new()
                {
                    Explicit = source.Explicit + 2
                });

            builder.Map<Source, DefaultDestination>()
                .MemberSelection(MemberSelection.Default)
                .Members((source, _) => new()
                {
                    Explicit = source.Explicit + 3
                });
        }
    }

    [MorphantMapper]
    public partial class LibraryDefaultMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, LibraryDefaultDestination>()
                .Members((source, _) => new()
                {
                    Explicit = source.Explicit + 4
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var source = new Source
            {
                Explicit = 10,
                Convention = 20
            };
            var context = default(MappingContext);
            var explicitMapper = new ExplicitMapper();
            var explicitResult =
                ((ITypeMapper<Source, ExplicitDestination>)
                    explicitMapper).Create(source, context);
            var autoResult =
                ((ITypeMapper<Source, AutoDestination>)
                    explicitMapper).Create(source, context);
            var defaultResult =
                ((ITypeMapper<Source, DefaultDestination>)
                    explicitMapper).Create(source, context);
            var libraryDefaultResult =
                ((ITypeMapper<Source, LibraryDefaultDestination>)
                    new LibraryDefaultMapper()).Create(source, context);

            if (explicitResult.Explicit != 11 ||
                explicitResult.Convention != -1 ||
                autoResult.Explicit != 12 ||
                autoResult.Convention != 20 ||
                defaultResult.Explicit != 13 ||
                defaultResult.Convention != -1 ||
                libraryDefaultResult.Explicit != 14 ||
                libraryDefaultResult.Convention != 20)
            {
                throw new InvalidOperationException(
                    "MemberSelection precedence was not preserved.");
            }
        }
    }
}
