// Compiled integration scenario: TypeMapperNestedMapTests/ExternalLookupTests::Resolves_a_nested_mapper_from_another_assembly
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Generator.UnitTests.TestAssets.ExternalLookupScenario;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExternalLookup_1ec1f24c
{
    public sealed record OuterSource(IExternalNestedSource Child);

    public sealed class OuterDestination
    {
        public ExternalNestedDestination Child { get; set; } =
            new(-1);
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper<OuterMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Members((source, _) => new()
                {
                    Child = Create(source.Child)
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new OuterMapper();
            var child = new ExternalNestedMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<
                    OuterSource,
                    OuterDestination>>(outer)
                .AddSingleton<ITypeMapper<
                    IExternalNestedSource,
                    ExternalNestedDestination>>(child)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();

            var result = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(new ExternalNestedSource(5)));

            if (result.Child.Value != 15 || child.Calls != 1)
            {
                throw new InvalidOperationException(
                    "The application-wide nested pair was not used.");
            }
        }
    }
}
