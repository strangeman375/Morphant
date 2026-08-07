// Compiled integration scenario: TypeMapperConstructorSelectionTests/GreediestAndLargestTests::Greediest_requires_an_explicit_choice_when_best_scores_tie
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GreediestAndLargest_d960be8d
{
    public sealed class Source
    {
        public int Id { get; init; }

        public int Code { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int id)
        {
        }

        public Destination(
            int code,
            string label = "default",
            params string[] tags)
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();

            try
            {
                mapper.Create(
                    new Source { Id = 17, Code = 31 },
                    default(MappingContext));
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Omitted optional and params arguments changed the Greediest score.");
        }
    }
}
