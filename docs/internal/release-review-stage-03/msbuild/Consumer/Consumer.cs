#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace Consumer
{
    [MorphantMapper]
    public partial class ConsumerMapper : TypeMapper<ConsumerMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Shared.Source, Shared.Destination>().Construct(s => new(s.Id));
    }
}