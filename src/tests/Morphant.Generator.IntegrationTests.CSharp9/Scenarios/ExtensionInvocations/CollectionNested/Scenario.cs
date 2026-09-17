#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionNested
{
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
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<int, string>().Convert(source =>
        {
#line 100 "CollectionCalls.cs"
            var first = source > 0 ? new BoxedBag { source++ }.Value : "skip";
            Func<BoxedBag> later = () => new BoxedBag { source++ };
            BoxedBag Local() => new() { source++ };
            var holder = new Holder { Before = source++, Items = { source++, source++ }, After = source++ };
#line default
            return first + "|" + later().Value + "|" + Local().Value + "|" + holder.Items.Value + "|" + source;
        });
    }
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<int, string> mapper = new Mapper();
            foreach (var update in new[] { false, true })
            {
                var positive = update ? mapper.Update(3, "previous") : mapper.Create(3);
                var zero = update ? mapper.Update(0, "previous") : mapper.Create(0);
                if (positive != "3:Configure|8:Configure|9:Configure|6:Configure|10" || zero != "skip|4:Configure|5:Configure|2:Configure|6")
                    throw new InvalidOperationException("Conditional execution, delayed callbacks, local mutations and member initialization order must be preserved.");
            }
        }
    }
}
