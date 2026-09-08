// Compiled integration scenario: exact derived-pair lookup law
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismLookupLaw_b82d0016
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

    [MorphantMapper]
    public partial class DerivedMapper : TypeMapper<DerivedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IDog, string>()
                .Convert(_ => "dog");
    }

    public static class Scenario
    {
        public static void Verify(bool update)
        {
            foreach (var count in new[] { 0, 1, 2 })
            {
                var services = new ServiceCollection()
                    .AddSingleton<ITypeMapper<IAnimal, object>, BaseMapper>()
                    .AddSingleton<IMapper, Mapper>();
                for (var index = 0; index < count; index++)
                    services.AddSingleton<ITypeMapper<IDog, string>, DerivedMapper>();
                using var provider = services.BuildServiceProvider();
                var mapper = provider.GetRequiredService<IMapper>();
                var source = new Dog();
                try
                {
                    var result = update ? mapper.Map<IAnimal, object>(source, "previous") : mapper.Map<IAnimal, object>(source);
                    if (count != 1 || !Equals(result, "dog"))
                        throw new InvalidOperationException("A matched derived pair must resolve exactly one registered mapper.");
                }
                catch (MappingException exception)
                    when (((count == 0 && exception is MappingNotFoundException) ||
                           (count == 2 && exception is AmbiguousMappingException)) &&
                          exception.Operation == (update ? MappingOperation.Update : MappingOperation.Create) &&
                          exception.SourceType == typeof(IDog) && exception.DestinationType == typeof(string)) { }
            }
        }
    }
}
