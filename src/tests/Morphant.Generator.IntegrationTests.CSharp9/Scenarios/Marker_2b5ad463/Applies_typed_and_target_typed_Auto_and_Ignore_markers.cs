// Compiled integration scenario: TypeMapperMemberTests/MarkerTests::Applies_typed_and_target_typed_Auto_and_Ignore_markers
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Marker_2b5ad463
{
    public sealed class Source
    {
        public int Automatic { get; init; }

        public int TypedAutomatic { get; init; }

        public string Ignored { get; init; } = string.Empty;

        public string TypedIgnored { get; init; } = string.Empty;

        public string Explicit { get; init; } = string.Empty;

        public string Convention { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination()
        {
            Ignored = "created-ignore";
            TypedIgnored = "created-typed-ignore";
        }

        public int Automatic { get; set; }

        public int TypedAutomatic { get; set; }

        public string Ignored { get; set; }

        public string TypedIgnored { get; set; }

        public string Explicit { get; set; } = string.Empty;

        public string Convention { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) => new()
                {
                    Automatic = Auto(),
                    TypedAutomatic = Auto<int>(),
                    Ignored = Ignore(),
                    TypedIgnored = Ignore<string>(),
                    Explicit = source.Explicit + "!"
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var source = new Source
            {
                Automatic = 1,
                TypedAutomatic = 2,
                Ignored = "source-ignore",
                TypedIgnored = "source-typed-ignore",
                Explicit = "explicit",
                Convention = "convention"
            };
            var created = mapper.Create(source, context);

            if (created.Automatic != 1 ||
                created.TypedAutomatic != 2 ||
                created.Ignored != "created-ignore" ||
                created.TypedIgnored != "created-typed-ignore" ||
                created.Explicit != "explicit!" ||
                created.Convention != "convention")
            {
                throw new InvalidOperationException(
                    "Create marker semantics were not preserved.");
            }

            var previous = new Destination
            {
                Ignored = "existing-ignore",
                TypedIgnored = "existing-typed-ignore"
            };
            var updated = mapper.Update(source, previous, context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Automatic != 1 ||
                updated.TypedAutomatic != 2 ||
                updated.Ignored != "existing-ignore" ||
                updated.TypedIgnored != "existing-typed-ignore" ||
                updated.Explicit != "explicit!" ||
                updated.Convention != "convention")
            {
                throw new InvalidOperationException(
                    "Update marker semantics were not preserved.");
            }
        }
    }
}
