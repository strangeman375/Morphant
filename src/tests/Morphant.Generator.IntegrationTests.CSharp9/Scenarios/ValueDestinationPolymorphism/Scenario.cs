#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ValueDestinationPolymorphism
{
    public class Animal { public int Value { get; set; } }
    public sealed class Dog : Animal { }
    public readonly struct ValueDestination
    {
        public ValueDestination(int value) => Value = value;
        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, ValueDestination>()
                .ForDerived<Dog, ValueDestination>()
                .Convert(_ => new ValueDestination(-1));
            builder.Map<Animal, ValueDestination?>()
                .ForDerived<Dog, ValueDestination?>()
                .Convert(_ => new ValueDestination(-1));
            builder.Map<Dog, ValueDestination>()
                .Convert((source, previous, context) => new ValueDestination(
                    source!.Value + (context.Operation == MappingOperation.Update ? 100 : 0) +
                    (previous.HasValue ? 10 : 0)));
            builder.Map<Dog, ValueDestination?>()
                .Convert((source, previous, context) => new ValueDestination(
                    source!.Value + (context.Operation == MappingOperation.Update ? 100 : 0) +
                    (previous.HasValue ? 10 : 0)));
        }
    }

    public static class Scenario
    {
        public static void Verify(bool nullable, string operation)
        {
            var source = new Dog { Value = 7 };
            var mapper = new TestMapper();
            ValueDestination? result;
            if (nullable)
            {
                var typed = (ITypeMapper<Animal, ValueDestination?>)mapper;
                result = operation == "Create" ? typed.Create(source) :
                    typed.Update(source, operation == "UpdateNull" ? null : new ValueDestination(1));
            }
            else
            {
                var typed = (ITypeMapper<Animal, ValueDestination>)mapper;
                result = operation == "Create" ? typed.Create(source) :
                    typed.Update(source, new ValueDestination(1));
            }

            var expected = operation == "Create" ? 7 : operation == "UpdateNull" ? 107 : 117;
            if (result?.Value != expected)
                throw new InvalidOperationException("The value branch lost its source, operation or previous value.");
        }
    }
}
