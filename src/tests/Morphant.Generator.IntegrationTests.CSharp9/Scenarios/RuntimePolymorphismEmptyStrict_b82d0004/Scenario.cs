// Compiled integration scenario: an empty strict dispatch table
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismEmptyStrict_b82d0004
{
    public interface IAnimal { }
    public sealed class Dog : IAnimal { }
    public class ConcreteAnimal { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IAnimal, object>()
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw)
                .Convert(_ => "interface-base");
            builder.Map<ConcreteAnimal, object>()
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw)
                .Convert(_ => "concrete-base");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();

            if (!Equals(
                    ((ITypeMapper<ConcreteAnimal, object>)mapper)
                        .Create(new ConcreteAnimal()),
                    "concrete-base"))
            {
                throw new InvalidOperationException(
                    "An exact concrete source skipped the base plan.");
            }

            try
            {
                ((ITypeMapper<IAnimal, object>)mapper).Create(new Dog());
                throw new InvalidOperationException(
                    "An empty strict dispatch table accepted a subtype.");
            }
            catch (UnmatchedPolymorphicMappingException)
            {
            }
        }
    }
}
