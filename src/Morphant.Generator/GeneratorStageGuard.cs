using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.Diagnostics;

namespace Morphant.Generator;

internal static class GeneratorStageGuard
{
    private static readonly string GeneratorVersion =
        typeof(MorphantGenerator).Assembly.GetName().Version is { } version
            ? version.ToString(3)
            : "unknown";

    public static GeneratorStageResult<TResult> Execute<TSource, TResult>(
        TSource source,
        string stageName,
        Func<TSource, CancellationToken, TResult> selector,
        Func<TSource, Location?> locationSelector,
        CancellationToken cancellationToken)
    {
        try
        {
            return GeneratorStageResult<TResult>.Success(
                selector(source, cancellationToken));
        }
        catch (Exception exception) when (CanReport(
                   exception,
                   cancellationToken))
        {
            return GeneratorStageResult<TResult>.Failed(
                CreateFailure(
                    source,
                    stageName,
                    exception,
                    locationSelector,
                    cancellationToken));
        }
    }

    public static IncrementalValuesProvider<TResult> Select<TSource, TResult>(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<TSource> source,
        string stageName,
        Func<TSource, CancellationToken, TResult> selector,
        Func<TSource, Location?> locationSelector)
    {
        var results = source.Select(
            (value, cancellationToken) => Execute(
                value,
                stageName,
                selector,
                locationSelector,
                cancellationToken));

        return Unwrap(context, results);
    }

    public static IncrementalValuesProvider<TResult>
        SelectTrackedSourceRequest<TSource, TResult>(
            IncrementalGeneratorInitializationContext context,
            IncrementalValuesProvider<TSource> source,
            string stageName,
            Func<TSource, CancellationToken, TResult> selector,
            Func<TSource, Location?> locationSelector)
        where TResult : IGeneratedSourceRequest
    {
        var results = source
            .Select((value, cancellationToken) =>
                new TrackedSourceRequestStageResult<TResult>(
                    Execute(
                        value,
                        stageName,
                        selector,
                        locationSelector,
                        cancellationToken)))
            .WithTrackingName(stageName);

        return Unwrap(
            context,
            results.Select(static (result, _) => result.Result));
    }

    public static IncrementalValueProvider<TResult> Select<TSource, TResult>(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<TSource> source,
        string stageName,
        Func<TSource, CancellationToken, TResult> selector,
        TResult fallback)
    {
        var results = source.Select(
            (value, cancellationToken) => Execute(
                value,
                stageName,
                selector,
                static _ => Location.None,
                cancellationToken));

        RegisterValueFailures(context, results);

        return results.Select((result, _) =>
            result.IsSuccess
                ? result.Value
                : fallback);
    }

    public static IncrementalValuesProvider<TResult> Unwrap<TResult>(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<GeneratorStageResult<TResult>> results)
    {
        var failures = results
            .Where(static result => !result.IsSuccess)
            .Select(static (result, _) => result.Failure)
            .Collect();

        context.RegisterSourceOutput(
            failures.Combine(context.CompilationProvider),
            static (productionContext, source) =>
            {
                var reportedFailures = new HashSet<string>(
                    StringComparer.Ordinal);

                foreach (var failure in source.Left)
                {
                    productionContext.CancellationToken
                        .ThrowIfCancellationRequested();

                    if (!reportedFailures.Add(failure.ReportHintName))
                    {
                        continue;
                    }

                    var diagnostic = failure.CreateDiagnostic();
                    var actualized =
                        DiagnosticLocationActualizer.Actualize(
                            ImmutableArray.Create(diagnostic),
                            source.Right,
                            productionContext.CancellationToken);

                    productionContext.ReportDiagnostic(actualized[0]);
                    failure.AddReportSource(productionContext);
                }
            });

        return results
            .Where(static result => result.IsSuccess)
            .Select(static (result, _) => result.Value);
    }

