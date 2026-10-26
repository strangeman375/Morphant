namespace Morphant.Generator.IntegrationTests.PackageConsumptionTests;

[TestFixture]
[NonParallelizable]
internal sealed partial class PackageConsumptionTests
{
    private ConsumerBuildWorkspace _packageWorkspace = null!;
    private string _testDirectory = null!;

    private static string RepositoryRoot => IntegrationTestEnvironment.RepositoryRoot;
    private static string Configuration => IntegrationTestEnvironment.BuildConfiguration;
    private string PackageVersion { get; } = $"0.0.0-package-consumption.{Guid.NewGuid():N}";
    private string PackageFeed => _packageWorkspace.PackageFeed;
    private string ConsumerDirectory => Path.Combine(_testDirectory, "consumer");
    private string ConsumerProject => Path.Combine(ConsumerDirectory, "Morphant.Generator.PackageTests.Consumer.csproj");
    private string ConsumerSource => Path.Combine(ConsumerDirectory, "Program.cs");
    private string ConsumerOutput => Path.Combine(_testDirectory, "bin") + Path.DirectorySeparatorChar;
    private string ConsumerIntermediate => Path.Combine(_testDirectory, "obj") + Path.DirectorySeparatorChar;
    private string CurrentIntermediate => Path.Combine(ConsumerIntermediate, Configuration, "net10.0");
    private string ConsumerGenerated => Path.Combine(ConsumerDirectory, "Generated", "Morphant");
    private string GeneratedDirectory => Path.Combine(ConsumerGenerated, "net10.0");
    private string StaleMorphantFile => Path.Combine(GeneratedDirectory, "Morphant.Generated.Removed.g.cs");
    private string UnrelatedGeneratedFile => Path.Combine(GeneratedDirectory, "Other.Generator.Unrelated.g.cs");
    private string OriginalConsumerSource { get; set; } = null!;

    private string[] BaseConsumerArguments =>
    [
        "run", "--project", ConsumerProject,
        "--configuration", Configuration,
        "-m:1", "-nodeReuse:false",
        $"-p:MorphantTestPackageVersion={PackageVersion}",
        $"-p:RestoreSources={PackageFeed}",
        $"-p:BaseOutputPath={ConsumerOutput}",
        $"-p:BaseIntermediateOutputPath={ConsumerIntermediate}",
        "-p:NuGetAudit=false"
    ];

    private string[] ConsumerArguments => [.. BaseConsumerArguments, "-p:MorphantGitSnapshot=true"];

    [OneTimeSetUp]
    public async Task Pack()
    {
        _packageWorkspace = new ConsumerBuildWorkspace();
        AssertSucceeded(await _packageWorkspace.PackMorphant(PackageVersion));
    }

    [OneTimeTearDown]
    public void DeletePackage() => _packageWorkspace?.Dispose();

