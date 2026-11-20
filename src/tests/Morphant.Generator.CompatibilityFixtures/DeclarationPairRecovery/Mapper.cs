using System;
using Morphant;
using Morphant.Context;

namespace Morphant.DeclarationPairRecovery;

public sealed class Source
{
}

public sealed class ConflictDestination
{
}

public sealed class IndependentDestination
{
}

[MorphantMapper]
public partial class RecoveryMapper :
    TypeMapper,
    ITypeMapper<Source, ConflictDestination>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, ConflictDestination>();
        builder.Map<Source, IndependentDestination>();
    }

    ConflictDestination ITypeMapper<Source, ConflictDestination>.Create(
        Source? source,
        MappingContext context) =>
        throw new NotSupportedException();

    ConflictDestination ITypeMapper<Source, ConflictDestination>.Update(
        Source? source,
        ConflictDestination? destination,
        MappingContext context) =>
        throw new NotSupportedException();
}
