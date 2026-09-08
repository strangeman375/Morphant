#nullable enable
using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullEntryPoints
{
    public sealed class Source { public int Id { get; set; } }
    public sealed class Destination { public int Id { get; set; } }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify(string entryPoint)
        {
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<Source, Destination>, TestMapper>()
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var direct = provider.GetRequiredService<ITypeMapper<Source, Destination>>();
            var facade = provider.GetRequiredService<IMapper>();
            var existing = new Destination { Id = 73 };
            var result = entryPoint switch
            {
                "CreateWithContext" => direct.Create(null, default),
                "UpdateWithContext" => direct.Update(null, existing, default),
                "Create" => direct.Create(null),
                "Update" => direct.Update(null, existing),
                "MapCreate" => facade.Map<Source, Destination>(null),
                "MapUpdate" => facade.Map<Source, Destination>(null, existing),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint))
            };
            if (result is not null || existing.Id != 73)
            {
                throw new InvalidOperationException(
                    "The default ReturnNull policy must return null without changing the previous destination.");
            }
        }
    }
}
