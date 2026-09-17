#nullable enable
using System;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Surfaces.Extensions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Surfaces
{
    public sealed class Source
    {
        public string? Text { get; set; }
        public bool Reuse { get; set; }
        public int Count { get; set; }
        public string? Read() { Count++; return Text; }
        public int Next() => ++Count;
    }
    public sealed class Destination
    {
        public Destination(int value) => Value = value;
        public int Value { get; set; }
    }
    [MorphantMapper]
    public partial class ConstructMapper : TypeMapper<ConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Read()?.Echo(source.Next()).Length ?? source.Next()));
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
                    return new(source.Read()?.Echo(source.Next()).Length ?? source.Next());
                });
    }
    [MorphantMapper]
    public partial class MembersMapper : TypeMapper<MembersMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(0))
                .Members(source => new() { Value = source.Read()?.Echo(source.Next()).Length ?? source.Next() });
    }
    [MorphantMapper]
    public partial class ConvertMapper : TypeMapper<ConvertMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Convert(source => new Destination(source!.Read()?.Echo(source!.Next()).Length ?? source!.Next()));
    }
    [MorphantMapper]
    public partial class ConstructUsingMapper : TypeMapper<ConstructUsingMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .ConstructUsing(source => new Destination(source.Read()?.Echo(source.Next()).Length ?? source.Next()));
    }
    [MorphantMapper]
    public partial class ResolveUsingMapper : TypeMapper<ResolveUsingMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .ResolveUsing((source, previous) => previous.HasValue && source.Reuse
                    ? previous.Value : new Destination(source.Read()?.Echo(source.Next()).Length ?? source.Next()));
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
                foreach (var text in new string?[] { "abc", null })
                {
                    var source = new Source { Text = text };
                    var expected = text is null ? 2 : 4;
                    var created = mappers[index].Create(source);
                    if (created.Value != expected || source.Count != 2)
                        throw new InvalidOperationException("Create must evaluate the receiver once and only the selected argument or fallback.");
                    source.Count = 0;
                    var fromNull = mappers[index].Update(source, null);
                    if (fromNull.Value != expected || source.Count != 2)
                        throw new InvalidOperationException("Update with no destination must preserve extension evaluation.");
                    foreach (var reuse in new[] { false, true })
                    {
                        source.Count = 0;
                        source.Reuse = reuse;
                        var previous = new Destination(99);
                        var result = mappers[index].Update(source, previous);
                        var skips = index == 0 || index == 4 || reuse && (index == 1 || index == 5);
                        if (result.Value != (skips ? 99 : expected) || source.Count != (skips ? 0 : 2))
                            throw new InvalidOperationException("Update must preserve replacement, reuse and conditional evaluation.");
                        var preservesIdentity = skips || index == 2;
                        if (ReferenceEquals(result, previous) != preservesIdentity)
                            throw new InvalidOperationException("Extension readability must preserve destination identity.");
                    }
                }
            }
        }
    }
}
