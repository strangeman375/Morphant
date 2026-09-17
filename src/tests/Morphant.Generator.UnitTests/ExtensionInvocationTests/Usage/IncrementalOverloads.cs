using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ExtensionInvocationTests.Usage;

internal sealed partial class ExtensionInvocationTests
{
    [TestCase(LanguageVersion.CSharp9, "ExtensionCases", "")]
    [TestCase(LanguageVersion.CSharp10, "ExtensionCases", "")]
    [TestCase(LanguageVersion.CSharp9, "ExtensionCases.Mappers", "")]
    [TestCase(LanguageVersion.CSharp10, "ExtensionCases.Mappers", "")]
    [TestCase(LanguageVersion.CSharp9, "", "")]
    [TestCase(LanguageVersion.CSharp10, "", "")]
    [TestCase(LanguageVersion.CSharp10, "ImportedGlobally", "global using ImportedGlobally;")]
    [TestCase(LanguageVersion.CSharp10, "ImportedGlobally", "global using static ImportedGlobally.CloserOperations;")]
    public void External_overload_edits_reconsider_extension_calls_without_changing_the_mapper_or_language(
        LanguageVersion version, string scope, string imports)
    {
        const string closer = """
    public static class CloserOperations
    {
        public static int Describe(this string value) => 99;
    }
""";
        var mapperSource = SourceScopeSource.Replace(closer, string.Empty, StringComparison.Ordinal);
        Assert.That(mapperSource, Is.Not.EqualTo(SourceScopeSource));
        var mapperFile = new GeneratorTestSourceFile("Mapper.cs", mapperSource);
        var conflicting = "#nullable enable\n#pragma warning disable CS1591\n" + imports + "\n" +
            (scope.Length == 0 ? closer : "namespace " + scope + "\n{\n" + closer + "\n}");
        var independent = conflicting.Replace("Describe(", "Other(", StringComparison.Ordinal);
        var absent = conflicting.Replace(closer,
            imports.Contains("using static", StringComparison.Ordinal) ? "public static class CloserOperations {}" : "",
            StringComparison.Ordinal);
        var initial = GeneratorTestDriver.Run("ExtensionInvocation",
            new[] { mapperFile, new GeneratorTestSourceFile("Overloads.cs", absent) }, version);
        var before = version == LanguageVersion.CSharp9 ? IncrementalOverloads9Sources : IncrementalOverloads10Sources;
        var after = version == LanguageVersion.CSharp9 ? SourceScope9Sources : SourceScope10Sources;
        var driver = initial.Driver;
        var compilation = initial.OutputCompilation.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        var mapperTree = compilation.SyntaxTrees.Single(tree => tree.FilePath == "Mapper.cs");
        var overloadTree = compilation.SyntaxTrees.Single(tree => tree.FilePath == "Overloads.cs");
        VerifyIncrementalExtensions(driver, initial.OutputCompilation, before);
        var edits = new[] { (conflicting, after), (absent, before), (independent, before), (conflicting, after), (independent, before) };
        foreach (var (source, expected) in edits)
        {
            var updatedTree = overloadTree.WithChangedText(SourceText.From(source, Encoding.UTF8));
            compilation = compilation.ReplaceSyntaxTree(overloadTree, updatedTree);
            overloadTree = updatedTree;
            Assert.That(compilation.SyntaxTrees.Single(tree => tree.FilePath == "Mapper.cs"), Is.SameAs(mapperTree));
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Assert.That(diagnostics, Is.Empty);
            VerifyIncrementalExtensions(driver, output, expected);
        }

        if (imports.Length != 0)
        {
            // Changing only a global import must also invalidate the generated
            // lookup while the original namespace-local using still wins.
            foreach (var (source, expected) in new[]
                     {
                         (conflicting.Replace(imports, "", StringComparison.Ordinal), before),
                         (conflicting, after),
                         (conflicting.Replace(imports, "", StringComparison.Ordinal), before)
                     })
            {
                var updatedTree = overloadTree.WithChangedText(SourceText.From(source, Encoding.UTF8));
                compilation = compilation.ReplaceSyntaxTree(overloadTree, updatedTree);
                overloadTree = updatedTree;
                Assert.That(compilation.SyntaxTrees.Single(tree => tree.FilePath == "Mapper.cs"), Is.SameAs(mapperTree));
                driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
                Assert.That(diagnostics, Is.Empty);
                VerifyIncrementalExtensions(driver, output, expected);
            }
        }
    }

