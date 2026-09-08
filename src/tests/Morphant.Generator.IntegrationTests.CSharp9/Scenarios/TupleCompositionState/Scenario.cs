#nullable enable
using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleCompositionState
{
    public sealed class Order { public decimal Amount { get; set; } }
    public sealed class Customer { public string Name { get; set; } = ""; }
    public sealed class Summary
    {
        public string Name { get; set; } = "";
        public decimal Total { get; set; }
    }
    public sealed class Audit { public decimal Amount { get; set; } }
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<(Order Order, Customer Customer, decimal Rate), Summary>()
                .Members(source => new() { Name = source.Customer.Name, Total = source.Order.Amount * (1m + source.Rate) });
            builder.Map<Order, Audit>();
            builder.Map<(Order Order, Customer Customer, decimal Rate), (Summary Summary, Audit Audit)>()
                .Members(source => new() { Summary = Map<Summary>(source), Audit = Map<Audit>(source.Order) });
        }
    }
    public static class Scenario
    {
        public static void Verify(bool update)
        {
            var generated = new TestMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<(Order Order, Customer Customer, decimal Rate), Summary>>(generated)
                .AddSingleton<ITypeMapper<Order, Audit>>(generated)
                .AddSingleton<ITypeMapper<(Order Order, Customer Customer, decimal Rate), (Summary Summary, Audit Audit)>>(generated)
                .AddSingleton<IMapper, Mapper>().BuildServiceProvider();
            var facade = provider.GetRequiredService<IMapper>();
            var source = (Order: new Order { Amount = 100m }, Customer: new Customer { Name = "Ada" }, Rate: 0.2m);
            var previous = (Summary: new Summary { Name = "old", Total = -1 }, Audit: new Audit { Amount = -2 });
            var result = update
                ? facade.Map<(Order Order, Customer Customer, decimal Rate), (Summary Summary, Audit Audit)>(source, previous)
                : facade.Map<(Order Order, Customer Customer, decimal Rate), (Summary Summary, Audit Audit)>(source);
            if (result.Summary.Name != "Ada" || result.Summary.Total != 120m || result.Audit.Amount != 100m ||
                (update && (!ReferenceEquals(result.Summary, previous.Summary) || !ReferenceEquals(result.Audit, previous.Audit))))
                throw new InvalidOperationException("Tuple composition must pass explicit state and preserve nested Update destinations.");
        }
    }
}
