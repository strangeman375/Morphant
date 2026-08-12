using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MappingCompletenessDiagnosticAnalyzer
{
    public static ImmutableArray<MappingCompletenessDiagnosticCandidate> Build(
        TypeMapperModel model,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<
            MappingCompletenessDiagnosticCandidate>();

        foreach (var mapping in model.Mappings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!CanAnalyze(mapping) ||
                mapping.CompletenessObservation is not { } observation)
            {
                continue;
            }

            var validation = mapping.EffectiveSettings
                .UnmappedMemberValidation;

            if (validation is
                UnmappedMemberValidationValue.Source or
                UnmappedMemberValidationValue.Strict)
            {
                AnalyzeSourceMembers(
                    mapping,
                    observation,
                    result,
                    cancellationToken);
            }

            if (validation is
                UnmappedMemberValidationValue.Destination or
                UnmappedMemberValidationValue.Strict)
            {
                AnalyzeDestinationMembers(
                    mapping,
                    observation,
                    result,
                    cancellationToken);
            }
        }

        return result.ToImmutable();
    }

    private static void AnalyzeSourceMembers(
        TypeMapperMappingModel mapping,
        CompletenessPlanningObservation observation,
        ImmutableArray<MappingCompletenessDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        for (var index = 0;
             index < observation.SupportedSourceMembers.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var member = observation.SupportedSourceMembers[index];

            if (ContainsMember(observation.ErrorDerivedUncertainty, member) ||
                observation.SourceUses.Any(use =>
                    AreSameMember(use.Member, member)) ||
                observation.SourceDiscards.Any(discard =>
                    AreSameMember(discard.Member, member)))
            {
                continue;
            }

            result.Add(CreateCandidate(
                MappingCompletenessDiagnosticKind.SourceMemberUnused,
                mapping,
                member,
                index,
                MappingTypeRole.Source,
                cancellationToken));
        }
    }

    private static void AnalyzeDestinationMembers(
        TypeMapperMappingModel mapping,
        CompletenessPlanningObservation observation,
        ImmutableArray<MappingCompletenessDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        for (var index = 0;
             index < observation.SupportedDestinationMembers.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var member = observation.SupportedDestinationMembers[index];

            if (ContainsMember(observation.ErrorDerivedUncertainty, member) ||
                observation.DestinationOccupancy.Any(occupancy =>
                    AreSameMember(occupancy.Member, member)))
            {
                continue;
            }

            result.Add(CreateCandidate(
                MappingCompletenessDiagnosticKind.DestinationMemberUnmapped,
                mapping,
                member,
                index,
                MappingTypeRole.Destination,
                cancellationToken));
        }
    }

    private static MappingCompletenessDiagnosticCandidate CreateCandidate(
        MappingCompletenessDiagnosticKind kind,
        TypeMapperMappingModel mapping,
        ISymbol member,
        int memberOrder,
        MappingTypeRole role,
        CancellationToken cancellationToken)
    {
        var context = mapping.AnalysisContext;
        var mapperIdentity = SymbolNameHelper.GetFullMetadataName(
            context.TargetMapper.OriginalDefinition);
        var pairKey = context.Identity.Source.Key + "->" +
            context.Identity.Destination.Key;
        var memberIdentity = DisplayMember(member);
        var identity = ((int)kind).ToString(
                System.Globalization.CultureInfo.InvariantCulture) +
            "|" + mapperIdentity +
            "|" + pairKey +
            "|" + memberIdentity;
        var primary = GetTypeArgumentLocation(
            context.Registration.Syntax,
            role);
        var declaration = GetDeclarationLocation(
            member,
            cancellationToken);
        var additional = declaration is null ||
            IsSameLocation(declaration, primary)
                ? ImmutableArray<Location>.Empty
                : [declaration];

        return new MappingCompletenessDiagnosticCandidate(
            kind,
            identity,
            mapperIdentity,
            pairKey,
            memberIdentity,
            memberOrder,
            primary,
            additional,
            memberIdentity,
            MapperContractDisplay.Create(
                context.SourceType,
                context.DestinationType));
    }

    private static bool CanAnalyze(TypeMapperMappingModel mapping)
    {
        return mapping.ManualMapping is null &&
               mapping.EffectiveSettings.HasExecutableOperation &&
               mapping.EffectiveSettings
                   .IsUnmappedMemberValidationValid &&
               mapping.EffectiveSettings.UnmappedMemberValidation is not
                   UnmappedMemberValidationValue.None &&
               GetReachablePaths(mapping) != MappingExecutionPathSet.None &&
               !(mapping.Failure is { } failure &&
                 IsCompletenessGate(failure.Reason));
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

    private static bool IsCompletenessGate(MappingFailureReason reason)
    {
        return reason is
            MappingFailureReason.UnsupportedMappingContract or
            MappingFailureReason.InvalidBaseConfiguration or
            MappingFailureReason.UnsupportedMapperBuilderFlow or
            MappingFailureReason.UnsupportedMappingBuilderFlow or
            MappingFailureReason.InvalidPairConfiguration or
            MappingFailureReason.InvalidManualSetting or
            MappingFailureReason.InvalidSetting or
            MappingFailureReason.InapplicableSetting;
    }

    private static bool ContainsMember(
        ImmutableArray<ISymbol> members,
        ISymbol member) => members.Any(candidate =>
            AreSameMember(candidate, member));

    private static bool AreSameMember(ISymbol left, ISymbol right)
    {
        return SymbolEqualityComparer.Default.Equals(left, right) ||
               StringComparer.Ordinal.Equals(
                   DisplayMember(left),
                   DisplayMember(right));
    }

    private static string DisplayMember(ISymbol member)
    {
        var containing = member.ContainingType?.ToDisplayString(
            SymbolDisplayFormats.FullyQualifiedNullable) ?? string.Empty;

        return containing + "." + member.Name;
    }

    private static Location GetTypeArgumentLocation(
        InvocationExpressionSyntax invocation,
        MappingTypeRole role)
    {
        var genericName = invocation.Expression
            .DescendantNodesAndSelf()
            .OfType<GenericNameSyntax>()
            .First(name =>
                name.Identifier.ValueText == "Map" &&
                name.TypeArgumentList.Arguments.Count == 2);
        var index = role == MappingTypeRole.Source ? 0 : 1;

        return genericName.TypeArgumentList.Arguments[index].GetLocation();
    }

    private static Location? GetDeclarationLocation(
        ISymbol member,
        CancellationToken cancellationToken)
    {
        foreach (var reference in member.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(cancellationToken);

            return syntax switch
            {
                PropertyDeclarationSyntax property =>
                    property.Identifier.GetLocation(),
                VariableDeclaratorSyntax variable =>
                    variable.Identifier.GetLocation(),
                FieldDeclarationSyntax field =>
                    field.Declaration.Variables.FirstOrDefault()
                        ?.Identifier.GetLocation(),
                _ => syntax.GetLocation()
            };
        }

        return null;
    }

    private static bool IsSameLocation(Location left, Location right)
    {
        return ReferenceEquals(left.SourceTree, right.SourceTree) &&
               left.SourceSpan == right.SourceSpan;
    }
}
