using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.CompatibilityDiagnosticsTests;

[TestFixture]
internal sealed class LibraryCompatibilityTests
{
    private const string RuntimeName = "TestRuntime";

    [TestCase(false)]
    [TestCase(true)]
    public void Reports_a_mapper_library_built_with_an_older_minor_line(
        bool projectReference)
    {
        var runtime = Runtime("0.6.0.0");
        var library = MapperLibrary("0.5.0.0", projectReference);
        var withoutLibrary = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            sources: [CompatibilityGeneratorTest.MapperSource],
            references: [runtime]);
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            sources: [CompatibilityGeneratorTest.MapperSource],
            references: [runtime, library]);

        CompatibilityGeneratorTest.AssertDiagnostics(withoutLibrary);
        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible("MappingLibrary", "0.5", "0.6"));
        Assert.That(GeneratedSources(withoutLibrary), Is.Not.Empty);
        Assert.That(
            GeneratedSources(result),
            Is.EqualTo(GeneratedSources(withoutLibrary)));
    }

    [TestCase("0.6.0.0", "0.6.0.0", null)]
    [TestCase("0.6.0.0", "0.6.3.0", null)]
    [TestCase(
        "0.5.0.0",
        "0.6.0.0",
        "Assembly 'UsageLibrary' was built with Morphant 0.5, which is incompatible with Morphant 0.6 used by this project.")]
    [TestCase(
        "0.7.0.0",
        "0.6.0.0",
        "Assembly 'UsageLibrary' was built with Morphant 0.7, which is incompatible with Morphant 0.6 used by this project.")]
    [TestCase(
        "0.9.0.0",
        "1.0.0.0",
        "Assembly 'UsageLibrary' was built with Morphant 0.9, which is incompatible with Morphant 1.0 used by this project.")]
    [TestCase("1.2.0.0", "1.3.0.0", null)]
    [TestCase("1.3.0.0", "1.3.0.0", null)]
    [TestCase(
        "1.4.0.0",
        "1.3.0.0",
        "Assembly 'UsageLibrary' was built with Morphant 1.4, which is incompatible with Morphant 1.3 used by this project.")]
    [TestCase(
        "1.9.0.0",
        "2.0.0.0",
        "Assembly 'UsageLibrary' was built with Morphant 1.9, which is incompatible with Morphant 2.0 used by this project.")]
    public void Compares_the_library_line_with_the_runtime_line(
        string libraryRuntimeVersion,
        string runtimeVersion,
        string? expectedMessage)
    {
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references:
            [
                Runtime(runtimeVersion),
                UsageLibrary("UsageLibrary", libraryRuntimeVersion)
            ]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            expectedMessage is null
                ? []
                : [new ExpectedCompatibilityDiagnostic("MORPH0063", expectedMessage)]);
    }

    [Test]
    public void Reports_each_incompatible_library_once_in_name_order()
    {
        var unrelated = CompatibilityGeneratorTest.CreateReference(
            "Unrelated",
"""
#nullable enable
#pragma warning disable CS1591

namespace Unrelated
{
    public sealed class Placeholder
    {
    }
}
""");
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references:
            [
                Runtime("0.6.0.0"),
                UsageLibrary("Beta", "0.4.0.0"),
                UsageLibrary("Gamma", "0.6.0.0"),
                UsageLibrary("Alpha", "0.5.0.0"),
                unrelated
            ]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible("Alpha", "0.5", "0.6"),
            Incompatible("Beta", "0.4", "0.6"));
    }

    [Test]
    public void Checks_libraries_only_against_a_compatible_runtime()
    {
        var library = UsageLibrary("UsageLibrary", "0.5.0.0");
        var missing = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [library]);
        var incompatible = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [Runtime("0.6.0.0", revision: "1"), library]);
        var olderLanguage = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp8,
            references: [Runtime("0.6.0.0"), library]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            missing,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0002",
                "Morphant requires a reference to a compatible runtime library."));
        CompatibilityGeneratorTest.AssertDiagnostics(
            incompatible,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0004",
                "The Morphant runtime is incompatible with this generator: " +
                "the runtime and generator versions do not match."));
        CompatibilityGeneratorTest.AssertDiagnostics(
            olderLanguage,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0001",
                "Morphant requires C# 9.0 or later, but this compilation uses C# 8.0."),
            Incompatible("UsageLibrary", "0.5", "0.6"));
    }

    [Test]
    public void Actualizes_the_diagnostic_when_the_library_is_rebuilt()
    {
        var runtime = Runtime("0.6.0.0");
        var clean = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            sources: [CompatibilityGeneratorTest.MapperSource],
            references: [runtime, MapperLibrary("0.6.0.0", false)]);
        var stale = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            sources: [CompatibilityGeneratorTest.MapperSource],
            references: [runtime, MapperLibrary("0.5.0.0", false)],
            driver: clean.Driver);
        var rebuilt = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            sources: [CompatibilityGeneratorTest.MapperSource],
            references: [runtime, MapperLibrary("0.6.0.0", false)],
            driver: stale.Driver);

        CompatibilityGeneratorTest.AssertDiagnostics(clean);
        CompatibilityGeneratorTest.AssertDiagnostics(
            stale,
            Incompatible("MappingLibrary", "0.5", "0.6"));
        CompatibilityGeneratorTest.AssertDiagnostics(rebuilt);
        Assert.That(GeneratedSources(clean), Is.Not.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(
                GeneratedSources(stale),
                Is.EqualTo(GeneratedSources(clean)));
            Assert.That(
                GeneratedSources(rebuilt),
                Is.EqualTo(GeneratedSources(clean)));
        });
    }

    [Test]
    public void Runtime_assembly_version_is_the_package_minor_line()
    {
        var runtime = typeof(TypeMapper<>).Assembly;
        var packageVersion = runtime
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        var line = Version.Parse(
            Regex.Match(packageVersion, @"^\d+\.\d+").Value);

        Assert.That(
            runtime.GetName().Version,
            Is.EqualTo(new Version(line.Major, line.Minor, 0, 0)));
    }

    private static PortableExecutableReference Runtime(
        string version,
        string revision = "2")
    {
        return RuntimeContractFixture.Compatible()
            .WithRevision(revision)
            .WithAssemblyVersion(version)
            .CreateReference(RuntimeName);
    }

    private static MetadataReference MapperLibrary(
        string runtimeVersion,
        bool projectReference)
    {
        var library = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            sources: [LibraryMapperSource],
            references: [Runtime(runtimeVersion)],
            assemblyName: "MappingLibrary");

        CompatibilityGeneratorTest.AssertDiagnostics(library);
        Assert.That(library.GeneratedSources, Is.Not.Empty);

        return projectReference
            ? library.OutputCompilation.ToMetadataReference()
            : CompatibilityGeneratorTest.EmitReference(
                library.OutputCompilation);
    }

    private static PortableExecutableReference UsageLibrary(
        string assemblyName,
        string runtimeVersion)
    {
        return CompatibilityGeneratorTest.CreateReference(
            assemblyName,
            RuntimeUsageSource,
            [Runtime(runtimeVersion)]);
    }

    private static ExpectedCompatibilityDiagnostic Incompatible(
        string library,
        string libraryLine,
        string runtimeLine)
    {
        return new ExpectedCompatibilityDiagnostic(
            "MORPH0063",
            $"Assembly '{library}' was built with Morphant {libraryLine}, " +
            $"which is incompatible with Morphant {runtimeLine} used by " +
            "this project.");
    }

    private static (string HintName, string Source)[] GeneratedSources(
        CompatibilityGeneratorResult result)
    {
        return result.GeneratedSources
            .Select(static source =>
                (source.HintName, source.SourceText.ToString()))
            .OrderBy(static source => source.HintName, StringComparer.Ordinal)
            .ToArray();
    }

    private const string LibraryMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace MappingLibrary
{
    public sealed class Order
    {
        public int Id { get; set; }
    }

    public sealed class OrderDto
    {
        public int Id { get; set; }
    }

    [MorphantMapper]
    public partial class LibraryMapper : TypeMapper<LibraryMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Order, OrderDto>();
        }
    }
}
""";

    private const string RuntimeUsageSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace RuntimeUsage
{
    public static class MapperAccess
    {
        public static Morphant.IMapper? Mapper { get; set; }
    }
}
""";
}
