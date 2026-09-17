#nullable enable
using System;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Layout.Calls;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Layout
{
    public sealed class Source
    {
        public string? Text { get; set; }
        public int Calls;
        public int Next() => ++Calls;
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, string>().Convert(source =>
            {
                var value = source!.Text
                    // Keep the conditional receiver on its original line.
                    ?.Echo(
                        source.Next());
                Action action = () =>
                    source.Text
                        ?.Touch();
                void Touch() =>
                    source.Text
                        ?.Touch();
                action();
                Touch();
                return $@"{value}
--{source.Text?.Echo(0)}";
            });
    }
    public static class Scenario
    {
        public static void Verify()
        {
            // Use the literal's source line ending, which Git checkout may change.
            const string literalLineEnding = @"
";
            ITypeMapper<Source, string> mapper = new Mapper();
            foreach (var text in new string?[] { "abc", null })
            {
                for (var update = 0; update < 2; update++)
                {
                    var source = new Source { Text = text };
                    Calls.Operations.Touches = 0;
                    var result = update == 0 ? mapper.Create(source) : mapper.Update(source, "old");
                    var expected = text is null ? literalLineEnding + "--" : "abc1" + literalLineEnding + "--abc0";
                    if (result != expected || source.Calls != (text is null ? 0 : 1) || Calls.Operations.Touches != (text is null ? 0 : 2))
                        throw new InvalidOperationException("Multiline conditional calls must preserve branch effects and literal line endings.");
                }
            }
        }
    }
}
