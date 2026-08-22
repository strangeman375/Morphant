// Compiled integration scenario: exact derived-pair lookup law
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismLookupLaw_b82d0016
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

    [MorphantMapper]
    public partial class DerivedMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IDog, string>()
                .Convert(_ => "dog");
    }

    public sealed class Provider : IServiceProvider
    {
        private readonly int _derivedCount;
        private readonly BaseMapper _baseMapper = new();
        private readonly DerivedMapper _derivedMapper = new();

        public Provider(int derivedCount) =>
            _derivedCount = derivedCount;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IEnumerable<
                    ITypeMapper<IAnimal, object>>))
            {
                return new ITypeMapper<IAnimal, object>[]
                {
                    _baseMapper
                };
            }

            if (serviceType == typeof(IEnumerable<
                    ITypeMapper<IDog, string>>))
            {
                return _derivedCount switch
                {
                    0 => Array.Empty<ITypeMapper<IDog, string>>(),
                    1 => new ITypeMapper<IDog, string>[]
                    {
                        _derivedMapper
                    },
                    _ => new ITypeMapper<IDog, string>[]
                    {
                        _derivedMapper,
                        _derivedMapper
                    }
                };
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
                    "A missing derived pair was accepted.");
            }
            catch (MappingNotFoundException exception)
            {
                if (exception.SourceType != typeof(IDog) ||
                    exception.DestinationType != typeof(string))
                {
                    throw new InvalidOperationException(
                        "Missing lookup reported the base pair.");
                }
            }

            if (!Equals(
                    new Mapper(new Provider(1))
                        .Map<IAnimal, object>(source),
                    "dog"))
            {
                throw new InvalidOperationException(
                    "The single derived pair was not invoked.");
            }

            try
            {
                new Mapper(new Provider(2))
                    .Map<IAnimal, object>(source);
                throw new InvalidOperationException(
                    "Ambiguous derived registrations were accepted.");
            }
            catch (AmbiguousMappingException exception)
            {
                if (exception.SourceType != typeof(IDog) ||
                    exception.DestinationType != typeof(string))
                {
                    throw new InvalidOperationException(
                        "Ambiguous lookup reported the base pair.");
                }
            }
        }
    }
}
