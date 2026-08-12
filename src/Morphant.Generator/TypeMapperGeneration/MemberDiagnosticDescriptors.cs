using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MemberDiagnosticDescriptors
{
    private const string Category = "Morphant.Members";

    public static readonly DiagnosticDescriptor InvalidRule = Create(
        "MORPH0040",
        "Member rule is invalid",
        "Member rule for '{0}' in contract '{1}' is invalid: {2}.");

    public static readonly DiagnosticDescriptor RequiredMember = Create(
        "MORPH0041",
        "Required destination member is not initialized",
        "Required destination member '{0}' in contract '{1}' is not " +
        "initialized on reachable paths: {2}.");

    public static readonly DiagnosticDescriptor UnavailableLifecycle = Create(
        "MORPH0042",
        "Member rule cannot be applied",
        "Member rule for '{0}' in contract '{1}' cannot be applied: {2}. " +
        "Reachable paths: {3}.");

    public static readonly DiagnosticDescriptor NullMembersPlan = Create(
        "MORPH0043",
        "Structured member plan is null",
        "Structured member plan for contract '{0}' cannot be null on " +
        "reachable paths: {1}.");

    private static DiagnosticDescriptor Create(
        string id,
        string title,
        string messageFormat)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            messageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: null,
            helpLinkUri: null,
            customTags: []);
    }
}
