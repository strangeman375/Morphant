// Compiled integration scenario: runtime polymorphic selection
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismSelection_b82d0001
{
    public interface IAnimal { }
    public interface IDog : IAnimal { }
    public interface IServiceDog : IDog { }
    public sealed class Dog : IDog { }
    public sealed class ProxyDog : IDog { }
    public sealed class ServiceDog : IServiceDog { }
    public sealed class Cat : IAnimal { }
    public abstract class AbstractAnimal { }
    public sealed class AbstractDog : AbstractAnimal { }
    public sealed class AbstractCat : AbstractAnimal { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IAnimal, object>()
                .ForDerived<IDog, string>()
                .ForDerived<IServiceDog, string>()
                .Convert(_ => "base");
            builder.Map<IDog, string>()
                .Convert(_ => "dog");
            builder.Map<IServiceDog, string>()
                .Convert(_ => "service");
            builder.Map<AbstractAnimal, object>()
                .ForDerived<AbstractDog, string>()
                .Convert(_ => "abstract-base");
            builder.Map<AbstractDog, string>()
                .Convert(_ => "abstract-dog");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<IAnimal, object>)new TestMapper();
            var abstractMapper =
                (ITypeMapper<AbstractAnimal, object>)new TestMapper();

            if (!Equals(mapper.Create(new Dog()), "dog") ||
                !Equals(mapper.Create(new ProxyDog()), "dog") ||
                !Equals(mapper.Create(new ServiceDog()), "service") ||
                !Equals(mapper.Create(new Cat()), "base") ||
                !Equals(
                    abstractMapper.Create(new AbstractDog()),
                    "abstract-dog") ||
                !Equals(
                    abstractMapper.Create(new AbstractCat()),
                    "abstract-base"))
            {
                throw new InvalidOperationException(
                    "Most-specific polymorphic selection is incorrect.");
            }
        }
    }
}
