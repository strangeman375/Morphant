#nullable enable
#pragma warning disable CS1591
using Morphant;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AuditConsumer")]
namespace Shared
{
    public sealed class Source { public int Id { get; set; } }
    public sealed class Destination
    {
        public Destination(int id) => Id = id;
        public int Id { get; set; }
    }
}
namespace Producer
{
    [MorphantMapper]
    public partial class ProducerMapper : TypeMapper<ProducerMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Shared.Source, Shared.Destination>();
    }
}