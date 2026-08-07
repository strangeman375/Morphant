// Compiled integration scenario: TypeMapperStructuredConstructTests/LifecycleTests::Keeps_an_unguarded_previous_selection_unsupported_for_Create
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_d46efb66
{
    public sealed class Source
    {
        public int Id { get; init; }

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

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct((source, previous) =>
                {
                    if (source.Reuse)
                    {
                        return previous;
                    }

                    return new(source.Id);
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var context = default(MappingContext);
            var created = mapper.Create(
                new Source { Id = 17 },
                context);
            var previous = new Destination(31);
            var updated = mapper.Update(
                new Source { Reuse = true },
                previous,
                context);

            if (created.Id != 17 ||
                !ReferenceEquals(previous, updated))
            {
                throw new InvalidOperationException(
                    "A reachable previous selection used the wrong operation semantics.");
            }

            try
            {
                mapper.Create(
                    new Source { Reuse = true },
                    context);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Create silently removed a reachable previous selection.");
        }
    }
}
