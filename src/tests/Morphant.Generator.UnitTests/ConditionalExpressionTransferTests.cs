using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class ConditionalExpressionTransferTests
{
    private static IEnumerable<TestCaseData> Cases()
    {
        var expressions = new (string Expression, string Type)[]
        {
            ("source.Text?.MaybeText() ?? \"fallback\"", "string"),
            ("source.Text?.Measure() + 1", "int?"),
            ("source.Text?.Measure() is > 0", "bool"),
            ("source.Text?.MaybeText() == \"text\"", "bool"),
            ("$\"{source.Text?.Measure():D2}\"", "string"),
            ("source.Text?.MaybeText() ?? throw new System.InvalidOperationException()", "string"),
            ("source.Text?.Echo().Length ?? -1", "int"),
            ("source.Text?.Echo().ToUpperInvariant() ?? \"fallback\"", "string"),
            ("source.Text?.Echo()?.MaybeText() ?? \"fallback\"", "string"),
            ("source.Text?.Length.Add() ?? -1", "int"),
            ("source.Text?.ToUpperInvariant().Echo() ?? \"fallback\"", "string"),
            ("source.Text?.Echo()[source.Next()] ?? '?'", "char"),
            ("source.Items?[source.Next()].Echo() ?? \"fallback\"", "string"),
            ("source.Number?.Add() ?? -1", "int"),
            ("source.Callback?.Invoke(source.Next()) ?? -1", "int"),
            ("source.Text?[source.Next()] ?? '?'", "char"),
            ("source.Enabled ? source.Text?.MaybeText() ?? \"yes\" : \"no\"", "string"),
            ("source.Enabled && source.Text?.Measure() > 0", "bool"),
            ("source.Text switch { null => \"missing\", var text => text.Echo() }", "string"),
            ("((object?)source.Text as string)?.Echo() ?? \"fallback\"", "string"),
            ("source.Text?.Echo().Echo() ?? \"fallback\"", "string"),
            ("source.Text?.Echo()?.Length.Add() ?? -1", "int"),
            ("source.Text?.Offset(source.Next())", "int?"),
            ("source.Text?.Echo()", "string?"),
            ("(source.Text ?? \"\").Offset(source.Next())", "int"),
            ("TransferExtensions.TextExtensions.Offset(source.Text ?? \"\", source.Next())", "int"),
            ("TransferExtensions.TextExtensions.Echo(source.Text ?? \"\")", "string"),
            ("source.Text?.Identity<string>().Length ?? -1", "int"),
            ("new Func<string>(() => source.Text?.MaybeText() ?? \"fallback\")", "Func<string>"),
            ("new Func<string>(() => { var conditionalReceiver = \"fallback\"; return source.Text?.MaybeText() ?? conditionalReceiver; })", "Func<string>"),
            ("new Action(() => source.Text?.Touch())", "Action"),
            ("new Action(() => { source.Text?.Echo()?.Touch(); })", "Action"),
            ("new Action(() => { void Touch() => source.Text?.Touch(); Touch(); })", "Action"),
        };
        foreach (var (expression, type) in expressions)
        foreach (var callback in new[] { "Construct", "Members", "Convert" })
            yield return new TestCaseData(expression, type, callback);
    }

    [TestCaseSource(nameof(Cases))]
    public void Compiles_composed_expressions_without_warnings(
        string expression,
        string valueType,
        string callback)
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using System;
using Morphant;
using TransferExtensions;

namespace TransferExtensions
{
    public static class TextExtensions
    {
        public static string? MaybeText(this string value) =>
            value.Length == 0 ? null : value;
        public static int? Measure(this string value) =>
            value.Length == 0 ? null : value.Length;
        public static string Echo(this string value) => value;
        public static int Add(this int value) => value + 1;
        public static int Offset(this string value, int offset) => value.Length + offset;
        public static T Identity<T>(this T value) => value;
        public static void Touch(this string value) { }
    }
}

namespace TestCase
{
    public sealed class Source
    {
        public string? Text { get; init; }
        public string[]? Items { get; init; }
        public int? Number { get; init; }
        public Func<int, int>? Callback { get; init; }
        public bool Enabled { get; init; }
        public int Next() => 0;
    }

    public sealed class Destination
    {
        public Destination(__TYPE__ value) => Value = value;
        public __TYPE__ Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().__CALLBACK__;
    }
}
""";

        var configuration = callback switch
        {
            "Construct" => "Construct(source => new(" + expression + "))",
            "Members" => "Members(source => new() { Value = " + expression + " })",
            "Convert" => "Convert(source => new Destination(" +
                expression.Replace("source.", "source!.") + "))",
            _ => throw new ArgumentOutOfRangeException(nameof(callback))
        };
        var result = GeneratorTestDriver.Run(
            "ConditionalExpressionTransfer",
            source.Replace("__TYPE__", valueType)
                .Replace("__CALLBACK__", configuration),
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
