using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncludeMembersTests;

[TestFixture]
internal sealed class InheritedScopeActualizationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Transitive_inclusions_follow_the_consuming_destination(bool genericTemplate)
    {
        const string mapperHint = "Morphant.Generated.TypeMapper.TestMapper__c4f37cd504cad969975e05a81c79a459.g.cs";
        var baseHint = genericTemplate
            ? "BaseSourceToBaseDto__fcf1f1f39dfbf3a5527513c966b1ee83.g.cs"
            : "BaseSourceToBaseDto__855c4cf5ee77ab45b8f4ed6c3cac4dc3.g.cs";
        string[] files =
        [
            "Morphant.Generated.Construction.BaseDto__d382f75a25f0c78b0f97bd219ac2eb23.g.cs",
            "Morphant.Generated.Construction.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
            "Morphant.Generated.Construction.MiddleDto__de09b69eea34b6af9ddac1fe5aeecb7c.g.cs",
            "Morphant.Generated.MappingExtension." + baseHint,
            "Morphant.Generated.MappingExtension.MiddleToMiddleDto__9c0e92bd59ecb3e07188a8f487b0e9c0.g.cs",
            "Morphant.Generated.MappingExtension.SourceToDestination__fd3791156a0b8842275fd74856c7bb41.g.cs",
            "Morphant.Generated.Member.BaseDto__d382f75a25f0c78b0f97bd219ac2eb23.g.cs",
            "Morphant.Generated.Member.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
            "Morphant.Generated.Member.MiddleDto__de09b69eea34b6af9ddac1fe5aeecb7c.g.cs",
            "Morphant.Generated.MemberExtension." + baseHint,
            "Morphant.Generated.MemberExtension.MiddleToMiddleDto__9c0e92bd59ecb3e07188a8f487b0e9c0.g.cs",
            "Morphant.Generated.MemberExtension.SourceToDestination__fd3791156a0b8842275fd74856c7bb41.g.cs",
            mapperHint
        ];

        var template = genericTemplate ? """
    internal abstract class Template<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder b) => b.Map<BaseSource<T>, BaseDto>().IncludeMembers(s => s.Included);
    }
""" : "";
        var mapperBase = genericTemplate ? "Template<Scope>" : "TypeMapper";
        var registration = genericTemplate ? "base.Configure(b);" :
            "b.Map<BaseSource<Scope>, BaseDto>().IncludeMembers(s => s.Included);";
        var mapper = SourceFile("Mapper.cs", $$"""
#nullable enable
using Morphant;
namespace TestCase
{
    internal class BaseSource<T> { public T Included { get; set; } = default!; }
    internal class Middle : BaseSource<Scope> { }
    internal sealed class Source : Middle { }
    internal class BaseDto { public string Name { get; set; } = ""; }
    internal class MiddleDto : BaseDto { }
    internal class Destination : MiddleDto { public int BranchPayloadCode { get; set; } }
    internal class Scope { public string Name { get; } = ""; public Branch Branch { get; } = new(); }
    internal class Branch { public Payload Payload { get; } = new(); }
{{template}}
    [MorphantMapper] internal partial class TestMapper : {{mapperBase}}
    {
        protected override void Configure(MapperBuilder b)
        {
            {{registration}}
            b.Map<Middle, MiddleDto>().IncludeBase<BaseSource<Scope>, BaseDto>();
            b.Map<Source, Destination>().Flattening(Flattening.Auto).IncludeBase<Middle, MiddleDto>();
        }
    }
    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source { Included = new Scope() };
            if (mapper.Create(source, default).BranchPayloadCode != Expect.Value ||
                mapper.Update(source, new Destination(), default).BranchPayloadCode != Expect.Value)
                throw new System.InvalidOperationException("Inherited flattening used a stale member path.");
        }
    }
}
""");
        GeneratorIncrementalityStep Edit(string member, int expected, params ExpectedIncrementalStage[] stages) =>
            ExecutableStep(member, [mapper, SourceFile("Payload.cs", $$"""
namespace TestCase
{
    internal class Payload { public int {{member}} => 41; }
    internal static class Expect { public static int Value => {{expected}}; }
}
""")], files, "TestCase.Scenario", stages);

        var changed = Stage("BuildTypeMapperRequests", Expected(mapperHint, IncrementalStepRunReason.Modified));
        RunAndAssert(LanguageVersion.CSharp9, static () => new MorphantGenerator(),
            Edit("Other", 0), Edit("Code", 41, changed), Edit("Other", 0, changed));
    }
}
