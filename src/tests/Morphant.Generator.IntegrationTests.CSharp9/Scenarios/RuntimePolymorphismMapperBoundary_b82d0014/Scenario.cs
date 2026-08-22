// Compiled integration scenario: application and standalone routing boundaries
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismMapperBoundary_b82d0014
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
        private readonly BaseMapper _baseMapper = new();
        private readonly DerivedMapper _derivedMapper = new();

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
                return new ITypeMapper<IDog, string>[]
                {
                    _derivedMapper
                };
            }

            return null;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var dog = new Dog();
            var application = new Mapper(new Provider());

            if (!Equals(
                    application.Map<IAnimal, object>(dog),
                    "dog"))
            {
                throw new InvalidOperationException(
                    "Application lookup did not reach the derived mapper.");
            }

            var standalone =
                (ITypeMapper<IAnimal, object>)new BaseMapper();

            try
            {
                standalone.Create(dog);
                throw new InvalidOperationException(
                    "Standalone lookup invented a derived registration.");
            }
            catch (MappingNotFoundException exception)
            {
                if (exception.SourceType != typeof(IDog) ||
                    exception.DestinationType != typeof(string))
                {
                    throw new InvalidOperationException(
                        "Standalone lookup reported the wrong exact pair.");
                }
            }
        }
    }
}
