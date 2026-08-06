using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class SharingTests
{
    [Test]
    public void Shares_equivalent_nested_calls_across_construction_and_members()
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

    public sealed record OuterSource(ChildSource Child);

    public sealed class OuterDestination
    {
        public OuterDestination(ChildDestination constructorValue)
        {
            ConstructorValue = constructorValue;
        }

        public ChildDestination ConstructorValue { get; }

        public ChildDestination MemberValue { get; set; } = new(-1);

        public ChildDestination UpdateValue { get; set; } = new(-1);
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Construct(source => new(Create(source.Child)))
                .Members((source, _) => new()
                {
                    MemberValue =
                        Create<ChildDestination>(source.Child),
                    UpdateValue = Update(source.Child, null)
                });
    }

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public int CreateCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public ChildDestination Create(
            ChildSource? source,
            MappingContext context)
        {
            CreateCalls++;
            return new ChildDestination(source!.Value + 10);
        }

        public ChildDestination Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context)
        {
            UpdateCalls++;
            return new ChildDestination(source!.Value + 20);
        }
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
            var child = new ChildMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<OuterSource, OuterDestination>>(
                outer);
            provider.Add<ITypeMapper<ChildSource, ChildDestination>>(
                child);
            var mapper = new Mapper(provider);

            var result = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(new ChildSource(5)));

            if (!ReferenceEquals(
                    result.ConstructorValue,
                    result.MemberValue) ||
                result.ConstructorValue.Value != 15 ||
                result.UpdateValue.Value != 25 ||
                child.CreateCalls != 1 ||
                child.UpdateCalls != 1)
            {
                throw new InvalidOperationException(
                    "Nested semantic identity or operation identity is " +
                    "incorrect.");
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
