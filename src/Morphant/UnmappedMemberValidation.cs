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
    /// Reports unused supported source members.
    /// </summary>
    Source,

    /// <summary>
    /// Reports unmapped supported destination members.
    /// </summary>
    Destination,

    /// <summary>
    /// Validates both source and destination members.
    /// </summary>
    Strict
}
