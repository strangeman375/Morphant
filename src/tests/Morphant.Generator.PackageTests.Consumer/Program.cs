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
    }

    public sealed class Destination
    {
        public int Value { get; set; } = 41;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    internal static class Program
    {
        public static void Main()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(
                new Source { Value = 17 },
                default(MappingContext));

            if (result.Value != 41)
            {
                throw new InvalidOperationException(
                    "MorphantMemberSelection was not visible to the " +
                    "packaged generator.");
            }
        }
    }
}
