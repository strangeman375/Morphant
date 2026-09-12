using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

[TestFixture]
internal sealed class StructuredResultTests
{
    [TestCase("Construct", "return new Destination(source.Id);", "new Destination(source.Id)")]
    [TestCase("Resolve", "return new Destination(source.Id);", "new Destination(source.Id)")]
    [TestCase("Construct", "return source.Cached;", "source.Cached")]
    [TestCase("Resolve", "return source.Cached;", "source.Cached")]
    [TestCase("Construct", "return Make(source);", "Make(source)")]
    [TestCase("Resolve", "return Make(source);", "Make(source)")]
    [TestCase("Resolve", "var cached = source.Cached; var alias = cached; return alias;", "alias")]
    [TestCase("Resolve", "if (previous.HasValue) return Identity(previous.Value); return new(source.Id);", "Identity(previous.Value)")]
    [TestCase("Resolve", "if (previous.HasValue) return previous.Value.Other; return new(source.Id);", "previous.Value.Other")]
    [TestCase("Resolve", "if (source.Alternative.TryGetValue(out var other)) return other; return new(source.Id);", "other")]
    [TestCase("Resolve", "if (source.TryGetValue(out var other)) return other; return new(source.Id);", "other")]
    [TestCase("Resolve", "if (previous.HasValue) { var option = source.Alternative; return option.Value; } return new(source.Id);", "option.Value")]
    [TestCase("Resolve", "return new DestinationConstruction(source.Id);", "new DestinationConstruction(source.Id)")]
    [TestCase("Resolve", "return new DerivedDestination(source.Id);", "new DerivedDestination(source.Id)")]
    [TestCase("Resolve", "return new Destination(source.Id) { Id = 3 };", "new Destination(source.Id) { Id = 3 }")]
    [TestCase("Resolve", "return previous.HasValue ? previous.Value : new(source.Id);", "new(source.Id)")]
    public void Rejects_arbitrary_destinations_with_actionable_diagnostic(
        string method, string body, string offendingExpression)
    {
        var result = Run(method, body);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics.Select(d => d.Id),
                Is.EqualTo(new[] { "MORPH0062" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
        var diagnostic = result.EffectiveDiagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(GeneratorTestDriver.GetSourceText(diagnostic.Location),
                Is.EqualTo(offendingExpression));
            Assert.That(diagnostic.GetMessage(), Is.EqualTo(
                method + " for mapping 'Source -> Destination' must return " +
                (method == "Resolve"
                    ? "the existing destination from previous or a construction expression"
                    : "a construction expression") +
                ". Use " + method + "Using to return another destination object."));
        });
    }

    [TestCase("if (previous.HasValue) return previous.Value; return new(source.Id);")]
    [TestCase("if (previous.HasValue) return ((previous.Value))!; return new(source.Id);")]
    [TestCase("if (previous.HasValue) { var first = previous.Value; var second = first; return second; } return new(source.Id);")]
    [TestCase("if (previous.HasValue) { Destination first = previous.Value; Destination second = first; return second; } return new(source.Id);")]
    [TestCase("if (previous.TryGetValue(out var existing)) return existing; return new(source.Id);")]
    [TestCase("if (previous.TryGetValue(out var existing)) { var alias = existing; return alias; } return new(source.Id);")]
    [TestCase("if (previous.TryGetValue(out var existing) && existing.Id == source.Id) return existing; return new(source.Id);")]
    [TestCase("if (!previous.TryGetValue(out var existing)) return new(source.Id); return existing;")]
    [TestCase("if (previous.HasValue) { switch (source.Id) { case 1: return previous.Value; default: return new(source.Id); } } return new(source.Id);")]
    [TestCase("var option = previous; if (option.HasValue) return option.Value; return new(source.Id);")]
    [TestCase("var option = previous; if (option.TryGetValue(out var existing)) return existing; return new(source.Id);")]
    public void Accepts_only_available_previous_values_and_unchanged_aliases(string body)
    {
        var result = Run("Resolve", body);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [TestCase("return previous.Value;")]
    [TestCase("var first = previous.Value; var second = first; return second;")]
    [TestCase("var existing = previous.Value; if (previous.HasValue) return existing; return new(source.Id);")]
    [TestCase("var option = previous; var existing = option.Value; if (option.HasValue) return existing; return new(source.Id);")]
    [TestCase("if (!previous.TryGetValue(out var existing)) return existing!; return new(source.Id);")]
    [TestCase("if (source.Id > 0) return previous.Value; return new(source.Id);")]
    public void Keeps_the_previous_availability_diagnostic(string body)
    {
        var result = Run("Resolve", body);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics.Select(d => d.Id),
                Is.EqualTo(new[] { "MORPH0038" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [TestCase("ConstructUsing", "return source.Cached;")]
    [TestCase("ResolveUsing", "return source.Cached;")]
    [TestCase("ConstructUsing", "return Make(source);")]
    [TestCase("ResolveUsing", "return Make(source);")]
    [TestCase("ConstructUsing", "return new Destination(source.Id);")]
    [TestCase("ResolveUsing", "return new Destination(source.Id);")]
    public void Factory_methods_accept_arbitrary_destinations(string method, string body)
    {
        var result = Run(method, body);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static GeneratorTestDriverResult Run(string method, string body) =>
        GeneratorTestDriver.Run("StructuredResultContracts",
            Source.Replace("__METHOD__", method)
                .Replace("__PARAMETERS__", method.StartsWith("Resolve", StringComparison.Ordinal)
                    ? "(source, previous)" : "source")
                .Replace("__BODY__", body), LanguageVersion.CSharp9);

    private const string Source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
public sealed class Source
{
    public int Id { get; set; }
    public Destination Cached { get; set; } = new Destination(1);
    public Option<Destination> Alternative => Option<Destination>.Some(Cached);
    public bool TryGetValue(out Destination value) { value = Cached; return true; }
}
public class Destination
{
    public Destination(int id) => Id = id;
    public int Id { get; set; }
    public Destination Other => new Destination(Id);
}
public sealed class DestinationConstruction : Destination
{
    public DestinationConstruction(int id) : base(id) { }
}
public sealed class DerivedDestination : Destination
{
    public DerivedDestination(int id) : base(id) { }
}
[MorphantMapper]
public partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>().__METHOD__(__PARAMETERS__ => { __BODY__ });
    private static Destination Make(Source source) => new Destination(source.Id);
    private static Destination Identity(Destination value) => value;
}
""";
}
