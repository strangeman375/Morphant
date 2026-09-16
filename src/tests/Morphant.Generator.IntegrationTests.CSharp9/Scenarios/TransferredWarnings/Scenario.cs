#nullable enable
#pragma warning disable CS1591
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TransferredWarnings
{
    public sealed class Source
    {
        public int Reads;
        public bool Reuse;
        [Obsolete("Legacy value.")]
        public string Legacy { get { Reads++; return "value"; } }
    }

    public sealed class Destination
    {
        public Destination(string text) => Text = text;
        public string Text { get; set; }
    }

    [MorphantMapper]
    public partial class ConstructMapper : TypeMapper<ConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
#pragma warning disable CS0618
                .Construct(source => new($@"first
  {source.Legacy} ""quoted"" {{braces}}
last"));
#pragma warning restore CS0618
    }

    [MorphantMapper]
    public partial class ResolveMapper : TypeMapper<ResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse) return previous.Value;
#pragma warning disable CS0618
                    return new($@"first
  {source.Legacy} ""quoted"" {{braces}}
last");
#pragma warning restore CS0618
                });
    }

    [MorphantMapper]
    public partial class MembersMapper : TypeMapper<MembersMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
#pragma warning disable CS0618
                    Text = $@"first
  {source.Legacy} ""quoted"" {{braces}}
last"
#pragma warning restore CS0618
                });
    }

    [MorphantMapper]
    public partial class ConstructUsingMapper : TypeMapper<ConstructUsingMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
#pragma warning disable CS0618
                .ConstructUsing(source => new Destination($@"first
  {source.Legacy} ""quoted"" {{braces}}
last"));
#pragma warning restore CS0618
    }

    [MorphantMapper]
    public partial class ResolveUsingMapper : TypeMapper<ResolveUsingMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ResolveUsing((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse) return previous.Value;
#pragma warning disable CS0618
                    return new Destination($@"first
  {source.Legacy} ""quoted"" {{braces}}
last");
#pragma warning restore CS0618
                });
    }

    [MorphantMapper]
    public partial class ConvertMapper : TypeMapper<ConvertMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
#pragma warning disable CS0618
                .Convert(source => new Destination($@"first
  {source!.Legacy} ""quoted"" {{braces}}
last"));
#pragma warning restore CS0618
    }

    public static class Scenario
    {
        public static void Verify(string callback, string operation)
        {
            ITypeMapper<Source, Destination> mapper = callback switch
            {
                "Construct" => new ConstructMapper(),
                "Resolve" => new ResolveMapper(),
                "Members" => new MembersMapper(),
                "ConstructUsing" => new ConstructUsingMapper(),
                "ResolveUsing" => new ResolveUsingMapper(),
                "Convert" => new ConvertMapper(),
                _ => throw new ArgumentOutOfRangeException(nameof(callback))
            };
            var source = new Source { Reuse = operation == "Reuse" };
            var previous = operation == "NullUpdate" ? null : new Destination("previous");
            var result = operation == "Create" ? mapper.Create(source) : mapper.Update(source, previous);
            var reused = operation is "Reuse" or "Replace" &&
                (callback is "Construct" or "ConstructUsing" or "Members" ||
                    callback is "Resolve" or "ResolveUsing" && source.Reuse);
            var evaluated = !reused || callback == "Members";
            var expected = evaluated ? @"first
  value ""quoted"" {braces}
last" : "previous";
            if (result.Text != expected || source.Reads != (evaluated ? 1 : 0) ||
                ReferenceEquals(result, previous) != reused)
                throw new InvalidOperationException("Warning transfer changed string contents, evaluation count or destination reuse.");
        }
    }
}
