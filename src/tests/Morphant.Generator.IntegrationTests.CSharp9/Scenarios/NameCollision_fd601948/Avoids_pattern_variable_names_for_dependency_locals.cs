// Compiled integration scenario: TypeMapperEvaluationTests/NameCollisionTests::Accepts_user_pattern_names_that_match_generated_temporaries
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NameCollision_fd601948
{
    public sealed class Source
    {
        public object? Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }

        public int Other { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int InvocationCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) =>
                {
                    switch (source.Value)
                    {
                        case int value:
                            return new()
                            {
                                Value = Touch(value),
                                Other = Touch(value)
                            };

                        default:
                            return new()
                            {
                                Value = 0,
                                Other = 0
                            };
                    }
                });

        private static int Touch(int value)
        {
            InvocationCount++;
            return value + 10;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var selected = mapper.Create(
                new Source { Value = 3 },
                context);
            var skipped = mapper.Create(
                new Source { Value = "other" },
                context);

            if (selected.Value != 13 ||
                selected.Other != 13 ||
                skipped.Value != 0 ||
                skipped.Other != 0 ||
                TestMapper.InvocationCount != 1)
            {
                throw new InvalidOperationException(
                    "Shared-local naming changed switch behavior.");
            }
        }
    }
}
