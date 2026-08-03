using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class MappingInterfaceNullabilityTests
{
    [Test]
    public void Consumer_observes_nullable_inputs_and_non_nullable_results()
    {
        var compilation = CreateCompilation();
        var mapperMethods = compilation
            .GetTypeByMetadataName("Morphant.IMapper")!
            .GetMembers(nameof(IMapper.Map))
            .OfType<IMethodSymbol>()
            .OrderBy(static method => method.Parameters.Length)
            .ToArray();
        var typeMapperMethods = compilation
            .GetTypeByMetadataName("Morphant.ITypeMapper`2")!
            .GetMembers(nameof(ITypeMapper<object, object>.Map))
            .OfType<IMethodSymbol>()
            .OrderBy(static method => method.Parameters.Length)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(mapperMethods, Has.Length.EqualTo(2));
            AssertMethod(mapperMethods[0], expectedParameterCount: 1);
            AssertMethod(mapperMethods[1], expectedParameterCount: 2);
            Assert.That(typeMapperMethods, Has.Length.EqualTo(2));
            AssertMethod(
                typeMapperMethods[0],
                expectedParameterCount: 2,
                contextParameterIndex: 1);
            AssertMethod(
                typeMapperMethods[1],
                expectedParameterCount: 3,
                contextParameterIndex: 2);
        });
    }

    private static void AssertMethod(
        IMethodSymbol method,
        int expectedParameterCount,
        int? contextParameterIndex = null)
    {
        Assert.Multiple(() =>
        {
            Assert.That(method.Name, Is.EqualTo(nameof(IMapper.Map)));
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
