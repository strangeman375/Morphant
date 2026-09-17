namespace Morphant.Generator.UnitTests.ExtensionInvocationTests.Usage;

internal sealed partial class ExtensionInvocationTests
{
    // lang=c#
    private const string CollectionCallerInformationSource =
"""
#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Morphant;
using Calls;
namespace Calls
{
    public static class Operations
    {
        public static int Twice(this int value)
        {
            ExtensionCases.Trace.Events.Add("value:" + value);
            return value * 2;
        }
    }
}
namespace ExtensionCases
{
    public static class Trace
    {
        public static readonly List<string> Events = new();
    }
    public sealed class Bag : IEnumerable
    {
        public readonly List<string> Values = new();
        public Bag() => Trace.Events.Add("new");
        public IEnumerator GetEnumerator() => Values.GetEnumerator();
        public void Add(int value, int added = 7,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0, [CallerArgumentExpression("value")] string expression = "")
        {
            Trace.Events.Add("add:" + value);
            Values.Add(value + ":" + added + ":" + member + ":" + file + ":" + line + ":" + expression);
        }
        public void Add(int value, int added, string member, string file, long line, string expression) =>
            throw new InvalidOperationException("Wrong overload.");
    }
    public sealed class ExtendedBag : IEnumerable
    {
        public string Value = "";
        public IEnumerator GetEnumerator() => Array.Empty<int>().GetEnumerator();
    }
    public static class CollectionOperations
    {
        public static void Add(this ExtendedBag bag, int value,
            [CallerMemberName] string member = "", [CallerLineNumber] int line = 0,
            [CallerArgumentExpression("value")] string expression = "") =>
            bag.Value = value + ":" + member + ":" + line + ":" + expression;
    }
    public sealed class BoxedBag : IEnumerable
    {
        public string Value = "";
        public IEnumerator GetEnumerator() => Array.Empty<int>().GetEnumerator();
        public void Add(int value, [CallerMemberName] object? member = null) => Value = value + ":" + member;
        public void Add(int value, string member) => throw new InvalidOperationException("Wrong caller argument conversion.");
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<int, string>().Convert(source =>
        {
#line 100 "CollectionCalls.cs"
            var bag = new Bag { source.Twice(), { (source + 1).Twice(), 9 } };
#line 110 "CollectionCalls.cs"
            var extended = new ExtendedBag { source.Twice() };
#line 120 "CollectionCalls.cs"
            var boxed = new BoxedBag { source };
#line default
            return string.Join("|", bag.Values) + "|" + extended.Value + "|" + boxed.Value;
        });
    }
}
""";
}
