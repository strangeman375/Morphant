using System.Collections.Immutable;
using Morphant.Generator.ConstructionSurface.ConstructionPlan;

namespace Morphant.Generator.ConstructionSurface.PairConfiguration;

internal sealed record PairConfigurationModel(
    string ExtensionContainerTypeName,
    string BuilderTypeName,
    string ReceiverTypeName,
    string DeclarativeSourceTypeName,
    string ManualSourceTypeName,
    string DestinationTypeName,
    string PreviousDestinationTypeName,
    bool HasStructuredConstruction,
    string ConstructionResultTypeName,
    string? MembersPlanTypeName,
    ImmutableArray<ConstructionTypeParameterModel> TypeParameters);
