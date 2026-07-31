using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TemplateSurface.TemplateExtension;

internal static class TemplateExtensionPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<TemplateDestinationTypeInfo> destinationTypes)
    {
        var templateExtensionRequests = destinationTypes
            .Collect()
            .SelectMany(static (destinationTypes, cancellationToken) =>
                BuildRequests(
                    destinationTypes,
                    cancellationToken))
            .WithTrackingName(
                MorphantGeneratorStageNames
                    .BuildTemplateExtensionRequests);

        context.RegisterSourceOutput(
            templateExtensionRequests,
            static (context, request) =>
            {
                context.AddSource(
                    request.HintName,
                    Generate(request));
            });
    }

    public static TemplateExtensionRequest Build(
        TemplateDestinationTypeInfo destinationType)
    {
        var genericDestination =
            RemoveSourceSpecificDetails(destinationType);

        return Build(
            TemplateExtensionGenerationKind.Generic,
            genericDestination,
            ImmutableArray.Create(genericDestination),
            HintNameHelper.ToHintNamePart(
                genericDestination.UsageIdentity));
    }

    private static ImmutableArray<TemplateExtensionRequest>
        BuildRequests(
            ImmutableArray<TemplateDestinationTypeInfo> destinationTypes,
            CancellationToken cancellationToken)
    {
        var groups =
            new Dictionary<
                string,
                List<TemplateDestinationTypeInfo>>(
                StringComparer.Ordinal);

        foreach (var destinationType in destinationTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var identity =
                destinationType.TemplateExtensionSignature.Identity;

            if (!groups.TryGetValue(identity, out var group))
            {
                group = new List<TemplateDestinationTypeInfo>();
                groups.Add(identity, group);
            }

            group.Add(destinationType);
        }

        var candidates = new List<TemplateExtensionRequestCandidate>(
            groups.Count);

        foreach (var group in groups.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var canonicalDestination =
                SelectCanonicalDestination(group);
            var firstKind = group[0].Kind;
            var hasMixedSurface =
                group.Any(destinationType =>
                    destinationType.Kind != firstKind);

            if (!hasMixedSurface)
            {
                if (firstKind != TemplateDestinationTypeKind.None &&
                    canonicalDestination.CanGenerateTemplateExtension)
                {
                    var genericDestination =
                        RemoveSourceSpecificDetails(
                            canonicalDestination);

                    candidates.Add(
                        new TemplateExtensionRequestCandidate(
                            TemplateExtensionGenerationKind.Generic,
                            genericDestination,
                            ImmutableArray.Create(
                                genericDestination)));
                }

                continue;
            }

            var pairSpecificMappings =
                BuildPairSpecificMappings(
                    group,
                    cancellationToken);

            if (!pairSpecificMappings.IsDefaultOrEmpty)
            {
                candidates.Add(
                    new TemplateExtensionRequestCandidate(
                        TemplateExtensionGenerationKind.PairSpecific,
                        canonicalDestination,
                        pairSpecificMappings));
            }
        }

        candidates.Sort(static (left, right) =>
        {
            var comparison = StringComparer.Ordinal.Compare(
                left.CanonicalDestinationType.UsageIdentity,
                right.CanonicalDestinationType.UsageIdentity);

            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(
                    left.CanonicalDestinationType.FullyQualifiedName,
                    right.CanonicalDestinationType.FullyQualifiedName);
        });

        var hintNamePartAllocator = new HintNamePartAllocator();

        var requests =
            ImmutableArray.CreateBuilder<TemplateExtensionRequest>(
                candidates.Count);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hintNamePart = hintNamePartAllocator.Allocate(
                candidate.CanonicalDestinationType.UsageIdentity);

            requests.Add(
                Build(
                    candidate.GenerationKind,
                    candidate.CanonicalDestinationType,
                    candidate.MappingTypes,
                    hintNamePart));
        }

        return requests.ToImmutable();
    }

    private static TemplateDestinationTypeInfo
        SelectCanonicalDestination(
            IReadOnlyList<TemplateDestinationTypeInfo> destinationTypes)
    {
        var canonical = destinationTypes[0];

        for (var index = 1;
             index < destinationTypes.Count;
             index++)
        {
            var candidate = destinationTypes[index];

            if (CompareCanonicalPreference(
                    candidate,
                    canonical) < 0)
            {
                canonical = candidate;
            }
        }

        return canonical;
    }

    private static TemplateDestinationTypeInfo
        RemoveSourceSpecificDetails(
            TemplateDestinationTypeInfo destinationType)
    {
        return destinationType with
        {
            SourceTypeSignature = default,
            SourceTypeFullyQualifiedName = string.Empty,
            CanGeneratePairSpecificTemplateExtension = false
        };
    }

    private static ImmutableArray<TemplateDestinationTypeInfo>
        BuildPairSpecificMappings(
            IReadOnlyList<TemplateDestinationTypeInfo> destinationTypes,
            CancellationToken cancellationToken)
    {
        var sourceGroups =
            new Dictionary<
                string,
                List<TemplateDestinationTypeInfo>>(
                StringComparer.Ordinal);

        foreach (var destinationType in destinationTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var identity =
                destinationType.SourceTypeSignature.Identity;

            if (!sourceGroups.TryGetValue(identity, out var group))
            {
                group = new List<TemplateDestinationTypeInfo>();
                sourceGroups.Add(identity, group);
            }

            group.Add(destinationType);
        }

        var mappings = new List<TemplateDestinationTypeInfo>(
            sourceGroups.Count);

        foreach (var sourceGroup in sourceGroups.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var firstKind = sourceGroup[0].Kind;

            if (firstKind == TemplateDestinationTypeKind.None ||
                sourceGroup.Any(mapping => mapping.Kind != firstKind))
            {
                continue;
            }

            var canonical = sourceGroup[0];

            for (var index = 1;
                 index < sourceGroup.Count;
                 index++)
            {
                var candidate = sourceGroup[index];

                if (ComparePairCanonicalPreference(
                        candidate,
                        canonical) < 0)
                {
                    canonical = candidate;
                }
            }

            if (canonical.CanGeneratePairSpecificTemplateExtension)
            {
                mappings.Add(canonical);
            }
        }

        mappings.Sort(static (left, right) =>
        {
            var comparison = StringComparer.Ordinal.Compare(
                left.SourceTypeSignature.Identity,
                right.SourceTypeSignature.Identity);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(
                left.SourceTypeFullyQualifiedName,
                right.SourceTypeFullyQualifiedName);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Kind.CompareTo(right.Kind);

            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(
                    left.FullyQualifiedName,
                    right.FullyQualifiedName);
        });

        return mappings.ToImmutableArray();
    }

    private static int CompareCanonicalPreference(
        TemplateDestinationTypeInfo left,
        TemplateDestinationTypeInfo right)
    {
        return CompareSignaturePreference(
            left.TemplateExtensionSignature,
            right.TemplateExtensionSignature,
            left.UsageIdentity,
            right.UsageIdentity,
            left.FullyQualifiedName,
            right.FullyQualifiedName);
    }

    private static int ComparePairCanonicalPreference(
        TemplateDestinationTypeInfo left,
        TemplateDestinationTypeInfo right)
    {
        var comparison = CompareSignaturePreference(
            left.SourceTypeSignature,
            right.SourceTypeSignature,
            left.SourceTypeFullyQualifiedName,
            right.SourceTypeFullyQualifiedName,
            left.FullyQualifiedName,
            right.FullyQualifiedName);

        return comparison != 0
            ? comparison
            : CompareCanonicalPreference(left, right);
    }

    private static int CompareSignaturePreference(
        TemplateExtensionSignatureInfo left,
        TemplateExtensionSignatureInfo right,
        string leftIdentity,
        string rightIdentity,
        string leftName,
        string rightName)
    {
        var comparison = left.DynamicTypeCount.CompareTo(
            right.DynamicTypeCount);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.NullableReferenceTypeCount.CompareTo(
            right.NullableReferenceTypeCount);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.ExplicitTupleElementNameCount.CompareTo(
            right.ExplicitTupleElementNameCount);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = StringComparer.Ordinal.Compare(
            leftIdentity,
            rightIdentity);

        return comparison != 0
            ? comparison
            : StringComparer.Ordinal.Compare(
                leftName,
                rightName);
    }

    private static TemplateExtensionRequest Build(
        TemplateExtensionGenerationKind generationKind,
        TemplateDestinationTypeInfo canonicalDestinationType,
        ImmutableArray<TemplateDestinationTypeInfo> mappingTypes,
        string hintNamePart)
    {
        var hintName = GeneratedSourceHintName.Create(
            "TemplateExtension",
            hintNamePart);

        return new TemplateExtensionRequest(
            generationKind,
            canonicalDestinationType,
            mappingTypes,
            hintName);
    }

    public static SourceText Generate(TemplateExtensionRequest request)
    {
        var source = request.GenerationKind switch
        {
            TemplateExtensionGenerationKind.Generic =>
                GenerateGeneric(request.MappingTypes[0]),
            TemplateExtensionGenerationKind.PairSpecific =>
                GeneratePairSpecific(request.MappingTypes),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request))
        };

        return SourceText.From(source, Encoding.UTF8);
    }

    private static string GenerateGeneric(
        TemplateDestinationTypeInfo destinationType)
    {
        return $@"// <auto-generated />
#nullable enable

namespace Morphant
{{
    internal static partial class MorphantGeneratedTemplateExtensions
    {{
        /// <summary>
        /// Configures a mapping template.
        /// </summary>
        /// <typeparam name=""TSource"">The source type.</typeparam>
        /// <param name=""builder"">The mapping builder to configure.</param>
        /// <param name=""template"">
        /// A lambda expression that receives the non-null source value and describes the mapping.
        /// </param>
        /// <returns>The <paramref name=""builder""/> instance.</returns>
        public static global::Morphant.MapperBuilder<TSource, {destinationType.FullyQualifiedName}> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, {destinationType.FullyQualifiedName}> builder,
            global::System.Func<TSource, {destinationType.TemplateResultTypeFullyQualifiedName}> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Configures a mapping template that depends on the destination's previous state.
        /// </summary>
        /// <typeparam name=""TSource"">The source type.</typeparam>
        /// <param name=""builder"">The mapping builder to configure.</param>
        /// <param name=""template"">
        /// A lambda expression that receives the non-null source value and the destination's
        /// previous value and describes the mapping. The previous value is
        /// <see langword=""default""/> when no destination exists.
        /// </param>
        /// <returns>The <paramref name=""builder""/> instance.</returns>
        public static global::Morphant.MapperBuilder<TSource, {destinationType.FullyQualifiedName}> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, {destinationType.FullyQualifiedName}> builder,
            global::System.Func<TSource, {destinationType.ExistingDestinationTypeFullyQualifiedName}, {destinationType.TemplateResultTypeFullyQualifiedName}> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }}
}}
"
            .Replace("\r\n", "\n")
            .Replace("\n", "\r\n");
    }

    private static string GeneratePairSpecific(
        ImmutableArray<TemplateDestinationTypeInfo> mappingTypes)
    {
        var writer = new CodeWriter();

        writer.Line("// <auto-generated />");
        writer.Line("#nullable enable");
        writer.Line();
        writer.OpenBlock("namespace Morphant");
        writer.OpenBlock(
            "internal static partial class " +
            "MorphantGeneratedTemplateExtensions");

        for (var index = 0;
             index < mappingTypes.Length;
             index++)
        {
            if (index > 0)
            {
                writer.Line();
            }

            WritePairSpecificOverloads(
                writer,
                mappingTypes[index]);
        }

        writer.CloseBlock();
        writer.CloseBlock();

        return writer.ToString();
    }

    private static void WritePairSpecificOverloads(
        CodeWriter writer,
        TemplateDestinationTypeInfo mappingType)
    {
        var builderType =
            "global::Morphant.MapperBuilder<" +
            mappingType.SourceTypeFullyQualifiedName +
            ", " +
            mappingType.FullyQualifiedName +
            ">";

        writer.Line("/// <summary>");
        writer.Line("/// Configures a mapping template.");
        writer.Line("/// </summary>");
        writer.Line(
            "/// <param name=\"builder\">The mapping builder to " +
            "configure.</param>");
        writer.Line("/// <param name=\"template\">");
        writer.Line(
            "/// A lambda expression that receives the non-null source " +
            "value and describes the mapping.");
        writer.Line("/// </param>");
        writer.Line(
            "/// <returns>The <paramref name=\"builder\"/> " +
            "instance.</returns>");
        writer.Line(
            $"public static {builderType} Template(");
        writer.Line(
            $"    this {builderType} builder,");
        writer.Line(
            "    global::System.Func<" +
            mappingType.SourceTypeFullyQualifiedName +
            ", " +
            mappingType.TemplateResultTypeFullyQualifiedName +
            "> template)");
        writer.Line(
            "    => throw new global::Morphant.Exceptions." +
            "RuntimeInvocationNotSupportedException();");
        writer.Line();
        writer.Line("/// <summary>");
        writer.Line(
            "/// Configures a mapping template that depends on the " +
            "destination's previous state.");
        writer.Line("/// </summary>");
        writer.Line(
            "/// <param name=\"builder\">The mapping builder to " +
            "configure.</param>");
        writer.Line("/// <param name=\"template\">");
        writer.Line(
            "/// A lambda expression that receives the non-null source " +
            "value and the destination's");
        writer.Line(
            "/// previous value and describes the mapping. The previous " +
            "value is");
        writer.Line(
            "/// <see langword=\"default\"/> when no destination exists.");
        writer.Line("/// </param>");
        writer.Line(
            "/// <returns>The <paramref name=\"builder\"/> " +
            "instance.</returns>");
        writer.Line(
            $"public static {builderType} Template(");
        writer.Line(
            $"    this {builderType} builder,");
        writer.Line(
            "    global::System.Func<" +
            mappingType.SourceTypeFullyQualifiedName +
            ", " +
            mappingType.ExistingDestinationTypeFullyQualifiedName +
            ", " +
            mappingType.TemplateResultTypeFullyQualifiedName +
            "> template)");
        writer.Line(
            "    => throw new global::Morphant.Exceptions." +
            "RuntimeInvocationNotSupportedException();");
    }

    private readonly record struct TemplateExtensionRequestCandidate
    (
        TemplateExtensionGenerationKind GenerationKind,
        TemplateDestinationTypeInfo CanonicalDestinationType,
        ImmutableArray<TemplateDestinationTypeInfo> MappingTypes
    );
}
