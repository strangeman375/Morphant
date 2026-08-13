using Microsoft.CodeAnalysis;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ManualConvertMappingPlanner
{
    private const string UnsupportedConvertMessage =
        "This Convert function is not supported.";

    public static ManualConvertMappingResult Build(
        ConvertConfigurationModel configuration,
        MappingAnalysisContext analysisContext,
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
                MappingFailureObservation.Create(
                    analysisContext,
                    MappingFailureReason.UnsupportedRuntimeCallback,
                    UnsupportedConvertMessage,
                    MappingObservationOriginKind.Callback,
                    MappingAffectedPath.All(MappingPlanPhase.Transfer),
                    configuration.Invocation,
                    configuration.Expression.DeclaringMapperType));
        }

        return new ManualConvertMappingResult(
            method.Value.HelperMethodName,
            method.Value.HelperMethodDeclaration,
            configuration.Form,
            Failure: null);
    }
}

internal readonly record struct ManualConvertMappingResult(
    string? HelperMethodName,
    string? HelperMethodDeclaration,
    ConvertConfigurationForm Form,
    MappingFailureObservation? Failure)
{
    public static ManualConvertMappingResult Unsupported(
        MappingFailureObservation failure) =>
        new(
            HelperMethodName: null,
            HelperMethodDeclaration: null,
            Form: default,
            Failure: failure);
}
