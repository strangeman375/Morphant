// Compiled integration scenario: TypeMapperConvertTests/ValueTypeTests::Reports_an_empty_previous_value_when_Create_reads_it
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MissingPreviousValue_9d7a0105
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<int, int>()
                .Convert((source, previous) => source + previous.Value);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var generated = new TestMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<int, int>>(generated)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();
            var createFailed = false;

            try
            {
                mapper.Map<int, int>(2);
            }
            catch (OptionValueMissingException)
            {
                createFailed = true;
            }

            var updated = mapper.Map(2, 5);

            if (!createFailed || updated != 7)
            {
                throw new InvalidOperationException(
                    "Convert did not preserve the observable Option value " +
                    "contract.");
            }
        }
    }
}
