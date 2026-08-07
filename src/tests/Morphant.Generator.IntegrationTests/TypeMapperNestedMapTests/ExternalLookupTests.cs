using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class ExternalLookupTests
{
    [Test]
    public void Resolves_a_nested_mapper_from_another_assembly()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Generator.UnitTests.TestAssets;

namespace TestCase
{
    public sealed record OuterSource(IReferencedNestedSource Child);

    public sealed class OuterDestination
    {
        public ReferencedNestedDestination Child { get; set; } =
            new(-1);
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Members((source, _) => new()
                {
                    Child = Create(source.Child)
                });
    }

    public sealed class ManualServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var service)
                ? service
                : null;

        public void Add<TService>(TService service)
            where TService : class =>
            _services[typeof(IEnumerable<TService>)] =
                new TService[] { service };
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new OuterMapper();
            var child = new ReferencedNestedMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<OuterSource, OuterDestination>>(
                outer);
            provider.Add<ITypeMapper<
                IReferencedNestedSource,
                ReferencedNestedDestination>>(child);
            var mapper = new Mapper(provider);

            var result = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(new ReferencedNestedSource(5)));

            if (result.Child.Value != 15 || child.Calls != 1)
            {
                throw new InvalidOperationException(
                    "The application-wide nested pair was not used.");
            }
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
