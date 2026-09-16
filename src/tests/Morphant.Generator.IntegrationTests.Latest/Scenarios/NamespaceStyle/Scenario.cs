#nullable enable
using Morphant.Context;
using global::System.Linq;

namespace Morphant.Generator.IntegrationTests.Latest.Scenarios.NamespaceStyle;

public static class System { }

public sealed class Source
{
    public string Name { get; set; } = string.Empty;
    public int[] Values { get; set; } = [];
}

public sealed class Destination
{
    public Destination(string value) => Value = value;
    public string Value { get; set; }
    public string Verbatim { get; set; } = string.Empty;
    public string Interpolated { get; set; } = string.Empty;
    public string Raw { get; set; } = string.Empty;
}

[MorphantMapper]
public partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .Construct(source => new(string.Join(",", from value in source.Values select value + 1)))
            .Members(source => new()
            {
                Verbatim = @"first
  second",
                Interpolated = $@"first {source.Name}
  second",
                Raw = $$"""
                    first {{source.Name}}
                      second
                    """
            });
}

public static class Scenario
{
    public static (string Verbatim, string Interpolated, string Raw) ExpectedStrings()
    {
        var name = "value";
        return (@"first
  second", $@"first {name}
  second", $$"""
            first {{name}}
              second
            """);
    }

    public static (Destination Result, bool Reused) Run(bool update, bool existing)
    {
        var mapper = (ITypeMapper<Source, Destination>)new Mapper();
        var source = new Source { Name = "value", Values = [1, 2] };
        var destination = existing ? new Destination("existing") : null;
        var result = update
            ? mapper.Update(source, destination, default(MappingContext))
            : mapper.Create(source, default(MappingContext));
        return (result, ReferenceEquals(result, destination));
    }
}
