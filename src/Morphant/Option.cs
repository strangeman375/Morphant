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
    /// Gets an option that contains no value.
    /// </summary>
    public static Option<T> None => default;

    /// <summary>
    /// Creates an option that contains the specified value.
    /// </summary>
    /// <param name="value">The value to store.</param>
    /// <returns>An option containing <paramref name="value"/>.</returns>
    public static Option<T> Some(T value) => new(value);

    /// <summary>
    /// Gets a value indicating whether this option contains a value.
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
    /// When this method returns <see langword="true"/>, contains the stored
    /// value; otherwise, contains <see langword="default"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when this option contains a value; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return HasValue;
    }
}
