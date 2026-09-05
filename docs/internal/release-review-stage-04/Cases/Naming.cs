using System;
using Morphant;
using Morphant.Generated.Types.A_Audit_002EStage04.N_Stage04Audit.N_Cases.Plans;
#if EXPLICIT_TUPLES
using IdNameConstruction = Morphant.Generated.Tuples.A_Audit_002EStage04.V2_a51caaf0c27a1203d7dd02a67a0a5455.TupleConstruction;
using IdNameMembers = Morphant.Generated.Tuples.A_Audit_002EStage04.V2_a51caaf0c27a1203d7dd02a67a0a5455.TupleMembers;
using CodeLabelConstruction = Morphant.Generated.Tuples.A_Audit_002EStage04.V2_24c9520aed1558ff9795890a2808dbb3.TupleConstruction;
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
                .Construct(source => new global::Morphant.Generated.Types.A_Audit_002EStage04.N_Stage04Audit.N_Cases.T_Other.Plans.OrderConstruction(source.Id));
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