    private static void VerifyIncrementalExtensions(GeneratorDriver driver, Compilation output, (string Hint, string Source)[] expected)
    {
        var result = driver.GetRunResult().Results.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error), Is.Empty);
            Assert.That(result.GeneratedSources.Select(item => (item.HintName, item.SourceText.ToString())),
                Is.EquivalentTo(expected.Select(item => (item.Hint, GeneratedSourceText.Normalize(item.Source)))));
        });
    }

    private static readonly (string Hint, string Source)[] IncrementalOverloads9Sources =
    [
        ("Morphant.Generated.MappingExtension.Mapper.StringToInt32__68e3eae43b0452f6e902974d4c0eaa7a.g.cs",
            // lang=c#
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        /// <summary>
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> ConstructUsing(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
            global::Morphant.Delegates.ConstructUsing<string, int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> ConstructUsing(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
            global::Morphant.Delegates.ConstructUsing<string, global::Morphant.Context.MappingContext, int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> ResolveUsing(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
            global::Morphant.Delegates.ResolveUsing<string, int, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> ResolveUsing(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
            global::Morphant.Delegates.ResolveUsing<string, int, global::Morphant.Context.MappingContext, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> Convert(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
            global::Morphant.Delegates.Convert<string?, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> Convert(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
            global::Morphant.Delegates.Convert<string?, int, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> Convert(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
            global::Morphant.Delegates.Convert<string?, int, global::Morphant.Context.MappingContext, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
"""),
        ("Morphant.Generated.TypeMapper.Mapper__f34b12dab1f8727313c364da14516bae.g.cs",
            // lang=c#
"""
// <auto-generated />
#nullable enable

using static global::Imported.Operations;

namespace ExtensionCases.Mappers
{
    public partial class Mapper :
        global::Morphant.ITypeMapper<string, int>
    {
        /// <inheritdoc/>
        protected override bool Supports(
            global::System.Type sourceType,
            global::System.Type destinationType) =>
                (sourceType == typeof(string) &&
                    destinationType == typeof(int)) ||
                base.Supports(sourceType, destinationType);

        /// <inheritdoc/>
        int global::Morphant.ITypeMapper<string, int>.Create(
            string? source,
            global::Morphant.Context.MappingContext context)
            => __ConvertDestination(source);

        /// <inheritdoc/>
        int global::Morphant.ITypeMapper<string, int>.Update(
            string? source,
            int destination,
            global::Morphant.Context.MappingContext context)
            => __ConvertDestination(source);

        private int __ConvertDestination(string? source) => source!.Describe() + source!.Length.Independent();
    }
}
"""),
    ];

    private static readonly (string Hint, string Source)[] IncrementalOverloads10Sources =
    [
        ("Morphant.Generated.MappingExtension.Mapper.StringToInt32__68e3eae43b0452f6e902974d4c0eaa7a.g.cs",
            // lang=c#
"""
// <auto-generated />
#nullable enable

namespace Morphant;

internal static partial class MorphantGeneratedMappingExtensions
{
    /// <summary>
    /// Creates a destination through a callback only when none exists.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> ConstructUsing(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
        global::Morphant.Delegates.ConstructUsing<string, int> construct)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Creates a destination through a callback only when none exists.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> ConstructUsing(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
        global::Morphant.Delegates.ConstructUsing<string, global::Morphant.Context.MappingContext, int> construct)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Chooses the destination through a callback on Create and Update.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> ResolveUsing(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
        global::Morphant.Delegates.ResolveUsing<string, int, int> resolve)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Chooses the destination through a callback on Create and Update.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> ResolveUsing(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
        global::Morphant.Delegates.ResolveUsing<string, int, global::Morphant.Context.MappingContext, int> resolve)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Maps with ordinary C#, bypassing null policies and member rules.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> Convert(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
        global::Morphant.Delegates.Convert<string?, int> mapping)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Maps with ordinary C#, bypassing null policies and member rules.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> Convert(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
        global::Morphant.Delegates.Convert<string?, int, int> mapping)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Maps with ordinary C#, bypassing null policies and member rules.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> Convert(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mappers.Mapper, string, int> builder,
        global::Morphant.Delegates.Convert<string?, int, global::Morphant.Context.MappingContext, int> mapping)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
}
"""),
        ("Morphant.Generated.TypeMapper.Mapper__f34b12dab1f8727313c364da14516bae.g.cs",
            // lang=c#
"""
// <auto-generated />
#nullable enable

using static global::Imported.Operations;

namespace ExtensionCases.Mappers;

public partial class Mapper :
    global::Morphant.ITypeMapper<string, int>
{
    /// <inheritdoc/>
    protected override bool Supports(
        global::System.Type sourceType,
        global::System.Type destinationType) =>
            (sourceType == typeof(string) &&
                destinationType == typeof(int)) ||
            base.Supports(sourceType, destinationType);

    /// <inheritdoc/>
    int global::Morphant.ITypeMapper<string, int>.Create(
        string? source,
        global::Morphant.Context.MappingContext context)
        => __ConvertDestination(source);

    /// <inheritdoc/>
    int global::Morphant.ITypeMapper<string, int>.Update(
        string? source,
        int destination,
        global::Morphant.Context.MappingContext context)
        => __ConvertDestination(source);

    private int __ConvertDestination(string? source) => source!.Describe() + source!.Length.Independent();
}
"""),
    ];
}
