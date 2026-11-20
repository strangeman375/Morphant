using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.Latest.DeclarativeValueSurface;

public sealed class Source
{
    public IReadOnlyList<int> Values { get; init; } = [];
}

public sealed class Destination
{
    public int[] Values { get; set; } = [];

    public Func<int, int> Transform { get; set; } = static value => value;
}

[MorphantMapper]
public sealed partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .Members((source, _) => new()
            {
                Values = Value<int[]>(
                    [.. source.Values, source.Values.Count]),
                Transform = Value<Func<int, int>>(
                    static value => value * 2)
            });
}

public static class Scenario
{
    public static void Verify()
    {
        var result =
            ((ITypeMapper<Source, Destination>)new TestMapper())
            .Create(
                new Source { Values = [2, 3] },
                default(MappingContext));

        if (!result.Values.SequenceEqual([2, 3, 2]) ||
            result.Transform(4) != 8)
        {
            throw new InvalidOperationException(
                "Latest target-typed values were not preserved.");
        }
    }
}
