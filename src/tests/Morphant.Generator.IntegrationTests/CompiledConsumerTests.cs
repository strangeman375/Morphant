using Microsoft.Extensions.DependencyInjection;
using CSharp11Consumer = Morphant.Generator.IntegrationTests.CSharp11;
using CSharp9Consumer = Morphant.Generator.IntegrationTests.CSharp9;
using LatestConsumer = Morphant.Generator.IntegrationTests.Latest;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class CompiledConsumerTests
{
    [Test]
    public void CSharp9_quick_start_compiles_and_executes()
    {
        using var provider = new ServiceCollection()
            .AddScoped<CSharp9Consumer.CSharp9Mapper>()
            .AddScoped<ITypeMapper<
                CSharp9Consumer.Customer,
                CSharp9Consumer.CustomerDto>>(serviceProvider =>
                serviceProvider.GetRequiredService<
                    CSharp9Consumer.CSharp9Mapper>())
            .AddScoped<IMapper, Mapper>()
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        using var scope = provider.CreateScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

        var result = mapper.Map<
            CSharp9Consumer.Customer,
            CSharp9Consumer.CustomerDto>(
            new CSharp9Consumer.Customer { Name = "Ada" });

        Assert.That(result.Name, Is.EqualTo("Ada"));
    }

    [Test]
    public void CSharp11_required_member_consumer_compiles_and_executes()
    {
        var mapper = (ITypeMapper<
            CSharp11Consumer.CSharp11Customer,
            CSharp11Consumer.CSharp11CustomerDto>)
            new CSharp11Consumer.CSharp11Mapper();

        var result = mapper.Create(new CSharp11Consumer.CSharp11Customer
        {
            Name = "Grace"
        });

        Assert.That(result.Name, Is.EqualTo("Grace"));
    }

    [Test]
    public void Multiple_assemblies_use_standard_DI_and_scoped_dependencies()
    {
        using var provider = CreateServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        firstScope.ServiceProvider
            .GetRequiredService<ScopeState>()
            .Prefix = "scope-a:";
        secondScope.ServiceProvider
            .GetRequiredService<ScopeState>()
            .Prefix = "scope-b:";
        var firstFormatter = firstScope.ServiceProvider
            .GetRequiredService<LatestConsumer.ILabelFormatter>();
        var secondFormatter = secondScope.ServiceProvider
            .GetRequiredService<LatestConsumer.ILabelFormatter>();
        var first = firstScope.ServiceProvider.GetRequiredService<IMapper>();
        var second = secondScope.ServiceProvider.GetRequiredService<IMapper>();

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
            Assert.That(
                firstScope.ServiceProvider.GetRequiredService<
                    LatestConsumer.ILabelFormatter>(),
                Is.SameAs(firstFormatter));
            Assert.That(secondFormatter, Is.Not.SameAs(firstFormatter));
        });
    }

    private static ServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection()
            .AddScoped<ScopeState>()
            .AddScoped<
                LatestConsumer.ILabelFormatter,
                PrefixFormatter>()
            .AddScoped<CSharp9Consumer.CSharp9Mapper>()
            .AddScoped<LatestConsumer.LatestMapper>()
            .AddScoped<ITypeMapper<
                CSharp9Consumer.Customer,
                CSharp9Consumer.CustomerDto>>(serviceProvider =>
                serviceProvider.GetRequiredService<
                    CSharp9Consumer.CSharp9Mapper>())
            .AddScoped<ITypeMapper<
                LatestConsumer.Customer,
                LatestConsumer.CustomerDto>>(serviceProvider =>
                serviceProvider.GetRequiredService<
                    LatestConsumer.LatestMapper>())
            .AddScoped<ITypeMapper<
                LatestConsumer.Order,
                LatestConsumer.OrderDto>>(serviceProvider =>
                serviceProvider.GetRequiredService<
                    LatestConsumer.LatestMapper>())
            .AddScoped<ITypeMapper<
                LatestConsumer.Summary,
                LatestConsumer.SummaryDto>>(serviceProvider =>
                serviceProvider.GetRequiredService<
                    LatestConsumer.LatestMapper>())
            .AddScoped<ITypeMapper<
                LatestConsumer.GenericSource<int>,
                LatestConsumer.GenericDestination<string>>>(
                serviceProvider => serviceProvider.GetRequiredService<
                    LatestConsumer.LatestMapper>())
            .AddScoped<ITypeMapper<
                LatestConsumer.NullableNumber,
                int?>>(serviceProvider =>
                serviceProvider.GetRequiredService<
                    LatestConsumer.LatestMapper>())
            .AddScoped<IMapper, Mapper>()
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }

    private sealed class PrefixFormatter : LatestConsumer.ILabelFormatter
    {
        private readonly ScopeState _state;

        public PrefixFormatter(ScopeState state)
        {
            _state = state;
        }

        public string Format(int value) => _state.Prefix + value;
    }

    private sealed class ScopeState
    {
        public string Prefix { get; set; } = string.Empty;
    }
}
