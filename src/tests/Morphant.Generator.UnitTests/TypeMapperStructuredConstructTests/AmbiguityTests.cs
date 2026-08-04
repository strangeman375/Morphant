using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperStructuredConstructTests;

[TestFixture]
internal sealed class AmbiguityTests
{
    [Test]
    public void Leaves_ambiguous_generated_constructor_for_the_compiler()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class Destination
    {
        public Destination(int value)
        {
        }

        public Destination(string value)
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(_ => new(Auto()));
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndAssertDiagnostics(
            LanguageVersion.CSharp9,
            source,
            "TestCase.cs(28,33): error CS0121: The call is ambiguous between the following methods or properties: 'DestinationConstruction.DestinationConstruction(ConstructorParameter<int>)' and 'DestinationConstruction.DestinationConstruction(ConstructorParameter<string>)'");
    }
}
