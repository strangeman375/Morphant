using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class SurfaceCapabilityTests
{
    [Test]
    public void Adds_removes_and_restores_member_outputs_while_retaining_construction()
    {
        const string construction =
            "Morphant.Generated.Construction.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs";
        const string mapping =
            "Morphant.Generated.MappingExtension.TestMapper.SourceToDestination__2087052f5a3645b79062a3a095263f92.g.cs";
        const string members =
            "Morphant.Generated.Member.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs";
        const string memberExtension =
            "Morphant.Generated.MemberExtension.TestMapper.SourceToDestination__2087052f5a3645b79062a3a095263f92.g.cs";
        const string mapper =
            "Morphant.Generated.TypeMapper.TestMapper__c4f37cd504cad969975e05a81c79a459.g.cs";
        var mapperFile = SourceFile("Mapper.cs", MapperSource);
        var emptyDestination = SourceFile("Models.cs", ModelsSource.Replace("__MEMBER__", ""));
        var writableDestination = SourceFile("Models.cs", ModelsSource.Replace(
            "__MEMBER__", "public int Value { get; set; }"));
        var withoutMembers = new[] { construction, mapping, mapper };
        var withMembers = new[] { construction, mapping, members, memberExtension, mapper };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step("no writable members", [mapperFile, emptyDestination], withoutMembers),
            Step(
                "writable member added",
                [mapperFile, writableDestination],
                withMembers,
                Stage("BuildConstructionPlanRequests",
                    Expected(construction, IncrementalStepRunReason.Cached)),
                Stage("BuildMappingExtensionRequests",
                    Expected(mapping, IncrementalStepRunReason.Unchanged)),
                Stage("BuildMemberPlanRequests",
                    Expected(members, IncrementalStepRunReason.New)),
                Stage("BuildMemberExtensionRequests",
                    Expected(memberExtension, IncrementalStepRunReason.New))),
            Step(
                "last writable member removed",
                [mapperFile, emptyDestination],
                withoutMembers,
                Stage("BuildConstructionPlanRequests",
                    Expected(construction, IncrementalStepRunReason.Cached)),
                Stage("BuildMappingExtensionRequests",
                    Expected(mapping, IncrementalStepRunReason.Unchanged)),
                Stage("BuildMemberPlanRequests",
                    Expected(members, IncrementalStepRunReason.Removed)),
                Stage("BuildMemberExtensionRequests",
                    Expected(memberExtension, IncrementalStepRunReason.Removed))),
            Step(
                "writable member restored",
                [mapperFile, writableDestination],
                withMembers,
                Stage("BuildConstructionPlanRequests",
                    Expected(construction, IncrementalStepRunReason.Cached)),
                Stage("BuildMappingExtensionRequests",
                    Expected(mapping, IncrementalStepRunReason.Unchanged)),
                Stage("BuildMemberPlanRequests",
                    Expected(members, IncrementalStepRunReason.New)),
                Stage("BuildMemberExtensionRequests",
                    Expected(memberExtension, IncrementalStepRunReason.New))));
    }

    // lang=c#
    private const string MapperSource =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string ModelsSource =
"""
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; set; }
    }
    public sealed class Destination
    {
        __MEMBER__
    }
}
""";
}
