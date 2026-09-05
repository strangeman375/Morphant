using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.ActualizationTests;

[TestFixture]
internal sealed class BrokenEditRecoveryActualizationTests
{
    private static readonly string[] GeneratedFiles =
    [
        "Morphant.Generated.Construction.TestCase_Destination.g.cs",
        "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs",
        "Morphant.Generated.Member.TestCase_Destination.g.cs",
        "Morphant.Generated.MemberExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs",
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs"
    ];

    private static readonly string[] SharedGeneratedFiles =
    [
        "Morphant.Generated.Construction.TestCase_Destination.g.cs",
        "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs",
        "Morphant.Generated.Member.TestCase_Destination.g.cs",
        "Morphant.Generated.MemberExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs"
    ];

    [Test]
    public void Matches_a_fresh_run_during_a_binding_error_and_after_recovery()
    {
        var initial = BuildSource("Members", 10, 17);
        var broken = BuildSource("MissingMembers", 10, 17);
        var recovered = BuildSource("Members", 20, 27);
        var brokenMemberStart = broken.IndexOf(
            "MissingMembers",
            StringComparison.Ordinal);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ExecutableStep(
                "valid configuration",
                [SourceFile("TestCase.cs", initial)],
                GeneratedFiles,
                "TestCase.Scenario"),
            StepWithDiagnostics(
                "temporarily unbound configuration",
                [SourceFile("TestCase.cs", broken)],
                GeneratedFiles,
                [
                    CompilerDiagnostic(
                        "CS1061",
                        DiagnosticSeverity.Error,
                        "TestCase.cs",
                        brokenMemberStart,
                        "MissingMembers".Length)
                ]),
            ExecutableStep(
                "configuration recovered with new behavior",
                [SourceFile("TestCase.cs", recovered)],
                GeneratedFiles,
                "TestCase.Scenario"));
    }

    [Test]
    public void Matches_a_fresh_run_during_a_syntax_error_and_after_recovery()
    {
        var initial = BuildSource("Members", 10, 17);
        var broken = DefaultSourceWithoutSemicolon;
        var recovered = BuildSource("Members", 20, 27);
        const string invocation = "builder.Map<Source, Destination>()";
        var missingSemicolonStart = broken.IndexOf(
            invocation,
            StringComparison.Ordinal) + invocation.Length;

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ExecutableStep(
                "valid configuration before syntax error",
                [SourceFile("TestCase.cs", initial)],
                GeneratedFiles,
                "TestCase.Scenario"),
            StepWithDiagnostics(
                "temporarily missing semicolon",
                [SourceFile("TestCase.cs", broken)],
                GeneratedFiles,
                [
                    CompilerDiagnostic(
                        "CS1002",
                        DiagnosticSeverity.Error,
                        "TestCase.cs",
                        missingSemicolonStart,
                        0)
                ]),
            ExecutableStep(
                "configuration recovered after syntax error",
                [SourceFile("TestCase.cs", recovered)],
                GeneratedFiles,
                "TestCase.Scenario"));
    }

    [Test]
    public void Purges_a_mapper_blocked_by_a_declaration_gate_and_restores_it()
    {
        var valid = BuildDeclarationSource("partial ");
        var invalid = BuildDeclarationSource(string.Empty);
        var mapperIdentifierStart = invalid.IndexOf(
            "class TestMapper",
            StringComparison.Ordinal) + "class ".Length;

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ExecutableStep(
                "partial mapper declaration",
                [SourceFile("TestCase.cs", valid)],
                GeneratedFiles,
                "TestCase.Scenario"),
            StepWithDiagnostics(
                "partial modifier temporarily removed",
                [SourceFile("TestCase.cs", invalid)],
                SharedGeneratedFiles,
                [
                    CompilerDiagnostic(
                        "MORPH0006",
                        DiagnosticSeverity.Error,
                        "TestCase.cs",
                        mapperIdentifierStart,
                        "TestMapper".Length)
                ]),
            ExecutableStep(
                "partial modifier restored",
                [SourceFile("TestCase.cs", valid)],
                GeneratedFiles,
                "TestCase.Scenario"));
    }

    private static string BuildSource(
        string memberMethod,
        int offset,
        int expectedValue)
    {
        return SourceTemplate
            .Replace("__MEMBER_METHOD__", memberMethod)
            .Replace("__OFFSET__", offset.ToString())
            .Replace("__EXPECTED_VALUE__", expectedValue.ToString());
    }

    private static string BuildDeclarationSource(string partialModifier)
    {
        return DeclarationSourceTemplate.Replace(
            "__PARTIAL__",
            partialModifier);
    }

    // lang=c#
    private const string SourceTemplate =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .__MEMBER_METHOD__((source, _) => new()
                {
                    Value = source.Value + __OFFSET__
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var destination = mapper.Create(new Source { Value = 7 });

            if (destination.Value != __EXPECTED_VALUE__)
            {
                throw new InvalidOperationException(
                    "Recovered configuration was not actualized.");
            }
        }
    }
}
""";

    // lang=c#
    private const string DefaultSourceWithoutSemicolon =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var destination = mapper.Create(new Source { Value = 7 });

            if (destination.Value != 7)
            {
                throw new InvalidOperationException(
                    "Default mapping was not actualized.");
            }
        }
    }
}
""";

    // lang=c#
    private const string DeclarationSourceTemplate =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public __PARTIAL__class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var destination = mapper.Create(new Source { Value = 17 });

            if (destination.Value != 17)
            {
                throw new InvalidOperationException(
                    "The mapper declaration was not actualized.");
            }
        }
    }
}
""";
}
