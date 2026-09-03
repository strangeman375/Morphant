// Compiled integration scenario: TypeMapperMemberTests/ExplicitRulesTests::Applies_explicit_rules_to_created_and_existing_results
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExplicitRules_3e30cac9
{
    public sealed class Source
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public int Count { get; init; }

        public int Initial { get; init; }

        public string Required { get; init; } = string.Empty;

        public string Convention { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination(int id)
        {
            Id = id;
        }

        public int Id { get; set; }

        public string Name { get; set; } = "initial";

        public int Count;

        public int Initial { get; init; }

        public required string Required { get; set; }

        public string Convention { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int ValueCount { get; private set; }

        public static int InitialCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(id: source.Id))
                .Members((source, previous) => new()
                {
                    Count = MapCount(source.Count),
                    Id = source.Id + 100,
                    Name = previous.HasValue
                        ? previous.Value.Name + "!"
                        : source.Name,
                    Initial = MapInitial(source.Initial),
                    Required = source.Required
                });

        private static int MapCount(int value)
        {
            ValueCount++;
            return value * 2;
        }

        private static int MapInitial(int value)
        {
            InitialCount++;
            return value;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var created = mapper.Create(
                new Source
                {
                    Id = 1,
                    Name = "created",
                    Count = 3,
                    Initial = 4,
                    Required = "required-create",
                    Convention = "auto-create"
                },
                context);

            if (created.Id != 101 ||
                created.Name != "created" ||
                created.Count != 6 ||
                created.Initial != 4 ||
                created.Required != "required-create" ||
                created.Convention != "auto-create")
            {
                throw new InvalidOperationException(
                    "Create did not apply the effective member plan.");
            }

            var previous = new Destination(9)
            {
                Name = "previous",
                Count = -1,
                Initial = 41,
                Required = "required-previous",
                Convention = "old"
            };
            var updated = mapper.Update(
                new Source
                {
                    Id = 2,
                    Name = "ignored",
                    Count = 5,
                    Initial = 7,
                    Required = "required-update",
                    Convention = "auto-update"
                },
                previous,
                context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Id != 102 ||
                updated.Name != "previous!" ||
                updated.Count != 10 ||
                updated.Initial != 41 ||
                updated.Required != "required-update" ||
                updated.Convention != "auto-update" ||
                TestMapper.ValueCount != 2 ||
                TestMapper.InitialCount != 1)
            {
                throw new InvalidOperationException(
                    "Update did not apply the effective member plan.");
            }
        }
    }
}
