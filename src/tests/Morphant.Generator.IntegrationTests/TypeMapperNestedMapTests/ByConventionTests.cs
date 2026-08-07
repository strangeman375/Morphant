using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class ByConventionTests
{
    [Test]
    public void Executes_nested_map_in_a_convention_constructor_override()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace TestCase
{
    public sealed record ChildSource(int Value);

    public sealed record ChildDestination(int Value);

    public sealed record OuterSource(int Id, ChildSource Child);

    public sealed class OuterDestination
    {
        public OuterDestination(int id, ChildDestination child)
        {
            Id = id;
            Child = child;
        }

        public int Id { get; }

        public ChildDestination Child { get; }
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Construct(source => new(
                    ByConvention(),
                    new()
                    {
                        child = Create<ChildDestination>(source.Child)
                    }));
    }

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public ChildDestination Create(
            ChildSource? source,
            MappingContext context) =>
            new(source!.Value + 1);

        public ChildDestination Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context) =>
            throw new InvalidOperationException(
                "The nested Update method was selected.");
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
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<OuterSource, OuterDestination>>(
                outer);
            provider.Add<ITypeMapper<ChildSource, ChildDestination>>(
                new ChildMapper());
            var mapper = new Mapper(provider);
            var result = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(7, new ChildSource(10)));

            if (result.Id != 7 || result.Child.Value != 11)
            {
                throw new InvalidOperationException(
                    "The convention constructor override was ignored.");
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
