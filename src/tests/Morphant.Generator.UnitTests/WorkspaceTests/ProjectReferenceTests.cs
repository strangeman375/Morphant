using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.WorkspaceTests;

[TestFixture]
internal sealed class ProjectReferenceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Actualizes_accessibility_when_friend_access_changes(bool constructor)
    {
        using var workspace = new GeneratorWorkspaceTest();
        var models = workspace.AddProject("ExternalModels");
        var consumer = workspace.AddProject("Consumer");
        var assembly = DocumentId.CreateNewId(models);
        var destination = constructor
            ? "public sealed class Destination { public int Value { get; } public Destination() { } internal Destination(int value) { Value = value; } }"
            : "public sealed class Destination { public int Value { get; internal set; } }";
        workspace.Apply(workspace.Solution
            .AddDocument(DocumentId.CreateNewId(models), "Models.cs", SourceText.From(
                $$"""
#nullable enable
namespace ExternalModels
{
    public sealed class Source { public int Value { get; set; } }
    {{destination}}
}
"""), filePath: "Models.cs")
            .AddDocument(assembly, "AssemblyInfo.cs", SourceText.From(""), filePath: "AssemblyInfo.cs")
            .AddDocument(DocumentId.CreateNewId(consumer), "Mapper.cs", SourceText.From(ConsumerSource), filePath: "Mapper.cs")
            .AddProjectReference(consumer, new ProjectReference(models)));
        string[] inaccessibleHints =
        [
            "Morphant.Generated.Construction.Destination__810e478cf158aee1d66e7bd808738fc5.g.cs",
            "Morphant.Generated.MappingExtension.SourceToDestination__17c94969546aa4837809d2e4e101f635.g.cs",
            "Morphant.Generated.TypeMapper.TestMapper__34eeba5c4c0622cda3036b677bc2b836.g.cs"
        ];
        var initial = await workspace.AssertProjectAsync(consumer, inaccessibleHints);

        workspace.Apply(workspace.Solution.WithDocumentText(assembly, SourceText.From(
            "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"Consumer\")]")));
        var accessible = await workspace.AssertProjectAsync(consumer, constructor ? inaccessibleHints : ConsumerHints);
        Assert.That(accessible.ToArray(), Is.Not.EqualTo(initial.ToArray()));

        workspace.Apply(workspace.Solution.WithDocumentText(assembly, SourceText.From("")));
        var restored = await workspace.AssertProjectAsync(consumer, inaccessibleHints);
        Assert.That(restored.ToArray(), Is.EqualTo(initial.ToArray()));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Actualizes_a_referenced_mapper_with_and_without_friend_access(bool friendAssembly)
    {
        using var workspace = new GeneratorWorkspaceTest();
        var models = workspace.AddProject("ExternalModels");
        var consumer = workspace.AddProject("Consumer");
        var modelsDocument = DocumentId.CreateNewId(models);
        var consumerDocument = DocumentId.CreateNewId(consumer);
        var initialModels = friendAssembly
            ? "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"Consumer\")]\n" + ModelsSource
            : ModelsSource;
        workspace.Apply(workspace.Solution
            .AddDocument(modelsDocument, "Models.cs", SourceText.From(initialModels), filePath: "Models.cs")
            .AddDocument(consumerDocument, "Mapper.cs", SourceText.From(ConsumerUsage), filePath: "Mapper.cs")
            .AddProjectReference(consumer, new ProjectReference(models)));

        string[] expectedHints = friendAssembly
            ? [ConsumerHints[0], ConsumerHints[2], ConsumerHints[4]]
            : ConsumerHints;
        ExpectedCompilerDiagnostic[] expectedDiagnostics = friendAssembly
            ? [CompilerDiagnostic("MORPH0065", DiagnosticSeverity.Error, "Mapper.cs",
                ConsumerUsage.IndexOf("Map<", StringComparison.Ordinal), 3)]
            : [];
        var initial = await workspace.AssertProjectAsync(consumer, expectedHints, expectedDiagnostics);
        await workspace.AssertProjectAsync(models, ModelHints);
        var compilation = (await workspace.Solution.GetProject(consumer)!.GetCompilationAsync())!;
        Assert.That(compilation.References.OfType<CompilationReference>()
            .Select(static reference => reference.Compilation.AssemblyName),
            Does.Contain("ExternalModels"));

        workspace.Apply(workspace.Solution.WithDocumentText(modelsDocument,
            SourceText.From(initialModels.Replace("int Value", "long Value", StringComparison.Ordinal))));
        var changed = await workspace.AssertProjectAsync(consumer, expectedHints, expectedDiagnostics);
        await workspace.AssertProjectAsync(models, ModelHints);
        Assert.That(changed.ToArray(), Is.Not.EqualTo(initial.ToArray()));

        workspace.Apply(workspace.Solution.WithDocumentText(modelsDocument, SourceText.From(initialModels)));
        var restored = await workspace.AssertProjectAsync(consumer, expectedHints, expectedDiagnostics);
        Assert.That(restored.ToArray(), Is.EqualTo(initial.ToArray()));
        await workspace.AssertProjectAsync(models, ModelHints);
    }

    [Test]
    public async Task Removes_and_restores_documents_when_a_project_reference_is_unloaded()
    {
        using var workspace = new GeneratorWorkspaceTest();
        var models = workspace.AddProject("ExternalModels");
        var consumer = workspace.AddProject("Consumer");
        workspace.Apply(workspace.Solution
            .AddDocument(DocumentId.CreateNewId(models), "Models.cs", SourceText.From(ModelsSource), filePath: "Models.cs")
            .AddDocument(DocumentId.CreateNewId(consumer), "Mapper.cs", SourceText.From(ConsumerSource), filePath: "Mapper.cs")
            .AddProjectReference(consumer, new ProjectReference(models)));
        var initial = await workspace.AssertProjectAsync(consumer, ConsumerHints);

        workspace.Apply(workspace.Solution.RemoveProjectReference(consumer, new ProjectReference(models)));
        await workspace.AssertProjectAsync(consumer, [],
        [
            CompilerDiagnostic("CS0246", DiagnosticSeverity.Error, "Mapper.cs",
                ConsumerSource.IndexOf("ExternalModels", StringComparison.Ordinal), "ExternalModels".Length),
            CompilerDiagnostic("CS0246", DiagnosticSeverity.Error, "Mapper.cs",
                ConsumerSource.IndexOf("Source,", StringComparison.Ordinal), "Source".Length),
            CompilerDiagnostic("CS0246", DiagnosticSeverity.Error, "Mapper.cs",
                ConsumerSource.IndexOf("Destination>", StringComparison.Ordinal), "Destination".Length)
        ]);

        workspace.Apply(workspace.Solution.AddProjectReference(consumer, new ProjectReference(models)));
        var restored = await workspace.AssertProjectAsync(consumer, ConsumerHints);
        Assert.That(restored.ToArray(), Is.EqualTo(initial.ToArray()));
    }

    private static readonly string[] ModelHints =
    [
        "Morphant.Generated.Construction.Destination__c759c59f9bc873a2e85ff505d7f16127.g.cs",
        "Morphant.Generated.MappingExtension.SourceToDestination__7b2e1fc5f07386fdf7ef5612332a805c.g.cs",
        "Morphant.Generated.Member.Destination__c759c59f9bc873a2e85ff505d7f16127.g.cs",
        "Morphant.Generated.MemberExtension.SourceToDestination__7b2e1fc5f07386fdf7ef5612332a805c.g.cs",
        "Morphant.Generated.TypeMapper.ExternalMapper__35e60aa8d9590156b630789e2a72d31e.g.cs"
    ];

    private static readonly string[] ConsumerHints =
    [
        "Morphant.Generated.Construction.Destination__810e478cf158aee1d66e7bd808738fc5.g.cs",
        "Morphant.Generated.MappingExtension.SourceToDestination__17c94969546aa4837809d2e4e101f635.g.cs",
        "Morphant.Generated.Member.Destination__810e478cf158aee1d66e7bd808738fc5.g.cs",
        "Morphant.Generated.MemberExtension.SourceToDestination__17c94969546aa4837809d2e4e101f635.g.cs",
        "Morphant.Generated.TypeMapper.TestMapper__34eeba5c4c0622cda3036b677bc2b836.g.cs"
    ];

    // lang=c#
    private const string ModelsSource =
"""
#nullable enable
#pragma warning disable CS1591
namespace ExternalModels
{
    public sealed class Source { public int Value { get; set; } }
    public sealed class Destination { public int Value { get; set; } }

    [Morphant.MorphantMapper]
    public partial class ExternalMapper : Morphant.TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string ConsumerSource =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
using ExternalModels;
namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string ConsumerUsage =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
using ExternalModels;
namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) => new() { Value = source.Value + 10 });
    }

    public static class Usage
    {
        public static ITypeMapper<Source, Destination> External() => new ExternalMapper();
        public static ITypeMapper<Source, Destination> Local() => new TestMapper();
    }
}
""";
}
