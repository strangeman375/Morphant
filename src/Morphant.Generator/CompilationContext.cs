using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator;

internal sealed record CompilationContext
(
    CSharpCompilation Compilation,
    LanguageVersion LanguageVersion,
    KnownSymbols? KnownSymbols
);
