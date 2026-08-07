using CSharp9Consumer = Morphant.Generator.IntegrationTests.CSharp9;
using LatestConsumer = Morphant.Generator.IntegrationTests.Latest;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class CompiledConsumerTests
{
    [Test]
    public void CSharp9_quick_start_compiles_and_executes()
    {
        var mapperImplementation = new CSharp9Consumer.CSharp9Mapper();
        var provider = new CSharp9Consumer.ManualServiceProvider();
        provider.Add<ITypeMapper<
            CSharp9Consumer.Customer,
            CSharp9Consumer.CustomerDto>>(
            mapperImplementation);
        var mapper = new Mapper(provider);

        var result = mapper.Map<
            CSharp9Consumer.Customer,
            CSharp9Consumer.CustomerDto>(
            new CSharp9Consumer.Customer { Name = "Ada" });

        Assert.That(result.Name, Is.EqualTo("Ada"));
    }

    [Test]
    public void Multiple_assemblies_use_manual_DI_and_scoped_dependencies()
    {
        var first = CreateMapper("scope-a:");
        var second = CreateMapper("scope-b:");

        var firstOrder = first.Map<
            LatestConsumer.Order,
            LatestConsumer.OrderDto>(new LatestConsumer.Order
        {
            Number = 7,
            Customer = new LatestConsumer.Customer { Name = "Ada" }
        });
        var secondOrder = second.Map<
            LatestConsumer.Order,
            LatestConsumer.OrderDto>(new LatestConsumer.Order
        {
            Number = 7,
            Customer = new LatestConsumer.Customer { Name = "Grace" }
        });
        var summary = first.Map<
            LatestConsumer.Summary,
            LatestConsumer.SummaryDto>(new LatestConsumer.Summary
        {
            Customer = new LatestConsumer.Customer { Name = "Linus" }
        });
        var generic = first.Map<
            LatestConsumer.GenericSource<int>,
            LatestConsumer.GenericDestination<string>>(new(9));
        var nullable = first.Map<LatestConsumer.NullableNumber, int?>(
            new LatestConsumer.NullableNumber { Value = 11 });
        var csharp9Customer = first.Map<
            CSharp9Consumer.Customer,
            CSharp9Consumer.CustomerDto>(
            new CSharp9Consumer.Customer { Name = "Margaret" });

        Assert.Multiple(() =>
        {
            Assert.That(firstOrder.Number, Is.EqualTo(7));
            Assert.That(firstOrder.Label, Is.EqualTo("scope-a:7"));
            Assert.That(firstOrder.Customer.Name, Is.EqualTo("Ada"));
            Assert.That(secondOrder.Label, Is.EqualTo("scope-b:7"));
            Assert.That(secondOrder.Customer.Name, Is.EqualTo("Grace"));
            Assert.That(summary.CustomerName, Is.EqualTo("Linus"));
            Assert.That(generic.Value, Is.EqualTo("scope-a:9"));
            Assert.That(nullable, Is.EqualTo(11));
            Assert.That(csharp9Customer.Name, Is.EqualTo("Margaret"));
        });
    }

    private static Mapper CreateMapper(string prefix)
    {
        var csharp9Mapper = new CSharp9Consumer.CSharp9Mapper();
        var latestMapper = new LatestConsumer.LatestMapper(
            new PrefixFormatter(prefix));
        var provider = new CSharp9Consumer.ManualServiceProvider();

        provider.Add<ITypeMapper<
            CSharp9Consumer.Customer,
            CSharp9Consumer.CustomerDto>>(csharp9Mapper);
        provider.Add<ITypeMapper<
            LatestConsumer.Customer,
            LatestConsumer.CustomerDto>>(latestMapper);
        provider.Add<ITypeMapper<
            LatestConsumer.Order,
            LatestConsumer.OrderDto>>(latestMapper);
        provider.Add<ITypeMapper<
            LatestConsumer.Summary,
            LatestConsumer.SummaryDto>>(latestMapper);
        provider.Add<ITypeMapper<
            LatestConsumer.GenericSource<int>,
            LatestConsumer.GenericDestination<string>>>(latestMapper);
        provider.Add<ITypeMapper<LatestConsumer.NullableNumber, int?>>(
            latestMapper);

        return new Mapper(provider);
    }

    private sealed class PrefixFormatter : LatestConsumer.ILabelFormatter
    {
        private readonly string _prefix;

        public PrefixFormatter(string prefix)
        {
            _prefix = prefix;
        }

        public string Format(int value) => _prefix + value;
    }
}
