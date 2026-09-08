// Compiled integration scenario: exact base-pair lookup precedes dispatch
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismBaseLookupLaw_b82d0017
{
    public interface IAnimal { }
    public interface IDog : IAnimal { }
    public sealed class Dog : IDog { }

    [MorphantMapper]
    public partial class BaseMapper : TypeMapper<BaseMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IAnimal, object>()
                .ForDerived<IDog, string>()
                .Convert(_ => "base");
    }

    public static class Scenario
    {
        public static void Verify(bool update)
        {
            foreach (var count in new[] { 0, 2 })
            {
                var services = new ServiceCollection()
                    .AddSingleton<IMapper, Mapper>()
                    .AddTransient<ITypeMapper<IDog, string>>(_ => throw new InvalidOperationException(
                        "The derived pair was queried before base selection."));
                for (var index = 0; index < count; index++)
                    services.AddSingleton<ITypeMapper<IAnimal, object>, BaseMapper>();
                using var provider = services.BuildServiceProvider();
                var mapper = provider.GetRequiredService<IMapper>();
                var source = new Dog();
                try
                {
                    _ = update ? mapper.Map<IAnimal, object>(source, "previous") : mapper.Map<IAnimal, object>(source);
                    throw new InvalidOperationException("Missing or duplicate base registrations must prevent dispatch.");
                }
                catch (MappingException exception)
                    when (((count == 0 && exception is MappingNotFoundException) ||
                           (count == 2 && exception is AmbiguousMappingException)) &&
                          exception.Operation == (update ? MappingOperation.Update : MappingOperation.Create) &&
                          exception.SourceType == typeof(IAnimal) && exception.DestinationType == typeof(object)) { }
            }
        }
    }
}
