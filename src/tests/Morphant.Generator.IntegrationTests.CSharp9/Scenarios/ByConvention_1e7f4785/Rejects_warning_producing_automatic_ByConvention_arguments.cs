// Compiled integration scenario: TypeMapperConstructorSelectionTests/ByConventionTests::Rejects_warning_producing_automatic_ByConvention_arguments
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByConvention_1e7f4785
{
    public sealed class Source
    {
        public string? Name { get; init; }
    }

    public sealed class Destination
    {
        public Destination(string name, int code = 0)
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Single)
                .Construct(_ => new(
                    ByConvention(),
                    new()
                    {
                        code = 47
                    }));
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();

            try
            {
                mapper.Create(
                    new Source { Name = null },
                    default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A nullable warning was accepted for automatic ByConvention mapping.");
        }
    }
}
