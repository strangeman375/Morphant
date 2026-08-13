namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class CompatibilityDiagnosticsTests
{
    private CompatibilityTestWorkspace _workspace = null!;

    [SetUp]
    public void SetUp()
    {
        _workspace = new CompatibilityTestWorkspace();
    }

    [TearDown]
    public void TearDown()
    {
        _workspace.Dispose();
    }

    [Test]
    public async Task Normal_package_builds_and_runs_a_generated_mapper()
    {
        var packageVersion =
            $"0.0.0-compatibility.{Guid.NewGuid():N}";

        var pack = await _workspace.PackMorphant(packageVersion);
        AssertSucceeded(pack);

        var run = await _workspace.RunPackageConsumer(packageVersion);
        AssertSucceeded(run);
        Assert.That(run.Output, Does.Not.Contain("MORPH000"));
    }

    [Test]
    public async Task Analyzer_without_runtime_reports_MORPH0002()
    {
        var build = await _workspace.BuildConsumer("AnalyzerOnly");

        AssertFailedWith(
            build.Process,
            "MORPH0002",
            "Morphant requires a reference to a compatible runtime library.");
        AssertNoMorphantGeneratedFiles(build.GeneratedDirectory);
    }

    [Test]
    public async Task Runtime_revision_2_reports_MORPH0004()
    {
        var runtime = await _workspace.BuildRuntimeCandidate(
            "RuntimeV2",
            "Morphant.TestRuntimeV2");
        AssertSucceeded(runtime.Process);

        var build = await _workspace.BuildConsumer(
            "MismatchedRuntime",
            runtime.AssemblyPath);

        AssertFailedWith(
            build.Process,
            "MORPH0004",
            "The Morphant runtime is incompatible with this generator: " +
            "the runtime and generator versions do not match.");
        AssertNoMorphantGeneratedFiles(build.GeneratedDirectory);
    }

    [Test]
    public async Task Two_runtime_candidates_report_one_MORPH0003()
    {
        var runtime = await _workspace.BuildRuntimeCandidate(
            "RuntimeV1",
            "Morphant.TestRuntimeV1");
        AssertSucceeded(runtime.Process);

        var build = await _workspace.BuildConsumer(
            "DuplicateRuntime",
            runtime.AssemblyPath);

        AssertFailedWith(
            build.Process,
            "MORPH0003",
            "Multiple Morphant runtime libraries were found. Reference " +
            "exactly one.");
        AssertNoMorphantGeneratedFiles(build.GeneratedDirectory);
    }

    [Test]
    public async Task CSharp8_reports_MORPH0001_and_generates_nothing()
    {
        var build = await _workspace.BuildConsumer("CSharp8");

        AssertFailedWith(
            build.Process,
            "MORPH0001",
            "Morphant requires C# 9.0 or later, but this compilation uses " +
            "C# 8.0.");
        AssertNoMorphantGeneratedFiles(build.GeneratedDirectory);
    }

    [Test]
    public async Task Configuration_changes_severity_but_not_the_gate()
    {
        var warning = await _workspace.BuildConsumer("CSharp8Warning");
        var warningDiagnostics = GetCompilerDiagnostics(
            warning.Process.Output);

        AssertSucceeded(warning.Process);
        Assert.That(warningDiagnostics.Length, Is.EqualTo(1));
        Assert.That(
            warningDiagnostics.Single(),
            Does.Contain("warning MORPH0001"));
        AssertNoMorphantGeneratedFiles(warning.GeneratedDirectory);

        var suppressed = await _workspace.BuildConsumer(
            "CSharp8Suppressed");

        AssertSucceeded(suppressed.Process);
        Assert.That(
            GetCompilerDiagnostics(suppressed.Process.Output),
            Is.Empty);
        AssertNoMorphantGeneratedFiles(suppressed.GeneratedDirectory);
    }

    private static void AssertSucceeded(ProcessResult result)
    {
        Assert.That(
            result.ExitCode,
            Is.EqualTo(0),
            $"dotnet {string.Join(' ', result.Arguments)} failed." +
            Environment.NewLine +
            result.Output);
    }

    private static void AssertFailedWith(
        ProcessResult result,
        string id,
        string message)
    {
        var diagnostics = GetCompilerDiagnostics(result.Output);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Not.EqualTo(0), result.Output);
            Assert.That(diagnostics.Length, Is.EqualTo(1), result.Output);
            Assert.That(diagnostics.Single(), Does.Contain("error " + id));
            Assert.That(diagnostics.Single(), Does.Contain(message));
        });
    }

    private static void AssertNoMorphantGeneratedFiles(string directory)
    {
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(
                directory,
                "Morphant.Generated.*",
                SearchOption.AllDirectories)
            : [];

        Assert.That(files, Is.Empty);
    }

    private static string[] GetCompilerDiagnostics(string output)
    {
        return output
            .Split('\n')
            .TakeWhile(static line =>
                !line.Contains("Build FAILED.", StringComparison.Ordinal) &&
                !line.Contains("Build succeeded.", StringComparison.Ordinal))
            .Where(static line =>
                line.Contains(": error ", StringComparison.Ordinal) ||
                line.Contains(": warning ", StringComparison.Ordinal))
            .Select(static line => line.Trim())
            .ToArray();
    }
}
