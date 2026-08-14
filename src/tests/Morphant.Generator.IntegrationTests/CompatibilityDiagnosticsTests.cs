namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class CompatibilityDiagnosticsTests
{
    [Test]
    public async Task Normal_package_builds_and_runs_a_generated_mapper()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var packageVersion =
            $"0.0.0-compatibility.{Guid.NewGuid():N}";

        var pack = await workspace.PackMorphant(packageVersion);
        AssertSucceeded(pack);

        var run = await workspace.RunPackageConsumer(packageVersion);
        AssertSucceeded(run);
        Assert.That(run.Output, Does.Not.Contain("MORPH000"));
    }

    [Test]
    public async Task Analyzer_without_runtime_reports_MORPH0002()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("AnalyzerOnly");

        AssertFailedWith(
            build.Process,
            "MORPH0002",
            "Morphant requires a reference to a compatible runtime library.");
        AssertNoMorphantGeneratedFiles(build);
    }

    [Test]
    public async Task Runtime_revision_2_reports_MORPH0004()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var runtime = await workspace.BuildRuntimeCandidate(
            "RuntimeV2",
            "Morphant.TestRuntimeV2");
        AssertSucceeded(runtime.Process);

        var build = await workspace.BuildConsumer(
            "MismatchedRuntime",
            runtime.AssemblyPath);

        AssertFailedWith(
            build.Process,
            "MORPH0004",
            "The Morphant runtime is incompatible with this generator: " +
            "the runtime and generator versions do not match.");
        AssertNoMorphantGeneratedFiles(build);
    }

    [Test]
    public async Task Partial_runtime_reports_incompatible_instead_of_missing()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var runtime = await workspace.BuildRuntimeCandidate(
            "PartialRuntime",
            "Morphant.TestPartialRuntime");
        AssertSucceeded(runtime.Process);

        var build = await workspace.BuildConsumer(
            "MismatchedRuntime",
            runtime.AssemblyPath);

        AssertFailedWith(
            build.Process,
            "MORPH0004",
            "The Morphant runtime is incompatible with this generator: " +
            "the runtime does not provide compatibility information.");
        AssertNoMorphantGeneratedFiles(build);
    }

    [Test]
    public async Task Two_runtime_candidates_report_one_MORPH0003()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var runtime = await workspace.BuildRuntimeCandidate(
            "RuntimeV1",
            "Morphant.TestRuntimeV1");
        AssertSucceeded(runtime.Process);

        var build = await workspace.BuildConsumer(
            "DuplicateRuntime",
            runtime.AssemblyPath);

        AssertFailedWith(
            build.Process,
            "MORPH0003",
            "Multiple Morphant runtime libraries were found. Reference " +
            "exactly one.");
        AssertNoMorphantGeneratedFiles(build);
    }

    [Test]
    public async Task Ambiguity_precedes_candidate_compatibility()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var runtime = await workspace.BuildRuntimeCandidate(
            "RuntimeV2",
            "Morphant.TestRuntimeV2");
        AssertSucceeded(runtime.Process);

        var build = await workspace.BuildConsumer(
            "DuplicateRuntime",
            runtime.AssemblyPath);

        AssertFailedWith(
            build.Process,
            "MORPH0003",
            "Multiple Morphant runtime libraries were found. Reference " +
            "exactly one.");
        AssertNoMorphantGeneratedFiles(build);
    }

    [Test]
    public async Task CSharp8_reports_MORPH0001_and_generates_nothing()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("CSharp8");

        AssertFailedWith(
            build.Process,
            "MORPH0001",
            "Morphant requires C# 9.0 or later, but this compilation uses " +
            "C# 8.0.");
        AssertNoMorphantGeneratedFiles(build);
    }

    [Test]
    public async Task Configuration_changes_severity_but_not_the_gate()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var warning = await workspace.BuildConsumer("CSharp8Warning");
        var warningDiagnostics = CompilerDiagnosticOutput.Read(
            warning.Process.Output);

        AssertSucceeded(warning.Process);
        Assert.That(warningDiagnostics.Length, Is.EqualTo(1));
        Assert.That(
            warningDiagnostics.Single(),
            Does.Contain("warning MORPH0001"));
        AssertNoMorphantGeneratedFiles(warning);

        var suppressed = await workspace.BuildConsumer(
            "CSharp8Suppressed");

        AssertSucceeded(suppressed.Process);
        Assert.That(
            CompilerDiagnosticOutput.Read(suppressed.Process.Output),
            Is.Empty);
        AssertNoMorphantGeneratedFiles(suppressed);
    }

    private static void AssertSucceeded(ProcessResult result)
    {
        Assert.That(
            result.ExitCode,
            Is.EqualTo(0),
            $"{result.Command} failed.{Environment.NewLine}{result.Output}");
    }

    private static void AssertFailedWith(
        ProcessResult result,
        string id,
        string message)
    {
        var diagnostics = CompilerDiagnosticOutput.Read(result.Output);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Not.EqualTo(0), result.Output);
            Assert.That(diagnostics.Length, Is.EqualTo(1), result.Output);
            Assert.That(diagnostics.Single(), Does.Contain("error " + id));
            Assert.That(diagnostics.Single(), Does.Contain(message));
        });
    }

    private static void AssertNoMorphantGeneratedFiles(ConsumerBuild build)
    {
        Assert.That(
            build.GetGeneratedFiles("Morphant.Generated.*"),
            Is.Empty);
    }
}
