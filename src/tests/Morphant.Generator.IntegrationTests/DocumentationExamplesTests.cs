using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class DocumentationExamplesTests
{
    [Test]
    public async Task Markdown_examples_compile_as_CSharp9_consumers_and_execute_the_documented_behavior()
    {
        var root = Path.Combine(Path.GetTempPath(), nameof(DocumentationExamplesTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var project = XDocument.Parse(ConsumerProject);
            project.Root!.Add(new XElement("ItemGroup",
                new XElement("Reference", new XAttribute("Include", "Morphant"),
                    new XElement("HintPath", typeof(TypeMapper<>).Assembly.Location)),
                new XElement("Analyzer", new XAttribute("Include", Path.Combine(
                    IntegrationTestEnvironment.RepositoryRoot, "src", "Morphant.Generator", "bin",
                    IntegrationTestEnvironment.BuildConfiguration, "netstandard2.0", "Morphant.Generator.dll")))));
            project.Save(Path.Combine(root, "Examples.csproj"));

            var calls = new List<string>();
            foreach (var (example, index) in Examples().Select((example, index) => (example, index)))
            {
                var snippet = ReadExample(example.Page, example.Heading, example.Block);
                var source = example.Consumer.Replace("__EXAMPLE__", snippet.TrimEnd().TrimEnd(';'),
                    StringComparison.Ordinal);
                var imports = Regex.IsMatch(source, @"^using Morphant;", RegexOptions.Multiline)
                    ? "using System;\nusing Morphant.Context;\n"
                    : "using System;\nusing Morphant.Context;\nusing Morphant;\n";
                File.WriteAllText(Path.Combine(root, $"Example{index}.cs"),
                    $"namespace Example{index}\n{{\n" + imports + source + "\n}\n");
                calls.Add($"Example{index}.Scenario.Verify();");
            }

            // All examples share one ordinary MSBuild compilation; no Roslyn execution in the test host.
            File.WriteAllText(Path.Combine(root, "Program.cs"), string.Join(Environment.NewLine, calls));
            var result = await DotNetCli.Run(root,
            [
                "run", "--project", Path.Combine(root, "Examples.csproj"),
                "--configuration", IntegrationTestEnvironment.BuildConfiguration,
                "-m:1", "-nodeReuse:false", "-p:UseSharedCompilation=false",
                $"-p:RestoreSources={root}", "-p:NuGetAudit=false"
            ]);
            Assert.That(result.ExitCode, Is.Zero, result.Command + Environment.NewLine + result.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string ReadExample(string page, string heading, int block)
    {
        var markdown = File.ReadAllText(Path.Combine(IntegrationTestEnvironment.RepositoryRoot, page))
            .ReplaceLineEndings("\n");
        var section = Regex.Match(markdown, "^" + Regex.Escape(heading) + @"\n(?<body>.*?)(?=^#{1,6} |\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        Assert.That(section.Success, Is.True, $"Missing section: {page}: {heading}");
        var examples = Regex.Matches(section.Groups["body"].Value, @"^```csharp\n(.*?)^```",
            RegexOptions.Multiline | RegexOptions.Singleline);
        Assert.That(examples.Count, Is.GreaterThan(block), $"Missing C# example {block}: {page}: {heading}");
        return examples[block].Groups[1].Value;
    }

    private static IEnumerable<Example> Examples()
    {
        // These two public entry pages intentionally own independent mapper declarations.
        foreach (var (page, heading) in new[]
        {
            ("README.md", "## Define a mapper"),
            ("docs/quick-start.md", "## Declare a mapping")
        })
        {
            yield return new(page, heading, 0,
"""
__EXAMPLE__
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<Customer, CustomerDto> mapper = new ApplicationMapper();
        var source = new Customer { Name = "Ada" };
        var created = mapper.Create(source);
        var existing = new CustomerDto { Name = "Old" };
        var updated = mapper.Update(source, existing);
        if (created.Name != "Ada" || updated.Name != "Ada" || !ReferenceEquals(existing, updated))
            throw new Exception("The introductory mapper must create and update the named member.");
    }
}
""");
        }

        foreach (var (id, block, mapper) in new[]
        {
            ("0005", 0, "ApplicationMapper"),
            ("0006", 0, "ApplicationMapper"),
            ("0007", 0, "Container.ApplicationMapper"),
            ("0058", 0, "OrderMapper"),
            ("0058", 1, "OrderMapper")
        })
        {
            yield return new($"docs/diagnostics/MORPH{id}.md", "## Fix", block,
                """
public sealed class Source { public int Value { get; set; } }
public sealed class Destination { public int Value { get; set; } }
__EXAMPLE__
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<Source, Destination> mapper = new __MAPPER__();
        if (mapper.Create(new Source { Value = 42 }).Value != 42)
            throw new Exception("A corrected mapper declaration must implement its configured mapping.");
    }
}
""".Replace("__MAPPER__", mapper, StringComparison.Ordinal));
        }

        yield return new("docs/api/map.md", "## Mapper declarations", 0,
"""
public sealed class OrderDto { public int Id { get; set; } }
public sealed class Order { public int Id { get; set; } }
__EXAMPLE__
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<OrderDto, Order> mapper = new OrderMapper();
        var existing = new Order();
        var source = new OrderDto { Id = 17 };
        if (mapper.Create(source).Id != 17 || !ReferenceEquals(existing, mapper.Update(source, existing)) ||
            existing.Id != 17)
            throw new Exception("The Map declaration example must generate both operations.");
    }
}
""");

        foreach (var (page, heading, block) in new[]
        {
            ("docs/diagnostics/MORPH0047.md", "## Fix", 0),
            ("docs/settings/unmapped-member-validation.md", "# Unmapped member validation", 1)
        })
        {
            yield return new(page, heading, block,
"""
public sealed class Source
{
    public string Name { get; set; } = "Ada";
    public int LegacyValue => throw new Exception("A compile-time discard must not invoke this getter.");
}
public sealed class Destination { public string Name { get; set; } = ""; }
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
            __EXAMPLE__;
}
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<Source, Destination> mapper = new TestMapper();
        var source = new Source();
        if (mapper.Create(source).Name != "Ada" || mapper.Update(source, new Destination()).Name != "Ada")
            throw new Exception("Acknowledging an unused source member must preserve the mapping.");
    }
}
""");
        }

        yield return new("docs/api/members.md", "## Reading `result`", 0,
"""
public sealed class Source { }
public sealed class Destination
{
    public Destination(int value) => Value = value;
    public int Value { get; set; }
}
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) => __EXAMPLE__;
}
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<Source, Destination> mapper = new TestMapper();
        var source = new Source();
        var existing = new Destination(20);
        if (mapper.Create(source).Value != 7 || mapper.Update(source, existing).Value != 30)
            throw new Exception("The operation guard must use a constant for construction and result for reuse.");
        try { mapper.Update(source, null); }
        catch (Morphant.Exceptions.NullDestinationException) { return; }
        throw new Exception("The documented result guard depends on rejecting null Update destinations.");
    }
}
""");

        yield return new("docs/api/resolve.md", "## Overloads", 0,
"""
public sealed class OrderDto { public int Id { get; set; } }
public sealed class Order
{
    public Order(int id) => Id = id;
    public int Id { get; }
}
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) => __EXAMPLE__;
}
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<OrderDto, Order> mapper = new TestMapper();
        var source = new OrderDto { Id = 17 };
        var created = mapper.Create(source);
        if (created.Id != 17 || !ReferenceEquals(created, mapper.Update(source, created)))
            throw new Exception("Resolve must reuse a destination with the same ID.");
        source.Id = 23;
        var replacement = mapper.Update(source, created);
        if (replacement.Id != 23 || ReferenceEquals(created, replacement) || mapper.Update(source, null).Id != 23)
            throw new Exception("Resolve must construct for an absent or mismatched destination.");
    }
}
""");

        yield return new("docs/api/convert.md", "## Overloads", 0,
"""
public sealed class OrderDto { public int Id { get; set; } public string Name { get; set; } = ""; }
public sealed class Order
{
    public Order(int id) => Id = id;
    public int Id { get; }
    public string Name { get; private set; } = "";
    public void UpdateFrom(OrderDto source) => Name = source.Name;
}
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) => __EXAMPLE__;
}
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<OrderDto, Order> mapper = new TestMapper();
        var source = new OrderDto { Id = 17, Name = "New" };
        var existing = new Order(3);
        var updated = mapper.Update(source, existing);
        if (mapper.Create(source).Name != "New" || !ReferenceEquals(existing, updated) || updated.Id != 3 ||
            updated.Name != "New" || mapper.Update(source, null).Id != 17 || mapper.Create(null) is not null ||
            mapper.Update(null, existing) is not null)
            throw new Exception("Convert must own null handling, construction, and reuse.");
    }
}
""");

        yield return new("docs/api/construct-using.md", "## Overloads", 0,
"""
public sealed class OrderDto { public int Id { get; set; } }
public interface IOrder { int Id { get; } }
public sealed class Order : IOrder { public Order(int id) => Id = id; public int Id { get; } }
public sealed class OrderFactory { public IOrder Create(int id) => id < 0 ? null! : new Order(id); }
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    private readonly OrderFactory orderFactory = new();
    protected override void Configure(MapperBuilder builder) => __EXAMPLE__;
}
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<OrderDto, IOrder> mapper = new TestMapper();
        var source = new OrderDto { Id = 17 };
        var existing = new Order(3);
        if (mapper.Create(source).Id != 17 || !ReferenceEquals(existing, mapper.Update(source, existing)) ||
            mapper.Update(source, null).Id != 17 || mapper.Create(new OrderDto { Id = -1 }) is not null)
            throw new Exception("ConstructUsing must create only when absent and preserve a null factory result.");
    }
}
""");

        yield return new("docs/api/resolve-using.md", "## Overloads", 0,
"""
public sealed class OrderDto { public int Id { get; set; } }
public interface IOrder { int Id { get; } }
public sealed class Order : IOrder { public Order(int id) => Id = id; public int Id { get; } }
public sealed class OrderFactory
{
    public static int Calls { get; private set; }
    public IOrder Create(int id)
    {
        Calls++;
        return id < 0 ? null! : new Order(id);
    }
}
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    private readonly OrderFactory orderFactory = new();
    protected override void Configure(MapperBuilder builder) => __EXAMPLE__;
}
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<OrderDto, IOrder> mapper = new TestMapper();
        var source = new OrderDto { Id = 17 };
        var created = mapper.Create(source);
        if (created.Id != 17 || !ReferenceEquals(created, mapper.Update(source, created)) || OrderFactory.Calls != 1)
            throw new Exception("ResolveUsing must create when absent and reuse a matching destination.");
        var existing = new Order(3);
        var replacement = mapper.Update(source, existing);
        if (replacement.Id != 17 || ReferenceEquals(existing, replacement) || existing.Id != 3 ||
            mapper.Update(source, null).Id != 17 || OrderFactory.Calls != 3)
            throw new Exception("A mismatched or null destination must select a new factory result.");
        var missing = new OrderDto { Id = -1 };
        if (mapper.Create(missing) is not null || mapper.Update(missing, created) is not null ||
            OrderFactory.Calls != 5)
            throw new Exception("A null factory result must remain final on Create and Update.");
        if (mapper.Create(null) is not null || mapper.Update(null, created) is not null || OrderFactory.Calls != 5)
            throw new Exception("Null source handling must finish before ResolveUsing is invoked.");
    }
}
""");

        yield return new("docs/recipes.md", "## Fill one constructor parameter explicitly", 0,
"""
public sealed class Tenant { public int ExternalId { get; set; } }
public sealed class OrderDto { public int Id { get; set; } public Tenant Tenant { get; set; } = new(); }
public sealed class Order
{
    public Order(int id, int tenantId) { Id = id; TenantId = tenantId; }
    public int Id { get; }
    public int TenantId { get; }
}
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) => __EXAMPLE__;
}
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<OrderDto, Order> mapper = new TestMapper();
        var created = mapper.Create(new OrderDto { Id = 17, Tenant = new Tenant { ExternalId = 23 } });
        if (created.Id != 17 || created.TenantId != 23)
            throw new Exception("ByConvention must combine inferred and explicit constructor arguments.");
    }
}
""");

        yield return new("docs/tuple-mapping.md", "## Named and unnamed elements", 0,
"""
public sealed class Source { public int Id { get; set; } public string DisplayName { get; set; } = ""; }
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) => __EXAMPLE__;
}
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<Source, (int, string)> mapper = new TestMapper();
        var source = new Source { Id = 17, DisplayName = "Ada" };
        if (mapper.Create(source) != (17, "Ada") || mapper.Update(source, (3, "Old")) != (17, "Ada"))
            throw new Exception("Explicit ItemN rules must map unnamed tuple elements on Create and Update.");
    }
}
""");

        yield return new("docs/configuration-inheritance.md", "## Include mapping rules", 0,
"""
public class Animal { public string Name { get; set; } = ""; }
public class AnimalDto { public string Name { get; set; } = ""; }
public sealed class Dog : Animal { public string Breed { get; set; } = ""; }
public sealed class DogDto : AnimalDto { public string Breed { get; set; } = ""; }
__EXAMPLE__
public static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<Dog, DogDto> mapper = new DogMapper();
        var source = new Dog { Name = "Rex", Breed = "Terrier" };
        var created = mapper.Create(source);
        var existing = new DogDto();
        var updated = mapper.Update(source, existing);
        if (created.Name != "Rex" || created.Breed != "Terrier" || updated.Name != "Rex" ||
            updated.Breed != "Terrier" || !ReferenceEquals(existing, updated))
            throw new Exception("IncludeBase must combine inherited and local member rules.");
    }
}
""");
    }

    private sealed record Example(string Page, string Heading, int Block, string Consumer);

    private const string ConsumerProject =
"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
""";
}
