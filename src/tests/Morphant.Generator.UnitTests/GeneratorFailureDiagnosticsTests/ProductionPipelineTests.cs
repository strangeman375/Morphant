using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.GeneratorFailureDiagnosticsTests;

[TestFixture]
internal sealed partial class ProductionPipelineTests
{
    private static readonly CSharpParseOptions Options = new(LanguageVersion.CSharp10);
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Append(typeof(TypeMapper<>).Assembly.Location).Distinct(StringComparer.Ordinal)
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();

    // lang=c#
    private const string Input = """
#nullable enable
global using Operators;
using Morphant;

namespace Operators
{
    public static class TextExtensions
    {
        public static string Keep(this string value) => value;
    }
}

namespace Fixture
{
    public sealed class Source { public string? Value { get; set; } }
    public sealed class Target
    {
        public Target(string value) { Value = value; }
        public string Value { get; }
    }

    [MorphantMapper]
    public partial class FirstMapper : TypeMapper<FirstMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Target>().Resolve((source, previous) =>
            {
                var name = source.Value;
                if (name is null) throw new System.InvalidOperationException("missing name");
                if (previous.HasValue && previous.Value.Value == name) return previous.Value;
                var value = name.Trim().Keep();
                return new(value);
            });
        }
    }

    [MorphantMapper]
    public partial class HealthyMapper : TypeMapper<HealthyMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Target>().Resolve((source, previous) =>
            {
                var name = source.Value;
                if (name is null) throw new System.InvalidOperationException("missing name");
                if (previous.HasValue && previous.Value.Value == name) return previous.Value;
                var value = name.Trim().Keep();
                return new(value);
            });
        }
    }
}
""";

    [Test]
    public void Healthy_production_output_matches_reviewed_sources()
    {
        var driver = NewDriver(new MorphantGenerator()).RunGeneratorsAndUpdateCompilation(
            Compile(), out var output, out var diagnostics);
        Assert.That(diagnostics, Is.Empty);
        AssertSources(driver.GetRunResult().Results.Single(), ExpectedSources);
        AssertCompiler(output);
    }

    [TestCase("GlobalExtensionCache", "FindMorphantMapperDeclarations", 0)]
    [TestCase("TypeContractCache", "FindMorphantMapperDeclarations", 5)]
    [TestCase("SemanticModel", "BuildTypeMapperModels", 0)]
    [TestCase("SharedConstruction", "BuildTypeMapperModels", 0)]
    [TestCase("LocalNames", "BuildTypeMapperModels", 0)]
    [TestCase("ExtensionCalls", "BuildTypeMapperModels", 0)]
    [TestCase("FinalizeSource", "BuildTypeMapperModels", 0)]
    [TestCase("Output", "AddTypeMapperSource", 0)]
    public void Real_stage_preserves_independent_mapper_and_recovers_after_edit(string point, string stage, int skip)
    {
        using var generator = new FaultInjectingGenerator(point, skip);
        var compilation = Compile();
        var driver = NewDriver(generator).RunGeneratorsAndUpdateCompilation(compilation, out var failedOutput, out var diagnostics);
        var result = driver.GetRunResult().Results.Single();
        Assert.That(generator.Failures, Is.EqualTo(1), "The test must actually reach and fail the production operation.");
        Assert.That(generator.Calls, Is.GreaterThan(skip));
        Assert.That(result.Exception, Is.Null);
        Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0057" }));
        var reportName = AssertFailure(diagnostics.Single(), stage);
        var expected = ExpectedSources.Where(source => source.Name != FirstMapperHint &&
            (stage != "FindMorphantMapperDeclarations" || source.Name != FirstExtensionHint));
        AssertSources(result, expected.Append((reportName, FailureReport(stage))));
        AssertCompiler(failedOutput, stage == "FindMorphantMapperDeclarations" ? [("CS1929", 600, 29)] : []);

        generator.Armed = false;
        // Change a transferred expression so the final output request also
        // changes. Unrelated edits correctly preserve cached output results.
        var edit = Input.IndexOf("missing name", StringComparison.Ordinal);
        var recovered = Compile(Input.Remove(edit, "missing name".Length).Insert(edit, "missing value"));
        var recoveredSources = ExpectedSources.Where(source => source.Name != FirstMapperHint)
            .Append((FirstMapperHint, Normalize(RecoveredFirstMapper)));
        driver = driver.RunGeneratorsAndUpdateCompilation(recovered, out var output, out diagnostics);
        Assert.That(diagnostics, Is.Empty);
        AssertSources(driver.GetRunResult().Results.Single(), recoveredSources);
        AssertCompiler(output);
        var fresh = NewDriver(new MorphantGenerator()).RunGeneratorsAndUpdateCompilation(recovered, out var freshOutput, out var freshDiagnostics);
        Assert.That(freshDiagnostics, Is.Empty);
        AssertSources(fresh.GetRunResult().Results.Single(), recoveredSources);
        AssertCompiler(freshOutput);
    }

    [TestCase("GlobalExtensionCache", 0)]
    [TestCase("TypeContractCache", 5)]
    [TestCase("SemanticModel", 0)]
    [TestCase("SharedConstruction", 0)]
    [TestCase("LocalNames", 0)]
    [TestCase("ExtensionCalls", 0)]
    [TestCase("FinalizeSource", 0)]
    [TestCase("Output", 0)]
    public void Real_stage_propagates_cancellation_and_retries_with_the_same_compilation(string point, int skip)
    {
        using var cancellation = new CancellationTokenSource();
        using var generator = new FaultInjectingGenerator(point, skip, cancellation);
        var compilation = Compile();
        var driver = NewDriver(generator);
        Assert.Throws<OperationCanceledException>(() =>
            driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cancellation.Token));
        Assert.That(generator.Failures, Is.EqualTo(1));
        Assert.That(generator.Calls, Is.EqualTo(skip + 1), "Cache tests fail after preceding entries were populated.");

