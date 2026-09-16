#nullable enable
#pragma warning disable CS1591
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.TransferredWarnings;

public sealed class Source
{
    public int Reads;
    [Obsolete("Legacy value.")]
    public int Legacy { get { Reads++; return 7; } }
}

[MorphantMapper]
public partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, string>()
#pragma warning disable CS0618
            .Convert(source => $$"""
                first
                  { {{source!.Legacy:D2}} }
                last
                """);
#pragma warning restore CS0618
}

public static class Scenario
{
    public static void Verify(bool update)
    {
        var source = new Source();
        ITypeMapper<Source, string> mapper = new Mapper();
        var result = update ? mapper.Update(source, "previous") : mapper.Create(source);
        const string expected = """
            first
              { 07 }
            last
            """;
        if (result != expected || source.Reads != 1)
            throw new InvalidOperationException("Warning transfer changed raw string contents or evaluated an interpolation twice.");
    }
}
