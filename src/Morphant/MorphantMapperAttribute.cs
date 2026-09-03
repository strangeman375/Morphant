namespace Morphant;

/// <summary>
/// Marks a partial mapper derived from <c>TypeMapper&lt;TMapper&gt;</c> for
/// generation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MorphantMapperAttribute : Attribute;
