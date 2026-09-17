#nullable enable
using System;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Chains.Calls;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Chains
{
    public sealed class Source
    {
        public string? Text { get; set; }
        public int? Number { get; set; }
        public string[]? Items { get; set; }
        public int IndexCalls;
        public int Index() { IndexCalls++; return 0; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, string>().Convert(source =>
            {
                Action action = () => source!.Text?.Touch();
                void Touch() => source!.Text?.Touch();
                action();
                Touch();
                source!.Text?.Echo()?.Touch();
                return (source.Text?.Echo().Echo()?.Identity<string>().Length ?? 0)
                    + ":" + (source.Number?.Add() ?? -1)
                    + ":" + (source.Items?[source.Index()].Echo().Length ?? 0)
                    + ":" + Calls.Operations.Echo(source.Text ?? "static")
                    + ":" + (source.Text ?? "empty")
                        // Keep the readable chain and this comment.
                        .Echo()
                        .Identity<string>();
            });
    }
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<Source, string> mapper = new Mapper();
            foreach (var empty in new[] { false, true })
            {
                var source = new Source { Text = empty ? null : "abc", Number = empty ? null : 2, Items = empty ? null : new[] { "xyz" } };
                var expected = empty ? "0:-1:0:static:empty" : "3:3:3:abc:abc";
                for (var update = 0; update < 2; update++)
                {
                    source.IndexCalls = 0;
                    Calls.Operations.Touches = 0;
                    var result = update == 0 ? mapper.Create(source) : mapper.Update(source, "old");
                    if (result != expected || source.IndexCalls != (empty ? 0 : 1) || Calls.Operations.Touches != (empty ? 0 : 3))
                        throw new System.InvalidOperationException("Chains, nullable values, indexers and void callbacks must retain their conditional evaluation.");
                }
            }
        }
    }
}
