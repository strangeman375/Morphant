using System;
using System.IO;
using System.Runtime.CompilerServices;
using Morphant;
using Stage05Audit.Cases.Extensions;
using Integer = System.Int32;
using static Stage05Audit.Cases.Probe;

namespace Stage05Audit.Cases.Extensions
{
    public static class TextExtensions
    {
        public static string Describe(this int value, int suffix = 7)
            => value + ":" + suffix;
    }
}

namespace Stage05Audit.Cases
{
    public sealed class Source { public int Value { get; set; } }
    public sealed class ConstructTag { }
    public sealed class ResolveTag { }
    public sealed class ConstructUsingTag { }
    public sealed class ResolveUsingTag { }
    public sealed class ConvertTag { }
    public sealed class NamesTag { }
    public sealed class OverloadsTag { }
    public sealed class CheckedTag { }
    public sealed class UncheckedTag { }

    public sealed class Destination<T>
    {
        public Destination(string text) { Text = text; }
        public string Text { get; }
    }

    public sealed class MemberDestination
    {
        public string Text { get; set; } = "";
    }

    public sealed class CallerDestination
    {
        public CallerDestination(int value,
            [CallerArgumentExpression("value")] string expression = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0,
            [CallerFilePath] string file = "")
        {
            Text = $"{value}|{expression}|{member}|{line}|{Path.GetFileName(file)}";
        }
        public string Text { get; }
    }

    public static class Probe
    {
        public static string Caller(int value,
            [CallerArgumentExpression("value")] string expression = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0,
            [CallerFilePath] string file = "")
            => $"{value}|{expression}|{member}|{line}|{Path.GetFileName(file)}";
        public static string Pick(int value) => "int:" + value;
        public static string Pick(object value) => "object:" + value;
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
#line 200 "Stage05Input.cs"
            builder.Map<Source, Destination<ConstructTag>>().Construct(input => new(Caller(input.Value + 1)));
#line 210 "Stage05Input.cs"
            builder.Map<Source, Destination<ResolveTag>>().Resolve((input, previous) => new(Caller(input.Value + 1)));
#line 220 "Stage05Input.cs"
            builder.Map<Source, MemberDestination>().Members(input => new() { Text = Caller(input.Value + 1) });
#line 230 "Stage05Input.cs"
            builder.Map<Source, Destination<ConstructUsingTag>>().ConstructUsing(input => new(Caller(input.Value + 1)));
#line 240 "Stage05Input.cs"
            builder.Map<Source, Destination<ResolveUsingTag>>().ResolveUsing((input, previous) => new(Caller(input.Value + 1)));
#line 250 "Stage05Input.cs"
            builder.Map<Source, Destination<ConvertTag>>().Convert(input => new(Caller(input!.Value + 1)));
#line 260 "Stage05Input.cs"
            builder.Map<Source, CallerDestination>().Convert(input => new(input!.Value + 1));
#line default
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
        public static void Run()
        {
            var mapper = new Mapper();
            var source = new Source { Value = 2 };
            Check.Equal("Construct caller info", "3|input.Value + 1|Configure|200|Stage05Input.cs",
                ((ITypeMapper<Source, Destination<ConstructTag>>)mapper).Create(source).Text);
            Check.Equal("Resolve caller info", "3|input.Value + 1|Configure|210|Stage05Input.cs",
                ((ITypeMapper<Source, Destination<ResolveTag>>)mapper).Create(source).Text);
            Check.Equal("Members caller info", "3|input.Value + 1|Configure|220|Stage05Input.cs",
                ((ITypeMapper<Source, MemberDestination>)mapper).Create(source).Text);
            Check.Equal("ConstructUsing caller info", "3|input.Value + 1|Configure|230|Stage05Input.cs",
                ((ITypeMapper<Source, Destination<ConstructUsingTag>>)mapper).Create(source).Text);
            Check.Equal("ResolveUsing caller info", "3|input.Value + 1|Configure|240|Stage05Input.cs",
                ((ITypeMapper<Source, Destination<ResolveUsingTag>>)mapper).Create(source).Text);
            Check.Equal("Convert caller info", "3|input!.Value + 1|Configure|250|Stage05Input.cs",
                ((ITypeMapper<Source, Destination<ConvertTag>>)mapper).Create(source).Text);
            Check.Equal("Constructor caller info", "3|input!.Value + 1|Configure|260|Stage05Input.cs",
                ((ITypeMapper<Source, CallerDestination>)mapper).Create(source).Text);
            Check.Equal("nameof, alias, extension", "input:" + nameof(Integer) + ":2:7",
                ((ITypeMapper<Source, Destination<NamesTag>>)mapper).Create(source).Text);
            Check.Equal("Overloads", "int:2|object:2",
                ((ITypeMapper<Source, Destination<OverloadsTag>>)mapper).Create(source).Text);
            Check.Throws<OverflowException>("checked overflow", () =>
                ((ITypeMapper<Source, Destination<CheckedTag>>)mapper).Create(source));
            Check.Equal("unchecked overflow", "-2147483647",
                ((ITypeMapper<Source, Destination<UncheckedTag>>)mapper).Create(source).Text);
        }
    }
}
