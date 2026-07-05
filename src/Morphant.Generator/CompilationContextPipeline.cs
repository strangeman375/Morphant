using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator;

internal static class CompilationContextPipeline
{
    public static IncrementalValueProvider<CompilationContext> Build(IncrementalGeneratorInitializationContext context)
    {
        return context.CompilationProvider
            .Combine(context.ParseOptionsProvider)
            .Select(static (x, _) =>
            {
                var (compilation, parseOptions) = x;
                return new CompilationContext((CSharpCompilation)compilation, ((CSharpParseOptions)parseOptions).LanguageVersion, new KnownSymbols(compilation));
            })
            .WithTrackingName(MorphantGeneratorStageNames.BuildCompilationContext);
    }
}
