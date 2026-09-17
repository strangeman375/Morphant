#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionEffects.ForeignCalls;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionEffects
{
    public static class Trace
    {
        public static readonly List<string> Events = new();
    }
    public sealed class Bag : IEnumerable, IDisposable
    {
        public string Label { get; }
        public string Value = "";
        public Bag(string label) { Label = label; Trace.Events.Add("new:" + label); }
        public IEnumerator GetEnumerator() => Array.Empty<int>().GetEnumerator();
        public void Add<T>(T value, [CallerMemberName] object? member = null,
            [CallerArgumentExpression("value")] string expression = "")
        {
            Trace.Events.Add(Label + ":add:" + value + ":" + member + ":" + expression);
            if (value is int number && number < 0) throw new InvalidOperationException("Add failed.");
            Value = value + ":" + member;
        }
        public void Add(int value, object? member, string expression) => throw new InvalidOperationException("Wrong overload.");
        public void Dispose() => Trace.Events.Add("dispose:" + Label);
    }
    public sealed class ForeignBag : IEnumerable
    {
        public string Value = "";
        public IEnumerator GetEnumerator() => Array.Empty<int>().GetEnumerator();
    }
    public sealed class DateBag : IEnumerable
    {
        public long Ticks;
        public string Member = "";
        public IEnumerator GetEnumerator() => Array.Empty<int>().GetEnumerator();
        public void Add(int value, [Optional, DateTimeConstant(123)] DateTime date,
            [CallerMemberName] string member = "") { Ticks = date.Ticks; Member = member; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<int, string>().Convert(source =>
        {
            var previous = new Bag("old");
            try { previous = new Bag("replacement") { -source }; }
            catch (InvalidOperationException) { Trace.Events.Add("caught:replacement"); }
            try { using var failed = new Bag("using-failed") { -source }; }
            catch (InvalidOperationException) { Trace.Events.Add("caught:using"); }
            using (var success = new Bag("using") { source }) { Trace.Events.Add("body"); }
            string Read(ref int number) => number > 0 ? new Bag("ref") { number++ }.Value : "skip";
            var value = source;
            var read = Read(ref value);
            var parsed = new Bag("out") { int.TryParse("4", out var result) ? result : source }.Value;
            Func<Task<string>> later = async () => new Bag("async") { await Task.FromResult(source) }.Value;
            try { _ = checked(new Bag("checked") { source + int.MaxValue }.Value); }
            catch (OverflowException) { Trace.Events.Add("overflow"); }
            var commented = new Bag("comments")
            {
                // Explain the value.
                source, // Explain the separator.
            }; // Explain the declaration.
            var date = new DateBag { source };
            var foreign = new ForeignBag { source };
            return previous.Label + "|" + read + "|" + value + "|" + parsed + "|" + result + "|" + later().GetAwaiter().GetResult() + "|" + commented.Value + "|" + date.Ticks + ":" + date.Member + "|" + foreign.Value;
        });
    }
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<int, string> mapper = new Mapper();
            var expectedEvents = new[]
            {
                "new:old", "new:replacement", "replacement:add:-2:Configure:-source", "caught:replacement",
                "new:using-failed", "using-failed:add:-2:Configure:-source", "caught:using",
                "new:using", "using:add:2:Configure:source", "body", "dispose:using",
                "new:ref", "ref:add:2:Configure:number++", "new:out",
                "out:add:4:Configure:int.TryParse(\"4\", out var result) ? result : source",
                "new:checked", "overflow", "new:comments", "comments:add:2:Configure:source",
                "foreign:add:2:Configure", "new:async", "async:add:2:Configure:await Task.FromResult(source)"
            };
            foreach (var update in new[] { false, true })
            {
                Trace.Events.Clear();
                var actual = update ? mapper.Update(2, "previous") : mapper.Create(2);
                if (actual != "old|2:Configure|3|4:Configure|4|2:Configure|2:Configure|123:Configure|2:Configure" ||
                    string.Join("|", Trace.Events) != string.Join("|", expectedEvents))
                    throw new InvalidOperationException("Assignment, disposal, checked arithmetic, deferred await and optional defaults changed: " + actual + "; " + string.Join("|", Trace.Events));
            }
        }
    }
}
