#nullable enable
using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationActivationFailure
{
    public sealed record Source(int Value);
    public sealed class Destination { public int Value { get; set; } }
    public sealed class ActivationException : Exception { }
    public sealed class Activation
    {
        public ActivationException Failure { get; } = new();
        public int Attempts { get; private set; }
        public void Run()
        {
            if (++Attempts == 1)
                throw Failure;
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public TestMapper(Activation activation) => activation.Run();
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify(bool update)
        {
            var activation = new Activation();
            using var provider = new ServiceCollection()
                .AddSingleton(activation)
                .AddTransient<ITypeMapper<Source, Destination>, TestMapper>()
                .AddSingleton<IMapper, Mapper>().BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();
            var previous = new Destination { Value = 3 };
            var source = new Source(17);
            try
            {
                _ = update ? mapper.Map(source, previous) : mapper.Map<Source, Destination>(source);
                throw new InvalidOperationException("DI activation failure was not propagated.");
            }
            catch (ActivationException exception) when (ReferenceEquals(exception, activation.Failure)) { }
            if (previous.Value != 3)
                throw new InvalidOperationException("Failed mapper activation changed the destination.");

            var result = update ? mapper.Map(source, previous) : mapper.Map<Source, Destination>(source);
            if (activation.Attempts != 2 || result.Value != 17 || (update && !ReferenceEquals(result, previous)))
                throw new InvalidOperationException("A failed activation must not poison subsequent mapper lookup.");
        }
    }
}
