using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class ConstructionControlFlowTests
{
    [Test]
    public void Executes_structured_construction_control_flow_and_opaque_factory_block()
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
        public int Id { get; init; }

        public int Mode { get; init; }

        public bool Override { get; init; }

        public bool Reuse { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }

    public interface IDirectDestination
    {
        int Value { get; set; }

        string Path { get; set; }
    }

    public sealed class DirectDestination : IDirectDestination
    {
        public DirectDestination(int value)
        {
            Value = value;
        }

        public int Value { get; set; }

        public string Path { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int LocalCount { get; private set; }

        public static int FactoryCount { get; private set; }

        public static int FailureCount { get; private set; }

        public static int DirectCount { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Construct((source, previous) =>
                {
                    const int offset = 2;
                    var id = Track(source.Id + offset);

                    if (previous.HasValue && source.Reuse)
                    {
                        return previous;
                    }

                    switch (source.Mode)
                    {
                        case 0:
                            return new(id);

                        case 1:
                            return new(
                                ByConvention(),
                                new()
                                {
                                    id = source.Override
                                        ? id * 10
                                        : Auto()
                                });

                        case 2:
                            return new(ByFactory<Destination>(() =>
                            {
                                var value = source.Id;

                                for (var index = 0; index < 2; index++)
                                {
                                    value++;
                                }

                                FactoryCount++;
                                return new Destination(value);
                            }));

                        default:
                            throw BuildFailure();
                    }
                });

            builder.Map<Source, IDirectDestination>()
                .Construct(source =>
                {
                    DirectCount++;
                    return new DirectDestination(source.Id);
                })
                .Members((source, _) => source.Override
                    ? new()
                    {
                        Value = source.Id + 100,
                        Path = "direct-first"
                    }
                    : new()
                    {
                        Value = source.Id + 200,
                        Path = "direct-second"
                    });
        }

        private static int Track(int value)
        {
            LocalCount++;
            return value;
        }

        private static Exception BuildFailure()
        {
            FailureCount++;
            return new InvalidOperationException("construction");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var explicitValue = mapper.Create(
                new Source { Id = 3, Mode = 0 },
                context);
            var overridden = mapper.Create(
                new Source
                {
                    Id = 3,
                    Mode = 1,
                    Override = true
                },
                context);
            var automatic = mapper.Create(
                new Source { Id = 4, Mode = 1 },
                context);
            var factory = mapper.Create(
                new Source { Id = 5, Mode = 2 },
                context);
            var previous = new Destination(17);
            var reused = mapper.Update(
                new Source { Id = 6, Reuse = true },
                previous,
                context);
            var directMapper =
                (ITypeMapper<Source, IDirectDestination>)
                new TestMapper();
            var directCreated = directMapper.Create(
                new Source { Id = 7, Override = true },
                context);
            var directPrevious = new DirectDestination(3);
            var directUpdated = directMapper.Update(
                new Source { Id = 8 },
                directPrevious,
                context);
            var directHelperCount = 0;

            foreach (var method in typeof(TestMapper).GetMethods(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic))
            {
                if (method.Name.StartsWith(
                    "__ConstructDestination",
                    StringComparison.Ordinal))
                {
                    directHelperCount++;
                }
            }

            if (explicitValue.Id != 5 ||
                overridden.Id != 50 ||
                automatic.Id != 4 ||
                factory.Id != 7 ||
                !ReferenceEquals(previous, reused) ||
                directCreated.Value != 107 ||
                directCreated.Path != "direct-first" ||
                !ReferenceEquals(directPrevious, directUpdated) ||
                directUpdated.Value != 208 ||
                directUpdated.Path != "direct-second" ||
                TestMapper.LocalCount != 5 ||
                TestMapper.FactoryCount != 1 ||
                TestMapper.FailureCount != 0 ||
                TestMapper.DirectCount != 1 ||
                directHelperCount != 1)
            {
                throw new InvalidOperationException(
                    "Structured construction control flow was lowered incorrectly.");
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
