using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MemberDiagnosticDescriptors
{
    private const string Category = "Morphant.Members";

    public static readonly DiagnosticDescriptor InvalidRule = Create(
        "MORPH0040",
        "Member rule is invalid",
        "Rule for destination member '{0}' is invalid in mapping '{1}': " +
        "{2}.");

    public static readonly DiagnosticDescriptor RequiredMember = Create(
        "MORPH0041",
        "Required destination member is not initialized",
        "Required destination member '{0}' is not initialized in mapping " +
        "'{1}'. Affected cases: {2}.");

    public static readonly DiagnosticDescriptor UnavailableLifecycle = Create(
        "MORPH0042",
        "Member rule cannot be applied",
        "Rule for destination member '{0}' cannot be applied in mapping " +
        "'{1}': {2}. Affected cases: {3}.");

    public static readonly DiagnosticDescriptor NullMembersPlan = Create(
        "MORPH0043",
        "Members returned no plan",
        "Members returned null or default for mapping '{0}'. Affected " +
        "cases: {1}.");

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
            helpLinkUri: DiagnosticHelpLink.For(id),
            customTags: []);
    }
}
