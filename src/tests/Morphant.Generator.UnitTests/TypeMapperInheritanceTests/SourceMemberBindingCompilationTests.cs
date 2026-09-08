using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class SourceMemberBindingCompilationTests
{
    [TestCase("Members")]
    [TestCase("Construct")]
    [TestCase("Resolve")]
    [TestCase("ConstructUsing")]
    [TestCase("ResolveUsing")]
    [TestCase("Convert")]
    [TestCase("IncludeMembers")]
    public void Preserves_the_generic_source_property_in_every_callback(string callback)
    {
        var result = Run(BuildSource(callback));
        AssertBinding(result, "Profile", "BaseSource");
    }

    [TestCase("source.Payload?.Profile.Value ?? 0", "Profile")]
    [TestCase("source.Payload?.Profile?.Value ?? 0", "Profile")]
    [TestCase("source.Payload.Read(new DerivedProfile()).Value", "Read")]
    [TestCase("source.Payload[0].Value", "this[]")]
    [TestCase("source.Payload?[0].Value ?? 0", "this[]")]
    public void Preserves_conditional_access_methods_and_indexers(string expression, string member)
    {
        var result = Run(BuildSource("Members").Replace("source.Payload.Profile.Value", expression, StringComparison.Ordinal));
        AssertBinding(result, member, "BaseSource");
    }

    [TestCase("Members")]
    [TestCase("IncludeMembers")]
    public void Preserves_the_declared_result_type_of_a_covariant_property(string callback)
    {
        var source = BuildSource(callback).Replace(
            "public new Profile Profile => new();", "public override DerivedProfile Profile => new();", StringComparison.Ordinal);
        var result = Run(source);
        AssertBinding(result, "Profile", "BaseSource");
        AssertBinding(result, "Value", "Profile");
    }

    [TestCase("Members")]
    [TestCase("IncludeMembers")]
    public void Actualizes_added_and_removed_hiding_on_one_driver(string callback)
    {
        string hidden = BuildSource(callback);
        string inherited = hidden.Replace("public new Profile Profile => new();", "", StringComparison.Ordinal);
        var before = Run(inherited);
        AssertBinding(before, "Profile", "BaseSource");
        var changed = Run(hidden, before.Driver);
        AssertBinding(changed, "Profile", "BaseSource");
        AssertBinding(Run(inherited, changed.Driver), "Profile", "BaseSource");
    }

    private static GeneratorTestDriverResult Run(string source, GeneratorDriver? driver = null) =>
        GeneratorTestDriver.Run("GenericSourceBindingConsumer", source, LanguageVersion.CSharp9, driver: driver);

    private static void AssertBinding(GeneratorTestDriverResult result, string name, string owner)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
        var selectedMembers = result.GeneratedSources.SelectMany(generated =>
        {
            var model = result.OutputCompilation.GetSemanticModel(generated.SyntaxTree);
            return generated.SyntaxTree.GetRoot().DescendantNodes()
                .OfType<ExpressionSyntax>()
                .Where(node => node is MemberAccessExpressionSyntax or MemberBindingExpressionSyntax or
                    ElementAccessExpressionSyntax or ElementBindingExpressionSyntax)
                .Select(node => model.GetSymbolInfo(node).Symbol)
                .Where(symbol => symbol?.Name == name && symbol.ContainingType?.Name != "Destination");
        }).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(selectedMembers, Is.Not.Empty);
            Assert.That(selectedMembers.Select(member => member!.ContainingType.Name), Has.All.EqualTo(owner));
        });
    }

    private static string BuildSource(string callback)
    {
        string rule = callback switch
        {
            "Members" => ".Members(source => new() { Value = source.Payload.Profile.Value })",
            "Construct" => ".Construct(source => new(source.Payload.Profile.Value))",
            "Resolve" => ".Resolve((source, _) => new(source.Payload.Profile.Value))",
            "ConstructUsing" => ".ConstructUsing(source => new(source.Payload.Profile.Value))",
            "ResolveUsing" => ".ResolveUsing((source, _) => new(source.Payload.Profile.Value))",
            "Convert" => ".Convert(source => source is null ? new(0) : new(source.Payload.Profile.Value))",
            "IncludeMembers" => ".IncludeMembers(source => source.Payload.Profile)",
            _ => throw new ArgumentOutOfRangeException(nameof(callback))
        };
        // lang=c#
        return $$"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public class Profile { public int Value { get; set; } = 11; }
    public sealed class DerivedProfile : Profile { public new int Value { get; set; } = 99; }
    public class BaseSource
    {
        public virtual Profile Profile => new();
        public Profile Read(Profile profile) => profile;
        public Profile this[int index] => new();
    }
    public sealed class Source : BaseSource
    {
        public new Profile Profile => new();
        public Profile Read(DerivedProfile profile) => profile;
        public new Profile this[int index] => new();
    }
    public sealed class Box<T> where T : BaseSource { public T Payload { get; set; } = default!; }
    public sealed class Destination { public Destination(int value = 0) { Value = value; } public int Value { get; set; } }
    public abstract class Family<TMapper, TSource> : TypeMapper<TMapper>
        where TMapper : Family<TMapper, TSource> where TSource : BaseSource
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Box<TSource>, Destination>(){{rule}};
    }
    [MorphantMapper]
    public partial class Mapper : Family<Mapper, Source>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Box<Source>, Destination>().IncludeBase<Box<Source>, Destination>();
        }
    }
}
""";
    }
}
