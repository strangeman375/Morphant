using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class MappingInterfaceNullabilityTests
{
    [Test]
    public void Consumer_observes_operation_names_nullable_inputs_and_non_nullable_results()
    {
        var compilation = CreateCompilation();
        var mapperMethods = compilation
            .GetTypeByMetadataName("Morphant.IMapper")!
            .GetMembers(nameof(IMapper.Map))
            .OfType<IMethodSymbol>()
            .OrderBy(static method => method.Parameters.Length)
            .ToArray();
        var typeMapper = compilation
            .GetTypeByMetadataName("Morphant.ITypeMapper`2")!;
        var option = compilation
            .GetTypeByMetadataName("Morphant.Option`1")!;
        var mappingContext = compilation
            .GetTypeByMetadataName("Morphant.Context.MappingContext")!;
        var createMethod = typeMapper
            .GetMembers(nameof(ITypeMapper<object, object>.Create))
            .OfType<IMethodSymbol>()
            .Single();
        var updateMethod = typeMapper
            .GetMembers(nameof(ITypeMapper<object, object>.Update))
            .OfType<IMethodSymbol>()
            .Single();
        var extensions = compilation
            .GetTypeByMetadataName("Morphant.TypeMapperExtensions")!
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method => method.IsExtensionMethod)
            .OrderBy(static method => method.Parameters.Length)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(mapperMethods, Has.Length.EqualTo(2));
            Assert.That(option.IsReadOnly, Is.True);
            Assert.That(mappingContext.IsReadOnly, Is.True);
            AssertMethod(
                mapperMethods[0],
                nameof(IMapper.Map),
                expectedParameterCount: 1);
            AssertMethod(
                mapperMethods[1],
                nameof(IMapper.Map),
                expectedParameterCount: 2);
            AssertMethod(
                createMethod,
                nameof(ITypeMapper<object, object>.Create),
                expectedParameterCount: 2,
                contextParameterIndex: 1);
            AssertMethod(
                updateMethod,
                nameof(ITypeMapper<object, object>.Update),
                expectedParameterCount: 3,
                contextParameterIndex: 2);
            Assert.That(
                typeMapper.GetMembers(nameof(IMapper.Map)),
                Is.Empty);
            Assert.That(extensions, Has.Length.EqualTo(2));
            AssertExtensionMethod(
                extensions[0],
                nameof(TypeMapperExtensions.Create),
                expectedParameterCount: 2);
            AssertExtensionMethod(
                extensions[1],
                nameof(TypeMapperExtensions.Update),
                expectedParameterCount: 3);
        });
    }

    [Test]
    public void Option_flow_annotation_refines_only_the_success_branch()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

internal static class Consumer
{
    public static int ReadPresent(Option<string> option)
    {
        if (option.TryGetValue(out var value))
        {
            return value.Length;
        }

        return 0;
    }

    public static int ReadWithoutChecking(Option<string> option)
    {
        option.TryGetValue(out var value);
        return value.Length;
    }
}
""";

        var diagnostics = CreateCompilation(source)
            .GetDiagnostics()
            .Where(static diagnostic =>
                diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Has.Length.EqualTo(1));
            Assert.That(diagnostics[0].Id, Is.EqualTo("CS8602"));
            Assert.That(
                diagnostics[0].GetMessage(),
                Is.EqualTo("Dereference of a possibly null reference."));
        });
    }

    private static void AssertExtensionMethod(
        IMethodSymbol method,
        string expectedName,
        int expectedParameterCount)
    {
        Assert.Multiple(() =>
        {
            Assert.That(method.Name, Is.EqualTo(expectedName));
            Assert.That(method.ReturnNullableAnnotation,
                Is.EqualTo(NullableAnnotation.NotAnnotated));
            Assert.That(method.Parameters,
                Has.Length.EqualTo(expectedParameterCount));
            Assert.That(method.Parameters[0].Name, Is.EqualTo("mapper"));
            Assert.That(method.Parameters[0].NullableAnnotation,
                Is.EqualTo(NullableAnnotation.NotAnnotated));
            Assert.That(method.Parameters[1].Name, Is.EqualTo("source"));
            Assert.That(method.Parameters[1].NullableAnnotation,
                Is.EqualTo(NullableAnnotation.Annotated));

            if (expectedParameterCount == 3)
            {
                Assert.That(
                    method.Parameters[2].Name,
                    Is.EqualTo("destination"));
                Assert.That(
                    method.Parameters[2].NullableAnnotation,
                    Is.EqualTo(NullableAnnotation.Annotated));
            }
        });
    }

    private static void AssertMethod(
        IMethodSymbol method,
        string expectedName,
        int expectedParameterCount,
        int? contextParameterIndex = null)
    {
        Assert.Multiple(() =>
        {
            Assert.That(method.Name, Is.EqualTo(expectedName));
            Assert.That(
                method.ReturnNullableAnnotation,
                Is.EqualTo(NullableAnnotation.NotAnnotated));
            Assert.That(
                method.Parameters,
                Has.Length.EqualTo(expectedParameterCount));

            var parameters = method.Parameters;
            Assert.That(
                parameters[0].NullableAnnotation,
                Is.EqualTo(NullableAnnotation.Annotated));

            if (expectedParameterCount == 2 &&
                contextParameterIndex is null)
            {
                Assert.That(
                    parameters[1].NullableAnnotation,
                    Is.EqualTo(NullableAnnotation.Annotated));
            }

            if (contextParameterIndex is { } index)
            {
                Assert.That(
                    parameters[index].Type.ToDisplayString(),
                    Is.EqualTo("Morphant.Context.MappingContext"));
                Assert.That(
                    parameters[index].NullableAnnotation,
                    Is.EqualTo(NullableAnnotation.NotAnnotated));
            }

            if (expectedParameterCount == 3)
            {
                Assert.That(
                    parameters[1].NullableAnnotation,
                    Is.EqualTo(NullableAnnotation.Annotated));
            }
        });
    }

    private static CSharpCompilation CreateCompilation(string? source = null)
    {
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");
        var references = trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Append(
                MetadataReference.CreateFromFile(
                    typeof(IMapper).Assembly.Location));

        var syntaxTrees = source is null
            ? null
            : new[]
            {
                CSharpSyntaxTree.ParseText(
                    source,
                    new CSharpParseOptions(LanguageVersion.CSharp9),
                    path: "Consumer.cs")
            };

        return CSharpCompilation.Create(
            "MappingInterfaceNullabilityProbe",
            syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }
}
