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
        context.RegisterSourceOutput(
            source,
            (productionContext, value) => ExecuteSourceOutput(
                productionContext,
                value,
                stageName,
                identitySelector,
                action));
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
        var failure = new GeneratorStageFailure(
            stageName,
            stageName,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            exception.ToString(),
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
        Location location;

        try
        {
            location = locationSelector(source) ?? Location.None;
        }
        catch (Exception locationException) when (CanReport(
                   locationException,
                   cancellationToken))
        {
            location = Location.None;
        }

        return new GeneratorStageFailure(
            stageName,
            BuildFailureIdentity(location, exception),
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            exception.ToString(),
            location);
    }

    private static string BuildFailureIdentity(
        Location location,
        Exception exception)
    {
        if (!location.IsInSource)
        {
            return exception.ToString();
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
        Action<SourceProductionContext, TSource> action)
    {
        try
        {
            action(productionContext, value);
        }
        catch (Exception exception) when (CanReport(
                   exception,
                   productionContext.CancellationToken))
        {
            string failureIdentity;

            try
            {
                failureIdentity = identitySelector(value) ??
                                  exception.ToString();
            }
            catch (Exception identityException) when (CanReport(
                       identityException,
                       productionContext.CancellationToken))
            {
                failureIdentity = exception.ToString();
            }

            var failure = new GeneratorStageFailure(
                stageName,
                failureIdentity,
                exception.GetType().FullName ??
                exception.GetType().Name,
                exception.Message,
                exception.ToString(),
                Location.None);

            productionContext.ReportDiagnostic(
                failure.CreateDiagnostic());
            failure.AddReportSource(productionContext);
        }
    }

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
            var readableIdentity =
                HintNameHelper.ToHintNamePart(StageName) + "__" +
                HintNameHelper.GetStableHash(identity);

            return GeneratedSourceHintName.Create(
                "GeneratorFailure",
                readableIdentity);
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
