namespace Morphant.Context;

using Morphant.Exceptions;

/// <summary>
/// Describes the current mapping call.
/// </summary>
/// <remarks>
/// Morphant creates a context for each mapping call. A default-initialized
/// context is not a valid mapping frame.
/// </remarks>
public readonly struct MappingContext
{
    private readonly MappingOperation _operation;
    private readonly IMapper? _mapper;
    private readonly bool _isInitialized;

    internal MappingContext(
        MappingOperation operation,
        IMapper mapper)
    {
        _operation = operation;
        _mapper = mapper;
        _isInitialized = true;
    }

    /// <summary>
    /// Gets the operation performed by the current call.
    /// </summary>
    /// <exception cref="InvalidMappingContextException">
    /// This value is a default-initialized context rather than a mapping
    /// frame created by Morphant.
    /// </exception>
    public MappingOperation Operation => _isInitialized
        ? _operation
        : throw new InvalidMappingContextException();

    /// <summary>
    /// Gets the mapper bound to the current mapping scope.
    /// </summary>
    /// <exception cref="InvalidMappingContextException">
    /// This value is a default-initialized context rather than a mapping
    /// frame created by Morphant.
    /// </exception>
    public IMapper Mapper => _isInitialized
        ? _mapper!
        : throw new InvalidMappingContextException();
}
