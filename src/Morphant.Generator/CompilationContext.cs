using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.Compatibility;

namespace Morphant.Generator;

internal sealed record CompilationContext
(
    CSharpCompilation Compilation,
    LanguageVersion LanguageVersion,
    CompilationCompatibility Compatibility,
    KnownSymbols? KnownSymbols
);
