#nullable enable

using System;
using Morphant;

namespace Morphant.Generator.CompatibilityFixtures.PackageConsumer
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
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }

    internal static class Program
    {
        public static void Main()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(new Source { Value = 17 });

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "Unexpected mapping result.");
            }
        }
    }
}
