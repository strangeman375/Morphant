// Compiled integration scenario: TypeMapperConstructorSelectionTests/ByConventionTests::Greediest_counts_written_ByConvention_rules_and_omissions
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0036

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByConvention_5e51e8dc
{
    public sealed class Source
    {
        public int Id { get; init; }

        public int Code { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Label { get; init; } = string.Empty;
    }

    public sealed class RuleDestination
    {
        public RuleDestination(int id)
        {
            Kind = "id";
            Value = id;
        }

        public RuleDestination(string name, int code = 0)
        {
            Kind = name;
            Value = code;
        }

        public string Kind { get; }

        public int Value { get; }
    }

    public sealed class IgnoredDestination
    {
        public IgnoredDestination(
            int id,
            string label = "default")
        {
        }

        public IgnoredDestination(
            int code,
            string label = "default",
            params string[] tags)
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, RuleDestination>()
                .ConstructorSelection(ConstructorSelection.Greediest)
                .Construct(source => new(
                    ByConvention(),
                    new()
                    {
                        name = source.Name,
                        code = 47
                    }));
            builder.Map<Source, IgnoredDestination>()
                .ConstructorSelection(ConstructorSelection.Greediest)
                .Construct(_ => new(
                    ByConvention(),
                    new()
                    {
                        label = Ignore()
                    }));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                Id = 17,
                Code = 31,
                Name = "configured",
                Label = "automatic"
            };
            var context = default(MappingContext);
            var selected =
                ((ITypeMapper<Source, RuleDestination>)mapper)
                    .Create(source, context);

            if (selected.Kind != "configured" ||
                selected.Value != 47)
            {
                throw new InvalidOperationException(
                    "Written ByConvention rules did not participate in Greediest.");
            }

            try
            {
                ((ITypeMapper<Source, IgnoredDestination>)mapper)
                    .Create(source, context);
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Ignored ByConvention arguments changed the Greediest score.");
        }
    }
}
