using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.Compatibility;

internal sealed record CompilationCompatibility(
    bool IsLanguageCompatible,
    RuntimeContractCompatibility RuntimeContract,
    KnownSymbols? KnownSymbols)
{
    public bool CanGenerate =>
        IsLanguageCompatible &&
        RuntimeContract.Kind == RuntimeContractCompatibilityKind.Compatible &&
        KnownSymbols is not null;

    public ImmutableArray<Diagnostic> CreateDiagnostics(
        LanguageVersion languageVersion)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>(2);

        if (!IsLanguageCompatible)
        {
            diagnostics.Add(Diagnostic.Create(
                CompatibilityDiagnosticDescriptors.UnsupportedLanguageVersion,
                Location.None,
                languageVersion.ToDisplayString()));
        }

        switch (RuntimeContract.Kind)
        {
            case RuntimeContractCompatibilityKind.Missing:
                diagnostics.Add(Diagnostic.Create(
                    CompatibilityDiagnosticDescriptors.RuntimeContractNotFound,
                    Location.None));
                break;

            case RuntimeContractCompatibilityKind.Ambiguous:
                diagnostics.Add(Diagnostic.Create(
                    CompatibilityDiagnosticDescriptors.AmbiguousRuntimeContract,
                    Location.None));
                break;

            case RuntimeContractCompatibilityKind.Incompatible:
                diagnostics.Add(Diagnostic.Create(
                    CompatibilityDiagnosticDescriptors.IncompatibleRuntimeContract,
                    Location.None,
                    RuntimeContract.Reason));
                break;

            case RuntimeContractCompatibilityKind.Compatible:
                break;

            default:
                throw new InvalidOperationException(
                    "The runtime compatibility result is invalid.");
        }

        return diagnostics.ToImmutable();
    }
}

internal sealed record RuntimeContractCompatibility(
    RuntimeContractCompatibilityKind Kind,
    string? Reason = null);

internal enum RuntimeContractCompatibilityKind
{
    Compatible,
    Missing,
    Ambiguous,
    Incompatible
}
