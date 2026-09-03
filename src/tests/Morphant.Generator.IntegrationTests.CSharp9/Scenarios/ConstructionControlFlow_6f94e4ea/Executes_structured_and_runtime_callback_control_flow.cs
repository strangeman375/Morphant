// Compiled integration scenario: TypeMapperDeclarativeControlFlowTests/ConstructionControlFlowTests::Executes_structured_and_runtime_callback_control_flow
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConstructionControlFlow_6f94e4ea
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
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int LocalCount { get; private set; }

        public static int FailureCount { get; private set; }

        public static int DirectCount { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
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

                        default:
                            throw BuildFailure();
                    }
                });

            builder.Map<Source, IDirectDestination>()
                .ConstructUsing(source =>
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

            if (explicitValue.Id != 5 ||
                overridden.Id != 50 ||
                automatic.Id != 4 ||
                !ReferenceEquals(previous, reused) ||
                directCreated.Value != 107 ||
                directCreated.Path != "direct-first" ||
                !ReferenceEquals(directPrevious, directUpdated) ||
                directUpdated.Value != 208 ||
                directUpdated.Path != "direct-second" ||
                TestMapper.LocalCount != 4 ||
                TestMapper.FailureCount != 0 ||
                TestMapper.DirectCount != 1)
            {
                throw new InvalidOperationException(
                    "Structured construction control flow was lowered incorrectly.");
            }
        }
    }
}
