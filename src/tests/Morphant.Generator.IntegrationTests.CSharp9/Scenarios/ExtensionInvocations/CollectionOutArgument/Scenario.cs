#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionOutArgument
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
            static string Join(string left, int right, string name) => left + ":" + right + ":" + name;
            return Join(new BoxedBag { int.TryParse("4", out var value) ? value : source }.Value, value = 5, nameof(value));
#line default
        });
    }
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<int, string> mapper = new Mapper();
            if (mapper.Create(3) != "4:Configure:5:value" || mapper.Update(0, "previous") != "4:Configure:5:value")
                throw new InvalidOperationException("The out binding must remain available to later writes and nameof.");
        }
    }
}
