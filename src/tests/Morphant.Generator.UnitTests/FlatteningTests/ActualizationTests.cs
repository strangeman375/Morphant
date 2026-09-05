using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.FlatteningTests;

[TestFixture]
internal sealed class ActualizationTests
{
    private const string Mapper =
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs";

    private static readonly string[] GeneratedFiles =
    [
        "Morphant.Generated.Construction.TestCase_Destination.g.cs",
        "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs",
        "Morphant.Generated.Member.TestCase_Destination.g.cs",
        "Morphant.Generated.MemberExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs",
        Mapper
    ];

    [Test]
    public void Actualizes_when_a_nested_source_contract_changes()
    {
        var stable = SourceFile("MapperAndScenario.cs", StableSource);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ExecutableStep(
                "matching path",
                [stable, SourceFile("Models.cs", Models("Name", "mapped"))],
                GeneratedFiles,
                "TestCase.Scenario"),
            ExecutableStep(
                "path removed",
                [stable, SourceFile("Models.cs", Models("Other", "initial"))],
                GeneratedFiles,
                "TestCase.Scenario",
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(Mapper, IncrementalStepRunReason.Modified))),
            ExecutableStep(
                "path restored",
                [stable, SourceFile("Models.cs", Models("Name", "mapped"))],
                GeneratedFiles,
                "TestCase.Scenario",
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(Mapper, IncrementalStepRunReason.Modified))));
    }

    [Test]
    public void Actualizes_a_nested_contract_below_an_included_scope()
    {
        var stable = SourceFile(
            "MapperAndScenario.cs",
            IncludedScopeStableSource);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ExecutableStep(
                "matching included path",
                [
                    stable,
                    SourceFile(
                        "Models.cs",
                        IncludedModels("Name", "mapped"))
                ],
                GeneratedFiles,
                "TestCase.Scenario"),
            ExecutableStep(
                "included path removed",
                [
                    stable,
                    SourceFile(
                        "Models.cs",
                        IncludedModels("Other", "initial"))
                ],
                GeneratedFiles,
                "TestCase.Scenario",
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(Mapper, IncrementalStepRunReason.Modified))),
            ExecutableStep(
                "included path restored",
                [
                    stable,
                    SourceFile(
                        "Models.cs",
                        IncludedModels("Name", "mapped"))
                ],
                GeneratedFiles,
                "TestCase.Scenario",
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(Mapper, IncrementalStepRunReason.Modified))));
    }

    [Test]
    public void Actualizes_when_output_nullability_changes()
    {
        var stable = SourceFile("MapperAndScenario.cs", StableSource);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "maybe-null read",
                [
                    stable,
                    SourceFile(
                        "Models.cs",
                        OutputNullabilityModels("MaybeNull"))
                ],
                GeneratedFiles),
            Step(
                "not-null read",
                [
                    stable,
                    SourceFile(
                        "Models.cs",
                        OutputNullabilityModels("NotNull"))
                ],
                GeneratedFiles,
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(Mapper, IncrementalStepRunReason.Modified))),
            Step(
                "maybe-null read restored",
                [
                    stable,
                    SourceFile(
                        "Models.cs",
                        OutputNullabilityModels("MaybeNull"))
                ],
                GeneratedFiles,
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(Mapper, IncrementalStepRunReason.Modified))));
    }

    private static string Models(string member, string expected) =>
        ModelsTemplate
            .Replace("__MEMBER__", member)
            .Replace("__EXPECTED__", expected);

    private static string IncludedModels(string member, string expected) =>
        IncludedModelsTemplate
            .Replace("__MEMBER__", member)
            .Replace("__EXPECTED__", expected);

    private static string OutputNullabilityModels(string attribute) =>
        OutputNullabilityModelsTemplate.Replace("__ATTRIBUTE__", attribute);

    // lang=c#
    private const string ModelsTemplate =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source
    {
        public Customer Customer { get; init; } = new Customer();
    }

    public sealed class Customer
    {
        public string __MEMBER__ { get; init; } = "mapped";
    }

    public sealed class Destination
    {
        public string CustomerName { get; set; } = "initial";
    }

    public static class Expected
    {
        public const string Value = "__EXPECTED__";
    }
}
""";

    // lang=c#
    private const string IncludedModelsTemplate =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source
    {
        public Profile Profile { get; init; } = new Profile();
    }

    public sealed class Profile
    {
        public Customer Customer { get; init; } = new Customer();
    }

    public sealed class Customer
    {
        public string __MEMBER__ { get; init; } = "mapped";
    }

    public sealed class Destination
    {
        public string CustomerName { get; set; } = "initial";
    }

    public static class Expected
    {
        public const string Value = "__EXPECTED__";
    }
}
""";

    // lang=c#
    private const string OutputNullabilityModelsTemplate =
"""
#nullable enable
#pragma warning disable CS1591

using System.Diagnostics.CodeAnalysis;

namespace TestCase
{
    public sealed class Source
    {
        [__ATTRIBUTE__]
        public Customer? Customer { get; init; } = new Customer();
    }

    public sealed class Customer
    {
        public string Name { get; init; } = "mapped";
    }

    public sealed class Destination
    {
        public string? CustomerName { get; set; } = "initial";
    }

    public static class Expected
    {
        public const string Value = "mapped";
    }
}
""";

    // lang=c#
    private const string StableSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
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
            var result = mapper.Create(new Source());

            if (result.CustomerName != Expected.Value)
            {
                throw new InvalidOperationException(
                    "Flattening was not actualized after a model edit.");
            }
        }
    }
}
""";

    // lang=c#
    private const string IncludedScopeStableSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .IncludeMembers(source => source.Profile);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(new Source());

            if (result.CustomerName != Expected.Value)
            {
                throw new InvalidOperationException(
                    "Included flattening was not actualized after a model " +
                    "edit.");
            }
        }
    }
}
""";

}
