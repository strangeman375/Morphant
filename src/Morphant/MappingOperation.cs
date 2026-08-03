namespace Morphant;

/// <summary>
/// Specifies the mapping operation performed by the current call.
/// </summary>
public enum MappingOperation
{
    /// <summary>
    /// Maps a source without a supplied destination.
    /// </summary>
    Create = 0,

    /// <summary>
    /// Maps a source with a supplied destination argument.
    /// </summary>
    Update
}
