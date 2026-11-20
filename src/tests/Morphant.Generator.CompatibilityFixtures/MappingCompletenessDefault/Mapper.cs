#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace MappingCompletenessDefault
{
    public sealed class Source
    {
        public int Used { get; init; }

        public int Unused { get; init; }
    }

    public sealed class Destination
    {
        public int Used { get; set; }

        public int Unmapped { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new() { Used = source.Used });
    }
}
