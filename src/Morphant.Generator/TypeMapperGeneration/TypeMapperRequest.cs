namespace Morphant.Generator.TypeMapperGeneration;

internal readonly record struct TypeMapperRequest
(
    string HintName,
    string Source
) : IGeneratedSourceRequest;
