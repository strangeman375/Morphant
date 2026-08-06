using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class ConventionMemberTests
{
    [Test]
    public void Completes_explicit_plans_after_constructor_direct_and_factory_results()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Seed { get; init; }

        public string Explicit { get; init; } = string.Empty;

        public string Convention { get; init; } = string.Empty;

        public int Field { get; init; }
    }

    public sealed class StructuredDestination
    {
        public StructuredDestination(int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }

        public string Explicit { get; set; } = string.Empty;

        public string Convention { get; set; } = string.Empty;

        public int Field;
    }

    public interface IDirectDestination
    {
        int Seed { get; }

        string Explicit { get; set; }

        string Convention { get; set; }

        int Field { get; set; }
    }

    public sealed class DirectDestination : IDirectDestination
    {
        public DirectDestination(int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }

        public string Explicit { get; set; } = string.Empty;

        public string Convention { get; set; } = string.Empty;

        public int Field { get; set; }
    }

    public sealed class FactoryDestination
    {
        public FactoryDestination()
        {
        }

        private FactoryDestination(int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }

        public string Explicit { get; set; } = string.Empty;

        public string Convention { get; set; } = string.Empty;

        public int Field;

        public static FactoryDestination Create(int seed) => new(seed);
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, StructuredDestination>()
                .Construct(source => new(seed: source.Seed))
                .Members((source, _) => new()
                {
                    Explicit = source.Explicit + "-structured"
                });

            builder.Map<Source, IDirectDestination>()
                .Construct(source => new DirectDestination(source.Seed))
                .Members((source, previous) => new()
                {
                    Explicit = previous.HasValue
                        ? previous.Value.Explicit + "-updated"
                        : source.Explicit + "-direct"
                });

            builder.Map<Source, FactoryDestination>()
                .Construct(source => new(
                    ByFactory(() => FactoryDestination.Create(source.Seed))))
                .Members((source, previous) => new()
                {
                    Explicit = previous.HasValue
                        ? previous.Value.Explicit + "-updated"
                        : source.Explicit + "-factory"
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var source = new Source
            {
                Seed = 7,
                Explicit = "explicit",
                Convention = "convention",
                Field = 11
            };
            var structured =
                ((ITypeMapper<Source, StructuredDestination>)mapper)
                .Create(source, context);
            var direct =
                ((ITypeMapper<Source, IDirectDestination>)mapper)
                .Create(source, context);
            var factory =
                ((ITypeMapper<Source, FactoryDestination>)mapper)
                .Create(source, context);

            if (structured.Seed != 7 ||
                structured.Explicit != "explicit-structured" ||
                structured.Convention != "convention" ||
                structured.Field != 11 ||
                direct.Seed != 7 ||
                direct.Explicit != "explicit-direct" ||
                direct.Convention != "convention" ||
                direct.Field != 11 ||
                factory.Seed != 7 ||
                factory.Explicit != "explicit-factory" ||
                factory.Convention != "convention" ||
                factory.Field != 11)
            {
                throw new InvalidOperationException(
                    "A creation result did not receive its member plan.");
            }

            var previous = new DirectDestination(99);
            previous.Explicit = "previous";
            var updated =
                ((ITypeMapper<Source, IDirectDestination>)mapper)
                .Update(source, previous, context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Seed != 99 ||
                updated.Explicit != "previous-updated" ||
                updated.Convention != "convention" ||
                updated.Field != 11)
            {
                throw new InvalidOperationException(
                    "An existing result did not receive its member plan.");
            }

            var previousFactory = new FactoryDestination
            {
                Explicit = "factory-previous"
            };
            var updatedFactory =
                ((ITypeMapper<Source, FactoryDestination>)mapper)
                .Update(source, previousFactory, context);

            if (!ReferenceEquals(previousFactory, updatedFactory) ||
                updatedFactory.Explicit != "factory-previous-updated" ||
                updatedFactory.Convention != "convention" ||
                updatedFactory.Field != 11)
            {
                throw new InvalidOperationException(
                    "A factory result used the wrong previous member plan.");
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
