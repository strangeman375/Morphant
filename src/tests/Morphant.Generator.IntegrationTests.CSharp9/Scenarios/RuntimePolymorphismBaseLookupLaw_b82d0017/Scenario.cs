// Compiled integration scenario: exact base-pair lookup precedes dispatch
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismBaseLookupLaw_b82d0017
{
    public interface IAnimal { }
    public interface IDog : IAnimal { }
    public sealed class Dog : IDog { }

    [MorphantMapper]
    public partial class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IAnimal, object>()
                .ForDerived<IDog, string>()
                .Convert(_ => "base");
    }

    public sealed class Provider : IServiceProvider
    {
        private readonly int _baseCount;
        private readonly BaseMapper _baseMapper = new();

        public Provider(int baseCount) =>
            _baseCount = baseCount;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IEnumerable<
                    ITypeMapper<IAnimal, object>>))
            {
                return _baseCount switch
                {
                    0 => Array.Empty<ITypeMapper<IAnimal, object>>(),
                    1 => new ITypeMapper<IAnimal, object>[]
                    {
                        _baseMapper
                    },
                    _ => new ITypeMapper<IAnimal, object>[]
                    {
                        _baseMapper,
                        _baseMapper
                    }
                };
            }

            if (serviceType == typeof(IEnumerable<
                    ITypeMapper<IDog, string>>))
            {
                throw new InvalidOperationException(
                    "The derived pair was queried before base selection.");
            }

            return null;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var source = (IAnimal)new Dog();

            try
            {
                new Mapper(new Provider(0))
                    .Map<IAnimal, object>(source);
                throw new InvalidOperationException(
                    "A missing base pair was accepted.");
            }
            catch (MappingNotFoundException exception)
            {
                AssertBasePair(exception);
            }

            try
            {
                new Mapper(new Provider(2))
                    .Map<IAnimal, object>(source);
                throw new InvalidOperationException(
                    "Ambiguous base pairs were accepted.");
            }
            catch (AmbiguousMappingException exception)
            {
                AssertBasePair(exception);
            }
        }

        private static void AssertBasePair(MappingException exception)
        {
            if (exception.SourceType != typeof(IAnimal) ||
                exception.DestinationType != typeof(object))
            {
                throw new InvalidOperationException(
                    "Lookup reported a derived pair before base selection.");
            }
        }
    }
}
