#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace Shared
{
    public sealed class Source<T> { public int Id { get; set; } }
    public sealed class Destination<T>
    {
        public Destination(int id) => Id = id;
        public int Id { get; set; }
    }
}
namespace Producer
{
    [MorphantMapper]
    public partial class ProducerMapper<TMapper, T> : TypeMapper<TMapper>
        where TMapper : ProducerMapper<TMapper, T>
        where T : class
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Shared.Source<T>, Shared.Destination<T>>();
    }
}
namespace Consumer
{
    [MorphantMapper]
    public partial class ConsumerMapper<TMapper, T> : Producer.ProducerMapper<TMapper, T>
        where TMapper : ConsumerMapper<TMapper, T>
        where T : class
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Shared.Source<T>, Shared.Destination<T>>().Construct(s => new(s.Id));
    }
}