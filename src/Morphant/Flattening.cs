namespace Morphant;

/// <summary>
/// Specifies whether convention-based mappings may read nested source
/// members by concatenating their names.
/// </summary>
public enum Flattening
{
    /// <summary>
    /// Inherits the setting. The fallback is <see cref="Auto"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Allows an unmatched destination name such as <c>CustomerName</c> to
    /// use a nested source path such as <c>Customer.Name</c>.
    /// </summary>
    Auto,

    /// <summary>
    /// Uses only direct convention source members.
    /// </summary>
    None
}
