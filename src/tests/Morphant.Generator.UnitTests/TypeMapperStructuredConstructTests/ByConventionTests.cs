using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperStructuredConstructTests;

[TestFixture]
internal sealed class ByConventionTests
{
    [Test]
    public void Selects_the_unambiguous_constructor_without_overrides()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Id { get; init; }
    }

    public sealed class Destination
    {
        public Destination()
        {
            Kind = "parameterless";
        }

        public Destination(int id)
        {
            Kind = "parameterized";
            Id = id;
        }

        public string Kind { get; }

        public int Id { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(_ => new(ByConvention()));
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source { Id = 17 };
            var context = default(MappingContext);
            var created = mapper.Map(source, context);
            var previous = new Destination(31);
            var updated = mapper.Map(source, previous, context);

            if (created.Kind != "parameterized" ||
                created.Id != 17 ||
                !ReferenceEquals(previous, updated) ||
                updated.Id != 31)
            {
                throw new InvalidOperationException(
                    "ByConvention did not use unambiguous construction semantics.");
            }
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Applies_written_rules_before_remaining_automatic_arguments()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;
using System.Collections.Generic;

namespace TestCase
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
            var destination = mapper.Map(
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
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
