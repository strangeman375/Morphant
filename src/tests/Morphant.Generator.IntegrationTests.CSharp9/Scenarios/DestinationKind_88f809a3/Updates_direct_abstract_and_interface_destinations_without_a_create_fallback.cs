// Compiled integration scenario: TypeMapperConventionTests/DestinationKindTests::Updates_direct_abstract_and_interface_destinations_without_a_create_fallback
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0035

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DestinationKind_88f809a3
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public interface IInterfaceDestination
    {
        int Value { get; set; }
    }

    public abstract class AbstractDestination
    {
        public int Value { get; set; }
    }

    public sealed class ConcreteDestination :
        AbstractDestination,
        IInterfaceDestination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, int>();
            builder.Map<Source, IInterfaceDestination>();
            builder.Map<Source, AbstractDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                Value = 47
            };
            var scalarMapper = (ITypeMapper<Source, int>)mapper;
            var interfaceMapper =
                (ITypeMapper<Source, IInterfaceDestination>)mapper;
            var abstractMapper =
                (ITypeMapper<Source, AbstractDestination>)mapper;
            var interfacePrevious = new ConcreteDestination();
            var abstractPrevious = new ConcreteDestination();

            if (scalarMapper.Update(
                    source,
                    13,
                    default(MappingContext)) != 13 ||
                !ReferenceEquals(
                    interfaceMapper.Update(
                        source,
                        interfacePrevious,
                        default(MappingContext)),
                    interfacePrevious) ||
                interfacePrevious.Value != 47 ||
                !ReferenceEquals(
                    abstractMapper.Update(
                        source,
                        abstractPrevious,
                        default(MappingContext)),
                    abstractPrevious) ||
                abstractPrevious.Value != 47)
            {
                throw new InvalidOperationException(
                    "A direct destination Update was not authoritative.");
            }

            try
            {
                _ = scalarMapper.Create(
                    source,
                    default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A direct destination unexpectedly used automatic construction.");
        }
    }
}
