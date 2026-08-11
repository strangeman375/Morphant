using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.Compatibility;

namespace Morphant.Generator;

internal static class CompilationContextPipeline
{
    public static IncrementalValueProvider<CompilationContext> Build(IncrementalGeneratorInitializationContext context)
    {
        return context.CompilationProvider
            .Combine(context.ParseOptionsProvider)
            .Select(static (source, _) =>
            {
                var (compilation, parseOptions) = source;
                var cSharpCompilation = (CSharpCompilation)compilation;
                var languageVersion =
                    ((CSharpParseOptions)parseOptions).LanguageVersion;
                var compatibility =
                    CompilationCompatibilityDetector.Detect(
                        cSharpCompilation,
                        languageVersion);

                return new CompilationContext(
                    cSharpCompilation,
                    languageVersion,
                    compatibility,
                    compatibility.CanGenerate
                        ? compatibility.KnownSymbols
                        : null);
            })
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildCompilationContext);
    }
}
