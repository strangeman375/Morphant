// Compiled integration scenario: application and standalone routing boundaries
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismMapperBoundary_b82d0014
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
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<IAnimal, object>, BaseMapper>()
                .AddSingleton<ITypeMapper<IDog, string>, DerivedMapper>()
                .AddSingleton<IMapper, Mapper>().BuildServiceProvider();
            var application = provider.GetRequiredService<IMapper>();
            var dog = new Dog();
            var result = update ? application.Map<IAnimal, object>(dog, "previous") : application.Map<IAnimal, object>(dog);
            if (!Equals(result, "dog"))
                throw new InvalidOperationException("Application lookup did not reach the derived mapper.");

            var standalone = (ITypeMapper<IAnimal, object>)new BaseMapper();
            try
            {
                _ = update ? standalone.Update(dog, "previous") : standalone.Create(dog);
                throw new InvalidOperationException("Standalone lookup invented a derived registration.");
            }
            catch (MappingNotFoundException exception)
                when (exception.Operation == (update ? MappingOperation.Update : MappingOperation.Create) &&
                      exception.SourceType == typeof(IDog) && exception.DestinationType == typeof(string)) { }
        }
    }
}
