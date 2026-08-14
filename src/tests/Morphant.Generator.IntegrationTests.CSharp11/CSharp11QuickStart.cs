#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp11;

public sealed class CSharp11Customer
{
    public string Name { get; init; } = string.Empty;
}

public sealed class CSharp11CustomerDto
{
    public required string Name { get; init; }
}

[MorphantMapper]
public sealed partial class CSharp11Mapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<CSharp11Customer, CSharp11CustomerDto>();
}
