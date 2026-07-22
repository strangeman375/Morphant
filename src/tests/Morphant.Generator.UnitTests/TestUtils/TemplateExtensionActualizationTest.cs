using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class TemplateExtensionActualizationTest
{
    private const string SourcePath = "TestCase.cs";
    private const string NewLine = "\r\n";

    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.CSharp9,
        DocumentationMode.Diagnose);

    private static readonly ImmutableArray<MetadataReference>
        DefaultReferences = BuildDefaultReferences();

    public static TemplateExtensionActualizationStep Step(
        string name,
        string source,
        params (string HintName, string Source)[] expectedSources)
    {
        return Step(
            name,
            new[] { SourceFile(SourcePath, source) },
            Array.Empty<MetadataReference>(),
            expectedSources);
    }

    public static TemplateExtensionActualizationStep Step(
        string name,
        string source,
        IReadOnlyCollection<MetadataReference> additionalReferences,
        params (string HintName, string Source)[] expectedSources)
    {
        return Step(
            name,
            new[] { SourceFile(SourcePath, source) },
            additionalReferences,
            expectedSources);
    }

    public static TemplateExtensionActualizationStep Step(
        string name,
        IReadOnlyCollection<TemplateExtensionActualizationSourceFile>
            sourceFiles,
        params (string HintName, string Source)[] expectedSources)
    {
        return Step(
            name,
            sourceFiles,
            Array.Empty<MetadataReference>(),
            expectedSources);
    }

    public static TemplateExtensionActualizationSourceFile SourceFile(
        string path,
        string source)
    {
        return new TemplateExtensionActualizationSourceFile(path, source);
    }

    public static (string HintName, string Source)
        ExpectedGeneratedReferenceExtension(
            string hintNamePart,
            string destinationType,
            string templateResultType,
            string? existingDestinationType = null)
    {
        return ExpectedExtension(
            hintNamePart,
            destinationType,
            existingDestinationType ?? destinationType + "?",
            templateResultType);
    }

    public static (string HintName, string Source)
        ExpectedDirectExtension(
            string hintNamePart,
            string destinationType,
            string existingDestinationType)
    {
        return ExpectedExtension(
            hintNamePart,
            destinationType,
            existingDestinationType,
            destinationType);
    }

    public static (string HintName, string Source)
        ExpectedGeneratedValueExtension(
            string hintNamePart,
            string destinationType,
            string templateResultType)
    {
        return ExpectedExtension(
            hintNamePart,
            destinationType,
            destinationType,
            templateResultType);
    }

    public static PortableExecutableReference CreateReference(
        string assemblyName,
        string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[]
            {
                ParseSource(source, assemblyName + ".cs")
            },
            DefaultReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        Assert.That(
            emitResult.Diagnostics
                .Where(static diagnostic =>
                    diagnostic.Severity == DiagnosticSeverity.Error),
            Is.Empty,
            $"Reference '{assemblyName}' must compile successfully.");

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    public static void RunAndAssert(
        params TemplateExtensionActualizationStep[] steps)
    {
        Assert.That(steps, Is.Not.Empty);

        var compilation = CreateCompilation(steps[0]);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[]
            {
                new TestTemplateExtensionGenerator().AsSourceGenerator()
            },
            parseOptions: ParseOptions);

        foreach (var step in steps)
        {
            compilation = ApplyStep(compilation, step);

            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics);

            AssertNoErrors(
                step.Name,
                generatorDiagnostics,
                outputCompilation.GetDiagnostics());

            AssertGeneratedSources(
                step,
                driver.GetRunResult());
        }
    }

    private static TemplateExtensionActualizationStep Step(
        string name,
        IReadOnlyCollection<TemplateExtensionActualizationSourceFile>
            sourceFiles,
        IReadOnlyCollection<MetadataReference> additionalReferences,
        params (string HintName, string Source)[] expectedSources)
    {
        return new TemplateExtensionActualizationStep(
            name,
            sourceFiles.ToImmutableArray(),
            additionalReferences.ToImmutableArray(),
            expectedSources
                .Select(static expectedSource =>
                    new ExpectedTemplateExtensionSource(
                        expectedSource.HintName,
                        NormalizeGeneratedSource(expectedSource.Source)))
                .ToImmutableArray());
    }

    private static (string HintName, string Source) ExpectedExtension(
        string hintNamePart,
        string destinationType,
        string existingDestinationType,
        string templateResultType)
    {
        var hintName =
            $"Morphant.Generated.TemplateExtension.{hintNamePart}.g.cs";

        // lang=c#
        var source = $$"""
                       // <auto-generated />
                       #nullable enable

                       namespace Morphant
                       {
                           internal static partial class MorphantGeneratedTemplateExtensions
                           {
                               /// <summary>
                               /// Configures a mapping template.
                               /// </summary>
                               /// <typeparam name="TSource">The source type.</typeparam>
                               /// <param name="builder">The mapping builder to configure.</param>
                               /// <param name="template">
                               /// A lambda expression that receives the source value and describes the mapping.
                               /// </param>
                               /// <returns>The <paramref name="builder"/> instance.</returns>
                               public static global::Morphant.MapperBuilder<TSource, {{destinationType}}> Template<TSource>(
                                   this global::Morphant.MapperBuilder<TSource, {{destinationType}}> builder,
                                   global::System.Func<TSource, {{templateResultType}}> template)
                                   => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

                               /// <summary>
                               /// Configures a mapping template that depends on the destination's previous state.
                               /// </summary>
                               /// <typeparam name="TSource">The source type.</typeparam>
                               /// <param name="builder">The mapping builder to configure.</param>
                               /// <param name="template">
                               /// A lambda expression that receives the source value and the previous destination value
                               /// and describes the mapping.
                               /// </param>
                               /// <returns>The <paramref name="builder"/> instance.</returns>
                               public static global::Morphant.MapperBuilder<TSource, {{destinationType}}> Template<TSource>(
                                   this global::Morphant.MapperBuilder<TSource, {{destinationType}}> builder,
                                   global::System.Func<TSource, {{existingDestinationType}}, {{templateResultType}}> template)
                                   => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
                           }
                       }
                       """;

        return (hintName, source);
    }

    private static CSharpCompilation CreateCompilation(
        TemplateExtensionActualizationStep step)
    {
        return CSharpCompilation.Create(
            "TemplateExtensionActualization",
            references: BuildReferences(step),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static CSharpCompilation ApplyStep(
        CSharpCompilation compilation,
        TemplateExtensionActualizationStep step)
    {
        var sourceFilesByPath = step.SourceFiles.ToDictionary(
            static sourceFile => sourceFile.Path,
            StringComparer.Ordinal);

        foreach (var previousTree in compilation.SyntaxTrees)
        {
            if (!sourceFilesByPath.ContainsKey(previousTree.FilePath))
            {
                compilation = compilation.RemoveSyntaxTrees(previousTree);
            }
        }

        foreach (var sourceFile in step.SourceFiles)
        {
            var previousTree = compilation.SyntaxTrees.SingleOrDefault(
                tree => tree.FilePath == sourceFile.Path);

            if (previousTree is not null &&
                previousTree.GetText().ToString() == sourceFile.Source)
            {
                continue;
            }

            var sourceTree = ParseSource(
                sourceFile.Source,
                sourceFile.Path);

            compilation = previousTree is null
                ? compilation.AddSyntaxTrees(sourceTree)
                : compilation.ReplaceSyntaxTree(previousTree, sourceTree);
        }

        return compilation.WithReferences(BuildReferences(step));
    }

    private static SyntaxTree ParseSource(
        string source,
        string path)
    {
        return CSharpSyntaxTree.ParseText(
            SourceText.From(source, Encoding.UTF8),
            ParseOptions,
            path);
    }

    private static ImmutableArray<MetadataReference> BuildReferences(
        TemplateExtensionActualizationStep step)
    {
        return DefaultReferences.AddRange(step.AdditionalReferences);
    }

    private static ImmutableArray<MetadataReference>
        BuildDefaultReferences()
    {
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");

        var referencePaths = trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Append(typeof(TypeMapper).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return referencePaths
            .Select(static path =>
                MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();
    }

    private static void AssertNoErrors(
        string stepName,
        params IEnumerable<Diagnostic>[] diagnosticGroups)
    {
        var errors = diagnosticGroups
            .SelectMany(static diagnostics => diagnostics)
            .Where(static diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            errors,
            Is.Empty,
            $"Step '{stepName}' must compile without errors.");
    }

    private static void AssertGeneratedSources(
        TemplateExtensionActualizationStep step,
        GeneratorDriverRunResult runResult)
    {
        var generatorResult = runResult.Results.Single();

        Assert.That(
            generatorResult.Exception,
            Is.Null,
            $"Step '{step.Name}' must not throw from the generator.");

        var actualSources = generatorResult.GeneratedSources
            .OrderBy(static generatedSource =>
                generatedSource.HintName,
                StringComparer.Ordinal)
            .ToArray();

        var expectedSources = step.ExpectedSources
            .OrderBy(static expectedSource =>
                expectedSource.HintName,
                StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            actualSources.Select(static source => source.HintName),
            Is.EqualTo(
                expectedSources.Select(static source => source.HintName)),
            $"Step '{step.Name}' generated an unexpected file set.");

        for (var i = 0; i < expectedSources.Length; i++)
        {
            Assert.That(
                actualSources[i].SourceText.ToString(),
                Is.EqualTo(expectedSources[i].Source),
                $"Step '{step.Name}', file " +
                $"'{expectedSources[i].HintName}'.");
        }
    }

    private static string NormalizeGeneratedSource(string source)
    {
        var normalized = source
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\n", NewLine);

        return normalized.EndsWith(NewLine, StringComparison.Ordinal)
            ? normalized
            : normalized + NewLine;
    }
}

internal sealed record TemplateExtensionActualizationStep(
    string Name,
    ImmutableArray<TemplateExtensionActualizationSourceFile> SourceFiles,
    ImmutableArray<MetadataReference> AdditionalReferences,
    ImmutableArray<ExpectedTemplateExtensionSource> ExpectedSources);

internal sealed record TemplateExtensionActualizationSourceFile(
    string Path,
    string Source);

internal sealed record ExpectedTemplateExtensionSource(
    string HintName,
    string Source);
