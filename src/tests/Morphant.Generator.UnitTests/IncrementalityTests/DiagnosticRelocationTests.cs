using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class DiagnosticRelocationTests
{
    private static readonly string[] Generated =
    [
        "Morphant.Generated.Construction.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
        "Morphant.Generated.MappingExtension.TestMapper.SourceToDestination__2087052f5a3645b79062a3a095263f92.g.cs",
        "Morphant.Generated.Member.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
        "Morphant.Generated.MemberExtension.TestMapper.SourceToDestination__2087052f5a3645b79062a3a095263f92.g.cs",
        "Morphant.Generated.TypeMapper.TestMapper__c4f37cd504cad969975e05a81c79a459.g.cs"
    ];

    [TestCase(false)]
    [TestCase(true)]
    public void Relocates_flattening_diagnostics_while_mapper_execution_stays_cached(bool recreateTrees)
    {
        Run(Mapper, FlatteningDtos, "CustomerAddressCity", recreateTrees, dto =>
        [
            CompilerDiagnostic("MORPH0051", DiagnosticSeverity.Error, "Dtos.cs",
                Position(dto, "CustomerAddressCity"), "CustomerAddressCity".Length,
                At(dto, "Customer"), At(dto, "Address"), At(dto, "City"), At(dto, "AddressCity"))
        ]);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Relocates_completeness_links_while_mapper_execution_stays_cached(bool recreateTrees)
    {
        var mapper = Mapper.Replace("();", "().UnmappedMemberValidation(UnmappedMemberValidation.Strict);");
        Run(mapper, CompletenessDtos, "Unmapped", recreateTrees, dto =>
        [
            CompilerDiagnostic("MORPH0047", DiagnosticSeverity.Warning, "Mapper.cs",
                mapper.IndexOf("Source,", StringComparison.Ordinal), "Source".Length, At(dto, "Unused")),
            CompilerDiagnostic("MORPH0048", DiagnosticSeverity.Warning, "Mapper.cs",
                mapper.IndexOf("Destination>", StringComparison.Ordinal), "Destination".Length, At(dto, "Unmapped"))
        ]);
    }

    private static void Run(string mapper, string original, string target, bool recreateTrees,
        Func<string, ExpectedCompilerDiagnostic[]> diagnostics)
    {
        GeneratorIncrementalityStep Edit(string name, string dto, bool initial = false)
        {
            var reason = initial ? IncrementalStepRunReason.New : IncrementalStepRunReason.Cached;
            var stages = new[]
            {
                StageReasons("BuildTypeMapperModels", Reason(reason, 1)),
                StageReasons("BuildTypeMapperRequests", Reason(reason, 1))
            };
            var files = new[] { SourceFile("Mapper.cs", mapper), SourceFile("Dtos.cs", dto) };
            return recreateTrees
                ? StepWithRecreatedSyntaxTreesAndDiagnostics(name, files, Generated, diagnostics(dto), stages)
                : StepWithDiagnostics(name, files, Generated, diagnostics(dto), stages);
        }

        var longerBodies = original.Replace("=> 1;", "=> 123456;");
        var declaration = "    public " + (target == "Unmapped" ? "int " : "string ") + target;
        var documentation = longerBodies.Replace(declaration, "    /// <summary>More documentation.</summary>\n" + declaration);
        // Keep the same word at its old offset, but inside a new comment.
        var prefix = declaration.Substring(0, declaration.Length - target.Length);
        var misleadingComment = original.Replace(declaration,
            "    //" + new string(' ', prefix.Length - 6) + target + "\n" + declaration);
        var unrelatedDeclaration = documentation.Replace("public sealed class Destination",
            "internal sealed class Unrelated { }\npublic sealed class Destination");

        RunAndAssert(LanguageVersion.CSharp9, static () => new MorphantGenerator(),
            Edit("initial", original, initial: true),
            Edit("longer bodies before diagnostic locations", longerBodies),
            Edit("documentation inserted", documentation),
            Edit("same word at the obsolete offset", misleadingComment),
            Edit("unrelated declaration inserted", unrelatedDeclaration),
            Edit("line endings changed", unrelatedDeclaration.Replace("\n", "\r\n")),
            Edit("original positions restored", original));
    }

    private static int Position(string dto, string name) => dto.IndexOf(" " + name + " { get;", StringComparison.Ordinal) + 1;

    private static ExpectedCompilerDiagnosticLocation At(string dto, string name) =>
        CompilerDiagnosticLocation("Dtos.cs", Position(dto, name), name.Length);

    // lang=c#
    private const string Mapper = """
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string FlatteningDtos = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
public sealed class Source
{
    public int Method() => 1;
    public Customer Customer { get; set; } = new();
}
public sealed class Customer
{
    public Address Address { get; set; } = new();
    public string AddressCity { get; set; } = "";
}
public sealed class Address
{
    public string City { get; set; } = "";
}
public sealed class Destination
{
    public int Method() => 1;
    public string CustomerAddressCity { get; set; } = "";
}
}
""";

    // lang=c#
    private const string CompletenessDtos = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
public sealed class Source
{
    public int Method() => 1;
    public int Unused { get; set; }
}
public sealed class Destination
{
    public int Method() => 1;
    public int Unmapped { get; set; }
}
}
""";
}
