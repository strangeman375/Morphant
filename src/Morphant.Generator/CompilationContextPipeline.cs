using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

                return new CompilationContext(
                    (CSharpCompilation)compilation,
                    ((CSharpParseOptions)parseOptions).LanguageVersion,
                    KnownSymbols.TryCreate(compilation));
            })
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildCompilationContext);
    }
}
