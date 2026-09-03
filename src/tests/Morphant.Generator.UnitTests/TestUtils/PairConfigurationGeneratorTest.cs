using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class PairConfigurationGeneratorTest :
    CSharpSourceGeneratorTest<
        TestPairConfigurationGenerator,
        DefaultVerifier>
{
    // The production pipeline binds against its in-memory generated surface.
    // This test-only fallback keeps the final verification compilation valid
    // without adding unrelated surface snapshots to this model category.
    // The generated overloads bind the mapping pair and win overload
    // resolution over this fully generic fallback while the semantic model
    // is built.
    private const string CompilerFallbackSource =
"""
#nullable enable

using Morphant.Context;
using Morphant.Delegates;

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        public static MappingBuilder<TMapper, TSource, TDestination> Construct<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            Construct<TSource, object> construct)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> Construct<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            Construct<TSource, MappingContextMarker, object> construct)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> Resolve<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            Resolve<TSource, TDestination, object> resolve)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> Resolve<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            Resolve<TSource, TDestination, MappingContextMarker, object> resolve)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> ConstructUsing<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            ConstructUsing<TSource, TDestination> construct,
            bool compilerFallback = false)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> ConstructUsing<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            ConstructUsing<TSource, MappingContext, TDestination> construct,
            bool compilerFallback = false)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> ResolveUsing<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            ResolveUsing<TSource, TDestination, TDestination> resolve,
            bool compilerFallback = false)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> ResolveUsing<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            ResolveUsing<TSource, TDestination, MappingContext, TDestination> resolve,
            bool compilerFallback = false)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> Members<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            Members<TSource, object> members)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> Members<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            Members<TSource, TDestination, object> members)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> Members<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            Members<TSource, TDestination, TDestination, object> members)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> Members<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            Members<TSource, TDestination, TDestination, MappingContextMarker, object> members)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> Convert<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            Convert<TSource, TDestination> convert,
            bool compilerFallback = false)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> Convert<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            Convert<TSource, TDestination, TDestination> convert,
            bool compilerFallback = false)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static MappingBuilder<TMapper, TSource, TDestination> Convert<TMapper, TSource, TDestination>(
            this MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>> builder,
            Convert<TSource, TDestination, MappingContext, TDestination> convert,
            bool compilerFallback = false)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

    private readonly LanguageVersion _languageVersion;

    private PairConfigurationGeneratorTest(LanguageVersion languageVersion)
    {
        _languageVersion = languageVersion;
        CompilerDiagnostics = CompilerDiagnostics.Warnings;
        TestState.AdditionalReferences.Add(typeof(TypeMapper<>).Assembly);
    }

    protected override ParseOptions CreateParseOptions()
    {
        return new CSharpParseOptions(
            _languageVersion,
            DocumentationMode.Diagnose);
    }

    public static async Task RunAndAssert(
        LanguageVersion languageVersion,
        string sourceFileContent,
        string expectedSource)
    {
        var test = new PairConfigurationGeneratorTest(languageVersion)
        {
            TestCode = sourceFileContent
        };

        test.TestState.Sources.Add(CompilerFallbackSource);

        test.TestState.GeneratedSources.Add(
        (
            typeof(TestPairConfigurationGenerator),
            "PairConfigurationModel.g.cs",
            GeneratedSourceText.Normalize(expectedSource)
        ));

        await test.RunAsync();
    }

    public static async Task RunAndAssertWithAnalyzerConfig(
        LanguageVersion languageVersion,
        string sourceFileContent,
        string analyzerConfig,
        string expectedSource)
    {
        var test = new PairConfigurationGeneratorTest(languageVersion)
        {
            TestCode = sourceFileContent
        };

        test.TestState.Sources.Add(CompilerFallbackSource);
        test.TestState.AnalyzerConfigFiles.Add(
        (
            "/.globalconfig",
            analyzerConfig
        ));
        test.TestState.GeneratedSources.Add(
        (
            typeof(TestPairConfigurationGenerator),
            "PairConfigurationModel.g.cs",
            GeneratedSourceText.Normalize(expectedSource)
        ));

        await test.RunAsync();
    }

}