    public static void RegisterSourceOutput<TSource>(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<TSource> source,
        string stageName,
        Func<TSource, string> identitySelector,
        Action<SourceProductionContext, TSource> action)
    {
        // File-producing stages own disjoint artifact kinds. Check names before
        // Roslyn merges per-item contexts, outside the callback's catch. Only
        // names are collected: unchanged requests retain their output cache.
        var identified = source.Select((value, cancellationToken) =>
        {
            string? identity;
            try { identity = identitySelector(value); }
            catch (Exception exception) when (CanReport(exception, cancellationToken)) { identity = null; }
            return new IdentifiedOutput<TSource>(value, identity, NormalizeOutputIdentity(identity));
        });
        var named = identified.Where(static value => value.Identity is not null);
        var collisions = named.Select(static (value, _) => value.NormalizedIdentity!)
            .Collect()
            .Select(static (names, _) => names
                .GroupBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.OrderBy(static name => name, StringComparer.Ordinal).First())
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToImmutableArray())
            .WithComparer(OutputNamesComparer.Instance);
        // Keep each input's position even while its output is suppressed. A
        // resolved collision must not invalidate unrelated output callbacks.
        var coordinated = named.Combine(collisions)
            .Select(static (value, _) => (Output: value.Left,
                Conflicts: value.Right.Contains(value.Left.NormalizedIdentity!, StringComparer.OrdinalIgnoreCase)));

        context.RegisterSourceOutput(coordinated, (productionContext, value) =>
        {
            if (!value.Conflicts)
                ExecuteSourceOutput(productionContext, value.Output.Value, stageName,
                    _ => value.Output.Identity!, action);
        });
        context.RegisterSourceOutput(collisions, (productionContext, names) =>
        {
            foreach (var identity in names)
            {
                productionContext.CancellationToken.ThrowIfCancellationRequested();
                var exception = new InvalidOperationException(
                    "Generated source hint name '" + identity + "' is not unique.");
                var description = DescribeException(exception, productionContext.CancellationToken);
                var failure = new GeneratorStageFailure(stageName, identity,
                    description.Type, description.Message, description.Details, Location.None);
                productionContext.ReportDiagnostic(failure.CreateDiagnostic());
                failure.AddReportSource(productionContext);
            }
        });

