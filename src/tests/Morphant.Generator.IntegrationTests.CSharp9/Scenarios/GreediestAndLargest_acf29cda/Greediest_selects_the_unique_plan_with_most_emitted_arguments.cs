// Compiled integration scenario: TypeMapperConstructorSelectionTests/GreediestAndLargestTests::Greediest_selects_the_unique_plan_with_most_emitted_arguments
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GreediestAndLargest_acf29cda
{
    public sealed class SparseSource
    {
        public int Id { get; init; }

        public int Code { get; init; }
    }

    public sealed class RichSource
    {
        public int Id { get; init; }

        public int Code { get; init; }

        public string Label { get; init; } = string.Empty;

        public string[] Tags { get; init; } = Array.Empty<string>();
    }

    public sealed class ApplicableDestination
    {
        public ApplicableDestination(int id)
        {
            Kind = "applicable";
            Value = id;
        }

        public ApplicableDestination(int code, string missing)
        {
            Kind = missing;
            Value = code;
        }

        public string Kind { get; }

        public int Value { get; }
    }

    public sealed class RichDestination
    {
        public RichDestination(int id)
        {
            Kind = "small";
            Value = id;
            Label = string.Empty;
            Tags = Array.Empty<string>();
        }

        public RichDestination(
            int code,
            string label = "default",
            params string[] tags)
        {
            Kind = "rich";
            Value = code;
            Label = label;
            Tags = tags;
        }

        public string Kind { get; }

        public int Value { get; }

        public string Label { get; }

        public string[] Tags { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<SparseSource, ApplicableDestination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
            builder.Map<RichSource, RichDestination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var applicable =
                ((ITypeMapper<SparseSource, ApplicableDestination>)mapper)
                    .Create(
                        new SparseSource { Id = 17, Code = 31 },
                        context);
            var tags = new[] { "one", "two" };
            var rich =
                ((ITypeMapper<RichSource, RichDestination>)mapper)
                    .Create(
                        new RichSource
                        {
                            Id = 17,
                            Code = 31,
                            Label = "mapped",
                            Tags = tags
                        },
                        context);

            if (applicable.Kind != "applicable" ||
                applicable.Value != 17 ||
                rich.Kind != "rich" ||
                rich.Value != 31 ||
                rich.Label != "mapped" ||
                !ReferenceEquals(tags, rich.Tags))
            {
                throw new InvalidOperationException(
                    "Greediest did not maximize emitted arguments.");
            }
        }
    }
}
