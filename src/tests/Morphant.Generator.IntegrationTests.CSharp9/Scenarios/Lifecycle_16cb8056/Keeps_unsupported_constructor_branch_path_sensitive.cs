// Compiled integration scenario: TypeMapperStructuredConstructTests/LifecycleTests::Keeps_unsupported_constructor_branch_path_sensitive
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0037

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_16cb8056
{
    public readonly struct Source
    {
        public int Id { get; init; }

        public bool Invalid { get; init; }
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
                .Construct(source =>
                    source.Invalid
                        ? new(Ignore())
                        : new(source.Id));
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var context = default(MappingContext);
            var valid = mapper.Create(
                new Source { Id = 17 },
                context);

            if (valid.Id != 17)
            {
                throw new InvalidOperationException(
                    "The reachable constructor branch was not executed.");
            }

            try
            {
                mapper.Create(
                    new Source { Id = 17, Invalid = true },
                    context);
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An unsupported reachable branch did not fail.");
        }
    }
}
