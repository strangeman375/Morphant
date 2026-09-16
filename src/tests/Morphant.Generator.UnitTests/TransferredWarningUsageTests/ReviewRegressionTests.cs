using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TransferredWarningUsageTests;

[TestFixture]
internal sealed class ReviewRegressionTests
{
    [Test]
    public void Keeps_multiline_interpolation_warnings_at_their_source(
        [Values("Construct", "Resolve", "Members", "ConstructUsing", "ResolveUsing", "Convert")] string callback,
        [Values("verbatim", "nested", "before", "after")] string placement)
    {
        var value = placement switch
        {
            "nested" => "Normalize($@\"first\n{$\"value:{source!.Legacy}\"}\nlast\")",
            "before" => "string.Concat($@\"first\n\", source!.Legacy)",
            "after" => "string.Concat(source!.Legacy, $@\"\nlast\")",
            _ => "Normalize($@\"first\n{source!.Legacy}\nlast\")"
        };
        var rule = callback switch
        {
            "Construct" => ".Construct(source => new(" + value + "))",
            "Resolve" => ".Resolve((source, previous) => { if (previous.HasValue) return previous.Value; return new(" + value + "); })",
            "Members" => ".Members(source => new() { Text = " + value + " })",
            "ConstructUsing" => ".ConstructUsing(source => new Destination(" + value + "))",
            "ResolveUsing" => ".ResolveUsing((source, previous) => previous.HasValue ? previous.Value : new Destination(" + value + "))",
            _ => ".Convert(source => new Destination(" + value + "))"
        };
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using System;
using System.Linq;
using Morphant;
namespace TestCase
{
    public sealed class Source
    {
        [Obsolete("Legacy value.")]
        public string Legacy => "value";
    }
    public sealed class Destination
    {
        public Destination(string text) => Text = text;
        public string Text { get; set; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()__RULE__;
        private static string Normalize(string text) =>
            string.Join("|", text.Split('\n').Select(line => line.Trim()));
    }
}
""";
        AssertSourceWarningOnly(Run(source.Replace("__RULE__", rule)),
            "CS0618", "source!.Legacy");
    }

    [Test]
    public void Keeps_local_declaration_warnings_at_their_source(
        [Values("Construct", "Resolve", "Members", "ConstructUsing", "ResolveUsing", "Convert")] string callback,
        [Values(ReportDiagnostic.Warn, ReportDiagnostic.Error, ReportDiagnostic.Suppress)] ReportDiagnostic reporting)
    {
        var rule = callback switch
        {
            "Construct" => ".Construct(source => { var unused = 1; return new(source.Value); })",
            "Resolve" => ".Resolve((source, previous) => { var unused = 1; if (previous.HasValue) return previous.Value; return new(source.Value); })",
            "Members" => ".Members(source => { var unused = 1; return new() { Value = source.Value }; })",
            "ConstructUsing" => ".ConstructUsing(source => { var unused = 1; return new Destination(source.Value); })",
            "ResolveUsing" => ".ResolveUsing((source, previous) => { var unused = 1; return previous.HasValue ? previous.Value : new Destination(source.Value); })",
            _ => ".Convert(source => { var unused = 1; return new Destination(source!.Value); })"
        };
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public sealed class Source { public int Value => 7; }
    public sealed class Destination
    {
        public Destination(int value) => Value = value;
        public int Value { get; set; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()__RULE__;
    }
}
""";
        var result = GeneratorTestDriver.Run("TestProject", source.Replace("__RULE__", rule),
            LanguageVersion.CSharp9, new Dictionary<string, ReportDiagnostic> { ["CS0219"] = reporting });
        if (reporting == ReportDiagnostic.Suppress) AssertNoDiagnostics(result);
        else AssertSourceWarningOnly(result, "CS0219", "unused",
            reporting == ReportDiagnostic.Error ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning);
    }

    [TestCase("ConstructUsing", false)]
    [TestCase("ConstructUsing", true)]
    [TestCase("ResolveUsing", false)]
    [TestCase("ResolveUsing", true)]
    [TestCase("Convert", false)]
    [TestCase("Convert", true)]
    public void Keeps_async_anonymous_method_warnings_at_their_source(
        string callback, bool nested)
    {
        var parameters = callback == "ResolveUsing"
            ? "(int source, Option<Task<int>> previous)"
            : "(int source)";
        var expression = nested
            ? parameters + " => { Func<Task<int>> pending = async delegate { return source; }; return pending(); }"
            : "async delegate" + parameters + " { return source; }";
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using System;
using System.Threading.Tasks;
using Morphant;
namespace TestCase
{
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<int, Task<int>>().__CALLBACK__(__EXPRESSION__);
    }
}
""";
        AssertSourceWarningOnly(Run(source.Replace("__CALLBACK__", callback)
            .Replace("__EXPRESSION__", expression)), "CS1998", "delegate");
    }

    [TestCase("Construct")]
    [TestCase("Resolve")]
    [TestCase("Members")]
    [TestCase("ConstructUsing")]
    [TestCase("ResolveUsing")]
    [TestCase("Convert")]
    public void Keeps_warning_directives_compiler_owned(string callback)
    {
        var expression = callback switch
        {
            "Construct" => ".Construct(source => { __WARNING__ return new(source); })",
            "Resolve" => ".Resolve((source, previous) => { __WARNING__ if (previous.HasValue) return previous.Value; return new(source); })",
            "Members" => ".Members(source => { __WARNING__ return new() { Value = source }; })",
            "ConstructUsing" => ".ConstructUsing(source => { __WARNING__ return new Destination(source); })",
            "ResolveUsing" => ".ResolveUsing((source, previous) => { __WARNING__ return previous.HasValue ? previous.Value : new Destination(source); })",
            _ => ".Convert(source => { __WARNING__ return new Destination(source); })"
        };
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public sealed class Destination
    {
        public Destination(int value) => Value = value;
        public int Value { get; set; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<int, Destination>()__EXPRESSION__;
    }
}
""";
        AssertSourceWarningOnly(Run(source.Replace("__EXPRESSION__",
                expression.Replace("__WARNING__", "\n#warning review reminder\n"))),
            "CS1030", "review reminder");
    }

