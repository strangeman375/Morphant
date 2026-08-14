namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class MappingCompletenessDiagnosticsTests
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
    public void Suppressed_warnings_do_not_change_create_or_update_behavior()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .MappingCompleteness_a11ce011.Scenario.Verify();
    }

    [Test]
    public async Task Msbuild_setting_and_editorconfig_configure_both_diagnostics()
    {
        var build = await _workspace.BuildConsumer(
            "MappingCompletenessOverrides");
        var diagnostics = GetCompilerDiagnostics(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(1),
                build.Process.Output);
            Assert.That(
                diagnostics.Select(GetDiagnosticId),
                Is.EqualTo(new[] { "MORPH0047", "MORPH0048" }));
            Assert.That(diagnostics[0], Does.Contain("warning MORPH0047"));
            Assert.That(diagnostics[1], Does.Contain("error MORPH0048"));
            Assert.That(
                Directory.GetFiles(
                    build.GeneratedDirectory,
                    "*.cs",
                    SearchOption.AllDirectories),
                Is.Not.Empty);
        });
    }

    [Test]
    public async Task Resolves_every_validation_value_and_configuration_level()
    {
        var build = await _workspace.BuildConsumer(
            "MappingCompletenessMatrix");
        var diagnostics = GetCompilerDiagnostics(build.Process.Output);
        var sourceDiagnostics = diagnostics
            .Where(static diagnostic =>
                GetDiagnosticId(diagnostic) == "MORPH0047")
            .ToArray();
        var destinationDiagnostics = diagnostics
            .Where(static diagnostic =>
                GetDiagnosticId(diagnostic) == "MORPH0048")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(0),
                build.Process.Output);
            Assert.That(sourceDiagnostics, Has.Length.EqualTo(3));
            Assert.That(destinationDiagnostics, Has.Length.EqualTo(4));
            Assert.That(
                sourceDiagnostics,
                Has.Some.Contains("AssemblyUnused"));
            Assert.That(
                sourceDiagnostics,
                Has.Some.Contains("RootUnused"));
            Assert.That(
                sourceDiagnostics,
                Has.Some.Contains("DefaultUnused"));
            Assert.That(
                destinationDiagnostics,
                Has.Some.Contains("AssemblyUnmapped"));
            Assert.That(
                destinationDiagnostics,
                Has.Some.Contains("PairUnmapped"));
            Assert.That(
                destinationDiagnostics,
                Has.Some.Contains("IncludedUnmapped"));
            Assert.That(
                destinationDiagnostics,
                Has.Some.Contains("BaseUnmapped"));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
            Assert.That(
                Directory.GetFiles(
                    build.GeneratedDirectory,
                    "*.cs",
                    SearchOption.AllDirectories),
                Is.Not.Empty);
        });
    }

    [Test]
    public async Task Library_default_disables_completeness_validation()
    {
        var build = await _workspace.BuildConsumer(
            "MappingCompletenessDefault");
        var diagnostics = GetCompilerDiagnostics(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(0),
                build.Process.Output);
            Assert.That(
                diagnostics.Where(static diagnostic =>
                    GetDiagnosticId(diagnostic) is
                        "MORPH0047" or "MORPH0048"),
                Is.Empty);
            Assert.That(
                Directory.GetFiles(
                    build.GeneratedDirectory,
                    "*.cs",
                    SearchOption.AllDirectories),
                Is.Not.Empty);
        });
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

    private static string GetDiagnosticId(string diagnostic)
    {
        var marker = diagnostic.Contains(": error ", StringComparison.Ordinal)
            ? ": error "
            : ": warning ";
        var start = diagnostic.IndexOf(marker, StringComparison.Ordinal) +
                    marker.Length;
        var end = diagnostic.IndexOf(':', start);

        return diagnostic[start..end];
    }
}
