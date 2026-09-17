#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionSurfaces
{
    public sealed class Source
    {
        public bool Reuse { get; set; }
        public int Count { get; set; }
        public int Next() => ++Count;
    }
    public sealed class Destination
    {
        public Destination(string value) => Value = value;
        public string Value { get; set; }
    }
    public sealed class Bag : IEnumerable
    {
        public string Value = "";
        public IEnumerator GetEnumerator() => Array.Empty<int>().GetEnumerator();
        public void Add<T>(T value, [CallerMemberName] object? member = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0,
            [CallerArgumentExpression("value")] string expression = "") =>
            Value = value + ":" + member + ":" + file + ":" + line + ":" + expression;
        public void Add(int value, object? member, string file, int line, string expression) =>
            throw new InvalidOperationException("Wrong overload.");
    }
    [MorphantMapper]
    public partial class ConstructMapper : TypeMapper<ConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
#line 200 "CollectionCallbacks.cs"
                .Construct(source => new(new Bag { source.Next() }.Value));
#line default
    }
    [MorphantMapper]
    public partial class ResolveMapper : TypeMapper<ResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse) return previous.Value;
#line 200 "CollectionCallbacks.cs"
                    return new(new Bag { source.Next() }.Value);
                });
#line default
    }
    [MorphantMapper]
    public partial class MembersMapper : TypeMapper<MembersMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new("initial"))
#line 200 "CollectionCallbacks.cs"
                .Members(source => new() { Value = new Bag { source.Next() }.Value });
#line default
    }
    [MorphantMapper]
    public partial class ConvertMapper : TypeMapper<ConvertMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                #line 200 "CollectionCallbacks.cs"
.Convert(source => new Destination(new Bag { source!.Next() }.Value));
#line default
    }
    [MorphantMapper]
    public partial class ConstructUsingMapper : TypeMapper<ConstructUsingMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
#line 200 "CollectionCallbacks.cs"
                .ConstructUsing(source => new Destination(new Bag { source.Next() }.Value));
#line default
    }
    [MorphantMapper]
    public partial class ResolveUsingMapper : TypeMapper<ResolveUsingMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .ResolveUsing((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse) return previous.Value;
#line 200 "CollectionCallbacks.cs"
                    return new Destination(new Bag { source.Next() }.Value);
                });
#line default
    }
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<Source, Destination>[] mappers =
            {
                new ConstructMapper(), new ResolveMapper(), new MembersMapper(),
                new ConvertMapper(), new ConstructUsingMapper(), new ResolveUsingMapper()
            };
            for (var index = 0; index < mappers.Length; index++)
            {
                var source = new Source();
                var expected = "1:Configure:" + System.IO.Path.Combine(SourceDirectory(), "CollectionCallbacks.cs") + ":200:" + (index == 3 ? "source!.Next()" : "source.Next()");
                var created = mappers[index].Create(source);
                if (created.Value != expected || source.Count != 1)
                    throw new InvalidOperationException("Create must call the selected generic Add once with all original caller values: " + created.Value);
                source.Count = 0;
                var fromNull = mappers[index].Update(source, null);
                if (fromNull.Value != expected || source.Count != 1)
                    throw new InvalidOperationException("Update without a destination must preserve initializer effects.");
                foreach (var reuse in new[] { false, true })
                {
                    source.Count = 0;
                    source.Reuse = reuse;
                    var previous = new Destination("previous");
                    var result = mappers[index].Update(source, previous);
                    var skips = index == 0 || index == 4 || reuse && (index == 1 || index == 5);
                    if (result.Value != (skips ? "previous" : expected) || source.Count != (skips ? 0 : 1) ||
                        ReferenceEquals(result, previous) != (skips || index == 2))
                        throw new InvalidOperationException("Initializer lowering must preserve replacement, reuse, member updates and destination identity.");
                }
            }
        }

        private static string SourceDirectory([CallerFilePath] string file = "") => System.IO.Path.GetDirectoryName(file)!;
    }
}
