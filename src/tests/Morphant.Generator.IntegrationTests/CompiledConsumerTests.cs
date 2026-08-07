using Morphant.Generator.IntegrationTests.CSharp9;
using Morphant.Generator.IntegrationTests.Latest;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class CompiledConsumerTests
{
    [Test]
    public void CSharp9_quick_start_compiles_and_executes()
    {
        var mapperImplementation = new CSharp9Mapper();
        var provider = new ManualServiceProvider();
        provider.Add<ITypeMapper<Customer, CustomerDto>>(
            mapperImplementation);
        var mapper = new Mapper(provider);

        var result = mapper.Map<Customer, CustomerDto>(
            new Customer { Name = "Ada" });

        Assert.That(result.Name, Is.EqualTo("Ada"));
    }

    [Test]
    public void Multiple_assemblies_use_manual_DI_and_scoped_dependencies()
    {
        var first = CreateMapper("scope-a:");
        var second = CreateMapper("scope-b:");

        var firstOrder = first.Map<Order, OrderDto>(new Order
        {
            Number = 7,
            Customer = new Customer { Name = "Ada" }
        });
        var secondOrder = second.Map<Order, OrderDto>(new Order
        {
            Number = 7,
            Customer = new Customer { Name = "Grace" }
        });
        var summary = first.Map<Summary, SummaryDto>(new Summary
        {
            Customer = new Customer { Name = "Linus" }
        });
        var generic = first.Map<
            GenericSource<int>,
            GenericDestination<string>>(new(9));
        var nullable = first.Map<NullableNumber, int?>(
            new NullableNumber { Value = 11 });

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
        });
    }

    private static Mapper CreateMapper(string prefix)
    {
        var csharp9Mapper = new CSharp9Mapper();
        var latestMapper = new LatestMapper(new PrefixFormatter(prefix));
        var provider = new ManualServiceProvider();

        provider.Add<ITypeMapper<Customer, CustomerDto>>(csharp9Mapper);
        provider.Add<ITypeMapper<Order, OrderDto>>(latestMapper);
        provider.Add<ITypeMapper<Summary, SummaryDto>>(latestMapper);
        provider.Add<ITypeMapper<
            GenericSource<int>,
            GenericDestination<string>>>(latestMapper);
        provider.Add<ITypeMapper<NullableNumber, int?>>(latestMapper);

        return new Mapper(provider);
    }

    private sealed class PrefixFormatter : ILabelFormatter
    {
        private readonly string _prefix;

        public PrefixFormatter(string prefix)
        {
            _prefix = prefix;
        }

        public string Format(int value) => _prefix + value;
    }

    private sealed class ManualServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, List<object>> _services = new();

        public object? GetService(Type serviceType)
        {
            if (!serviceType.IsGenericType ||
                serviceType.GetGenericTypeDefinition() != typeof(IEnumerable<>))
            {
                return null;
            }

            var elementType = serviceType.GetGenericArguments()[0];
            var services = _services.TryGetValue(elementType, out var values)
                ? values
                : [];
            var result = Array.CreateInstance(elementType, services.Count);

            for (var index = 0; index < services.Count; index++)
            {
                result.SetValue(services[index], index);
            }

            return result;
        }

        public void Add<TService>(TService service)
            where TService : class
        {
            if (!_services.TryGetValue(typeof(TService), out var services))
            {
                services = [];
                _services.Add(typeof(TService), services);
            }

            services.Add(service);
        }
    }
}
