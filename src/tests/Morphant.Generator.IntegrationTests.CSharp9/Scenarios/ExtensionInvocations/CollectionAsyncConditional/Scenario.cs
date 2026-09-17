#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionAsyncConditional
{
    public static class Trace
    {
        public static readonly List<string> Events = new();
        public static readonly System.Threading.AsyncLocal<int> State = new();
    }
    public sealed class BoxedBag : IEnumerable
    {
        public string Value = "";
        public IEnumerator GetEnumerator() => Array.Empty<int>().GetEnumerator();
        public void Add<T>(T value, [CallerMemberName] object? member = null)
        {
            Trace.Events.Add(value + ":" + Trace.State.Value);
            Value = value + ":" + member;
        }
        public void Add(int value, object? member) => throw new InvalidOperationException("Wrong caller argument conversion.");
    }
    public sealed class Holder
    {
        public int Before { get; set; }
        public BoxedBag Items { get; } = new();
        public int After { get; set; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<int, string>().Convert(source =>
        {
#line 100 "CollectionCalls.cs"
            Func<System.Threading.Tasks.Task<string>> read = async () => source > 0 ? new BoxedBag { (Trace.State.Value = source) + await System.Threading.Tasks.Task.FromResult(0), await System.Threading.Tasks.Task.FromResult(source + 1) }.Value + ":" + Trace.State.Value : "skip";
            return read().GetAwaiter().GetResult();
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
                Trace.State.Value = 7;
                Trace.Events.Clear();
                var actual = update ? mapper.Update(3, "previous") : mapper.Create(3);
                if (actual != "4:Configure:3" || string.Join(",", Trace.Events) != "3:3,4:3" || Trace.State.Value != 7)
                    throw new InvalidOperationException("Awaited values and Add calls must share the original async context: " + actual);
                Trace.Events.Clear();
                if (mapper.Create(0) != "skip" || Trace.Events.Count != 0 || Trace.State.Value != 7)
                    throw new InvalidOperationException("An unselected async initializer must have no effects.");
            }
        }
    }
}
