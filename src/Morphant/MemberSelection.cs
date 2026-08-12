namespace Morphant;

/// <summary>
/// Specifies how destination members are selected for mapping.
/// </summary>
public enum MemberSelection
{
    /// <summary>
    /// Inherits the setting. The fallback is <see cref="Auto"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Maps explicit members and eligible members matched by convention.
    /// </summary>
    Auto,

    /// <summary>
    /// Maps only explicitly configured members.
    /// </summary>
    Explicit
}
