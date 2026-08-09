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

    private static CSharpCompilation CreateCompilation()
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

        return CSharpCompilation.Create(
            "MappingInterfaceNullabilityProbe",
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }
}
