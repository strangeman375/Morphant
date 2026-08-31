using System.Collections.Immutable;

namespace Morphant.Generator.Incrementality;

internal static class HintNameCollisions
{
    public static HintNameAllocations Build(
        ImmutableArray<HintNameIdentity> identities,
        CancellationToken cancellationToken)
    {
        var orderedIdentities = identities.ToArray();

        Array.Sort(
            orderedIdentities,
            static (left, right) =>
                StringComparer.Ordinal.Compare(
                    left.StableIdentity,
                    right.StableIdentity));

        var allocator = new HintNamePartAllocator();
        var allocations =
            ImmutableArray.CreateBuilder<HintNameAllocation>();

        foreach (var identity in orderedIdentities)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var allocatedPart = allocator.Allocate(
                identity.StableIdentity,
                identity.ReadableHintNamePart);

            if (!StringComparer.Ordinal.Equals(
                    allocatedPart,
                    identity.ReadableHintNamePart))
            {
                allocations.Add(
                    new HintNameAllocation(
                        identity.StableIdentity,
                        allocatedPart));
            }
        }

        return new HintNameAllocations(allocations.ToImmutable());
    }

    public static string Resolve(
        HintNameAllocations allocations,
        string stableIdentity)
    {
        return Resolve(
            allocations,
            stableIdentity,
            HintNameHelper.ToHintNamePart(stableIdentity));
    }

    public static string Resolve(
        HintNameAllocations allocations,
        string stableIdentity,
        string readableHintNamePart)
    {
        foreach (var allocation in allocations.Items)
        {
            if (StringComparer.Ordinal.Equals(
                    allocation.StableIdentity,
                    stableIdentity))
            {
                return allocation.HintNamePart;
            }
        }

        return readableHintNamePart;
    }
}

internal readonly record struct HintNameIdentity(
    string StableIdentity,
    string ReadableHintNamePart);

internal readonly record struct HintNameAllocation(
    string StableIdentity,
    string HintNamePart);

internal readonly record struct HintNameAllocations(
    ImmutableArray<HintNameAllocation> Items);

internal sealed class HintNameAllocationsComparer :
    IEqualityComparer<HintNameAllocations>
{
    public static HintNameAllocationsComparer Instance { get; } = new();

    public bool Equals(
        HintNameAllocations left,
        HintNameAllocations right)
    {
        return left.Items.SequenceEqual(right.Items);
    }

    public int GetHashCode(HintNameAllocations value)
    {
        var hash = 17;

        foreach (var item in value.Items)
        {
            hash = unchecked(hash * 31 + item.GetHashCode());
        }

        return hash;
    }
}
