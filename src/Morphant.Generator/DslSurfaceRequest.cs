namespace Morphant.Generator;

internal readonly record struct DslSurfaceRequest(
    string HintName,
    string Source) : IGeneratedSourceRequest;
