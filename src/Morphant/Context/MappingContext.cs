namespace Morphant.Context;

/// <summary>
/// Describes the current mapping call.
/// </summary>
/// <remarks>
/// Morphant creates a context for each mapping call. A default-initialized
/// context is not a valid mapping frame.
/// </remarks>
public readonly struct MappingContext
{
    internal MappingContext(
        MappingOperation operation,
        IMapper mapper)
    {
        Operation = operation;
        Mapper = mapper;
    }

    /// <summary>
    /// Gets the operation performed by the current call.
    /// </summary>
    public MappingOperation Operation { get; }

    /// <summary>
    /// Gets the mapper bound to the current mapping scope.
    /// </summary>
    public IMapper Mapper { get; }
}
