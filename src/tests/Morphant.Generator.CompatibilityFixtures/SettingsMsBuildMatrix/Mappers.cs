#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace SettingsMsBuildMatrix
{
    public sealed class ManualSource
    {
    }

    public sealed class ManualDestination
    {
    }

    public sealed class DeclarativeSource
    {
        public int Value { get; init; }
    }

    public sealed class DeclarativeDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ManualSource, ManualDestination>()
                .Convert(_ => new ManualDestination());

            builder.Map<DeclarativeSource, DeclarativeDestination>(
                MappingMode.CreateAndUpdate);
        }
    }
}
