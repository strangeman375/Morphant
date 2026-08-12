namespace Morphant;

/// <summary>
/// Marks a partial mapper derived from <see cref="TypeMapper"/> for generation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MorphantMapperAttribute : Attribute;
