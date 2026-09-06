using System;
using Morphant;
using Morphant.Generated.N_04410b8797eac248bdacd41fcd44b4e7;
#if EXPLICIT_TUPLES
using IdNameConstruction = Morphant.Generated.N_0091d958e8508e00fd80bfb186fbb85f.TupleConstruction;
using IdNameMembers = Morphant.Generated.N_0091d958e8508e00fd80bfb186fbb85f.TupleMembers;
using CodeLabelConstruction = Morphant.Generated.N_0a141b2531917ee0a5c60366270ef969.TupleConstruction;
#endif

namespace Stage04Audit.Cases
{
    public sealed class Source
    {
        public int Id { get; set; }
        public int Code { get; set; }
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public sealed class Order
    {
        public Order(int id) => Id = id;
        public int Id { get; }
        public string Name { get; set; } = "";
    }

    public class Other
    {
        public sealed class Order
        {
            public Order(int id) => Id = id;
            public int Id { get; }
        }
    }

    public class Envelope<T>
    {
        public sealed class Item
        {
            public Item(T id) => Id = id;
            public T Id { get; }
        }
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Order>()
                .Construct(source => new OrderConstruction(source.Id))
                .Members(source => new OrderMembers { Name = source.Name });
            builder.Map<Source, Other.Order>()
                .Construct(source => new global::Morphant.Generated.N_03efaaffc00be1734817e6af64dc04df.OrderConstruction(source.Id));
            builder.Map<Source, Envelope<int>.Item>();
            builder.Map<Source, (int Id, string Name)>()
#if EXPLICIT_TUPLES
                .Construct(source => new IdNameConstruction(source.Id, source.Name))
                .Members(source => new IdNameMembers { Name = source.Name })
#endif
                ;
            builder.Map<Source, Tuple<int, string>>().Construct(source => new(source.Id, source.Name));
        }
    }

    [MorphantMapper]
    public partial class OtherMapper : TypeMapper<OtherMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, (int Code, string Label)>()
#if EXPLICIT_TUPLES
                .Construct(source => new CodeLabelConstruction(source.Code, source.Label))
#endif
                ;
    }

    internal static class Scenario
    {
        internal static void Run()
        {
            var source = new Source { Id = 11, Code = 13, Name = "first", Label = "second" };
            var mapper = new Mapper();
            var order = ((ITypeMapper<Source, Order>)mapper).Create(source);
            Check.Equal("explicit-short-construction", 11, order.Id);
            Check.Equal("explicit-short-members", "first", order.Name);
            Check.Equal("explicit-full-construction", 11, ((ITypeMapper<Source, Other.Order>)mapper).Create(source).Id);
            Check.Equal("nested-generic-construction", 11, ((ITypeMapper<Source, Envelope<int>.Item>)mapper).Create(source).Id);
            Check.Equal("first-tuple-presentation", (11, "first"), ((ITypeMapper<Source, (int Id, string Name)>)mapper).Create(source));
            Check.Equal("second-tuple-presentation", (13, "second"), ((ITypeMapper<Source, (int Code, string Label)>)new OtherMapper()).Create(source));
            Check.Equal("system-tuple-construction", Tuple.Create(11, "first"), ((ITypeMapper<Source, Tuple<int, string>>)mapper).Create(source));
        }
    }
}