    [TestCase("OLD001", true)]
    [TestCase("class", true)]
    [TestCase("OLD-001", false)]
    [TestCase("001", false)]
    public void Keeps_custom_obsolete_diagnostics_compiler_owned(string diagnosticId, bool suppressible)
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using System;
using Morphant;
namespace TestCase
{
    public sealed class Source
    {
        [Obsolete("Legacy value.", DiagnosticId = "__ID__")]
        public int Legacy => 7;
    }
    public sealed class Destination
    {
        public Destination(int value) => Value = value;
        public int Value { get; set; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new() { Value = source.Legacy });
    }
}
""";
        var result = Run(source.Replace("__ID__", diagnosticId));
        if (suppressible)
        {
            AssertSourceWarningOnly(result, diagnosticId, "source.Legacy");
            return;
        }

        const string generatedPath = "Morphant.Generator/Morphant.Generator.MorphantGenerator/" +
            "Morphant.Generated.TypeMapper.Mapper__f6afa7d38a3eeb111905cdec012f335d.g.cs";
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors.Select(diagnostic =>
                (diagnostic.Id, diagnostic.Severity, diagnostic.Location.SourceTree!.FilePath,
                    GeneratorTestDriver.GetSourceText(diagnostic.Location))), Is.EqualTo(new[]
                {
                    (diagnosticId, DiagnosticSeverity.Warning, "TestCase.cs", "source.Legacy"),
                    (diagnosticId, DiagnosticSeverity.Warning, generatedPath, "source.Legacy"),
                    (diagnosticId, DiagnosticSeverity.Warning, generatedPath, "source.Legacy")
                }));
        });
    }

    [TestCase("ConstructUsing", false)]
    [TestCase("ConstructUsing", true)]
    [TestCase("ResolveUsing", false)]
    [TestCase("ResolveUsing", true)]
    public void Does_not_warn_for_synthesized_factory_result_declarations(
        string callback, bool hasMember)
    {
        var expression = callback == "ResolveUsing"
            ? "(source, previous) => null!"
            : "source => null!";
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591, CS0618
using System;
using Morphant;
namespace TestCase
{
    public sealed class Source { public int Value => 7; }
    [Obsolete("Legacy type.")]
    public sealed class Destination { __MEMBER__ }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().__CALLBACK__(__EXPRESSION__);
    }
}
""";
        AssertNoDiagnostics(Run(source.Replace("__CALLBACK__", callback)
            .Replace("__EXPRESSION__", expression)
            .Replace("__MEMBER__", hasMember ? "public int Value { get; set; }" : "")));
    }

    [TestCase("ConstructUsing")]
    [TestCase("ResolveUsing")]
    [TestCase("Convert")]
    public void Preserves_suppression_on_explicit_obsolete_type_construction(string callback)
    {
        var expression = callback == "ResolveUsing"
            ? "(source, previous) => previous.HasValue ? previous.Value : new Destination()"
            : "source => new Destination()";
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591, CS0618
using System;
using Morphant;
namespace TestCase
{
    public sealed class Source { }
    [Obsolete("Legacy type.")]
    public sealed class Destination { }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().__CALLBACK__(__EXPRESSION__);
    }
}
""";
        AssertNoDiagnostics(Run(source.Replace("__CALLBACK__", callback)
            .Replace("__EXPRESSION__", expression)));
    }

    private static GeneratorTestDriverResult Run(string source) =>
        GeneratorTestDriver.Run("TestProject", source, LanguageVersion.CSharp9);

    private static void AssertSourceWarningOnly(
        GeneratorTestDriverResult result, string id, string sourceSpan,
        DiagnosticSeverity severity = DiagnosticSeverity.Warning)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors.Select(diagnostic =>
                (diagnostic.Id, diagnostic.Severity,
                    diagnostic.Location.SourceTree!.FilePath,
                    GeneratorTestDriver.GetSourceText(diagnostic.Location))),
                Is.EqualTo(new[] { (id, severity, "TestCase.cs", sourceSpan) }));
        });
    }

    private static void AssertNoDiagnostics(GeneratorTestDriverResult result)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
