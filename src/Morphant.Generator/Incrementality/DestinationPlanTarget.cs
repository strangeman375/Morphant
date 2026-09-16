using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.Incrementality;

internal readonly record struct DestinationPlanTarget(
    INamedTypeSymbol Destination,
    string AssemblyIdentity,
    string MetadataName,
    bool IsTuple,
    string PlanIdentity)
{
    public string Identity => IsTuple
        ? "tuple|" + PlanIdentity
        : AssemblyIdentity + "|" + MetadataName;

    public static DestinationPlanTarget Create(ITypeSymbol type, Compilation compilation)
    {
        var destination = DestinationCapabilityPolicy.GetDestinationType(type, compilation);
        var tuple = BclTupleShapePolicy.TryCreate(destination);
        var planIdentity = tuple is null ? string.Empty : BclTuplePlanNaming.BuildStableIdentity(tuple);
        var definition = tuple is null ? destination.OriginalDefinition : destination;

        return new DestinationPlanTarget(
            definition,
            definition.ContainingAssembly.Identity.ToString(),
            tuple is null ? SymbolNameHelper.GetFullMetadataName(definition) : "Tuple." + planIdentity,
            tuple is not null,
            planIdentity);
    }
}
