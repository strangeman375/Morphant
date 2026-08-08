using Microsoft.CodeAnalysis;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ManualConvertMappingPlanner
{
    private const string UnsupportedConvertMessage =
        "The configured Convert is not supported.";

    public static ManualConvertMappingResult Build(
        ConvertConfigurationModel configuration,
        INamedTypeSymbol mapperType,
        HashSet<string> usedGeneratedMethodNames,
        CancellationToken cancellationToken)
    {
        var hasPrevious = configuration.Form is
            ConvertConfigurationForm.SourceAndPrevious or
            ConvertConfigurationForm.SourcePreviousAndContext;
        var hasContext = configuration.Form ==
            ConvertConfigurationForm.SourcePreviousAndContext;
        var helperMethodName = UserResultMappingPlanner.AllocateName(
            "__ConvertDestination",
            usedGeneratedMethodNames);
        var method = RuntimeCallbackMethodPlanner.Build(
            configuration.Expression,
            hasPrevious,
            hasContext,
            mapperType,
            helperMethodName,
            cancellationToken);

        if (method is null)
        {
            usedGeneratedMethodNames.Remove(helperMethodName);
            return ManualConvertMappingResult.Unsupported(
                UnsupportedConvertMessage);
        }

        return new ManualConvertMappingResult(
            method.Value.HelperMethodName,
            method.Value.HelperMethodDeclaration,
            configuration.Form,
            UnsupportedMessage: null);
    }
}

internal readonly record struct ManualConvertMappingResult(
    string? HelperMethodName,
    string? HelperMethodDeclaration,
    ConvertConfigurationForm Form,
    string? UnsupportedMessage)
{
    public static ManualConvertMappingResult Unsupported(string message) =>
        new(
            HelperMethodName: null,
            HelperMethodDeclaration: null,
            Form: default,
            UnsupportedMessage: message);
}
