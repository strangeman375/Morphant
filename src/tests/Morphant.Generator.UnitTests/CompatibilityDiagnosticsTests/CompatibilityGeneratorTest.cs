using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.CompatibilityDiagnosticsTests;

internal static class CompatibilityGeneratorTest
{
    private static readonly ImmutableArray<MetadataReference>
        FrameworkReferences = BuildFrameworkReferences();
    private static int _assemblyIndex;

    public static PortableExecutableReference ActualRuntimeReference =>
        MetadataReference.CreateFromFile(typeof(TypeMapper).Assembly.Location);

    public static CompatibilityGeneratorResult Run(
        LanguageVersion languageVersion,
        IReadOnlyCollection<string>? sources = null,
        IReadOnlyCollection<MetadataReference>? references = null,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
        GeneratorDriver? driver = null)
    {
        var parseOptions = new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Diagnose);
        var syntaxTrees = (sources ?? [EmptySource])
            .Select((source, index) =>
                CSharpSyntaxTree.ParseText(
                    SourceText.From(source, Encoding.UTF8),
                    parseOptions,
                    $"TestCase{index}.cs"))
            .ToImmutableArray();
        var specificDiagnosticOptions = diagnosticOptions is null
            ? ImmutableDictionary<string, ReportDiagnostic>.Empty
            : diagnosticOptions.ToImmutableDictionary(StringComparer.Ordinal);
        var compilation = CSharpCompilation.Create(
            "CompatibilityConsumer",
            syntaxTrees,
            FrameworkReferences.AddRange(references ?? []),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                specificDiagnosticOptions: specificDiagnosticOptions));
        driver ??= CSharpGeneratorDriver.Create(
            [new MorphantGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.WithUpdatedParseOptions(parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _);
        var runResult = driver.GetRunResult();
        var generatorResult = runResult.Results.Single();
        var unexpectedCompilerDiagnostics = outputCompilation.GetDiagnostics()
            .Where(diagnostic =>
                !diagnostic.Id.StartsWith("MORPH", StringComparison.Ordinal) &&
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or
                    DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            generatorResult.Exception,
            Is.Null,
            "The production generator must not throw.");
        Assert.That(
            unexpectedCompilerDiagnostics,
            Is.Empty,
            "The consumer must not have unrelated compiler diagnostics." +
            Environment.NewLine +
            string.Join(Environment.NewLine, unexpectedCompilerDiagnostics));

        return new CompatibilityGeneratorResult(
            driver,
            compilation,
            outputCompilation,
            generatorResult.Diagnostics,
            generatorResult.GeneratedSources);
    }

    public static PortableExecutableReference CreateCompatibleRuntimeReference(
        string revision = "1",
        Func<string, string>? mutate = null,
        string? assemblyName = null)
    {
        var source = CompatibleRuntimeSource.Replace(
            "%%REVISION%%",
            revision,
            StringComparison.Ordinal);

        if (mutate is not null)
        {
            source = mutate(source);
        }

        return CreateReference(
            assemblyName ?? NextAssemblyName("CompatibleRuntime"),
            source);
    }

    public static PortableExecutableReference CreateReference(
        string assemblyName,
        string source)
    {
        var parseOptions = new CSharpParseOptions(
            LanguageVersion.CSharp9,
            DocumentationMode.Diagnose);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [
                CSharpSyntaxTree.ParseText(
                    SourceText.From(source, Encoding.UTF8),
                    parseOptions,
                    assemblyName + ".cs")
            ],
            FrameworkReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        var failures = emitResult.Diagnostics
            .Where(static diagnostic =>
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or
                    DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();

        Assert.That(
            failures,
            Is.Empty,
            $"Reference '{assemblyName}' must compile without diagnostics." +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    public static void AssertDiagnostics(
        CompatibilityGeneratorResult result,
        params ExpectedCompatibilityDiagnostic[] expected)
    {
        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(expected.Select(static diagnostic => diagnostic.Id)),
            string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.GetMessage()),
            Is.EqualTo(expected.Select(static diagnostic => diagnostic.Message)));

        foreach (var diagnostic in result.Diagnostics)
        {
            Assert.Multiple(() =>
            {
                Assert.That(diagnostic.Location, Is.EqualTo(Location.None));
                Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            });
        }
    }

    private static ImmutableArray<MetadataReference> BuildFrameworkReferences()
    {
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable.");

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).Equals(
                "Morphant.dll",
                StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path =>
                (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static string NextAssemblyName(string prefix)
    {
        return prefix + Interlocked.Increment(ref _assemblyIndex);
    }

    public const string EmptySource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Placeholder
    {
    }
}
""";

    public const string MapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }
}
""";

    // This is a test-owned compatible revision-1 contract. It intentionally
    // contains only the bootstrap surface needed to exercise the generator
    // as a black box; it is not assembled from production manifest data.
    private const string CompatibleRuntimeSource =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Reflection;

[assembly: AssemblyMetadata("Morphant.GeneratorContractVersion", "%%REVISION%%")]

namespace Morphant
{
    public sealed class MorphantMapperAttribute : Attribute
    {
        public MorphantMapperAttribute()
        {
        }
    }

    public enum ConstructorSelection
    {
        Default = 0,
        Explicit = 1,
        Parameterless = 2,
        Single = 3,
        Unambiguous = 4,
        Greediest = 5,
        Largest = 6
    }

    [Flags]
    public enum MappingMode
    {
        Default = 0,
        Create = 1,
        Update = 2,
        CreateAndUpdate = 3
    }

    public enum MemberSelection
    {
        Default = 0,
        Auto = 1,
        Explicit = 2
    }

    public enum NullDestinationHandling
    {
        Default = 0,
        Create = 1,
        Throw = 2
    }

    public enum NullSourceHandling
    {
        Default = 0,
        ReturnNull = 1,
        ReturnDestination = 2,
        Throw = 3
    }

    public enum UnmappedMemberValidation
    {
        Default = 0,
        None = 1,
        Source = 2,
        Destination = 3,
        Strict = 4
    }

    public interface ITypeMapper<in TSource, TDestination>
    {
        TDestination Create(
            TSource? source,
            Context.MappingContext context);

        TDestination Update(
            TSource? source,
            TDestination? destination,
            Context.MappingContext context);
    }

    public interface IMapper
    {
        TDestination Map<TSource, TDestination>(TSource? source);

        TDestination Map<TSource, TDestination>(
            TSource? source,
            TDestination? destination);
    }

    public sealed class Mapper : IMapper
    {
        public Mapper(IServiceProvider serviceProvider)
        {
        }

        public TDestination Map<TSource, TDestination>(TSource? source) =>
            throw new NotSupportedException();

        public TDestination Map<TSource, TDestination>(
            TSource? source,
            TDestination? destination) =>
            throw new NotSupportedException();
    }

    public readonly struct Option<T>
    {
        public static Option<T> None => default;
        public static Option<T> Some(T value) => default;
        public bool HasValue => false;
        public T Value => throw new NotSupportedException();

        public bool TryGetValue(out T value)
        {
            value = default!;
            return false;
        }
    }

    public abstract class TypeMapper
    {
        protected internal virtual bool Supports(
            Type sourceType,
            Type destinationType) => false;

        protected abstract void Configure(MapperBuilder builder);

        protected static Markers.ByConventionMarker ByConvention() =>
            throw new NotSupportedException();

        protected static Markers.AutoMarker Auto() =>
            throw new NotSupportedException();

        protected static Markers.AutoMarker<T> Auto<T>() =>
            throw new NotSupportedException();

        protected static Markers.IgnoreMarker Ignore() =>
            throw new NotSupportedException();

        protected static Markers.IgnoreMarker<T> Ignore<T>() =>
            throw new NotSupportedException();

        protected static Markers.ValueMarker<T> Value<T>(T value) =>
            throw new NotSupportedException();

        protected static Markers.MapMarker Map() =>
            throw new NotSupportedException();

        protected static Markers.MapMarker Map(object? source) =>
            throw new NotSupportedException();

        protected static Markers.MapMarker<T> Map<T>() =>
            throw new NotSupportedException();

        protected static Markers.MapMarker<T> Map<T>(object? source) =>
            throw new NotSupportedException();

        protected static Markers.MapMarker Create(object? source) =>
            throw new NotSupportedException();

        protected static Markers.MapMarker<T> Create<T>(object? source) =>
            throw new NotSupportedException();

        protected static Markers.MapMarker Update(
            object? source,
            object? destination) =>
            throw new NotSupportedException();

        protected static Markers.MapMarker<T> Update<T>(
            object? source,
            object? destination) =>
            throw new NotSupportedException();
    }

    public abstract class MapperBuilderBase<T>
        where T : MapperBuilderBase<T>
    {
        public T NullSourceHandling(NullSourceHandling value) =>
            throw new NotSupportedException();

        public T NullDestinationHandling(NullDestinationHandling value) =>
            throw new NotSupportedException();

        public T ConstructorSelection(ConstructorSelection value) =>
            throw new NotSupportedException();

        public T MemberSelection(MemberSelection value) =>
            throw new NotSupportedException();

        public T UnmappedMemberValidation(UnmappedMemberValidation value) =>
            throw new NotSupportedException();
    }

    public sealed class MapperBuilder : MapperBuilderBase<MapperBuilder>
    {
        public MapperBuilder MappingMode(MappingMode value) =>
            throw new NotSupportedException();

        public MapperBuilder<TSource, TDestination>
            Map<TSource, TDestination>(
                MappingMode value = Morphant.MappingMode.Default) =>
            throw new NotSupportedException();
    }

    public sealed class MapperBuilder<TSource, TDestination> :
        MapperBuilderBase<MapperBuilder<TSource, TDestination>>
    {
        public MapperBuilder<TSource, TDestination>
            IncludeBase<TBaseSource, TBaseDestination>() =>
            throw new NotSupportedException();
    }

    public static class TypeMapperExtensions
    {
        public static TDestination Create<TSource, TDestination>(
            this ITypeMapper<TSource, TDestination> mapper,
            TSource? source) =>
            throw new NotSupportedException();

        public static TDestination Update<TSource, TDestination>(
            this ITypeMapper<TSource, TDestination> mapper,
            TSource? source,
            TDestination? destination) =>
            throw new NotSupportedException();
    }
}

namespace Morphant.Context
{
    public enum MappingOperation
    {
        Create = 1,
        Update = 2
    }

    public readonly struct MappingContext
    {
        public MappingOperation Operation =>
            throw new NotSupportedException();

        public IMapper Mapper => throw new NotSupportedException();
    }

    public abstract class MappingContextMarker
    {
        public abstract MappingOperation Operation { get; }
    }
}

namespace Morphant.Delegates
{
    public delegate TResult Construct<in TSource, out TResult>(TSource source);
    public delegate TResult Construct<in TSource, in TContext, out TResult>(
        TSource source,
        TContext context);
    public delegate TResult ConstructUsing<in TSource, out TResult>(
        TSource source);
    public delegate TResult ConstructUsing<in TSource, in TContext, out TResult>(
        TSource source,
        TContext context);
    public delegate TResult Convert<in TSource, out TResult>(TSource source);
    public delegate TResult Convert<in TSource, TPrevious, out TResult>(
        TSource source,
        Option<TPrevious> previous);
    public delegate TResult Convert<
        in TSource,
        TPrevious,
        in TContext,
        out TResult>(
        TSource source,
        Option<TPrevious> previous,
        TContext context);
    public delegate TMembers Members<in TSource, out TMembers>(TSource source);
    public delegate TMembers Members<in TSource, TPrevious, out TMembers>(
        TSource source,
        Option<TPrevious> previous);
    public delegate TMembers Members<
        in TSource,
        TPrevious,
        in TResult,
        out TMembers>(
        TSource source,
        Option<TPrevious> previous,
        TResult result);
    public delegate TMembers Members<
        in TSource,
        TPrevious,
        in TResult,
        in TContext,
        out TMembers>(
        TSource source,
        Option<TPrevious> previous,
        TResult result,
        TContext context);
    public delegate TResult Resolve<in TSource, TPrevious, out TResult>(
        TSource source,
        Option<TPrevious> previous);
    public delegate TResult Resolve<
        in TSource,
        TPrevious,
        in TContext,
        out TResult>(
        TSource source,
        Option<TPrevious> previous,
        TContext context);
    public delegate TResult ResolveUsing<in TSource, TPrevious, out TResult>(
        TSource source,
        Option<TPrevious> previous);
    public delegate TResult ResolveUsing<
        in TSource,
        TPrevious,
        in TContext,
        out TResult>(
        TSource source,
        Option<TPrevious> previous,
        TContext context);
}

namespace Morphant.Markers
{
    public abstract class ConstructorMarker
    {
        private protected ConstructorMarker()
        {
        }
    }

    public sealed class ByConventionMarker : ConstructorMarker
    {
    }

    public abstract class MemberMarker
    {
        private protected MemberMarker()
        {
        }
    }

    public sealed class AutoMarker : MemberMarker
    {
    }

    public sealed class AutoMarker<T> : MemberMarker
    {
        public static implicit operator AutoMarker<T>(T value) =>
            throw new NotSupportedException();
    }

    public sealed class IgnoreMarker : MemberMarker
    {
    }

    public sealed class IgnoreMarker<T> : MemberMarker
    {
        public static implicit operator IgnoreMarker<T>(T value) =>
            throw new NotSupportedException();
    }

    public abstract class MapMarker : MemberMarker
    {
        private protected MapMarker()
        {
        }
    }

    public sealed class MapMarker<T> : MapMarker
    {
        public static implicit operator MapMarker<T>(T value) =>
            throw new NotSupportedException();
    }

    public sealed class ValueMarker<T>
    {
    }
}

namespace Morphant.Members
{
    using Morphant.Markers;

    public sealed class ConstructorParameter<T>
    {
        public static implicit operator ConstructorParameter<T>(T value) =>
            throw new NotSupportedException();
        public static implicit operator ConstructorParameter<T>(AutoMarker value) =>
            throw new NotSupportedException();
        public static implicit operator ConstructorParameter<T>(AutoMarker<T> value) =>
            throw new NotSupportedException();
        public static implicit operator ConstructorParameter<T>(IgnoreMarker value) =>
            throw new NotSupportedException();
        public static implicit operator ConstructorParameter<T>(IgnoreMarker<T> value) =>
            throw new NotSupportedException();
        public static implicit operator ConstructorParameter<T>(MapMarker value) =>
            throw new NotSupportedException();
        public static implicit operator ConstructorParameter<T>(ValueMarker<T> value) =>
            throw new NotSupportedException();
    }

    public sealed class Member<T>
    {
        public static implicit operator Member<T>(T value) =>
            throw new NotSupportedException();
        public static implicit operator Member<T>(AutoMarker value) =>
            throw new NotSupportedException();
        public static implicit operator Member<T>(AutoMarker<T> value) =>
            throw new NotSupportedException();
        public static implicit operator Member<T>(IgnoreMarker value) =>
            throw new NotSupportedException();
        public static implicit operator Member<T>(IgnoreMarker<T> value) =>
            throw new NotSupportedException();
        public static implicit operator Member<T>(MapMarker value) =>
            throw new NotSupportedException();
        public static implicit operator Member<T>(ValueMarker<T> value) =>
            throw new NotSupportedException();
    }
}

namespace Morphant.Exceptions
{
    using Morphant.Context;

    public abstract class MorphantException : Exception
    {
        private protected MorphantException()
        {
        }
    }

    public abstract class MappingException : MorphantException
    {
        private protected MappingException()
        {
        }

        public MappingOperation Operation => default;
        public Type SourceType => throw new NotSupportedException();
        public Type DestinationType => throw new NotSupportedException();
    }

    public sealed class AmbiguousMappingException : MappingException
    {
        public AmbiguousMappingException(
            MappingOperation operation,
            Type sourceType,
            Type destinationType)
        {
        }
    }

    public sealed class InvalidMappingContextException : MorphantException
    {
        public InvalidMappingContextException()
        {
        }
    }

    public sealed class InvalidMappingRegistrationException : MappingException
    {
        public InvalidMappingRegistrationException(
            MappingOperation operation,
            Type sourceType,
            Type destinationType)
        {
        }
    }

    public sealed class MappingConfigurationException : MappingException
    {
        public MappingConfigurationException(
            MappingOperation operation,
            Type sourceType,
            Type destinationType,
            string reason)
        {
        }

        public string Reason => string.Empty;
    }

    public sealed class MappingNotFoundException : MappingException
    {
        public MappingNotFoundException(
            MappingOperation operation,
            Type sourceType,
            Type destinationType)
        {
        }
    }

    public sealed class MappingOperationNotSupportedException : MappingException
    {
        public MappingOperationNotSupportedException(
            MappingOperation operation,
            Type sourceType,
            Type destinationType,
            MappingMode effectiveMappingMode)
        {
        }

        public MappingMode EffectiveMappingMode => default;
    }

    public sealed class MappingScopeCompletedException : MappingException
    {
        public MappingScopeCompletedException(
            MappingOperation operation,
            Type sourceType,
            Type destinationType)
        {
        }
    }

    public sealed class NestedDestinationTypeMismatchException : MappingException
    {
        public NestedDestinationTypeMismatchException(
            MappingOperation operation,
            Type sourceType,
            Type destinationType,
            Type expectedDestinationType,
            Type? actualDestinationType)
        {
        }

        public Type? ActualDestinationType => null;
        public Type ExpectedDestinationType => throw new NotSupportedException();
    }

    public sealed class NullDestinationException : MappingException
    {
        public NullDestinationException(
            MappingOperation operation,
            Type sourceType,
            Type destinationType)
        {
        }
    }

    public sealed class NullSourceException : MappingException
    {
        public NullSourceException(
            MappingOperation operation,
            Type sourceType,
            Type destinationType)
        {
        }
    }

    public sealed class OptionValueMissingException : MorphantException
    {
        public OptionValueMissingException()
        {
        }
    }

    public sealed class RuntimeInvocationNotSupportedException : MorphantException
    {
        public RuntimeInvocationNotSupportedException()
        {
        }
    }

    public sealed class UnmatchedMappingSwitchException : MappingException
    {
        public UnmatchedMappingSwitchException(
            MappingOperation operation,
            Type sourceType,
            Type destinationType)
        {
        }
    }
}
""";
}

internal sealed record CompatibilityGeneratorResult(
    GeneratorDriver Driver,
    CSharpCompilation InputCompilation,
    Compilation OutputCompilation,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<GeneratedSourceResult> GeneratedSources);

internal sealed record ExpectedCompatibilityDiagnostic(
    string Id,
    string Message);
