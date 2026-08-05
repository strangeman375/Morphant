using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class NameCollisionTests
{
    [Test]
    public void Avoids_pattern_variable_names_for_dependency_locals()
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
        public object? Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }

        public int Other { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
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
            var selected = mapper.Map(
                new Source { Value = 3 },
                context);
            var skipped = mapper.Map(
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
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
