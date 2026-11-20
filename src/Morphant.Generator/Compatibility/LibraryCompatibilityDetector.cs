using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.Compatibility;

internal static class LibraryCompatibilityDetector
{
    // A library records the runtime line it was compiled against in its
    // assembly reference; the runtime assembly version is major.minor.0.0.
    public static ImmutableArray<Diagnostic> Detect(
        Compilation compilation,
        IAssemblySymbol runtime,
        CancellationToken cancellationToken)
    {
        var runtimeIdentity = runtime.Identity;
        var incompatible = new List<(string Library, Version Version)>();

        foreach (var library in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (SymbolEqualityComparer.Default.Equals(library, runtime))
            {
                continue;
            }

            var reference = library.Modules
                .SelectMany(static module => module.ReferencedAssemblies)
                .FirstOrDefault(identity =>
                    IsIncompatibleRuntime(identity, runtimeIdentity));

            if (reference is not null &&
                (compilation.GetMetadataReference(library) is not
                     CompilationReference project ||
                 UsesIncompatibleRuntime(
                     project.Compilation,
                     runtimeIdentity,
                     cancellationToken)))
            {
                incompatible.Add((library.Identity.Name, reference.Version));
            }
        }

        return incompatible
            .OrderBy(static library => library.Library, StringComparer.Ordinal)
            .Select(library => Diagnostic.Create(
                CompatibilityDiagnosticDescriptors.IncompatibleLibrary,
                Location.None,
                library.Library,
                FormatLine(library.Version),
                FormatLine(runtimeIdentity.Version)))
            .ToImmutableArray();
    }

    // A project reference lists every reference passed to its compilation.
    // Like a compiled library, count only the references its code uses.
    private static bool UsesIncompatibleRuntime(
        Compilation project,
        AssemblyIdentity runtime,
        CancellationToken cancellationToken)
    {
        return project.GetUsedAssemblyReferences(cancellationToken)
            .Any(reference =>
                project.GetAssemblyOrModuleSymbol(reference) is
                    IAssemblySymbol assembly &&
                IsIncompatibleRuntime(assembly.Identity, runtime));
    }

    private static bool IsIncompatibleRuntime(
        AssemblyIdentity reference,
        AssemblyIdentity runtime)
    {
        return string.Equals(
                   reference.Name,
                   runtime.Name,
                   StringComparison.OrdinalIgnoreCase) &&
               reference.PublicKeyToken.SequenceEqual(runtime.PublicKeyToken) &&
               !IsCompatible(reference.Version, runtime.Version);
    }

    // Before 1.0 the minor line must match. From 1.0 the major version must
    // match and the library must not require a newer minor version.
    private static bool IsCompatible(Version library, Version runtime)
    {
        return library.Major == runtime.Major &&
               (runtime.Major == 0
                   ? library.Minor == runtime.Minor
                   : library.Minor <= runtime.Minor);
    }

    private static string FormatLine(Version version)
    {
        return version.Major.ToString(CultureInfo.InvariantCulture) + "." +
               version.Minor.ToString(CultureInfo.InvariantCulture);
    }
}
