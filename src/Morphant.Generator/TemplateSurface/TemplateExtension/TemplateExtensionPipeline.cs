using System.Collections.Immutable;
using System.Globalization;
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
            .Where(static destinationType =>
                destinationType.CanGenerateTemplateExtension)
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

    public static TemplateExtensionRequest Build(TemplateDestinationTypeInfo destinationType)
    {
        return Build(
            destinationType,
            HintNameHelper.ToHintNamePart(
                destinationType.UsageIdentity));
    }

    private static ImmutableArray<TemplateExtensionRequest>
        BuildRequests(
            ImmutableArray<TemplateDestinationTypeInfo> destinationTypes,
            CancellationToken cancellationToken)
    {
        var canonicalDestinations =
            new Dictionary<string, TemplateDestinationTypeInfo>(
                StringComparer.Ordinal);

        foreach (var destinationType in destinationTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var signature = destinationType.TemplateExtensionSignature;

            if (!canonicalDestinations.TryGetValue(
                    signature.Identity,
                    out var current) ||
                CompareCanonicalPreference(
                    destinationType,
                    current) < 0)
            {
                canonicalDestinations[signature.Identity] =
                    destinationType;
            }
        }

        var orderedDestinations = canonicalDestinations.Values.ToArray();

        Array.Sort(
            orderedDestinations,
            static (left, right) =>
            {
                var comparison = StringComparer.Ordinal.Compare(
                    left.UsageIdentity,
                    right.UsageIdentity);

                return comparison != 0
                    ? comparison
                    : StringComparer.Ordinal.Compare(
                        left.FullyQualifiedName,
                        right.FullyQualifiedName);
            });

        var usedHintNameParts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var requests =
            ImmutableArray.CreateBuilder<TemplateExtensionRequest>(
                orderedDestinations.Length);

        foreach (var destinationType in orderedDestinations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hintNamePart = HintNameHelper.ToHintNamePart(
                destinationType.UsageIdentity);

            if (!usedHintNameParts.Add(hintNamePart))
            {
                var readableHintNamePart =
                    HintNameHelper.ToReadableHintNamePart(
                        destinationType.UsageIdentity);

                hintNamePart = HintNameHelper.AppendStableHash(
                    readableHintNamePart,
                    destinationType.UsageIdentity);

                var collisionIndex = 2;

                while (!usedHintNameParts.Add(hintNamePart))
                {
                    hintNamePart =
                        HintNameHelper.AppendStableHash(
                            readableHintNamePart,
                            destinationType.UsageIdentity) +
                        "_" +
                        collisionIndex.ToString(
                            CultureInfo.InvariantCulture);

                    collisionIndex++;
                }
            }

            requests.Add(Build(destinationType, hintNamePart));
        }

        return requests.ToImmutable();
    }

    private static int CompareCanonicalPreference(
        TemplateDestinationTypeInfo left,
        TemplateDestinationTypeInfo right)
    {
        var leftSignature = left.TemplateExtensionSignature;
        var rightSignature = right.TemplateExtensionSignature;

        var comparison = leftSignature.DynamicTypeCount.CompareTo(
            rightSignature.DynamicTypeCount);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = leftSignature.NullableReferenceTypeCount.CompareTo(
            rightSignature.NullableReferenceTypeCount);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = leftSignature.ExplicitTupleElementNameCount.CompareTo(
            rightSignature.ExplicitTupleElementNameCount);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = StringComparer.Ordinal.Compare(
            left.UsageIdentity,
            right.UsageIdentity);

        return comparison != 0
            ? comparison
            : StringComparer.Ordinal.Compare(
                left.FullyQualifiedName,
                right.FullyQualifiedName);
    }

    private static TemplateExtensionRequest Build(
        TemplateDestinationTypeInfo destinationType,
        string hintNamePart)
    {
        var hintName =
            "Morphant.TemplateExtensions." +
            hintNamePart +
            ".g.cs";

        return new TemplateExtensionRequest(
            destinationType,
            hintName);
    }

    public static SourceText Generate(TemplateExtensionRequest request)
    {
        var destinationType = request.TemplateDestinationType;

        var source = $@"// <auto-generated />
#nullable enable

namespace Morphant
{{
    internal static partial class MorphantGeneratedTemplateExtensions
    {{
        public static global::Morphant.MapperBuilder<TSource, {destinationType.FullyQualifiedName}> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, {destinationType.FullyQualifiedName}> builder,
            global::System.Func<TSource, {destinationType.TemplateResultTypeFullyQualifiedName}> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static global::Morphant.MapperBuilder<TSource, {destinationType.FullyQualifiedName}> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, {destinationType.FullyQualifiedName}> builder,
            global::System.Func<TSource, {destinationType.ExistingDestinationTypeFullyQualifiedName}, {destinationType.TemplateResultTypeFullyQualifiedName}> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }}
}}
"
            .Replace("\r\n", "\n")
            .Replace("\n", "\r\n");

        return SourceText.From(source, Encoding.UTF8);
    }
}
