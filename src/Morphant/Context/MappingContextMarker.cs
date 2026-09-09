using System.Diagnostics.CodeAnalysis;

namespace Morphant.Context;

/// <summary>
/// Exposes declarative context for the current mapping.
/// </summary>
/// <remarks>
/// Available only inside Construct, Resolve, and Members lambdas.
/// Only <see cref="Operation"/> may be read; there is no runtime instance.
/// </remarks>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/declarative-expressions.md"/>
[ExcludeFromCodeCoverage]
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
