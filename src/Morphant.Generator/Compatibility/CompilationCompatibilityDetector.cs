using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.Compatibility;

internal static class CompilationCompatibilityDetector
{
    private const string ContractVersionMetadataName =
        "Morphant.GeneratorContractVersion";
    private const string AssemblyMetadataAttributeMetadataName =
        "System.Reflection.AssemblyMetadataAttribute";
    private const int SupportedContractRevision = 1;

    public static CompilationCompatibility Detect(
        CSharpCompilation compilation,
        LanguageVersion languageVersion)
    {
        var isLanguageCompatible =
            languageVersion >= LanguageVersion.CSharp9;
        var runtimeContract = DetectRuntimeContract(
            compilation,
            out var compatibleAssembly);
        var knownSymbols =
            isLanguageCompatible &&
            runtimeContract.Kind == RuntimeContractCompatibilityKind.Compatible &&
            compatibleAssembly is not null
                ? KnownSymbols.TryCreate(compilation)
                : null;

        if (isLanguageCompatible &&
            runtimeContract.Kind == RuntimeContractCompatibilityKind.Compatible &&
            knownSymbols is null)
        {
            runtimeContract = Incompatible(
                "the runtime API does not match this generator");
        }

        return new CompilationCompatibility(
            isLanguageCompatible,
            runtimeContract,
            knownSymbols);
    }

    private static RuntimeContractCompatibility DetectRuntimeContract(
        CSharpCompilation compilation,
        out IAssemblySymbol? compatibleAssembly)
    {
        compatibleAssembly = null;
        var candidates = EnumerateAssemblies(compilation)
            .Where(IsRuntimeContractCandidate)
            .ToImmutableArray();

        if (candidates.IsEmpty)
        {
            return new RuntimeContractCompatibility(
                RuntimeContractCompatibilityKind.Missing);
        }

        if (candidates.Length != 1 ||
            RuntimeContractManifest.HasAmbiguousSymbol(candidates[0]))
        {
            return new RuntimeContractCompatibility(
                RuntimeContractCompatibilityKind.Ambiguous);
        }

        var candidate = candidates[0];
        var revisions = GetContractRevisionValues(candidate);

        if (revisions.IsEmpty)
        {
            return Incompatible(
                "the runtime does not provide compatibility information");
        }

        if (revisions.Length != 1)
        {
            return Incompatible(
                "the runtime contains duplicate compatibility information");
        }

        var revisionValue = revisions[0];

        if (!TryParseCanonicalRevision(revisionValue, out var revision))
        {
            return Incompatible(
                "the runtime contains invalid compatibility information");
        }

        if (revision != SupportedContractRevision)
        {
            return Incompatible(
                "the runtime and generator versions do not match");
        }

        var shapeFailure = RuntimeContractManifest.FindFirstFailure(candidate);

        if (shapeFailure is not null)
        {
            return Incompatible(
                "the runtime API does not match this generator");
        }

        compatibleAssembly = candidate;
        return new RuntimeContractCompatibility(
            RuntimeContractCompatibilityKind.Compatible);
    }

    private static IEnumerable<IAssemblySymbol> EnumerateAssemblies(
        CSharpCompilation compilation)
    {
        yield return compilation.Assembly;

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            yield return assembly;
        }
    }

    private static bool IsRuntimeContractCandidate(IAssemblySymbol assembly)
    {
        return !GetContractRevisionValues(assembly).IsEmpty ||
               RuntimeContractManifest.DeclaresAnySymbol(assembly);
    }

    private static ImmutableArray<string?> GetContractRevisionValues(
        IAssemblySymbol assembly)
    {
        var values = ImmutableArray.CreateBuilder<string?>();

        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass ||
                RuntimeContractManifest.GetFullMetadataName(attributeClass) !=
                    AssemblyMetadataAttributeMetadataName ||
                attribute.ConstructorArguments.Length < 1 ||
                attribute.ConstructorArguments[0].Value is not string key ||
                key != ContractVersionMetadataName)
            {
                continue;
            }

            values.Add(
                attribute.ConstructorArguments.Length == 2 &&
                attribute.ConstructorArguments[1].Kind == TypedConstantKind.Primitive
                    ? attribute.ConstructorArguments[1].Value as string
                    : null);
        }

        return values.ToImmutable();
    }

    private static bool TryParseCanonicalRevision(
        string? value,
        out int revision)
    {
        return int.TryParse(
                   value,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out revision) &&
               revision.ToString(CultureInfo.InvariantCulture) == value;
    }

    private static RuntimeContractCompatibility Incompatible(string reason)
    {
        return new RuntimeContractCompatibility(
            RuntimeContractCompatibilityKind.Incompatible,
            reason);
    }
}
