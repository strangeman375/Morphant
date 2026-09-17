namespace Morphant.Generator.UnitTests.ExtensionInvocationTests.Usage;

internal sealed partial class ExtensionInvocationTests
{
    // lang=c#
    private const string CommentMarkersSource =
"""
#nullable enable
#pragma warning disable CS1591
using System;
using Morphant;
using Calls;
namespace Calls
{
    public static class Operations
    {
        public static int Twice(this int value) => value * 2;
        public static string Echo(this string value) => value;
        public static void Record(this string value) { }
    }
}
namespace ExtensionCases
{
    [MorphantMapper]
    public partial class PlainMapper : TypeMapper<PlainMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<int, int>().Convert(source =>
            Operations.Twice/*Morphant.ExtensionCall*/(source) + source /*Morphant.ExtensionConditional:bad*/);
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<int, string>().Convert(source =>
        {
            var direct = Operations.Twice/*Morphant.ExtensionCall*/(source);
            var value = source /*Morphant.ExtensionConditional:bad*/ + 1;
            var reduced = source /*Morphant.Extension_Call*/ .Twice();
            string? text = source == 0 ? null : "value";
            Action action = () /*Morphant.ExtensionBody:AA==*/ => text /*Morphant.ExtensionConditional:AAAA*/ ?.Record();
            action();
            return direct + ":" + value + ":" + reduced + ":" + text?.Echo() + ":" + "/*Morphant.Extension__Call*/";
        });
    }
}
""";
}
