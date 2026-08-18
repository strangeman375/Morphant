using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.TypeMapperGeneration;

internal static class IncludeMembersDiagnosticDescriptors
{
    private const string Category = "Morphant.IncludeMembers";

    public static readonly DiagnosticDescriptor InvalidSelector = Create(
        "MORPH0049",
        "IncludeMembers selector is invalid",
        "IncludeMembers is invalid for mapping '{0}' in mapper '{1}': {2}.");

    public static readonly DiagnosticDescriptor AmbiguousMember = Create(
        "MORPH0050",
        "Included source member is ambiguous",
        "IncludeMembers is ambiguous for mapping '{0}' in mapper '{1}': {2}. " +
        "Remove one of the conflicting scopes.");

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