        // If an identity itself failed, the original action may still succeed.
        // Keep these exceptional inputs together so identical failure reports
        // are emitted once, without affecting the normal per-file output path.
        context.RegisterSourceOutput(identified.Where(static value => value.Identity is null).Collect(),
            (productionContext, values) =>
            {
                var reportedFailures = new HashSet<string>(StringComparer.Ordinal);
                foreach (var value in values)
                {
                    productionContext.CancellationToken.ThrowIfCancellationRequested();
                    ExecuteSourceOutput(productionContext, value.Value, stageName,
                        static _ => null!, action, reportedFailures);
                }
            });
    }

    private static string? NormalizeOutputIdentity(string? identity)
    {
        if (identity is null) return null;
        var normalized = identity.Replace('\\', '/');
        return normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? normalized : normalized + ".cs";
    }

    private readonly record struct IdentifiedOutput<T>(T Value, string? Identity, string? NormalizedIdentity);

    private sealed class OutputNamesComparer : IEqualityComparer<ImmutableArray<string>>
    {
        public static OutputNamesComparer Instance { get; } = new();
        public bool Equals(ImmutableArray<string> left, ImmutableArray<string> right) =>
            left.SequenceEqual(right, StringComparer.Ordinal);
        public int GetHashCode(ImmutableArray<string> value) => value.Length;
    }

    public static void RegisterSourceOutput<TSource>(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<TSource> source,
        string stageName,
        string failureIdentity,
        Action<SourceProductionContext, TSource> action)
    {
        context.RegisterSourceOutput(
            source,
            (productionContext, value) => ExecuteSourceOutput(
                productionContext,
                value,
                stageName,
                _ => failureIdentity,
                action));
    }

    public static void RegisterInitializationFailure(
        IncrementalGeneratorInitializationContext context,
        string stageName,
        Exception exception)
    {
        var description = DescribeException(exception, CancellationToken.None);
        var failure = new GeneratorStageFailure(
            stageName,
            stageName,
            description.Type,
            description.Message,
            description.Details,
            Location.None);

        context.RegisterSourceOutput(
            context.CompilationProvider,
            (productionContext, _) =>
            {
                productionContext.ReportDiagnostic(
                    failure.CreateDiagnostic());
                failure.AddReportSource(productionContext);
            });
    }

    public static bool CanReport(
        Exception exception,
        CancellationToken cancellationToken)
    {
        return exception is not OutOfMemoryException and
               not StackOverflowException and
               not AccessViolationException &&
               (exception is not OperationCanceledException ||
                !cancellationToken.IsCancellationRequested);
    }

    private static GeneratorStageFailure CreateFailure<TSource>(
        TSource source,
        string stageName,
        Exception exception,
        Func<TSource, Location?> locationSelector,
        CancellationToken cancellationToken)
    {
        var description = DescribeException(exception, cancellationToken);
        Location location;
        string identity;

        try
        {
            location = locationSelector(source) ?? Location.None;
            identity = BuildFailureIdentity(location, description.Details);
        }
        catch (Exception locationException) when (CanReport(
                   locationException,
                   cancellationToken))
        {
            location = Location.None;
            identity = description.Details;
        }

        return new GeneratorStageFailure(
            stageName,
            identity,
            description.Type,
            description.Message,
            description.Details,
            location);
    }

    private static string BuildFailureIdentity(
        Location location,
        string exceptionDetails)
    {
        if (!location.IsInSource)
        {
            return exceptionDetails;
        }

        var lineSpan = location.GetLineSpan();
        var start = lineSpan.StartLinePosition;
        var end = lineSpan.EndLinePosition;

        return lineSpan.Path.Replace('\\', '/') + "|" +
               start.Line + "|" +
               start.Character + "|" +
               end.Line + "|" +
               end.Character;
    }

    private static void ExecuteSourceOutput<TSource>(
        SourceProductionContext productionContext,
        TSource value,
        string stageName,
        Func<TSource, string> identitySelector,
        Action<SourceProductionContext, TSource> action,
        HashSet<string>? reportedFailures = null)
    {
        try
        {
            action(productionContext, value);
        }
        catch (Exception exception) when (CanReport(
                   exception,
                   productionContext.CancellationToken))
        {
            var description = DescribeException(exception, productionContext.CancellationToken);
            string failureIdentity;

            try
            {
                failureIdentity = identitySelector(value) ??
                                  description.Details;
            }
            catch (Exception identityException) when (CanReport(
                       identityException,
                       productionContext.CancellationToken))
            {
                failureIdentity = description.Details;
            }

            var failure = new GeneratorStageFailure(
                stageName,
                failureIdentity,
                description.Type,
                description.Message,
                description.Details,
                Location.None);

            if (reportedFailures is not null && !reportedFailures.Add(failure.ReportHintName)) return;
            productionContext.ReportDiagnostic(
                failure.CreateDiagnostic());
            failure.AddReportSource(productionContext);
        }
    }

    private static ExceptionDescription DescribeException(
        Exception exception,
        CancellationToken cancellationToken)
    {
        var type = exception.GetType().FullName ?? exception.GetType().Name;
        var message = ReadExceptionText(
            () => exception.Message,
            "[Exception message unavailable.]",
            cancellationToken);
        string? details;
        try
        {
            details = exception.ToString();
        }
        catch (Exception formattingException) when (CanReport(formattingException, cancellationToken))
        {
            details = null;
        }

        if (details is null)
        {
            var stackTrace = ReadExceptionText(() => exception.StackTrace, string.Empty, cancellationToken);
            details = type + ": " + message + "\r\n" +
                      (stackTrace.Length == 0 ? string.Empty : stackTrace + "\r\n") +
                      "[Full exception details unavailable.]";
        }

        return new ExceptionDescription(type, message, details);
    }

    private static string ReadExceptionText(
        Func<string?> read,
        string fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            return read() ?? fallback;
        }
        catch (Exception exception) when (CanReport(exception, cancellationToken))
        {
            return fallback;
        }
    }

    private readonly record struct ExceptionDescription(string Type, string Message, string Details);

    private static void RegisterValueFailures<TResult>(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<GeneratorStageResult<TResult>> results)
    {
        context.RegisterSourceOutput(
            results,
            static (productionContext, result) =>
            {
                if (!result.IsSuccess)
                {
                    productionContext.ReportDiagnostic(
                        result.Failure.CreateDiagnostic());
                    result.Failure.AddReportSource(productionContext);
                }
            });
    }

    internal readonly record struct GeneratorStageResult<T>(
        bool IsSuccess,
        T Value,
        GeneratorStageFailure Failure)
    {
        public static GeneratorStageResult<T> Success(T value) =>
            new(true, value, default);

        public static GeneratorStageResult<T> Failed(
            GeneratorStageFailure failure) =>
            new(false, default!, failure);
    }

    internal readonly record struct TrackedSourceRequestStageResult<T>(
        GeneratorStageResult<T> Result)
        where T : IGeneratedSourceRequest
    {
        public string HintName => Result.IsSuccess
            ? Result.Value.HintName
            : Result.Failure.ReportHintName;
    }

    internal readonly record struct GeneratorStageFailure(
        string StageName,
        string FailureIdentity,
        string ExceptionType,
        string ExceptionMessage,
        string ExceptionDetails,
        Location Location)
    {
        public string ReportHintName => BuildReportHintName();

        public Diagnostic CreateDiagnostic()
        {
            var reportHintName = ReportHintName;

            return Diagnostic.Create(
                GeneratorFailureDiagnosticDescriptors.UnexpectedFailure,
                Location,
                ImmutableDictionary<string, string?>.Empty
                    .Add("GeneratorVersion", GeneratorVersion)
                    .Add("StageName", StageName)
                    .Add("ExceptionType", ExceptionType)
                    .Add("ExceptionMessage", ExceptionMessage)
                    .Add("ExceptionDetails", ExceptionDetails)
                    .Add("ReportHintName", reportHintName),
                GeneratorVersion,
                StageName,
                ExceptionType,
                ExceptionMessage,
                reportHintName);
        }

        public void AddReportSource(
            SourceProductionContext productionContext)
        {
            productionContext.AddSource(
                ReportHintName,
                SourceText.From(
                    BuildReportSource(),
                    Encoding.UTF8));
        }

        private string BuildReportHintName()
        {
            var identity = StageName + "|" +
                           FailureIdentity + "|" +
                           ExceptionType;
            return GeneratedSourceHintName.Create(
                "GeneratorFailure",
                HintNameHelper.ToHintNamePart(StageName),
                HintNameHelper.GetStableHash128(identity));
        }

        private string BuildReportSource()
        {
            var details = ExceptionDetails.Replace("*/", "* /");
            var builder = new StringBuilder();

            builder.Append("// <auto-generated />\r\n");
            builder.Append("#nullable enable\r\n\r\n");
            builder.Append("/*\r\n");
            builder.Append("MORPH0057: Morphant generator ");
            builder.Append(GeneratorVersion);
            builder.Append(" failed unexpectedly.\r\n");
            builder.Append("Stage: ");
            builder.Append(StageName);
            builder.Append("\r\n\r\n");
            builder.Append(details.Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Replace("\n", "\r\n"));
            builder.Append("\r\n*/\r\n");

            return builder.ToString();
        }
    }
}

internal interface IGeneratedSourceRequest
{
    string HintName { get; }
}
