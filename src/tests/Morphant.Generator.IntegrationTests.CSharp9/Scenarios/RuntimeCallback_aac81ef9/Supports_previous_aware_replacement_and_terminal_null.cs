// Compiled integration scenario: TypeMapperCreationResultTests/RuntimeCallbackFormsTests::Supports_previous_aware_replacement_and_terminal_null
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimeCallback_aac81ef9
{
    public sealed class Source
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public bool Reuse { get; init; }

        public bool ReturnNull { get; init; }
    }

    public sealed class Destination
    {
        private string _name = string.Empty;

        public Destination(int id, bool replacedPrevious)
        {
            Id = id;
            ReplacedPrevious = replacedPrevious;
        }

        public static int AssignmentCount { get; private set; }

        public int Id { get; }

        public bool ReplacedPrevious { get; }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                AssignmentCount++;
            }
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int FactoryCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ResolveUsing((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse)
                    {
                        return previous.Value;
                    }

                    return Create(
                        source.Id,
                        previous.HasValue,
                        source.ReturnNull);
                });

        private static Destination Create(
            int id,
            bool replacedPrevious,
            bool returnNull)
        {
            FactoryCount++;

            return returnNull
                ? null!
                : new Destination(id, replacedPrevious);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var context = default(MappingContext);
            var created = mapper.Create(
                new Source { Id = 1, Name = "created" },
                context);
            var previous = new Destination(7, false);
            var reused = mapper.Update(
                new Source
                {
                    Id = 2,
                    Name = "reused",
                    Reuse = true
                },
                previous,
                context);
            var replaced = mapper.Update(
                new Source { Id = 3, Name = "replaced" },
                previous,
                context);
            var nullResult = mapper.Create(
                new Source
                {
                    Id = 4,
                    Name = "must not assign",
                    ReturnNull = true
                },
                context);

            if (created.Id != 1 || created.ReplacedPrevious ||
                created.Name != "created" ||
                !ReferenceEquals(previous, reused) ||
                reused.Name != "reused" ||
                ReferenceEquals(previous, replaced) ||
                replaced.Id != 3 || !replaced.ReplacedPrevious ||
                replaced.Name != "replaced" ||
                nullResult is not null ||
                TestMapper.FactoryCount != 3 ||
                Destination.AssignmentCount != 3)
            {
                throw new InvalidOperationException(
                    "Runtime resolution or terminal null semantics changed.");
            }
        }
    }
}
