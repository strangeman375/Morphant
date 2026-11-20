#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace RegistrationOverrides;

public sealed class Envelope<T> { }
public sealed class UnsupportedDestination { }
public sealed class DuplicateSource { }
public sealed record DuplicateDestination(int Value);
public sealed record ConflictDestination(int Value);
public sealed class IndependentSource { }
public sealed record IndependentDestination(int Value);
public sealed class UnavailableDestination { }

public partial class Container
{
    private sealed class HiddenSource { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<HiddenSource, UnavailableDestination>();
            builder.Map<T, UnsupportedDestination>();

            builder.Map<DuplicateSource, DuplicateDestination>()
                .Convert(source => new DuplicateDestination(101));
            builder.Map<DuplicateSource, DuplicateDestination>()
                .Convert(source => new DuplicateDestination(202));

            builder.Map<Envelope<T>, ConflictDestination>()
                .Convert(source => new ConflictDestination(1));
            builder.Map<Envelope<int>, ConflictDestination>()
                .Convert(source => new ConflictDestination(2));

            builder.Map<IndependentSource, IndependentDestination>()
                .Convert(source => new IndependentDestination(303));
        }
    }
}
