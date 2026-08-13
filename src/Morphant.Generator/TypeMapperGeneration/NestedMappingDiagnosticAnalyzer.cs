using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class NestedMappingDiagnosticAnalyzer
{
    public static ImmutableArray<NestedMappingDiagnosticCandidate> Build(
        TypeMapperModel model,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<
            NestedMappingDiagnosticCandidate>();

        foreach (var mapping in model.Mappings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!CanAnalyze(mapping))
            {
                continue;
            }

            var paths = GetReachablePaths(mapping);

            if (paths == MappingExecutionPathSet.None)
            {
                continue;
            }

            AnalyzeMapping(
                mapping,
                paths,
                result,
                cancellationToken);
        }

        return result.ToImmutable();
    }

    private static void AnalyzeMapping(
        TypeMapperMappingModel mapping,
        MappingExecutionPathSet paths,
        ImmutableArray<NestedMappingDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (mapping.Failure is { } mappingFailure)
        {
            AnalyzeFailure(
                mapping,
                mappingFailure,
                paths,
                result,
                cancellationToken);

            if (!IsNestedFailure(mappingFailure.Reason))
            {
                return;
            }
        }

        if (mapping.ControlFlow is { } controlFlow)
        {
            var noPrevious = paths & MappingExecutionPathSet.NoPrevious;

            if (noPrevious != MappingExecutionPathSet.None)
            {
                AnalyzeNode(
                    controlFlow.CreateRoot,
                    noPrevious,
                    result,
                    cancellationToken);
            }

            var existing = paths &
                MappingExecutionPathSet.UpdateWithPrevious;

            if (existing != MappingExecutionPathSet.None)
            {
                AnalyzeNode(
                    controlFlow.UpdateRoot,
                    existing,
                    result,
                    cancellationToken);
            }

            return;
        }

        AnalyzeFailure(
            mapping,
            mapping.CreateFailure,
            paths & MappingExecutionPathSet.NoPrevious,
            result,
            cancellationToken);
        AnalyzeFailure(
            mapping,
            mapping.UpdateFailure,
            paths & MappingExecutionPathSet.UpdateWithPrevious,
            result,
            cancellationToken);
        AnalyzeMemberNode(
            mapping,
            mapping.PostMemberControlFlow,
            paths,
            result,
            cancellationToken);
    }

    private static void AnalyzeNode(
        TypeMapperControlFlowNode node,
        MappingExecutionPathSet paths,
        ImmutableArray<NestedMappingDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (node.Leaf is { } leaf)
        {
            AnalyzeMapping(
                leaf,
                paths,
                result,
                cancellationToken);
            return;
        }

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            AnalyzeNode(
                evaluationContinuation,
                paths,
                result,
                cancellationToken);
            return;
        }

        if (node.SwitchExpression is not null)
        {
            foreach (var section in node.SwitchSections)
            {
                AnalyzeNode(
                    section.Branch,
                    paths,
                    result,
                    cancellationToken);
            }

            if (node.SwitchContinuation is { } continuation)
            {
                AnalyzeNode(
                    continuation,
                    paths,
                    result,
                    cancellationToken);
            }

            return;
        }

        if (node.Condition is null)
        {
            return;
        }

        AnalyzeNode(
            node.WhenTrue!,
            paths,
            result,
            cancellationToken);
        AnalyzeNode(
            node.WhenFalse!,
            paths,
            result,
            cancellationToken);
    }

    private static void AnalyzeMemberNode(
        TypeMapperMappingModel mapping,
        TypeMapperMemberControlFlowNode? node,
        MappingExecutionPathSet paths,
        ImmutableArray<NestedMappingDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        if (node is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        AnalyzeFailure(
            mapping,
            node.Failure,
            paths,
            result,
            cancellationToken);

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            AnalyzeMemberNode(
                mapping,
                evaluationContinuation,
                paths,
                result,
                cancellationToken);
            return;
        }

        if (node.SwitchExpression is not null)
        {
            foreach (var section in node.SwitchSections)
            {
                AnalyzeMemberNode(
                    mapping,
                    section.Branch,
                    paths,
                    result,
                    cancellationToken);
            }

            AnalyzeMemberNode(
                mapping,
                node.SwitchContinuation,
                paths,
                result,
                cancellationToken);
            return;
        }

        if (node.Condition is null)
        {
            return;
        }

        AnalyzeMemberNode(
            mapping,
            node.WhenTrue,
            paths,
            result,
            cancellationToken);
        AnalyzeMemberNode(
            mapping,
            node.WhenFalse,
            paths,
            result,
            cancellationToken);
    }

    private static void AnalyzeFailure(
        TypeMapperMappingModel mapping,
        MappingFailureObservation? failure,
        MappingExecutionPathSet paths,
        ImmutableArray<NestedMappingDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        if (failure is null ||
            !IsNestedFailure(failure.Reason) ||
            paths == MappingExecutionPathSet.None)
        {
            return;
        }

        var affectedPaths = paths & failure.AffectedPath.Paths;

        if (affectedPaths == MappingExecutionPathSet.None)
        {
            return;
        }

        var observations = failure.NestedObservations.IsDefaultOrEmpty
            ? mapping.NestedObservations
            : failure.NestedObservations;

        foreach (var observation in observations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (observation.FailureKind ==
                    NestedMappingFailureKind.None)
            {
                continue;
            }

            var observationPaths = affectedPaths & observation.Paths;

            if (observationPaths == MappingExecutionPathSet.None)
            {
                continue;
            }

            result.Add(BuildCandidate(
                mapping,
                observation,
                observationPaths,
                cancellationToken));
        }
    }

    private static NestedMappingDiagnosticCandidate BuildCandidate(
        TypeMapperMappingModel mapping,
        NestedMappingObservation observation,
        MappingExecutionPathSet paths,
        CancellationToken cancellationToken)
    {
        var kind = ToDiagnosticKind(observation.FailureKind);
        var marker = observation.ProducerSymbol.Name;
        var contract = MapperContractDisplay.Create(
            mapping.AnalysisContext.SourceType,
            mapping.AnalysisContext.DestinationType);
        var primary = GetPrimaryLocation(observation, kind);
        var additional = BuildAdditionalLocations(
            observation,
            kind,
            primary,
            cancellationToken);
        var reason = BuildReason(observation);
        var nestedDestinationType = Display(
            observation.InferredDestinationType);
        var targetType = Display(observation.TargetType);
        var mapperIdentity = SymbolNameHelper.GetFullMetadataName(
            mapping.AnalysisContext.TargetMapper.OriginalDefinition);
        var sourceMapperIdentity = SymbolNameHelper.GetFullMetadataName(
            observation.SourceMapper.OriginalDefinition);
        var pairKey = mapping.AnalysisContext.Identity.Source.Key + "->" +
            mapping.AnalysisContext.Identity.Destination.Key;
        var originIdentity = sourceMapperIdentity + "|" +
            LocationIdentity(observation.Producer.GetLocation());
        var detail = ((int)observation.FailureKind).ToString() + "|" +
            reason + "|" + nestedDestinationType + "|" + targetType;
        var identity = ((int)kind).ToString() + "|" + mapperIdentity + "|" +
            pairKey + "|" + originIdentity + "|" +
            TargetIdentity(observation) + "|" + detail;

        return new NestedMappingDiagnosticCandidate(
            kind,
            identity,
            mapperIdentity,
            GetLevelOrder(
                mapping.AnalysisContext.TargetMapper,
                observation.SourceMapper),
            pairKey,
            originIdentity,
            primary.SourceSpan.Start,
            detail,
            StringComparer.Ordinal.Equals(
                mapperIdentity,
                sourceMapperIdentity),
            primary,
            additional,
            contract,
            marker,
            reason,
            nestedDestinationType,
            targetType,
            paths);
    }

    private static NestedMappingDiagnosticKind ToDiagnosticKind(
        NestedMappingFailureKind kind)
    {
        return kind switch
        {
            NestedMappingFailureKind.SourceTypeUnknown or
            NestedMappingFailureKind.ParameterlessSourceUnavailable or
            NestedMappingFailureKind.DestinationTypeUnknown =>
                NestedMappingDiagnosticKind.PairUnknown,
            NestedMappingFailureKind.ResultIncompatible =>
                NestedMappingDiagnosticKind.ResultIncompatible,
            _ => NestedMappingDiagnosticKind.UpdateDestinationInvalid
        };
    }

    private static string BuildReason(NestedMappingObservation observation)
    {
        switch (observation.FailureKind)
        {
            case NestedMappingFailureKind.SourceTypeUnknown:
                return observation.InferredDestinationType is null
                    ? "source expression has no compile-time type and the " +
                      "destination type is not specified"
                    : "source expression has no compile-time type";

            case NestedMappingFailureKind.ParameterlessSourceUnavailable:
                return "Map() could not find exactly one " +
                       "readable source member named '" +
                       (observation.TargetName ?? string.Empty) + "'" +
                       (observation.InferredDestinationType is null
                           ? "; the destination type is not specified"
                           : string.Empty);

            case NestedMappingFailureKind.DestinationTypeUnknown:
                return "use Map<TDestination>(...) to specify the " +
                       "destination type";

            case NestedMappingFailureKind.ExplicitDestinationIncompatible:
                return "destination type '" +
                       Display(observation.ExplicitDestinationType) +
                       "' cannot be assigned to '" +
                       Display(observation.InferredDestinationType) + "'";

            case NestedMappingFailureKind
                .ExplicitNullForNonNullableValue:
                return "null cannot be used for non-nullable destination " +
                       "type '" +
                       Display(observation.InferredDestinationType) + "'";

            case NestedMappingFailureKind.AdaptiveCurrentUnavailable:
                return "Map could not find the current destination for '" +
                       (observation.TargetName ?? string.Empty) + "'";

            case NestedMappingFailureKind.AdaptiveCurrentIncompatible:
                return "current destination of type '" +
                       Display(observation.CurrentDestinationType) +
                       "' cannot be used as '" +
                       Display(observation.InferredDestinationType) + "'";

            case NestedMappingFailureKind.AdaptiveCurrentAmbiguous:
                return "this Map call matches more than one current " +
                       "destination: " +
                       string.Join(
                           ", ",
                           observation.AdaptiveLocalTargets
                               .Select(NormalizeAdaptiveTarget)
                               .Distinct(StringComparer.Ordinal));

            case NestedMappingFailureKind.ReadOnlyProxyInvalid:
                return "Update requires a readable reference-type " +
                       "destination member here";

            default:
                return string.Empty;
        }
    }

    private static Location GetPrimaryLocation(
        NestedMappingObservation observation,
        NestedMappingDiagnosticKind kind)
    {
        if (kind == NestedMappingDiagnosticKind.PairUnknown &&
            observation.FailureKind ==
                NestedMappingFailureKind.SourceTypeUnknown &&
            observation.SourceExpression is { } sourceExpression)
        {
            return sourceExpression.GetLocation();
        }

        if (kind == NestedMappingDiagnosticKind.ResultIncompatible &&
            GetGenericTypeArgument(observation.Producer) is { } typeArgument)
        {
            return typeArgument.GetLocation();
        }

        if (kind ==
                NestedMappingDiagnosticKind.UpdateDestinationInvalid &&
            observation.FailureKind is
                NestedMappingFailureKind.ExplicitDestinationIncompatible or
                NestedMappingFailureKind
                    .ExplicitNullForNonNullableValue or
                NestedMappingFailureKind.ReadOnlyProxyInvalid &&
            observation.ExplicitDestination is { } destination)
        {
            return destination.GetLocation();
        }

        return GetInvocationNameLocation(observation.Producer);
    }

    private static ImmutableArray<Location> BuildAdditionalLocations(
        NestedMappingObservation observation,
        NestedMappingDiagnosticKind kind,
        Location primary,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<Location>();

        if (observation.FailureKind ==
                NestedMappingFailureKind.ReadOnlyProxyInvalid)
        {
            AddDistinct(
                result,
                GetInvocationNameLocation(observation.Producer),
                primary);
        }

        if (kind == NestedMappingDiagnosticKind.PairUnknown &&
            observation.InferredDestinationType is null &&
            observation.FailureKind !=
                NestedMappingFailureKind.DestinationTypeUnknown)
        {
            AddDistinct(
                result,
                GetInvocationNameLocation(observation.Producer),
                primary);
        }

        if (observation.FailureKind !=
                NestedMappingFailureKind.AdaptiveCurrentAmbiguous &&
            observation.TargetDesignator is { } targetDesignator)
        {
            AddDistinct(result, targetDesignator.GetLocation(), primary);
        }

        if (observation.FailureKind ==
                NestedMappingFailureKind.AdaptiveCurrentAmbiguous)
        {
            foreach (var designator in
                     observation.AdaptiveTargetDesignators)
            {
                AddDistinct(result, designator.GetLocation(), primary);
            }
        }

        if (observation.TargetSymbol is { } targetSymbol &&
            GetDeclarationLocation(
                targetSymbol,
                cancellationToken) is { } declaration)
        {
            AddDistinct(result, declaration, primary);
        }

        if (kind ==
                NestedMappingDiagnosticKind.UpdateDestinationInvalid &&
            observation.CurrentDestinationSymbol is { } currentSymbol &&
            GetDeclarationLocation(
                currentSymbol,
                cancellationToken) is { } currentDeclaration)
        {
            AddDistinct(result, currentDeclaration, primary);
        }

        if (kind ==
                NestedMappingDiagnosticKind.UpdateDestinationInvalid &&
            GetGenericTypeArgument(observation.Producer) is
                { } destinationType)
        {
            AddDistinct(result, destinationType.GetLocation(), primary);
        }

        return result.ToImmutable();
    }

    private static TypeSyntax? GetGenericTypeArgument(
        InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            GenericNameSyntax generic =>
                generic.TypeArgumentList.Arguments.FirstOrDefault(),
            MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax generic
            } => generic.TypeArgumentList.Arguments.FirstOrDefault(),
            _ => null
        };
    }

    private static Location GetInvocationNameLocation(
        InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            SimpleNameSyntax name => name.Identifier.GetLocation(),
            MemberAccessExpressionSyntax access =>
                access.Name.Identifier.GetLocation(),
            _ => invocation.Expression.GetLocation()
        };
    }

    private static Location? GetDeclarationLocation(
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(cancellationToken);

            return syntax switch
            {
                PropertyDeclarationSyntax property =>
                    property.Identifier.GetLocation(),
                VariableDeclaratorSyntax variable =>
                    variable.Identifier.GetLocation(),
                ParameterSyntax parameter =>
                    parameter.Identifier.GetLocation(),
                _ => syntax.GetLocation()
            };
        }

        return null;
    }

    private static string Display(ITypeSymbol? type)
    {
        return type is null
            ? string.Empty
            : MapperContractDisplay.CreateType(type);
    }

    private static string NormalizeAdaptiveTarget(string target)
    {
        var separator = target.LastIndexOf('.');
        return separator < 0 ? target : target.Substring(separator + 1);
    }

    private static string TargetIdentity(
        NestedMappingObservation observation)
    {
        if (observation.TargetSymbol is { } target)
        {
            return target.ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable);
        }

        return observation.TerminalTarget is { } terminal
            ? terminal.SyntaxTree.FilePath + "|" + terminal.SpanStart + "|" +
              terminal.Span.Length
            : string.Empty;
    }

    private static int GetLevelOrder(
        INamedTypeSymbol mapper,
        INamedTypeSymbol sourceMapper)
    {
        var sourceIdentity = SymbolNameHelper.GetFullMetadataName(
            sourceMapper.OriginalDefinition);
        var order = 0;

        for (var current = mapper;
             current is not null;
             current = current.BaseType)
        {
            if (StringComparer.Ordinal.Equals(
                    SymbolNameHelper.GetFullMetadataName(
                        current.OriginalDefinition),
                    sourceIdentity))
            {
                return order;
            }

            order++;
        }

        return int.MaxValue;
    }

    private static MappingExecutionPathSet GetReachablePaths(
        TypeMapperMappingModel mapping)
    {
        var settings = mapping.EffectiveSettings;
        var result = MappingExecutionPathSet.None;

        if (settings.SupportsCreate &&
            mapping.CreateOperationFailure is null)
        {
            result |= MappingExecutionPathSet.Create;
        }

        if (settings.SupportsUpdate &&
            mapping.UpdateOperationFailure is null)
        {
            result |= MappingExecutionPathSet.UpdateWithPrevious;

            if (mapping.DestinationCanBeNull &&
                settings.NullDestinationHandling ==
                    NullDestinationHandlingValue.Create)
            {
                result |= MappingExecutionPathSet.UpdateWithoutPrevious;
            }
        }

        return result;
    }

    private static bool CanAnalyze(TypeMapperMappingModel mapping)
    {
        return mapping.ManualMapping is null &&
               mapping.EffectiveSettings.HasExecutableOperation &&
               !(mapping.Failure is { } failure &&
                 IsPriorCategoryFailure(failure.Reason));
    }

    private static bool IsPriorCategoryFailure(MappingFailureReason reason)
    {
        return reason is
            MappingFailureReason.UnsupportedMappingContract or
            MappingFailureReason.InvalidBaseConfiguration or
            MappingFailureReason.UnsupportedMapperBuilderFlow or
            MappingFailureReason.UnsupportedMappingBuilderFlow or
            MappingFailureReason.InvalidPairConfiguration or
            MappingFailureReason.InvalidManualSetting or
            MappingFailureReason.InvalidSetting or
            MappingFailureReason.InapplicableSetting or
            MappingFailureReason.CallbackCannotBeTransferred or
            MappingFailureReason.UnsupportedRuntimeCallback or
            MappingFailureReason.UnsupportedStructuredCallback or
            MappingFailureReason.UnsupportedStructuredSyntax or
            MappingFailureReason.StructuredResultRequiresDestination or
            MappingFailureReason.MissingConstructionPolicy or
            MappingFailureReason.ConstructorSelectionFailed or
            MappingFailureReason.ConstructorParameterRuleInvalid or
            MappingFailureReason.TerminalPreviousWithoutValue or
            MappingFailureReason.TerminalNullConstruction or
            MappingFailureReason.MemberRuleInvalid or
            MappingFailureReason.MemberLifecycleInvalid or
            MappingFailureReason.TerminalNullMembers;
    }

    private static bool IsNestedFailure(MappingFailureReason reason)
    {
        return reason is
            MappingFailureReason.NestedPairUnknown or
            MappingFailureReason.NestedResultIncompatible or
            MappingFailureReason.NestedUpdateDestinationInvalid;
    }

    private static void AddDistinct(
        ImmutableArray<Location>.Builder locations,
        Location location,
        Location primary)
    {
        if (!SameLocation(location, primary) &&
            !locations.Any(candidate => SameLocation(candidate, location)))
        {
            locations.Add(location);
        }
    }

    private static bool SameLocation(Location left, Location right)
    {
        return ReferenceEquals(left.SourceTree, right.SourceTree) &&
               left.SourceSpan == right.SourceSpan;
    }

    private static string LocationIdentity(Location location)
    {
        return (location.SourceTree?.FilePath ?? string.Empty) + "|" +
               location.SourceSpan.Start + "|" +
               location.SourceSpan.Length;
    }
}
