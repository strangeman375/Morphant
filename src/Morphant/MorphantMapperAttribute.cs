namespace Morphant;

/// <summary>
/// Marks a mapper for processing by the Morphant source generator.
/// </summary>
/// <remarks>
/// The annotated type must derive from <see cref="TypeMapper"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MorphantMapperAttribute : Attribute;
