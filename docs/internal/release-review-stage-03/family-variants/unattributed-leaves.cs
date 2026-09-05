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
    public abstract class ProducerMapper<TMapper, T> : TypeMapper<TMapper>
        where TMapper : ProducerMapper<TMapper, T>
        where T : class
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Shared.Source<T>, Shared.Destination<T>>();
    }
}
namespace Consumer
{
    public abstract class ConsumerMapper<TMapper, T> : Producer.ProducerMapper<TMapper, T>
        where TMapper : ConsumerMapper<TMapper, T>
        where T : class, new()
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Shared.Source<T>, Shared.Destination<T>>().Construct(s => new(s.Id));
    }
}
namespace Leaves
{
    public sealed class State { }
    [Morphant.MorphantMapper]
    public partial class ProducerLeaf : Producer.ProducerMapper<ProducerLeaf, State>
    { protected override void Configure(MapperBuilder builder) => base.Configure(builder); }
    [Morphant.MorphantMapper]
    public partial class ConsumerLeaf : Consumer.ConsumerMapper<ConsumerLeaf, State>
    { protected override void Configure(MapperBuilder builder) => base.Configure(builder); }
}
