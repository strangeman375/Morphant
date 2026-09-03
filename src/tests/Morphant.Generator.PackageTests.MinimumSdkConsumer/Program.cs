#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace MinimumSdkConsumer
{
    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    internal static class Program
    {
        public static void Main()
        {
            var mapper = (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(new Source { Value = 17 });

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "The minimum supported SDK consumer did not execute " +
                    "generated mapping code.");
            }
        }
    }
}
