#nullable enable
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionPattern
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
            var first = source switch { > 0 and var value => new BoxedBag { value }.Value, _ => "skip" };
#line default
            return first;
        });
    }
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<int, string> mapper = new Mapper();
            if (mapper.Create(3) != "3:Configure" || mapper.Create(0) != "skip" || mapper.Update(4, "previous") != "4:Configure")
                throw new InvalidOperationException("An initializer must retain the selected pattern variable and branch.");
        }
    }
}
