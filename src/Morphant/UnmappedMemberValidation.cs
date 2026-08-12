namespace Morphant;

/// <summary>
/// Specifies which unused source and destination members are validated.
/// </summary>
public enum UnmappedMemberValidation
{
    /// <summary>
    /// Inherits the setting. The fallback is <see cref="None"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Disables unmapped-member validation.
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
    /// Validates both source and destination members.
    /// </summary>
    Strict
}
