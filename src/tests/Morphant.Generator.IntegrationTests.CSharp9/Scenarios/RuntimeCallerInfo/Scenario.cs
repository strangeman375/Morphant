#nullable enable
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimeCallerInfo
{
    public sealed class Source { public int Value { get; set; } }
    public sealed class ConstructTag { }
    public sealed class ResolveTag { }
    public sealed class ConvertTag { }
    public sealed class Destination<T>
    {
        public Destination(CallSite site) => Site = site;
        public CallSite Site { get; }
    }
    public sealed class CallSite
    {
        public CallSite(int value,
            [CallerArgumentExpression("value")] string expression = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0,
            [CallerFilePath] string file = "")
        {
            Value = value;
            Expression = expression;
            Member = member;
            Line = line;
            File = file;
        }
        public int Value { get; }
        public string Expression { get; }
        public string Member { get; }
        public int Line { get; }
        public string File { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        private static CallSite Capture(int value,
            [CallerArgumentExpression("value")] string expression = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0,
            [CallerFilePath] string file = "") => new(value, expression, member, line, file);

        protected override void Configure(MapperBuilder builder)
        {
#line 4100 "RuntimeCallerInfoInput.cs"
            builder.Map<Source, Destination<ConstructTag>>().ConstructUsing(input => new(Capture(input.Value + 1)));
#line 4200 "RuntimeCallerInfoInput.cs"
            builder.Map<Source, Destination<ResolveTag>>().ResolveUsing((input, previous) => new(Capture(input.Value + 1)));
#line 4300 "RuntimeCallerInfoInput.cs"
            builder.Map<Source, Destination<ConvertTag>>().Convert(input => new(Capture(input!.Value + 1)));
#line 4400 "RuntimeCallerInfoInput.cs"
            builder.Map<Source, CallSite>().Convert(input => new(input!.Value + 1));
#line default
        }
    }

    public static class Scenario
    {
        public static void Verify(string callback, bool update)
        {
            var concrete = new TestMapper();
            var source = new Source { Value = 2 };
            CallSite site;
            switch (callback)
            {
                case "ConstructUsing":
                    var construct = (ITypeMapper<Source, Destination<ConstructTag>>)concrete;
                    site = (update ? construct.Update(source, null) : construct.Create(source)).Site;
                    break;
                case "ResolveUsing":
                    var resolve = (ITypeMapper<Source, Destination<ResolveTag>>)concrete;
                    site = (update ? resolve.Update(source, null) : resolve.Create(source)).Site;
                    break;
                case "Convert":
                    var convert = (ITypeMapper<Source, Destination<ConvertTag>>)concrete;
                    site = (update ? convert.Update(source, null) : convert.Create(source)).Site;
                    break;
                case "Constructor":
                    var constructor = (ITypeMapper<Source, CallSite>)concrete;
                    site = update ? constructor.Update(source, null) : constructor.Create(source);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(callback));
            }
            var expectedLine = callback switch
            {
                "ConstructUsing" => 4100,
                "ResolveUsing" => 4200,
                "Convert" => 4300,
                _ => 4400
            };
            var expectedExpression = callback is "Convert" or "Constructor" ? "input!.Value + 1" : "input.Value + 1";
            if (site.Value != 3 || site.Expression != expectedExpression || site.Member != "Configure" ||
                site.Line != expectedLine || Path.GetFileName(site.File) != "RuntimeCallerInfoInput.cs")
            {
                throw new InvalidOperationException(
                    $"Caller information changed: {site.Value}|{site.Expression}|{site.Member}|{site.Line}|{site.File}.");
            }
        }
    }
}