    [SetUp]
    public void CreateConsumer()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "Morphant.PackageConsumptionTests", Guid.NewGuid().ToString("N"));
        var sourceDirectory = Path.Combine(RepositoryRoot, "src", "tests", "Morphant.Generator.PackageTests.Consumer");
        Directory.CreateDirectory(ConsumerDirectory);
        File.Copy(Path.Combine(sourceDirectory, Path.GetFileName(ConsumerProject)), ConsumerProject);
        File.Copy(Path.Combine(sourceDirectory, "Program.cs"), ConsumerSource);
        OriginalConsumerSource = File.ReadAllText(ConsumerSource);
    }

    [TearDown]
    public void DeleteConsumer()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    private async Task CreateSnapshot()
    {
        Directory.CreateDirectory(GeneratedDirectory);
        await File.WriteAllTextAsync(UnrelatedGeneratedFile, "// unrelated generator output");
        AssertSucceeded(await DotNetCli.Run(RepositoryRoot, ConsumerArguments));
        AssertGeneratedFileSet(GeneratedDirectory, MapperGeneratedFiles);
    }

    private static readonly string[] MapperGeneratedFiles =
    [
        "Morphant.Generated.TypeMapper.TestMapper__7e86e73f6eb648062a5781f4afc22623.g.cs"
    ];

    private static readonly string[] PrimaryFullGeneratedFiles =
    [
        "Morphant.Generated.Construction.Destination__8178863269c5aa3285fa9684f4ebcf69.g.cs",
        "Morphant.Generated.MappingExtension.TestMapper.SourceToDestination__7d2e9d5139c36ceb1a54369c508fedbb.g.cs",
        "Morphant.Generated.Member.Destination__8178863269c5aa3285fa9684f4ebcf69.g.cs",
        "Morphant.Generated.MemberExtension.TestMapper.SourceToDestination__7d2e9d5139c36ceb1a54369c508fedbb.g.cs",
        "Morphant.Generated.TypeMapper.TestMapper__7e86e73f6eb648062a5781f4afc22623.g.cs"
    ];

    private static readonly string[] BothFullGeneratedFiles =
    [
        .. PrimaryFullGeneratedFiles,
        "Morphant.Generated.Construction.SecondDestination__c628102412d6f474062e97b9d407b734.g.cs",
        "Morphant.Generated.MappingExtension.TestMapper.SecondSourceToSecondDestination__559079e44ffdc362d732d75a6db5c428.g.cs",
        "Morphant.Generated.Member.SecondDestination__c628102412d6f474062e97b9d407b734.g.cs",
        "Morphant.Generated.MemberExtension.TestMapper.SecondSourceToSecondDestination__559079e44ffdc362d732d75a6db5c428.g.cs"
    ];

    private static void AssertGeneratedFileSet(
        string directory,
        IReadOnlyCollection<string> expected)
    {
        var actual = Directory.GetFiles(
                directory,
                "Morphant.Generated.*.g.cs",
                SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            actual,
            Is.EqualTo(expected.Order(StringComparer.Ordinal)),
            "The on-disk generated file set must match the current " +
            "mapping configuration exactly.");

        foreach (var file in actual)
        {
            var bytes = File.ReadAllBytes(Path.Combine(directory, file!));
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var withoutCrLf = text.Replace("\r\n", string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(
                    withoutCrLf,
                    Does.Not.Contain('\n').And.Not.Contain('\r'),
                    $"{file} must use deterministic CRLF line endings.");
            });
        }
    }

    private static Dictionary<string, DateTime> SnapshotWriteTimes(
        string directory)
    {
        return Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .ToDictionary(
                static path => Path.GetFileName(path)!,
                File.GetLastWriteTimeUtc,
                StringComparer.Ordinal);
    }

    private static Dictionary<string, byte[]> SnapshotContents(
        string directory)
    {
        return Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .ToDictionary(
                static path => Path.GetFileName(path)!,
                File.ReadAllBytes,
                StringComparer.Ordinal);
    }

    private static Dictionary<string, string> SnapshotTree(string directory)
    {
        var result = Directory.GetDirectories(
                directory,
                "*",
                SearchOption.AllDirectories)
            .ToDictionary(
                path => "D:" + Path.GetRelativePath(directory, path),
                static _ => string.Empty,
                StringComparer.Ordinal);

        foreach (var file in Directory.GetFiles(
                     directory,
                     "*",
                     SearchOption.AllDirectories))
        {
            result.Add(
                "F:" + Path.GetRelativePath(directory, file),
                Convert.ToBase64String(File.ReadAllBytes(file)));
        }

        return result;
    }

    private static string[] BuildArguments(
        IReadOnlyList<string> runArguments,
        string project)
    {
        return
        [
            "build",
            project,
            .. runArguments.Skip(3)
        ];
    }

    private static string[] RebuildArguments(
        IReadOnlyList<string> runArguments,
        string project) =>
    [
        .. BuildArguments(runArguments, project),
        "-t:Rebuild"
    ];

    private static string[] WithConfiguration(
        IReadOnlyList<string> arguments,
        string configuration)
    {
        var result = arguments.ToArray();
        var option = Array.IndexOf(result, "--configuration");
        Assert.That(option, Is.GreaterThanOrEqualTo(0));
        result[option + 1] = configuration;
        return result;
    }

    private static void AssertSucceeded(ProcessResult result)
    {
        Assert.That(
            result.ExitCode,
            Is.EqualTo(0),
            $"{result.Command} failed.{Environment.NewLine}{result.Output}");
    }

    private static void AssertFailed(ProcessResult result)
    {
        Assert.That(
            result.ExitCode,
            Is.Not.EqualTo(0),
            $"{result.Command} unexpectedly succeeded." +
            $"{Environment.NewLine}{result.Output}");
    }
}
