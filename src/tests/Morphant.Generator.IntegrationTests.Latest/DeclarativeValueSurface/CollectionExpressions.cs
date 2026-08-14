using System.Collections.Immutable;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.Latest.DeclarativeValueSurface;

public sealed class CollectionExpressionSource
{
    public IReadOnlyList<int> Values { get; init; } = [];

    public int ReadCount { get; private set; }

    public IReadOnlyList<int> ReadValues()
    {
        ReadCount++;
        return Values;
    }
}

public sealed class CollectionExpressionConstructDestination
{
    public CollectionExpressionConstructDestination(
        ImmutableArray<int> values)
    {
        Values = values;
    }

    public ImmutableArray<int> Values { get; }
}

public sealed class CollectionExpressionResolveDestination
{
    public CollectionExpressionResolveDestination(
        ImmutableArray<int> values)
    {
        Values = values;
    }

    public ImmutableArray<int> Values { get; }
}

public sealed class CollectionExpressionMembersDestination
{
    public ImmutableArray<int> Values { get; set; } =
        ImmutableArray<int>.Empty;
}

[MorphantMapper]
public sealed partial class CollectionExpressionMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder
            .Map<
                CollectionExpressionSource,
                CollectionExpressionConstructDestination>()
            .Construct(source => new(
                values: Value<ImmutableArray<int>>(
                    [.. source.ReadValues(), 11])));

        builder
            .Map<
                CollectionExpressionSource,
                CollectionExpressionResolveDestination>()
            .Resolve((source, _) => new(
                values: Value<ImmutableArray<int>>(
                    [22, .. source.ReadValues()])));

        builder
            .Map<
                CollectionExpressionSource,
                CollectionExpressionMembersDestination>()
            .Members((source, _) => new()
            {
                Values = Value<ImmutableArray<int>>(
                    [.. source.ReadValues(), 33])
            });
    }
}

public static class CollectionExpressionScenario
{
    public static void Verify()
    {
        var mapper = new CollectionExpressionMapper();
        var source = new CollectionExpressionSource
        {
            Values = [2, 3]
        };
        var context = default(MappingContext);

        var constructed = ((ITypeMapper<
                CollectionExpressionSource,
                CollectionExpressionConstructDestination>)mapper)
            .Create(source, context);
        var resolvedOnCreate = ((ITypeMapper<
                CollectionExpressionSource,
                CollectionExpressionResolveDestination>)mapper)
            .Create(source, context);
        var previous = new CollectionExpressionResolveDestination([99]);
        var resolvedOnUpdate = ((ITypeMapper<
                CollectionExpressionSource,
                CollectionExpressionResolveDestination>)mapper)
            .Update(source, previous, context);
        var members = ((ITypeMapper<
                CollectionExpressionSource,
                CollectionExpressionMembersDestination>)mapper)
            .Create(source, context);

        if (!constructed.Values.SequenceEqual([2, 3, 11]) ||
            !resolvedOnCreate.Values.SequenceEqual([22, 2, 3]) ||
            !resolvedOnUpdate.Values.SequenceEqual([22, 2, 3]) ||
            ReferenceEquals(resolvedOnUpdate, previous) ||
            !members.Values.SequenceEqual([2, 3, 33]) ||
            source.ReadCount != 4)
        {
            throw new InvalidOperationException(
                "Collection expressions were not preserved across all " +
                "structured mapping surfaces.");
        }
    }
}
