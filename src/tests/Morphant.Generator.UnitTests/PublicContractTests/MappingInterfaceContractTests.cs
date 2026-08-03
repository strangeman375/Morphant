using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.PublicContractTests;

[TestFixture]
internal sealed class MappingInterfaceContractTests
{
    [Test]
    public void IMapper_declares_nullable_inputs_and_non_nullable_results()
    {
        var interfaceType = CreateCompilation()
            .GetTypeByMetadataName("Morphant.IMapper")!;
        var methods = interfaceType
            .GetMembers(nameof(IMapper.Map))
            .OfType<IMethodSymbol>()
            .OrderBy(static method => method.Parameters.Length)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(methods, Has.Length.EqualTo(2));
            AssertMethod(
                methods[0],
                expectedParameterCount: 1);
            AssertMethod(
                methods[1],
                expectedParameterCount: 2);
        });
    }

    [Test]
    public void ITypeMapper_declares_nullable_inputs_and_non_nullable_results()
    {
        var interfaceType = CreateCompilation()
            .GetTypeByMetadataName("Morphant.ITypeMapper`2")!;
        var genericParameters = interfaceType.TypeParameters;
        var methods = interfaceType
            .GetMembers(nameof(ITypeMapper<object, object>.Map))
            .OfType<IMethodSymbol>()
            .OrderBy(static method => method.Parameters.Length)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                genericParameters[0].Variance,
                Is.EqualTo(VarianceKind.In));
            Assert.That(
                genericParameters[1].Variance,
                Is.EqualTo(VarianceKind.None));
            Assert.That(methods, Has.Length.EqualTo(2));

            AssertMethod(
                methods[0],
                expectedParameterCount: 2,
                contextParameterIndex: 1);
            AssertMethod(
                methods[1],
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
                    Is.EqualTo("Morphant.MappingContext"));
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
            "PublicContractProbe",
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }
}
