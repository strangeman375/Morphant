namespace Morphant.Generator.UnitTests.InheritanceDiagnosticsTests;

[TestFixture]
internal sealed class SourceMemberBindingTests
{
    [Test]
    public void Accepts_conditional_access_to_an_implicit_struct_implementation()
    {
        // lang=c#
        const string source = """
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public interface ISource { int Value { get; } }
    public struct Source : ISource { public int Value => 11; }
    public sealed class Box<T> where T : struct, ISource { public T? Payload; }
    public sealed class Destination { public int? Value { get; set; } }
    public abstract class Family<TMapper, TSource> : TypeMapper<TMapper>
        where TMapper : Family<TMapper, TSource> where TSource : struct, ISource
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Box<TSource>, Destination>()
            .Members(source => new() { Value = source.Payload?.Value });
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
        var result = InheritanceDiagnosticsGeneratorTest.Run(source);
        Assert.Multiple(() =>
        {
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
        });
    }

    [TestCase("Members", ".Members(source => new() { Value = source.Payload.Profile.Value })")]
    [TestCase("IncludeMembers", ".IncludeMembers(source => source.Payload.Profile)")]
    public void Rejects_copying_an_explicit_struct_receiver_and_recovers_after_an_edit(string callback, string rule)
    {
        // lang=c#
        string invalid = $$"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public sealed class Profile { public int Value { get; set; } }
    public interface IProfileSource { Profile Profile { get; } }
    public struct Source : IProfileSource
    {
        public int Reads;
        Profile IProfileSource.Profile { get { Reads++; return new() { Value = 11 }; } }
        public Profile Profile => new() { Value = 99 };
    }
    public sealed class Box<T> where T : IProfileSource { public T Payload = default!; }
    public sealed class Destination { public int Value { get; set; } }
    public abstract class Family<TMapper, TSource> : TypeMapper<TMapper>
        where TMapper : Family<TMapper, TSource> where TSource : IProfileSource
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
        var rejected = InheritanceDiagnosticsGeneratorTest.Run(invalid);
        Assert.Multiple(() =>
        {
            Assert.That(rejected.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(rejected.EffectiveDiagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0028" }));
            var diagnostic = rejected.EffectiveDiagnostics.Single();
            Assert.That(InheritanceDiagnosticsGeneratorTest.SourceText(diagnostic.Location), Is.EqualTo("IncludeBase"));
            Assert.That(diagnostic.AdditionalLocations.Select(InheritanceDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { callback, "Profile" }));
            Assert.That(rejected.OutputCompilation.GetTypeByMetadataName("TestCase.Mapper")!.AllInterfaces
                .Select(type => type.ToDisplayString()),
                Does.Contain("Morphant.ITypeMapper<TestCase.Box<TestCase.Source>, TestCase.Destination>"));
        });

        var recovered = InheritanceDiagnosticsGeneratorTest.Run(
            invalid.Replace("public struct Source", "public sealed class Source", StringComparison.Ordinal), driver: rejected.Driver);
        Assert.Multiple(() =>
        {
            Assert.That(recovered.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(recovered.EffectiveDiagnostics, Is.Empty);
        });
        var rejectedAgain = InheritanceDiagnosticsGeneratorTest.Run(invalid, driver: recovered.Driver);
        Assert.Multiple(() =>
        {
            Assert.That(rejectedAgain.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(rejectedAgain.EffectiveDiagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0028" }));
        });
    }
}
