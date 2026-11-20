// Compiled integration scenario: TypeMapperStructuredConstructTests/ByConventionTests::Applies_written_rules_before_remaining_automatic_arguments
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;
using System.Collections.Generic;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByConvention_064618fb
{
    public sealed class Source
    {
        private readonly int _id;

        public Source(int id, string label)
        {
            _id = id;
            Label = label;
        }

        public int Id
        {
            get
            {
                TestMapper.Events.Add("id");
                return _id;
            }
        }

        public string Label { get; }
    }

    public sealed class Destination
    {
        public Destination(
            int id,
            string label = "default",
            params string[] tags)
        {
            Id = id;
            Label = label;
            Tags = tags;
        }

        public int Id { get; }

        public string Label { get; }

        public string[] Tags { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static List<string> Events { get; } = new();

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(
                    ByConvention(),
                    new()
                    {
                        label = Track(source.Label),
                        tags = Ignore(),
                        id = Auto()
                    }));

        private static string Track(string value)
        {
            Events.Add("label");
            return value;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var destination = mapper.Create(
                new Source(17, "configured"),
                default(MappingContext));

            if (destination.Id != 17 ||
                destination.Label != "configured" ||
                destination.Tags.Length != 0 ||
                TestMapper.Events.Count != 2 ||
                TestMapper.Events[0] != "label" ||
                TestMapper.Events[1] != "id")
            {
                throw new InvalidOperationException(
                    "ByConvention rules were not evaluated in written order.");
            }
        }
    }
}
