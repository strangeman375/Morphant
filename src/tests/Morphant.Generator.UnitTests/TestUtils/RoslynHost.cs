using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class RoslynHost
{
    public static Version Version { get; } =
        typeof(CSharpCompilation).Assembly.GetName().Version!;

    // Roslyn 5 removed CS1998 rather than suppressing it by default.
    public static bool ReportsAsyncWithoutAwaitWarning => Version.Major < 5;
}
