#nullable enable
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InterfaceDestination
{
    public sealed class Source { public int Id { get; set; } }
    public interface IDestination { int Id { get; set; } }
    public struct Destination : IDestination { public int Id { get; set; } }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public int FactoryCalls { get; private set; }
        private IDestination CreateDestination()
        {
            FactoryCalls++;
            return new Destination { Id = -1 };
        }
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, IDestination>().ConstructUsing(source => CreateDestination());
    }

    public static class Scenario
    {
        public static void Verify(string operation)
        {
            var concrete = new TestMapper();
            var mapper = (ITypeMapper<Source, IDestination>)concrete;
            var source = new Source { Id = 11 };
            IDestination previous = new Destination { Id = 73 };
            var result = operation switch
            {
                "Create" => mapper.Create(source),
                "UpdateNull" => mapper.Update(source, null),
                "UpdateExisting" => mapper.Update(source, previous),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            var reuse = operation == "UpdateExisting";
            if (result is not Destination || result.Id != 11 || concrete.FactoryCalls != (reuse ? 0 : 1) ||
                (reuse && (!ReferenceEquals(result, previous) || previous.Id != 11)))
            {
                throw new InvalidOperationException("Mapping through an interface must mutate the retained box and skip its factory on reuse.");
            }
        }
    }
}
