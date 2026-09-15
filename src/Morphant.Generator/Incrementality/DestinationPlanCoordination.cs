using System.Collections.Immutable;

namespace Morphant.Generator.Incrementality;

internal static class DestinationPlanCoordinationBuilder
{
    public static DestinationPlanCoordination Build(
        ImmutableArray<DestinationPlanCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var owners = new Dictionary<string, DestinationPlanOwner>(
            StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!owners.TryGetValue(
                    candidate.DestinationIdentity,
                    out var owner) ||
                StringComparer.Ordinal.Compare(
                    candidate.CandidateIdentity,
                    owner.CandidateIdentity) < 0)
            {
                owners[candidate.DestinationIdentity] =
                    new DestinationPlanOwner(
                        candidate.DestinationIdentity,
                        candidate.CandidateIdentity);
            }
        }

        var orderedOwners = owners.Values
            .OrderBy(
                static owner => owner.DestinationIdentity,
                StringComparer.Ordinal)
            .ToImmutableArray();
        return new DestinationPlanCoordination(orderedOwners);
    }
}

internal readonly record struct DestinationPlanCandidate(
    string CandidateIdentity,
    string DestinationIdentity,
    string AssemblyIdentity,
    string MetadataName,
    bool IncludeInitOnlyProperties);

internal readonly record struct DestinationPlanOwner(
    string DestinationIdentity,
    string CandidateIdentity);

internal readonly record struct DestinationPlanCoordination(
    ImmutableArray<DestinationPlanOwner> Owners)
{
    public bool IsOwner(DestinationPlanCandidate candidate)
    {
        var index = Owners.BinarySearch(
            new DestinationPlanOwner(
                candidate.DestinationIdentity,
                string.Empty),
            DestinationPlanOwnerComparer.Instance);

        return index >= 0 &&
               StringComparer.Ordinal.Equals(
                   Owners[index].CandidateIdentity,
                   candidate.CandidateIdentity);
    }
}

internal sealed class DestinationPlanCoordinationComparer :
    IEqualityComparer<DestinationPlanCoordination>
{
    public static DestinationPlanCoordinationComparer Instance { get; } =
        new();

    public bool Equals(
        DestinationPlanCoordination left,
        DestinationPlanCoordination right)
    {
        return left.Owners.SequenceEqual(right.Owners);
    }

    public int GetHashCode(DestinationPlanCoordination value)
    {
        var hash = 17;

        foreach (var owner in value.Owners)
        {
            hash = unchecked(hash * 31 + owner.GetHashCode());
        }

        return hash;
    }
}

internal sealed class DestinationPlanOwnerComparer :
    IComparer<DestinationPlanOwner>
{
    public static DestinationPlanOwnerComparer Instance { get; } = new();

    public int Compare(
        DestinationPlanOwner left,
        DestinationPlanOwner right)
    {
        return StringComparer.Ordinal.Compare(
            left.DestinationIdentity,
            right.DestinationIdentity);
    }
}
