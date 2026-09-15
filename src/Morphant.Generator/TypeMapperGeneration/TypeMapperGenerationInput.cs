using System.Collections.Immutable;

namespace Morphant.Generator.TypeMapperGeneration;

internal readonly record struct TypeMapperGenerationInput(
    string HintName,
    string Source,
    ImmutableArray<CallbackDiagnosticCandidate> CallbackDiagnostics,
    ImmutableArray<ConstructionDiagnosticCandidate> ConstructionDiagnostics,
    ImmutableArray<MemberDiagnosticCandidate> MemberDiagnostics,
    ImmutableArray<NestedMappingDiagnosticCandidate>
        NestedMappingDiagnostics,
    ImmutableArray<MappingCompletenessDiagnosticCandidate>
        MappingCompletenessDiagnostics,
    ImmutableArray<IncludeMembersDiagnosticCandidate>
        IncludeMembersDiagnostics,
    ImmutableArray<FlatteningDiagnosticCandidate> FlatteningDiagnostics);
