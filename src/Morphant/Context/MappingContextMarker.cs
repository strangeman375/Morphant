namespace Morphant.Context;

/// <summary>
/// Exposes declarative context for the current mapping.
/// </summary>
/// <remarks>
/// This type exists only for target typing generated declarative callbacks.
/// Morphant does not create a runtime instance of it. Only
/// <see cref="Operation"/> may be read by supported declarative code.
/// </remarks>
public abstract class MappingContextMarker
{
    private protected MappingContextMarker()
    {
    }

    /// <summary>
    /// Gets the operation performed by the current call.
    /// </summary>
    public abstract MappingOperation Operation { get; }
}
