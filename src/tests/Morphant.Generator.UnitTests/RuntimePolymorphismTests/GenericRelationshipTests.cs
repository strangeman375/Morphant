using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class GenericRelationshipTests
{
    [TestCase("IProducer<T>", "IProducer<string>", "class")]
    [TestCase("IConsumer<T>", "IConsumer<string>", "class")]
    [TestCase("IInvariant<T>", "IInvariant<string>", "class")]
    [TestCase("Producer<T>", "IProducer<string>", "class")]
    [TestCase("IProducer<IProducer<T>>", "IProducer<IProducer<string>>", "class")]
    [TestCase("Parent<T>", "Child<string>", "class")]
    [TestCase("Outer<T>.Item", "Outer<string>.Item", "class")]
    [TestCase("T[]", "string[]", "class")]
    [TestCase("T", "IProducer<string>", "class")]
    [TestCase("IProducer<T>", "IProducer<int>", "struct")]
    [TestCase("IProducer<T>", "IProducer<object>", "class")]
    public void Reports_a_relationship_that_can_change_after_substitution(string first, string second, string constraint)
    {
        var source = CreateSource(first, second, constraint);
        var result = GeneratorTestDriver.Run("UnknownPolymorphicRelationship", source, LanguageVersion.CSharp9);
        Assert.Multiple(() =>
        {
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.EffectiveDiagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0061" }));
        });
        var diagnostic = result.EffectiveDiagnostics.Single();
        var firstStart = source.IndexOf("ForDerived<" + first, StringComparison.Ordinal) + "ForDerived<".Length;
        var secondStart = source.IndexOf("ForDerived<" + second, StringComparison.Ordinal) + "ForDerived<".Length;
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(diagnostic.Location.SourceSpan, Is.EqualTo(new TextSpan(secondStart, second.Length)));
            Assert.That(diagnostic.AdditionalLocations.Select(location => location.SourceSpan),
                Is.EqualTo(new[] { new TextSpan(firstStart, first.Length) }));
            Assert.That(diagnostic.GetMessage(), Is.EqualTo(
                $"The relationship between ForDerived source types '{Display(first)}' and '{Display(second)}' " +
                "in mapping 'object -> TestCase.Result<T>' depends on unknown generic arguments. " +
                "Declare branches whose relative specificity is known at generation time."));
        });
    }

    [TestCase("Parent<T>", "Child<T>", "class")]
    [TestCase("IProducer<T>", "ISpecific<T>", "class")]
    [TestCase("ILeft<T>", "IRight<T>", "class")]
    [TestCase("IProducer<T>", "IProducer<string>", "struct")]
    [TestCase("IInvariant<T>", "IInvariant<string>", "struct")]
    [TestCase("IProducer<T>", "IProducer<string>", "class, System.IDisposable")]
    [TestCase("IProducer<T>", "IProducer<object>", "class, System.IDisposable")]
    [TestCase("IInvariant<(T, T)>", "IInvariant<(string, int)>", "class")]
    [TestCase("IProducer<string>", "IProducer<object>", "class")]
    [TestCase("T", "Unrelated", "Parent<string>")]
    public void Accepts_relationships_known_from_declarations_or_constraints(string first, string second, string constraint)
    {
        var source = CreateSource(first, second, constraint);
        var result = GeneratorTestDriver.Run("KnownPolymorphicRelationship", source, LanguageVersion.CSharp9);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static string Display(string type) => type is "T" or "T[]" or "string[]" ? type :
        "TestCase." + type.Replace("IProducer<IProducer", "IProducer<TestCase.IProducer", StringComparison.Ordinal);

    private static string CreateSource(string first, string second, string constraint) =>
        Template.Replace("__FIRST__", first, StringComparison.Ordinal)
            .Replace("__SECOND__", second, StringComparison.Ordinal)
            .Replace("__CONSTRAINT__", constraint, StringComparison.Ordinal);

    // lang=c#
    private const string Template =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public interface IProducer<out T> { }
    public interface IConsumer<in T> { }
    public interface IInvariant<T> { }
    public interface ILeft<T> { }
    public interface IRight<T> { }
    public interface ISpecific<T> : IProducer<T> { }
    public sealed class Producer<T> : IProducer<T> { }
    public class Parent<T> { }
    public sealed class Child<T> : Parent<T> { }
    public sealed class Unrelated { }
    public class Outer<T> { public sealed class Item { } }
    public class Result<T> { }
    public sealed class First<T> : Result<T> { }
    public sealed class Second<T> : Result<T> { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper<TestMapper<T>> where T : __CONSTRAINT__
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<object, Result<T>>()
                .ForDerived<__FIRST__, First<T>>()
                .ForDerived<__SECOND__, Second<T>>()
                .Convert(_ => new Result<T>());
    }
}
""";
}
