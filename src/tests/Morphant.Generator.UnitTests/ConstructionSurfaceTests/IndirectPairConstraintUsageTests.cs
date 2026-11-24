using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.MappingRegistrationTests;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class IndirectPairConstraintUsageTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void An_unconditional_variant_conversion_is_available_to_both_mappers(bool constrainedFirst)
    {
        const string definitions = """
#nullable enable
using System.Collections.Generic;
internal interface IOut<out T> { }
internal sealed class Both<T> : IOut<List<T>>, IOut<string[]> { }
internal sealed class Request<T> where T : IOut<IEnumerable<object>> { }
""";
        const string mapper = """
#nullable enable
using Morphant;
[MorphantMapper] internal partial class MAPPER<T> : TypeMapper BOUNDS
{
    protected override void Configure(MapperBuilder b) => b.Map<Request<Both<T>>,int>().Convert(_ => 1);
}
""";
        var strict = mapper.Replace("MAPPER", "Strict").Replace("BOUNDS", "where T : class");
        var loose = mapper.Replace("MAPPER", "Loose").Replace("BOUNDS", "");
        var result = MappingRegistrationGeneratorTest.Run(
            [new("Types.cs", definitions), new("A.cs", constrainedFirst ? strict : loose),
                new("B.cs", constrainedFirst ? loose : strict)], LanguageVersion.CSharp9);
        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }
}
