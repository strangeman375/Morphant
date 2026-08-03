namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class OptionTests
{
    [Test]
    public void Distinguishes_none_from_some_null_for_reference_values()
    {
        var none = Option<string?>.None;
        var someNull = Option<string?>.Some(null);
        var someValue = Option<string?>.Some("value");

        Assert.Multiple(() =>
        {
            AssertNone(none, expectedDefault: null);
            AssertSome(someNull, expected: null);
            AssertSome(someValue, expected: "value");
        });
    }

    [Test]
    public void Distinguishes_none_from_some_null_for_nullable_values()
    {
        var none = Option<int?>.None;
        var someNull = Option<int?>.Some(null);
        var someValue = Option<int?>.Some(42);

        Assert.Multiple(() =>
        {
            AssertNone(none, expectedDefault: null);
            AssertSome(someNull, expected: null);
            AssertSome(someValue, expected: 42);
        });
    }

    [Test]
    public void Distinguishes_none_from_some_default_for_non_nullable_values()
    {
        var none = default(Option<int>);
        var someDefault = Option<int>.Some(default);
        var someValue = Option<int>.Some(42);

        Assert.Multiple(() =>
        {
            AssertNone(none, expectedDefault: 0);
            AssertSome(someDefault, expected: 0);
            AssertSome(someValue, expected: 42);
        });
    }

    [Test]
    public void Preserves_nullable_nested_generic_arguments()
    {
        var envelope = new Envelope<string?>(null);
        var option = Option<Envelope<string?>>.Some(envelope);

        Assert.Multiple(() =>
        {
            Assert.That(option.HasValue, Is.True);
            Assert.That(option.Value, Is.SameAs(envelope));
            Assert.That(option.Value.Value, Is.Null);
            Assert.That(option.TryGetValue(out var actual), Is.True);
            Assert.That(actual, Is.SameAs(envelope));
        });
    }

    private static void AssertNone<T>(
        Option<T> option,
        T expectedDefault)
    {
        Assert.That(option.HasValue, Is.False);
        Assert.That(option.TryGetValue(out var value), Is.False);
        Assert.That(value, Is.EqualTo(expectedDefault));
        Assert.That(
            () => option.Value,
            Throws.TypeOf<InvalidOperationException>());
    }

    private static void AssertSome<T>(Option<T> option, T expected)
    {
        Assert.That(option.HasValue, Is.True);
        Assert.That(option.Value, Is.EqualTo(expected));
        Assert.That(option.TryGetValue(out var value), Is.True);
        Assert.That(value, Is.EqualTo(expected));
    }

    private sealed record Envelope<T>(T Value);
}
