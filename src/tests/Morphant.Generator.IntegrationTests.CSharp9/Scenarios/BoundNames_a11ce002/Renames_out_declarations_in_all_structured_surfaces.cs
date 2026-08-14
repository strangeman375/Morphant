// Compiled integration scenario: TypeMapperEvaluationTests/NameCollisionTests::Accepts_out_variables_in_all_structured_callbacks
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.BoundNames_a11ce002
{
    public sealed class Source
    {
        public string Text { get; init; } = string.Empty;
    }

    public sealed class ConstructDestination
    {
        public ConstructDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class ResolveDestination
    {
        public ResolveDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class MembersDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct(input =>
                {
                    if (int.TryParse(input.Text, out var context))
                    {
                        return new(context);
                    }

                    return new(-1);
                });

            builder.Map<Source, ResolveDestination>()
                .Resolve((input, _) =>
                {
                    if (int.TryParse(input.Text, out var destination))
                    {
                        return new(destination);
                    }

                    return new(-1);
                });

            builder.Map<Source, MembersDestination>()
                .Members(input =>
                {
                    if (int.TryParse(input.Text, out var source))
                    {
                        return new() { Value = source };
                    }

                    return new() { Value = -1 };
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Text = "23" };

            AssertValue<ConstructDestination>(
                mapper,
                source,
                destination => destination.Value);
            AssertValue<ResolveDestination>(
                mapper,
                source,
                destination => destination.Value);
            AssertValue<MembersDestination>(
                mapper,
                source,
                destination => destination.Value);

            var previous = new ResolveDestination(-1);
            var updated =
                ((ITypeMapper<Source, ResolveDestination>)mapper).Update(
                    source,
                    previous,
                    default(MappingContext));

            if (updated.Value != 23)
            {
                throw new InvalidOperationException(
                    "The Resolve out declaration was not transferred.");
            }
        }

        private static void AssertValue<TDestination>(
            TestMapper mapper,
            Source source,
            Func<TDestination, int> read)
        {
            var destination =
                ((ITypeMapper<Source, TDestination>)mapper).Create(
                    source,
                    default(MappingContext));

            if (read(destination) != 23)
            {
                throw new InvalidOperationException(
                    "An out declaration was not transferred.");
            }
        }
    }
}
