#nullable enable
using System;
using Morphant;
using Integer = System.Int32;
using static Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExpressionContext.Helpers;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExpressionContext
{
    public sealed class Source { public int Value { get; set; } }
    public sealed class NamesTag { }
    public sealed class OverloadsTag { }
    public sealed class CheckedTag { }
    public sealed class UncheckedTag { }
    public sealed class Destination<T>
    {
        public Destination(string text) => Text = text;
        public string Text { get; }
    }
    public static class Helpers
    {
        public static string Describe(this int value, int suffix = 7) => value + ":" + suffix;
        public static string Pick(int value) => "int:" + value;
        public static string Pick(object value) => "object:" + value;
    }
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<NamesTag>>()
                .Construct(input => new(nameof(input) + ":" + nameof(Integer) + ":" + input.Value.Describe()));
            builder.Map<Source, Destination<OverloadsTag>>()
                .Convert(input => new(Pick(input!.Value) + "|" + Pick((object)input.Value)));
            builder.Map<Source, Destination<CheckedTag>>()
                .Convert(input => new(checked(input!.Value + int.MaxValue).ToString()));
            builder.Map<Source, Destination<UncheckedTag>>()
                .Convert(input => new(unchecked(input!.Value + int.MaxValue).ToString()));
        }
    }
    public static class Scenario
    {
        public static void VerifyNames()
        {
            ITypeMapper<Source, Destination<NamesTag>> mapper = new TestMapper();
            if (mapper.Create(new Source { Value = 2 }).Text != "input:Integer:2:7")
                throw new InvalidOperationException("nameof must retain source names and the extension must retain its optional argument.");
        }
        public static void VerifyOverloads()
        {
            ITypeMapper<Source, Destination<OverloadsTag>> mapper = new TestMapper();
            if (mapper.Create(new Source { Value = 2 }).Text != "int:2|object:2")
                throw new InvalidOperationException("Transferred invocations must retain their selected overloads.");
        }
        public static void VerifyOverflow(bool checkedExpression)
        {
            var concrete = new TestMapper();
            var source = new Source { Value = 2 };
            if (checkedExpression)
            {
                try
                {
                    ((ITypeMapper<Source, Destination<CheckedTag>>)concrete).Create(source);
                }
                catch (OverflowException) { return; }
                throw new InvalidOperationException("A checked expression must still throw on overflow.");
            }
            var result = ((ITypeMapper<Source, Destination<UncheckedTag>>)concrete).Create(source);
            if (result.Text != "-2147483647")
                throw new InvalidOperationException("An unchecked expression must retain integer wraparound.");
        }
    }
}
