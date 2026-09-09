using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.WorkspaceTests;

[TestFixture]
internal sealed class DocumentLifecycleTests
{
    [Test]
    public async Task Actualizes_generated_documents_after_add_rename_remove_and_restore()
    {
        using var workspace = new GeneratorWorkspaceTest();
        var project = workspace.AddProject("Consumer");
        var mapper = DocumentId.CreateNewId(project);
        workspace.Apply(workspace.Solution.AddDocument(DocumentId.CreateNewId(project),
            "Models.cs", SourceText.From(ModelsSource), filePath: "Models.cs"));
        await workspace.AssertProjectAsync(project, []);

        workspace.Apply(workspace.Solution.AddDocument(mapper,
            "Mapper.cs", SourceText.From(MapperSource), filePath: "Mapper.cs"));
        var initial = await workspace.AssertProjectAsync(project, InitialHints);

        workspace.Apply(workspace.Solution
            .WithDocumentName(mapper, "MovedMapper.cs")
            .WithDocumentFilePath(mapper, "Moved/MovedMapper.cs"));
        var moved = await workspace.AssertProjectAsync(project, InitialHints);
        Assert.That(moved.ToArray(), Is.EqualTo(initial.ToArray()));

        workspace.Apply(workspace.Solution.WithDocumentText(mapper,
            SourceText.From(MapperSource.Replace("TestMapper", "RenamedMapper", StringComparison.Ordinal))));
        await workspace.AssertProjectAsync(project,
        [
            "Morphant.Generated.Construction.TestCase_Destination.g.cs",
            "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination__TestCase_RenamedMapper.g.cs",
            "Morphant.Generated.Member.TestCase_Destination.g.cs",
            "Morphant.Generated.MemberExtension.TestCase_Source__TestCase_Destination__TestCase_RenamedMapper.g.cs",
            "Morphant.Generated.TypeMapper.TestCase_RenamedMapper.g.cs"
        ]);

        workspace.Apply(workspace.Solution.RemoveDocument(mapper));
        await workspace.AssertProjectAsync(project, []);
        workspace.Apply(workspace.Solution.AddDocument(mapper,
            "Mapper.cs", SourceText.From(MapperSource), filePath: "Mapper.cs"));
        var restored = await workspace.AssertProjectAsync(project, InitialHints);
        Assert.That(restored.ToArray(), Is.EqualTo(initial.ToArray()));
    }

    [Test]
    public async Task Recovers_generated_DSL_binding_and_executes_the_edited_configuration()
    {
        using var workspace = new GeneratorWorkspaceTest();
        var project = workspace.AddProject("Consumer");
        var mapper = DocumentId.CreateNewId(project);
        var scenario = DocumentId.CreateNewId(project);
        workspace.Apply(workspace.Solution
            .AddDocument(DocumentId.CreateNewId(project), "Models.cs", SourceText.From(ModelsSource), filePath: "Models.cs")
            .AddDocument(mapper, "Mapper.cs", SourceText.From(UsageSource), filePath: "Mapper.cs")
            .AddDocument(scenario, "Scenario.cs", SourceText.From(ScenarioSource), filePath: "Scenario.cs"));
        await workspace.AssertProjectAsync(project, InitialHints, scenarioTypeName: "TestCase.Scenario");

        var broken = UsageSource.Replace(".Members(", ".MissingMembers(", StringComparison.Ordinal);
        workspace.Apply(workspace.Solution.WithDocumentText(mapper, SourceText.From(broken)));
        await workspace.AssertProjectAsync(project, InitialHints,
        [
            CompilerDiagnostic("CS1061", DiagnosticSeverity.Error, "Mapper.cs",
                broken.IndexOf("MissingMembers", StringComparison.Ordinal), "MissingMembers".Length)
        ]);

        workspace.Apply(workspace.Solution
            .WithDocumentText(mapper, SourceText.From(UsageSource.Replace("+ 10", "+ 20", StringComparison.Ordinal)))
            .WithDocumentText(scenario, SourceText.From(ScenarioSource.Replace("!= 17", "!= 27", StringComparison.Ordinal))));
        await workspace.AssertProjectAsync(project, InitialHints, scenarioTypeName: "TestCase.Scenario");
    }

    private static readonly string[] InitialHints =
    [
        "Morphant.Generated.Construction.TestCase_Destination.g.cs",
        "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs",
        "Morphant.Generated.Member.TestCase_Destination.g.cs",
        "Morphant.Generated.MemberExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs",
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs"
    ];

    // lang=c#
    private const string ModelsSource =
"""
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public sealed class Source { public int Value { get; set; } }
    public sealed class Destination { public int Value { get; set; } }
}
""";

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
    private const string UsageSource =
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
            builder.Map<Source, Destination>()
                .Members((source, _) => new() { Value = source.Value + 10 });
    }
}
""";

    // lang=c#
    private const string ScenarioSource =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source { Value = 7 };
            var created = mapper.Create(source);
            var existing = new Destination();
            var updated = mapper.Update(source, existing);
            if (created.Value != 17 || updated.Value != 17 || !object.ReferenceEquals(existing, updated))
                throw new System.InvalidOperationException("The edited configuration was not applied.");
        }
    }
}
""";
}
