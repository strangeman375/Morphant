using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.Incrementality;

internal static class TypeContractDependencies
{
    public static INamedTypeSymbol? ResolveType(
        CSharpCompilation compilation,
        string assemblyIdentity,
        string metadataName)
    {
        if (StringComparer.Ordinal.Equals(
                compilation.Assembly.Identity.ToString(),
                assemblyIdentity))
        {
            return compilation.Assembly.GetTypeByMetadataName(metadataName);
        }

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is
                    IAssemblySymbol assembly &&
                StringComparer.Ordinal.Equals(
                    assembly.Identity.ToString(),
                    assemblyIdentity))
            {
                return assembly.GetTypeByMetadataName(metadataName);
            }
        }

        return null;
    }

    public static ImmutableArray<TypeContractDependency> Build(
        INamedTypeSymbol type,
        CSharpCompilation compilation,
        CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<TypeContractDependency>();
        var visitedTypes = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);

        AddTypeAndContainingTypeDependencies(
            type,
            compilation,
            result,
            visitedTypes,
            cancellationToken);

        if (type.TypeKind == TypeKind.Interface)
        {
            foreach (var baseInterface in type.AllInterfaces)
            {
                AddTypeAndContainingTypeDependencies(
                    baseInterface,
                    compilation,
                    result,
                    visitedTypes,
                    cancellationToken);
            }
        }
        else
        {
            for (var baseType = type.BaseType;
                 baseType is not null;
                 baseType = baseType.BaseType)
            {
                AddTypeAndContainingTypeDependencies(
                    baseType,
                    compilation,
                    result,
                    visitedTypes,
                    cancellationToken);
            }
        }

        return result.ToImmutable();
    }

    public static bool Equal(
        ImmutableArray<TypeContractDependency> left,
        ImmutableArray<TypeContractDependency> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!StringComparer.Ordinal.Equals(
                    left[index].Identity,
                    right[index].Identity) ||
                !VersionsEqual(
                    left[index].Version,
                    right[index].Version))
            {
                return false;
            }
        }

        return true;
    }

    public static int AddHash(
        int hash,
        ImmutableArray<TypeContractDependency> dependencies)
    {
        foreach (var dependency in dependencies)
        {
            hash = AddHash(hash, dependency.Identity);
            hash = unchecked(
                hash * 31 +
                (dependency.Version is TypeContractSourceVersion source
                    ? source.GetHashCode()
                    : RuntimeHelpers.GetHashCode(dependency.Version)));
        }

        return hash;
    }

    public static int AddHash<T>(int hash, T value)
    {
        return unchecked(
            hash * 31 +
            EqualityComparer<T>.Default.GetHashCode(value!));
    }

    private static void AddTypeAndContainingTypeDependencies(
        INamedTypeSymbol type,
        CSharpCompilation compilation,
        ImmutableArray<TypeContractDependency>.Builder result,
        HashSet<ISymbol> visitedTypes,
        CancellationToken cancellationToken)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();

        for (var current = type.OriginalDefinition;
             current is not null;
             current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        while (containingTypes.Count > 0)
        {
            AddTypeDependencies(
                containingTypes.Pop(),
                compilation,
                result,
                visitedTypes,
                cancellationToken);
        }
    }

    private static void AddTypeDependencies(
        INamedTypeSymbol type,
        CSharpCompilation compilation,
        ImmutableArray<TypeContractDependency>.Builder result,
        HashSet<ISymbol> visitedTypes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!visitedTypes.Add(type))
        {
            return;
        }

        var metadataName = SymbolNameHelper.GetFullMetadataName(type);
        var syntaxReferences = type.DeclaringSyntaxReferences
            .OrderBy(
                static syntaxReference =>
                    syntaxReference.SyntaxTree.FilePath,
                StringComparer.Ordinal)
            .ThenBy(
                static syntaxReference => syntaxReference.Span.Start)
            .ToArray();

        if (syntaxReferences.Length > 0)
        {
            var partIndicesByPath = new Dictionary<string, int>(
                StringComparer.Ordinal);

            foreach (var syntaxReference in syntaxReferences)
            {
                var path = syntaxReference.SyntaxTree.FilePath;

                partIndicesByPath.TryGetValue(path, out var partIndex);
                partIndicesByPath[path] = partIndex + 1;

                result.Add(
                    new TypeContractDependency(
                        metadataName +
                        "|source|" +
                        path +
                        "|" +
                        partIndex.ToString(CultureInfo.InvariantCulture),
                        BuildSourceVersion(
                            syntaxReference,
                            compilation,
                            cancellationToken)));
            }

            return;
        }

        var metadataReference = compilation.GetMetadataReference(
            type.ContainingAssembly);

        if (metadataReference is not null)
        {
            result.Add(
                new TypeContractDependency(
                    metadataName +
                    "|metadata|" +
                    type.ContainingAssembly.Identity,
                    metadataReference));
        }
    }

    private static TypeContractSourceVersion BuildSourceVersion(
        SyntaxReference syntaxReference,
        CSharpCompilation compilation,
        CancellationToken cancellationToken)
    {
        var declaration = syntaxReference.GetSyntax(cancellationToken);
        var parseOptions =
            (CSharpParseOptions)syntaxReference.SyntaxTree.Options;

        return new TypeContractSourceVersion(
            declaration.ToFullString(),
            BuildSemanticContext(
                declaration,
                compilation,
                cancellationToken),
            BuildParseOptionsVersion(parseOptions));
    }

    private static string BuildSemanticContext(
        SyntaxNode declaration,
        CSharpCompilation compilation,
        CancellationToken cancellationToken)
    {
        var semanticModel = compilation.GetSemanticModel(
            declaration.SyntaxTree);
        var result = new StringBuilder();

        foreach (var typeSyntax in declaration
                     .DescendantNodesAndSelf()
                     .OfType<TypeSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var type = semanticModel.GetTypeInfo(
                typeSyntax,
                cancellationToken).Type;

            result
                .Append("type|")
                .Append(typeSyntax.SpanStart - declaration.SpanStart)
                .Append('|');

            if (type is null)
            {
                result.Append("<unresolved>");
            }
            else
            {
                result
                    .Append(
                        type.ToDisplayString(
                            SymbolDisplayFormats
                                .FullyQualifiedNullable))
                    .Append('|')
                    .Append((int)type.NullableAnnotation);
            }

            result.AppendLine();
        }

        foreach (var attribute in declaration
                     .DescendantNodesAndSelf()
                     .OfType<AttributeSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var constructor = semanticModel.GetSymbolInfo(
                attribute,
                cancellationToken).Symbol;

            result
                .Append("attribute|")
                .Append(attribute.SpanStart - declaration.SpanStart)
                .Append('|')
                .Append(
                    constructor?.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat) ??
                    "<unresolved>")
                .AppendLine();
        }

        foreach (var expression in GetConstantExpressions(declaration))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var constant = semanticModel.GetConstantValue(
                expression,
                cancellationToken);
            var type = semanticModel.GetTypeInfo(
                expression,
                cancellationToken).Type;

            result
                .Append("constant|")
                .Append(expression.SpanStart - declaration.SpanStart)
                .Append('|')
                .Append(
                    type?.ToDisplayString(
                        SymbolDisplayFormats
                            .FullyQualifiedNullable) ??
                    "<unresolved>")
                .Append('|')
                .Append(FormatConstant(constant))
                .AppendLine();
        }

        return result.ToString();
    }

    private static IEnumerable<ExpressionSyntax> GetConstantExpressions(
        SyntaxNode declaration)
    {
        foreach (var argument in declaration
                     .DescendantNodesAndSelf()
                     .OfType<AttributeArgumentSyntax>())
        {
            yield return argument.Expression;
        }

        foreach (var parameter in declaration
                     .DescendantNodesAndSelf()
                     .OfType<ParameterSyntax>())
        {
            if (parameter.Default is { } defaultValue)
            {
                yield return defaultValue.Value;
            }
        }
    }

    private static string FormatConstant(Optional<object?> constant)
    {
        if (!constant.HasValue)
        {
            return "<not-constant>";
        }

        if (constant.Value is null)
        {
            return "null";
        }

        return constant.Value.GetType().FullName + ":" +
               (SymbolDisplay.FormatPrimitive(
                    constant.Value,
                    quoteStrings: true,
                    useHexadecimalNumbers: false) ??
                Convert.ToString(
                    constant.Value,
                    CultureInfo.InvariantCulture));
    }

    private static string BuildParseOptionsVersion(
        CSharpParseOptions parseOptions)
    {
        return parseOptions.LanguageVersion + "|" +
               parseOptions.DocumentationMode + "|" +
               parseOptions.Kind + "|" +
               string.Join(
                   ",",
                   parseOptions.PreprocessorSymbolNames.OrderBy(
                       static symbol => symbol,
                       StringComparer.Ordinal));
    }

    private static bool VersionsEqual(object left, object right)
    {
        if (left is TypeContractSourceVersion sourceLeft &&
            right is TypeContractSourceVersion sourceRight)
        {
            return sourceLeft == sourceRight;
        }

        return ReferenceEquals(left, right);
    }
}

internal readonly record struct TypeContractDependency(
    string Identity,
    object Version);

internal readonly record struct TypeContractSourceVersion(
    string Declaration,
    string SemanticContext,
    string ParseOptions);
