#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionCallerInformation.Calls;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionCallerInformation
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
        public void Add<T>(T value, [CallerMemberName] object? member = null) => Value = value + ":" + member;
        public void Add(int value, object? member) => throw new InvalidOperationException("Wrong caller argument conversion.");
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
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<int, string> mapper = new Mapper();
            foreach (var update in new[] { false, true })
            {
                Trace.Events.Clear();
                var actual = update ? mapper.Update(3, "previous") : mapper.Create(3);
                var file = System.IO.Path.Combine(SourceDirectory(), "CollectionCalls.cs");
                var expected = "6:7:Configure:" + file + ":100:source.Twice()|8:9:Configure:" + file + ":100:(source + 1).Twice()|6:Configure:110:source.Twice()|3:Configure";
                if (actual != expected || string.Join(",", Trace.Events) != "new,value:3,add:6,value:4,add:8,value:3")
                    throw new InvalidOperationException("Collection caller values, overloads, argument expressions and order must match their source call sites: " + actual + "; " + string.Join(",", Trace.Events));
            }
        }

        private static string SourceDirectory([CallerFilePath] string file = "") => System.IO.Path.GetDirectoryName(file)!;
    }
}
