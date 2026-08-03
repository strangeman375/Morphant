using System.Collections.Immutable;
using Morphant.Generator.ConstructionSurface.ConstructionPlan;

namespace Morphant.Generator.ConstructionSurface.PairConfiguration;

internal sealed record PairConfigurationModel(
    string BuilderTypeName,
    string DeclarativeSourceTypeName,
    string ManualSourceTypeName,
    string DestinationTypeName,
    string PreviousDestinationTypeName,
    string ConstructionResultTypeName,
    ImmutableArray<ConstructionTypeParameterModel> TypeParameters);
