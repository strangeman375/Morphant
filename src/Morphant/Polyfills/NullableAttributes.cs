// ReSharper disable once CheckNamespace // polyfill
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Specifies that the output will be non-null if the named parameter is non-null.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class NotNullIfNotNullAttribute : Attribute
{
    /// <summary>
    /// Initializes the attribute with the associated parameter name.
    /// </summary>
    /// <param name="parameterName">
    /// The associated parameter name.  The output will be non-null if the argument to the parameter specified is non-null.
    /// </param>
    public NotNullIfNotNullAttribute(string parameterName) => ParameterName = parameterName;

    /// <summary>
    /// Gets the associated parameter name.
    /// </summary>
    public string ParameterName { get; }
}

/// <summary>
/// Specifies that an output may be <see langword="null"/> when a method
/// returns the specified value.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class MaybeNullWhenAttribute : Attribute
{
    /// <summary>
    /// Initializes the attribute with the return value whose path may produce
    /// a <see langword="null"/> output.
    /// </summary>
    /// <param name="returnValue">
    /// The return value associated with a potentially
    /// <see langword="null"/> output.
    /// </param>
    public MaybeNullWhenAttribute(bool returnValue) =>
        ReturnValue = returnValue;

    /// <summary>
    /// Gets the return value associated with a potentially
    /// <see langword="null"/> output.
    /// </summary>
    public bool ReturnValue { get; }
}
