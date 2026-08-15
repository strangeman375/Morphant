using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class GeneratedCodeExecution
{
    public static void AssertScenario(
        string stepName,
        Compilation compilation,
        string scenarioTypeName)
    {
        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        AssertNoWarningsOrErrors(
            $"Step '{stepName}' emit",
            emitResult.Diagnostics);

        var assembly = Assembly.Load(stream.ToArray());
        var verify = assembly
            .GetType(scenarioTypeName, throwOnError: true)!
            .GetMethod(
                "Verify",
                BindingFlags.Public | BindingFlags.Static) ??
            throw new InvalidOperationException(
                $"{scenarioTypeName}.Verify was not found.");

        try
        {
            verify.Invoke(null, null);
        }
        catch (TargetInvocationException exception)
        {
            throw new AssertionException(
                exception.InnerException?.ToString() ??
                exception.ToString());
        }
    }

    private static void AssertNoWarningsOrErrors(
        string scope,
        IEnumerable<Diagnostic> diagnostics)
    {
        var failures = diagnostics
            .Where(static diagnostic =>
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or
                    DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            failures,
            Is.Empty,
            scope + " must compile without warnings or errors." +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }
}
