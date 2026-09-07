using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class MemberBindingCompilationTests
{
    [TestCase("Describe(ReadPayload())", "Describe", "TestCase.Payload", 0)]
    [TestCase("Describe(this.ReadPayload())", "Describe", "TestCase.Payload", 0)]
    [TestCase("Describe(Payload)", "Describe", "TestCase.Payload", 0)]
    [TestCase("Describe(this.Payload)", "Describe", "TestCase.Payload", 0)]
    [TestCase("DescribeMapper(this)", "DescribeMapper", "TestCase.BaseMapper<TestCase.Mapper>", 0)]
    [TestCase("Identify((TMapper)this)", "Identify", "TestCase.Mapper", 1)]
    [TestCase("this.Identify((TMapper)this)", "Identify", "TestCase.Mapper", 1)]
    public void Preserves_the_selected_overload_in_generated_callbacks(
        string expression, string methodName, string parameterType, int arity)
    {
        // lang=c#
        string source = $$"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { public string Text { get; set; } = ""; }
    public class Payload { }
    public sealed class DerivedPayload : Payload { }
    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper> where TMapper : BaseMapper<TMapper>
    {
        protected virtual Payload ReadPayload() => new();
        protected virtual Payload Payload => new();
        protected static string Describe(Payload value) => "original";
        protected static string Describe(DerivedPayload value) => "changed";
        protected static string DescribeMapper(BaseMapper<TMapper> mapper) => "original";
        protected static string DescribeMapper(TMapper mapper) => "changed";
        protected string Identify<TValue>(TValue value) => "original";
        protected string Identify(Mapper value) => "changed";
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Convert(_ => new() { Text = {{expression}} });
    }
    [MorphantMapper]
    public partial class Mapper : BaseMapper<Mapper>
    {
        protected override DerivedPayload ReadPayload() => new();
        protected override DerivedPayload Payload => new();
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>().IncludeBase<Source, Destination>();
        }
    }
}
""";
        var result = GeneratorTestDriver.Run("InheritedBindingConsumer", source, LanguageVersion.CSharp9);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });

        var calls = result.GeneratedSources.SelectMany(generated =>
        {
            var model = result.OutputCompilation.GetSemanticModel(generated.SyntaxTree);
            return generated.SyntaxTree.GetRoot().DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Select(call => model.GetSymbolInfo(call).Symbol)
                .OfType<IMethodSymbol>()
                .Where(method => method.Name == methodName);
        }).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.Not.Empty);
            Assert.That(calls.Select(method => method.ContainingType.Name), Has.All.EqualTo("BaseMapper"));
            Assert.That(calls.Select(method => method.Arity), Has.All.EqualTo(arity));
            Assert.That(calls.Select(method => method.Parameters.Single().Type.ToDisplayString()),
                Has.All.EqualTo(parameterType));
        });
    }
}
