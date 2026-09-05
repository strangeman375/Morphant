#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace CallbackOverrides;

public sealed class Source
{
    public int Value { get; init; }
}

public sealed class MethodGroupDestination { }

public sealed class CaptureDestination
{
    public CaptureDestination(int value) { }
}

public sealed class GrammarDestination
{
    public int Value { get; set; }
}

public sealed class MutationDestination
{
    public int Value { get; set; }
}

public sealed class MarkerDestination { }

[MorphantMapper]
public partial class CallbackMapper : TypeMapper<CallbackMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        var offset = Environment.TickCount;

        builder.Map<Source, MethodGroupDestination>()
            .Construct(BuildConstruction);
        builder.Map<Source, CaptureDestination>()
            .ConstructUsing(source => new CaptureDestination(offset));
        builder.Map<Source, GrammarDestination>()
            .Members(source =>
            {
                Observe(source.Value);
                return new() { Value = source.Value };
            });
        builder.Map<Source, MutationDestination>()
            .Members((source, previous, result) => new()
            {
                Value = result.Value++
            });
        builder.Map<Source, MarkerDestination>()
            .Convert(source =>
                (MarkerDestination)(object)Value(
                    new MarkerDestination()));
    }

    private static global::Morphant.Generated.Types.A_CallbackOverrides.N_CallbackOverrides.Plans.MethodGroupDestinationConstruction
        BuildConstruction(Source source) => new();

    private static void Observe(int value) { }
}
