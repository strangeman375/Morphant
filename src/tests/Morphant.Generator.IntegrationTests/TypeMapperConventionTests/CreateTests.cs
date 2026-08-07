using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperConventionTests;

[TestFixture]
internal sealed class CreateTests
{
    [Test]
    public void Creates_a_new_result_and_applies_creation_member_conventions()
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

        public string Name { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public int Id { get; init; }

        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source
            {
                Id = 17,
                Name = "created"
            };
            var first = mapper.Create(source, default(MappingContext));
            var second = mapper.Create(source, default(MappingContext));

            if (first.Id != 17 ||
                first.Name != "created" ||
                second.Id != 17 ||
                second.Name != "created" ||
                ReferenceEquals(first, second))
            {
                throw new InvalidOperationException(
                    "Convention Create produced an unexpected result.");
            }
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
