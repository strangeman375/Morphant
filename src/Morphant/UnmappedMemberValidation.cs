namespace Morphant;

/// <summary>
/// Specifies which unused source and destination members are validated.
/// </summary>
public enum UnmappedMemberValidation
{
    /// <summary>
    /// Inherits the next less specific setting. If no level specifies a
    /// value, Morphant uses <see cref="None"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Does not require every source or destination member to participate in
    /// the mapping plan.
    /// </summary>
    None,

    /// <summary>
    /// Requires every supported source member to participate in the mapping
    /// plan.
    /// </summary>
    Source,

    /// <summary>
    /// Requires every supported destination member to participate in the
    /// mapping plan.
    /// </summary>
    Destination,

    /// <summary>
    /// Requires every supported source and destination member to participate
    /// in the mapping plan.
    /// </summary>
    Strict
}