        generator.Armed = false;
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Assert.That(diagnostics, Is.Empty);
        AssertSources(driver.GetRunResult().Results.Single(), ExpectedSources);
        AssertCompiler(output);
    }

    [Test]
    public void Production_initialization_reports_its_original_failure()
    {
        using var generator = new FaultInjectingGenerator("Initialize");
        var driver = NewDriver(generator).RunGeneratorsAndUpdateCompilation(Compile(), out var output, out _);
        var result = driver.GetRunResult().Results.Single();
        Assert.That(generator.Failures, Is.EqualTo(1));
        Assert.That(result.Exception, Is.Null);
        Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0057" }));
        var reportName = AssertFailure(result.Diagnostics.Single(), "Initialize");
        AssertSources(result, [(reportName, FailureReport("Initialize"))]);
        AssertCompiler(output, [("CS1061", 630, 7), ("CS1061", 1230, 7)]);
    }

    private static string AssertFailure(Diagnostic diagnostic, string stage)
    {
        var (reportName, start, length) = stage switch
        {
            "FindMorphantMapperDeclarations" =>
                ("Morphant.Generated.GeneratorFailure.FindMorphantMapperDeclarations__a63e53ba321adfd41779c866be368ec7.g.cs", 427, 590),
            "BuildTypeMapperModels" =>
                ("Morphant.Generated.GeneratorFailure.BuildTypeMapperModels__d21eba6826beb85b658b2e32679805b6.g.cs", 469, 11),
            "AddTypeMapperSource" =>
                ("Morphant.Generated.GeneratorFailure.AddTypeMapperSource__c72bf8d845176027bf88ea5af3e54fd9.g.cs", 0, 0),
            "Initialize" =>
                ("Morphant.Generated.GeneratorFailure.Initialize__8d08a741db357f264b9e93cf345caffa.g.cs", 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };
        const string exceptionType =
            "Morphant.Generator.UnitTests.GeneratorFailureDiagnosticsTests.FaultInjectingGenerator+InjectedFailure";
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(diagnostic.Location.SourceSpan.Start, Is.EqualTo(start));
            Assert.That(diagnostic.Location.SourceSpan.Length, Is.EqualTo(length));
            if (length == 0)
                Assert.That(diagnostic.Location, Is.EqualTo(Location.None));
            else
                Assert.That(diagnostic.Location.SourceTree!.FilePath, Is.EqualTo("Input.cs"));
            Assert.That(diagnostic.GetMessage(), Is.EqualTo(
                "Morphant generator 0.4.0 failed unexpectedly in stage '" + stage + "': " +
                exceptionType + ": deliberate production failure. Full exception details are available in generated file '" + reportName + "'."));
            Assert.That(diagnostic.Properties, Is.EqualTo(ImmutableDictionary<string, string?>.Empty
                .Add("GeneratorVersion", "0.4.0").Add("StageName", stage)
                .Add("ExceptionType", exceptionType).Add("ExceptionMessage", "deliberate production failure")
                .Add("ExceptionDetails", "InjectedFailure: deliberate production failure").Add("ReportHintName", reportName)));
        });
        return reportName;
    }

    private static string FailureReport(string stage) => Normalize(stage switch
    {
        "FindMorphantMapperDeclarations" => """
// <auto-generated />
#nullable enable

/*
MORPH0057: Morphant generator 0.4.0 failed unexpectedly.
Stage: FindMorphantMapperDeclarations

InjectedFailure: deliberate production failure
*/
""",
        "BuildTypeMapperModels" => """
// <auto-generated />
#nullable enable

/*
MORPH0057: Morphant generator 0.4.0 failed unexpectedly.
Stage: BuildTypeMapperModels

InjectedFailure: deliberate production failure
*/
""",
        "AddTypeMapperSource" => """
// <auto-generated />
#nullable enable

/*
MORPH0057: Morphant generator 0.4.0 failed unexpectedly.
Stage: AddTypeMapperSource

InjectedFailure: deliberate production failure
*/
""",
        "Initialize" => """
// <auto-generated />
#nullable enable

/*
MORPH0057: Morphant generator 0.4.0 failed unexpectedly.
Stage: Initialize

InjectedFailure: deliberate production failure
*/
""",
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    });

    private static CSharpCompilation Compile(string source = Input) => CSharpCompilation.Create("FailureRecovery",
        [CSharpSyntaxTree.ParseText(source.Replace("\r\n", "\n"), Options, "Input.cs")], References,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    private static GeneratorDriver NewDriver(IIncrementalGenerator generator) =>
        CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: Options);
    private static void AssertCompiler(Compilation compilation, params (string Id, int Start, int Length)[] expected) =>
        Assert.That(compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .Select(diagnostic => (diagnostic.Id, diagnostic.Severity, diagnostic.Location.SourceTree!.FilePath,
                diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length)),
            Is.EqualTo(expected.Select(diagnostic =>
                (diagnostic.Id, DiagnosticSeverity.Error, "Input.cs", diagnostic.Start, diagnostic.Length))));
    private static void AssertSources(GeneratorRunResult result, IEnumerable<(string Name, string Source)> expected)
    {
        Assert.That(result.Exception, Is.Null);
        Assert.That(result.GeneratedSources.Select(source => (source.HintName, source.SourceText.ToString()))
            .OrderBy(source => source.HintName, StringComparer.Ordinal),
            Is.EqualTo(expected.OrderBy(source => source.Name, StringComparer.Ordinal)));
    }
    private static string Normalize(string source) => source.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n";
}
