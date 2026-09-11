using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.MemberDiagnosticsTests;

[TestFixture]
internal sealed class ConstructorResultDependencyTests
{
    [TestCase("", true)]
    [TestCase(".Construct(_ => new(7))", true)]
    [TestCase(".Construct(_ => new(ByConvention()))", true)]
    [TestCase(".Resolve((_, previous) => new(7))", true)]
    [TestCase("", false)]
    [TestCase(".Construct(_ => new(7))", false)]
    public void Requires_an_independent_value_before_reading_result(string construction, bool sourceMember)
    {
        var source = Consumer(construction, "", "return new() { Value = result.Value + 10 };", sourceMember);
        var result = MemberDiagnosticsGeneratorTest.Run(source);
        if (sourceMember || construction.Length != 0)
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.EffectiveDiagnostics, Is.Empty);
                Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            });
            return;
        }
        var diagnostic = result.MemberDiagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics.Select(d => d.Id), Is.EqualTo(new[] { "MORPH0042" }));
            Assert.That(MemberDiagnosticsGeneratorTest.SourceText(diagnostic.Location), Is.EqualTo("Value"));
            Assert.That(diagnostic.AdditionalLocations.Select(MemberDiagnosticsGeneratorTest.SourceText),
                Does.Contain("result"));
            Assert.That(diagnostic.GetMessage(), Does.Contain("constructor parameter 'value'"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [TestCase(".NullDestinationHandling(NullDestinationHandling.Throw)", "return new() { Value = context.Operation is MappingOperation.Create ? 7 : result.Value + 10 };")]
    [TestCase(".NullDestinationHandling(NullDestinationHandling.Throw)", "if (context.Operation == MappingOperation.Create) return new() { Value = 7 }; return new() { Value = result.Value + 10 };")]
    [TestCase(".NullDestinationHandling(NullDestinationHandling.Throw)", "var value = context.Operation == MappingOperation.Create ? 7 : result.Value + 10; return new() { Value = value };")]
    [TestCase("", "return new() { Value = previous.HasValue ? result.Value + 10 : 7 };")]
    [TestCase("", "return new() { Value = previous.HasValue ? previous.Value.Value + 10 : 7 };")]
    [TestCase("", "return new() { Value = context.Operation is MappingOperation.Create || !previous.HasValue ? 7 : result.Value + 10 };")]
    [TestCase("", "var operation = context.Operation; var updating = operation is not MappingOperation.Create && previous.HasValue; return new() { Value = updating ? result.Value + 10 : 7 };")]
    [TestCase(".NullDestinationHandling(NullDestinationHandling.Throw)", "return new() { Value = context.Operation switch { MappingOperation.Create => 7, _ => result.Value + 10 } };")]
    [TestCase(".NullDestinationHandling(NullDestinationHandling.Throw)", "switch (context.Operation) { case MappingOperation.Create: return new() { Value = 7 }; default: return new() { Value = result.Value + 10 }; }")]
    [TestCase(".NullDestinationHandling(NullDestinationHandling.Throw)", "return context.Operation switch { MappingOperation.Create => new() { Value = 7 }, _ => new() { Value = result.Value + 10 } };")]
    public void Allows_values_available_on_every_creation_path(string settings, string body)
    {
        var result = MemberDiagnosticsGeneratorTest.Run(Consumer("", settings, body));
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [TestCase("return new() { Value = context.Operation is MappingOperation.Create ? 7 : result.Value + 10 };")]
    [TestCase("if (context.Operation == MappingOperation.Create) return new() { Value = 7 }; return new() { Value = result.Value + 10 };")]
    public void Reports_only_Update_without_previous_when_Create_is_guarded(string body)
    {
        var result = MemberDiagnosticsGeneratorTest.Run(Consumer("", "", body, sourceMember: false));
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics.Select(d => d.Id), Is.EqualTo(new[] { "MORPH0042" }));
            Assert.That(result.MemberDiagnostics.Single().GetMessage(),
                Does.EndWith("Affected cases: Update without an existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [TestCase("var alias = result; return new() { Value = alias.Value + 10 };")]
    [TestCase("if (result.Value > 0) return new() { Value = 7 }; return new() { Value = 8 };")]
    public void Reports_indirect_result_dependencies(string body)
    {
        var result = MemberDiagnosticsGeneratorTest.Run(Consumer("", "", body, sourceMember: false));
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Not.Empty);
            Assert.That(result.EffectiveDiagnostics.Select(d => d.Id), Is.All.EqualTo("MORPH0042"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [TestCase(".ConstructUsing(_ => new Destination(7))", "return new() { Value = result.Value + 10 };")]
    [TestCase(".ResolveUsing((_, previous) => previous.HasValue ? previous.Value : new Destination(7))", "return new() { Value = result.Value + 10 };")]
    public void Allows_result_after_a_runtime_factory(string construction, string body)
    {
        var result = MemberDiagnosticsGeneratorTest.Run(Consumer(construction, "", body));
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Allows_result_from_an_independent_replacement()
    {
        var result = MemberDiagnosticsGeneratorTest.Run(Consumer(
            ".Resolve((source, previous) => { if (previous.HasValue && source.Value > 0) return previous; return new(7); })", "",
            "return new() { Value = previous.HasValue ? result.Value + 10 : 7 };"));
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_required_result_dependency_for_an_explicit_constructor()
    {
        var source = Consumer(".Construct(_ => new(7))", "", "return new() { Value = result.Value + 10 };")
            .Replace("public int Value { get; set; }", "public required int Value { get; set; }");
        var result = MemberDiagnosticsGeneratorTest.Run(source, LanguageVersion.CSharp11);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics.Select(d => d.Id), Is.EqualTo(new[] { "MORPH0042" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [TestCase("Create", "", "context.Operation == MappingOperation.Create ? 7 : result.Value + 10")]
    [TestCase("Update", ".NullDestinationHandling(NullDestinationHandling.Throw)", "result.Value + 10")]
    public void Ignores_disabled_creation_paths(string mode, string settings, string expression)
    {
        var source = Consumer("", settings, "return new() { Value = " + expression + " };")
            .Replace("Map<Source, Destination>()", "Map<Source, Destination>(MappingMode." + mode + ")");
        var result = MemberDiagnosticsGeneratorTest.Run(source);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_result_in_the_wrong_operation_branch()
    {
        var result = MemberDiagnosticsGeneratorTest.Run(Consumer("", "",
            "return new() { Value = context.Operation is MappingOperation.Create ? result.Value + 10 : 7 };", sourceMember: false));
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics.Select(d => d.Id), Is.EqualTo(new[] { "MORPH0042" }));
            Assert.That(result.MemberDiagnostics.Single().GetMessage(), Does.EndWith("Affected cases: Create."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Severity_does_not_change_recovery_and_guard_edits_actualize_one_driver()
    {
        var source = Consumer("", "", "return new() { Value = result.Value + 10 };", sourceMember: false);
        var visible = MemberDiagnosticsGeneratorTest.Run(source);
        var suppressed = MemberDiagnosticsGeneratorTest.Run(source, diagnosticOptions:
            new Dictionary<string, ReportDiagnostic> { ["MORPH0042"] = ReportDiagnostic.Suppress });
        var warning = MemberDiagnosticsGeneratorTest.Run(source, diagnosticOptions:
            new Dictionary<string, ReportDiagnostic> { ["MORPH0042"] = ReportDiagnostic.Warn });
        var guarded = MemberDiagnosticsGeneratorTest.Run(
            source.Replace("result.Value + 10", "previous.HasValue ? result.Value + 10 : 7"), driver: visible.Driver);
        var restored = MemberDiagnosticsGeneratorTest.Run(source, driver: guarded.Driver);
        Assert.Multiple(() =>
        {
            Assert.That(visible.MemberDiagnostics.Single().Id, Is.EqualTo("MORPH0042"));
            Assert.That(warning.MemberDiagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(suppressed.EffectiveDiagnostics, Is.Empty);
            Assert.That(guarded.EffectiveDiagnostics, Is.Empty);
            Assert.That(restored.MemberDiagnostics.Single().GetMessage(), Is.EqualTo(visible.MemberDiagnostics.Single().GetMessage()));
            Assert.That(Sources(suppressed), Is.EqualTo(Sources(visible)));
            Assert.That(Sources(warning), Is.EqualTo(Sources(visible)));
            Assert.That(Sources(restored), Is.EqualTo(Sources(visible)));
            Assert.That(Sources(guarded), Is.Not.EqualTo(Sources(visible)));
            foreach (var run in new[] { visible, suppressed, warning, guarded, restored })
                Assert.That(run.CompilerWarningsAndErrors, Is.Empty);
        });

        static object[] Sources(MemberDiagnosticsGeneratorResult result) => result.GeneratedSources
            .OrderBy(s => s.HintName, StringComparer.Ordinal)
            .Select(s => (object)(s.HintName, s.SourceText.ToString())).ToArray();
    }

    private static string Consumer(string construction, string settings, string body, bool sourceMember = true) => $$"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
using Morphant.Context;
namespace TestCase
{
    public sealed class Source { {{(sourceMember ? "public int Value => 7;" : "")}} }
    public sealed class Destination
    {
        public Destination(int value) { Value = value; }
        public int Value { get; set; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>(){{settings}}{{construction}}
                .Members((source, previous, result, context) => { {{body}} });
    }
}
""";
}
