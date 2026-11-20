using System;
using Morphant;

namespace Morphant.DeclarationSuppressedSupports;

public sealed class Source
{
}

public sealed class Destination
{
}

[MorphantMapper]
public partial class SuppressedMapper : TypeMapper
{
    private new bool Supports(Type sourceType, Type destinationType) => false;

    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
