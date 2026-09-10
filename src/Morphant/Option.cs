using System.Diagnostics.CodeAnalysis;
using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Represents the presence or absence of a value.
/// </summary>
/// <typeparam name="T">The type of the optional value.</typeparam>
public readonly struct Option<T>
{
    private readonly T _value;

    private Option(T value)
    {
        _value = value;
        HasValue = true;
    }

    /// <summary>
    /// Contains no value.
    /// </summary>
    public static Option<T> None => default;

    /// <summary>
    /// Creates an option that contains the specified value.
    /// </summary>
    /// <param name="value">The value to store.</param>
    /// <returns>An option containing <paramref name="value"/>.</returns>
    public static Option<T> Some(T value) => new(value);

    /// <summary>
    /// Whether a value is present.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// Gets the contained value.
    /// </summary>
    /// <exception cref="OptionValueMissingException">
    /// The option contains no value.
    /// </exception>
    public T Value =>
        HasValue
            ? _value
            : throw new OptionValueMissingException();

    /// <summary>
    /// Attempts to get the contained value.
    /// </summary>
    /// <param name="value">
    /// The stored value if present; otherwise, default.
    /// </param>
    /// <returns>
    /// Whether a value was present.
    /// </returns>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return HasValue;
    }
}
