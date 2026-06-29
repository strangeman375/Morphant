using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator;

[Generator]
public sealed class MorphantGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var mapCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsMapperBuilderMapCallCandidate(node),
                transform: static (ctx, ct) => TryGetMapperBuilderMapCallInfo(ctx, ct))
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x!.Value);

        context.RegisterSourceOutput(
            mapCalls.Collect(),
            static (sourceProductionContext, mapCalls) =>
            {
                var destinations = GetUniqueSupportedDestinations(mapCalls);
                if (!destinations.IsEmpty)
                {
                    sourceProductionContext.AddSource(
                        "Morphant.DiscoveredDestinations.g.cs",
                        BuildDebugSource(destinations));
                }
            });
    }

    private static bool IsMapperBuilderMapCallCandidate(SyntaxNode node) =>
        node is InvocationExpressionSyntax
        {
            ArgumentList.Arguments.Count: <= 1,
            Expression: MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax
                {
                    TypeArgumentList.Arguments.Count: 2,
                    Identifier.ValueText: "Map"
                }
            }
        };

    private static MapperBuilderMapCallInfo? TryGetMapperBuilderMapCallInfo(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, cancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return null;
        }

        var mapperBuilderType = context.SemanticModel.Compilation
            .GetTypeByMetadataName("Morphant.MapperBuilder");
        if (!IsMapperBuilderMapMethod(method, mapperBuilderType!))
        {
            return null;
        }

        var sourceType = method.TypeArguments[0];
        var destinationType = method.TypeArguments[1];

        return new MapperBuilderMapCallInfo(sourceType, destinationType);
    }

    private static bool IsMapperBuilderMapMethod(IMethodSymbol method, INamedTypeSymbol mapperBuilderType) =>
        method is
        {
            MethodKind: MethodKind.Ordinary,
            Parameters.Length: 1,
            TypeArguments.Length: 2,
            Name: "Map",
            ContainingType: { } containingType
        }
        && SymbolEqualityComparer.Default.Equals(containingType, mapperBuilderType);

    private static ImmutableArray<INamedTypeSymbol> GetUniqueSupportedDestinations(
        ImmutableArray<MapperBuilderMapCallInfo> mapCalls)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var mapCall in mapCalls)
        {
            var destinationType = NormalizeDestinationType(mapCall.DestinationType);

            if (!IsSupportedDestinationType(destinationType))
            {
                continue;
            }

            var namedDestinationType = (INamedTypeSymbol)destinationType;

            var alreadyAdded = builder.Any(x =>
                SymbolEqualityComparer.Default.Equals(x, namedDestinationType));

            if (alreadyAdded)
            {
                continue;
            }

            builder.Add(namedDestinationType);
        }

        return builder.ToImmutable();
    }

    private static ITypeSymbol NormalizeDestinationType(ITypeSymbol destinationType)
    {
        return destinationType.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
    }

    private static bool IsSupportedDestinationType(ITypeSymbol destinationType)
    {
        if (destinationType is not INamedTypeSymbol namedType)
        {
            return false;
        }

        // Пока не поддерживаем generic destination types:
        // ApiResponse<UserModel>, List<UserModel>, etc.
        if (namedType.IsGenericType)
        {
            return false;
        }

        // Пока не поддерживаем tuple destination types.
        if (namedType.IsTupleType)
        {
            return false;
        }

        // Пока не поддерживаем массивы и прочие спец. формы.
        // Массивы обычно не INamedTypeSymbol, но оставляем намерение явно.
        if (destinationType.TypeKind != TypeKind.Class
            && destinationType.TypeKind != TypeKind.Struct
            && destinationType.TypeKind != TypeKind.Interface)
        {
            return false;
        }

        return true;
    }

    private static string BuildDebugSource(
        ImmutableArray<INamedTypeSymbol> destinations)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Morphant.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal static class __MorphantDiscoveredDestinations");
        sb.AppendLine("{");

        foreach (var destination in destinations)
        {
            var displayName = destination.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            sb.Append("    // ");
            sb.AppendLine(displayName);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }
}
