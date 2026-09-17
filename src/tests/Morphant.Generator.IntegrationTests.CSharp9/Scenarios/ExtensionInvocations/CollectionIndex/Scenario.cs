#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionIndex
{
    public static class Trace { public static readonly List<string> Events = new(); }
    public sealed class BoxedBag : IEnumerable
    {
        public string Value = "";
        public IEnumerator GetEnumerator() => Array.Empty<int>().GetEnumerator();
        public void Add<T>(T value, [CallerMemberName] object? member = null) => Value = value + ":" + member;
        public void Add(int value, object? member) => throw new InvalidOperationException("Wrong caller argument conversion.");
    }
    public sealed class Holder
    {
        public int Before { get; set; }
        public BoxedBag Items { get; } = new();
        public int After { get; set; }
    }
    public sealed class Indexed
    {
        public BoxedBag Bag = new();
        public BoxedBag this[int index] { get { Trace.Events.Add("get:" + index); return Bag; } }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        private static int Next() { Trace.Events.Add("index"); return 1; }
        protected override void Configure(MapperBuilder builder) => builder.Map<int, string>().Convert(source =>
        {
#line 100 "CollectionCalls.cs"
            var holder = new Indexed { [Next()] = { source, source + 1 } };
            return holder.Bag.Value;
#line default
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
                if (actual != "4:Configure" || string.Join(",", Trace.Events) != "index,get:1,get:1")
                    throw new InvalidOperationException("An index argument must run once, and its getter must run before each Add.");
            }
        }
    }
}
