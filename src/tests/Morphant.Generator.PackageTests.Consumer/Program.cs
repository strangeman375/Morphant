#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.PackageTests.Consumer
{
    public sealed class Source
    {
        public int Value { get; init; }

        public int ImplicitOnly { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; } = 41;

        public int ImplicitOnly { get; set; } = 43;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) => new()
                {
                    Value = source.Value
                });
    }

    internal static class Program
    {
        public static void Main()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(
                new Source { Value = 17, ImplicitOnly = 73 },
                default(MappingContext));

            if (result.Value != 17 ||
                result.ImplicitOnly != 43 ||
                typeof(global::Morphant.Generated.Types.A_Morphant_002EGenerator_002EPackageTests_002EConsumer.N_Morphant.N_Generator.N_PackageTests.N_Consumer.Plans.DestinationMembers).Name !=
                "DestinationMembers")
            {
                throw new InvalidOperationException(
                    "The generated Members API or packaged Explicit member " +
                    "selection was not available to the consumer.");
            }
        }
    }
}
