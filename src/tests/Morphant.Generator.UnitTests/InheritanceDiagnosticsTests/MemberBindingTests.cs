namespace Morphant.Generator.UnitTests.InheritanceDiagnosticsTests;

[TestFixture]
internal sealed class MemberBindingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Rejects_hidden_protected_virtual_calls_in_every_callback(bool explicitThis)
    {
        var result = InheritanceDiagnosticsGeneratorTest.Run(BuildVirtualSource(explicitThis));
        AssertBlockedFamilies(result);
    }

    [Test]
    public void Accepts_the_original_virtual_slot_when_its_declaring_type_is_accessible()
    {
        string source = BuildVirtualSource(explicitThis: true)
            .Replace("protected virtual string Read()", "internal virtual string Read()", StringComparison.Ordinal);
        var result = InheritanceDiagnosticsGeneratorTest.Run(source);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Actualizes_hidden_member_access_and_recovers_on_one_driver()
    {
        string invalidSource = BuildVirtualSource(explicitThis: false);
        string validSource = invalidSource.Replace(
            "protected virtual string Read()", "internal virtual string Read()", StringComparison.Ordinal);
        var invalid = InheritanceDiagnosticsGeneratorTest.Run(invalidSource);
        AssertBlockedFamilies(invalid);
        var valid = InheritanceDiagnosticsGeneratorTest.Run(validSource, driver: invalid.Driver);
        Assert.Multiple(() =>
        {
            Assert.That(valid.EffectiveDiagnostics, Is.Empty);
            Assert.That(valid.CompilerWarningsAndErrors, Is.Empty);
        });
        var invalidAgain = InheritanceDiagnosticsGeneratorTest.Run(invalidSource, driver: valid.Driver);
        AssertBlockedFamilies(invalidAgain);
    }

    [Test]
    public void Rejects_a_nonvirtual_member_hidden_in_an_intermediate_base()
    {
        string source = BuildVirtualSource(explicitThis: false)
            .Replace("protected virtual string Read()", "protected string Read()", StringComparison.Ordinal)
            .Replace("[MorphantMapper]", """
public abstract class IntermediateMapper<TMapper> : BaseMapper<TMapper>
    where TMapper : IntermediateMapper<TMapper>
{
    protected new string Read() => "intermediate";
}
[MorphantMapper]
""", StringComparison.Ordinal)
            .Replace("Mapper : BaseMapper<Mapper>", "Mapper : IntermediateMapper<Mapper>", StringComparison.Ordinal);
        var result = InheritanceDiagnosticsGeneratorTest.Run(source);
        AssertBlockedFamilies(result);
    }

    [Test]
    public void Does_not_require_runtime_binding_for_nameof_or_callback_local_functions()
    {
        // lang=c#
        const string source = """
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { public string Text { get; set; } = ""; }
    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper> where TMapper : BaseMapper<TMapper>
    {
        protected virtual string Read() => "base";
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Convert(_ =>
            {
                string Read() => "local";
                return new() { Text = nameof(this.Read) + Read() };
            });
    }
    [MorphantMapper]
    public partial class Mapper : BaseMapper<Mapper>
    {
        protected new string Read() => "derived";
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>().IncludeBase<Source, Destination>();
        }
    }
}
""";
        var result = InheritanceDiagnosticsGeneratorTest.Run(source);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static void AssertBlockedFamilies(InheritanceDiagnosticsGeneratorResult result)
    {
        string[] families = ["Construct", "Resolve", "Members", "ConstructUsing", "ResolveUsing", "Convert"];
        Assert.Multiple(() =>
        {
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.EffectiveDiagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0028", "MORPH0028", "MORPH0028", "MORPH0028", "MORPH0028", "MORPH0028" }));
            Assert.That(result.EffectiveDiagnostics.Select(diagnostic =>
                InheritanceDiagnosticsGeneratorTest.SourceText(diagnostic.Location)), Has.All.EqualTo("IncludeBase"));
            Assert.That(result.EffectiveDiagnostics.Select(diagnostic =>
                InheritanceDiagnosticsGeneratorTest.SourceText(diagnostic.AdditionalLocations[0])), Is.EquivalentTo(families));
            Assert.That(result.EffectiveDiagnostics.Select(diagnostic => diagnostic.AdditionalLocations.Count), Has.All.EqualTo(2));
            Assert.That(result.EffectiveDiagnostics.Select(diagnostic =>
                InheritanceDiagnosticsGeneratorTest.SourceText(diagnostic.AdditionalLocations[1])), Has.All.EqualTo("Read"));
        });
    }

    private static string BuildVirtualSource(bool explicitThis)
    {
        string read = explicitThis ? "this.Read()" : "Read()";
        // lang=c#
        return $$"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public sealed class Source { }
    public sealed class ConstructTag { }
    public sealed class ResolveTag { }
    public sealed class FactoryTag { }
    public sealed class ResolverTag { }
    public sealed class ConvertTag { }
    public sealed class Destination<T> { public Destination(string text) { Text = text; } public string Text { get; } }
    public sealed class MemberDestination { public string Text { get; set; } = ""; }
    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper> where TMapper : BaseMapper<TMapper>
    {
        protected virtual string Read() => "base";
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<ConstructTag>>().Construct(_ => new({{read}}));
            builder.Map<Source, Destination<ResolveTag>>().Resolve((_, previous) => new({{read}}));
            builder.Map<Source, MemberDestination>().Members(_ => new() { Text = {{read}} });
            builder.Map<Source, Destination<FactoryTag>>().ConstructUsing(_ => new({{read}}));
            builder.Map<Source, Destination<ResolverTag>>().ResolveUsing((_, previous) => new({{read}}));
            builder.Map<Source, Destination<ConvertTag>>().Convert(_ => new({{read}}));
        }
    }
    [MorphantMapper]
    public partial class Mapper : BaseMapper<Mapper>
    {
        protected new string Read() => "derived";
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination<ConstructTag>>().IncludeBase<Source, Destination<ConstructTag>>();
            builder.Map<Source, Destination<ResolveTag>>().IncludeBase<Source, Destination<ResolveTag>>();
            builder.Map<Source, MemberDestination>().IncludeBase<Source, MemberDestination>();
            builder.Map<Source, Destination<FactoryTag>>().IncludeBase<Source, Destination<FactoryTag>>();
            builder.Map<Source, Destination<ResolverTag>>().IncludeBase<Source, Destination<ResolverTag>>();
            builder.Map<Source, Destination<ConvertTag>>().IncludeBase<Source, Destination<ConvertTag>>();
        }
    }
}
""";
    }
}
