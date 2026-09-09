namespace Morphant.Context;

using Morphant.Exceptions;

/// <summary>
/// Describes the current mapping call.
/// </summary>
/// <remarks>
/// Supplied by Morphant. A default-initialized context is invalid.
/// </remarks>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/runtime-dispatch.md"/>
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
    /// The context is default-initialized.
    /// </exception>
    public MappingOperation Operation => _isInitialized
        ? _operation
        : throw new InvalidMappingContextException();

    /// <summary>
    /// Gets the mapper bound to the current mapping scope.
    /// </summary>
    /// <remarks>
    /// Use only during the current top-level mapping call. Do not retain it or
    /// use it concurrently.
    /// </remarks>
    /// <exception cref="InvalidMappingContextException">
    /// The context is default-initialized.
    /// </exception>
    public IMapper Mapper => _isInitialized
        ? _mapper!
        : throw new InvalidMappingContextException();
}
