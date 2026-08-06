using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class PreviousAndResultTests
{
    [Test]
    public void Uses_only_explicit_outer_previous_and_result_children()
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
        public OuterDestination(ChildDestination seed)
        {
            Seed = seed;
        }

        public ChildDestination Seed { get; }

        public ChildDestination FromPrevious { get; set; } = new(-1);

        public ChildDestination FromResult { get; set; } = new(-1);
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Construct((source, previous) =>
                    previous.HasValue
                        ? new(new ChildDestination(30))
                        : new(new ChildDestination(10)))
                .Members((source, previous, result) => new()
                {
                    FromPrevious = Map<ChildDestination>(
                        source.Child,
                        previous.HasValue
                            ? previous.Value.Seed
                            : null),
                    FromResult = Map<ChildDestination>(
                        source.Child,
                        result.Seed)
                });
    }

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public List<int?> PreviousValues { get; } = new();

        public ChildDestination Map(
            ChildSource? source,
            MappingContext context) =>
            throw new InvalidOperationException(
                "A two-argument nested Map became Create.");

        public ChildDestination Map(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context)
        {
            if (context.Operation != MappingOperation.Update)
            {
                throw new InvalidOperationException(
                    "A two-argument nested Map did not use Update.");
            }

            PreviousValues.Add(destination?.Value);
            return new ChildDestination(
                source!.Value + (destination?.Value ?? 100));
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
            var source = new OuterSource(new ChildSource(1));

            var created = mapper.Map<OuterSource, OuterDestination>(
                source);
            var previous = new OuterDestination(
                new ChildDestination(20));
            var updated = mapper.Map(source, previous);

            if (created.Seed.Value != 10 ||
                created.FromPrevious.Value != 101 ||
                created.FromResult.Value != 11 ||
                ReferenceEquals(previous, updated) ||
                previous.Seed.Value != 20 ||
                updated.Seed.Value != 30 ||
                updated.FromPrevious.Value != 21 ||
                updated.FromResult.Value != 31)
            {
                throw new InvalidOperationException(
                    "Outer previous/result child selection is incorrect.");
            }

            int?[] expected = { null, 10, 20, 30 };

            if (child.PreviousValues.Count != expected.Length)
            {
                throw new InvalidOperationException(
                    "A nested mapping was skipped or duplicated.");
            }

            for (var index = 0; index < expected.Length; index++)
            {
                if (child.PreviousValues[index] != expected[index])
                {
                    throw new InvalidOperationException(
                        "The generator substituted a child implicitly.");
                }
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
