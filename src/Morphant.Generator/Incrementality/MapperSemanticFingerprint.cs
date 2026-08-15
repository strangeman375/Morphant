using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.Incrementality;

internal readonly record struct MapperSemanticFingerprint(
    string Signature,
    ImmutableArray<TypeContractDependency> Dependencies);

internal readonly record struct MapperSemanticInput(
    ClassDeclarationSyntax AttributedDeclaration,
    AttributeSyntax Attribute,
    INamedTypeSymbol MapperType,
    CompilationContext Context,
    MapperSemanticFingerprint Fingerprint);

// Narrows the global CompilationProvider to the declarations and semantic
// contracts that can affect one mapper. An equal fingerprint deliberately
// keeps the matching CompilationContext alive for the cached mapper pipeline.
internal static class MapperSemanticFingerprintBuilder
{
    public static MapperSemanticFingerprint Build(
        ClassDeclarationSyntax attributedDeclaration,
        AttributeSyntax attribute,
        INamedTypeSymbol mapperType,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        var signature = new StringBuilder();
        var dependencyTypes = new DependencyTypeSet();
        dependencyTypes.Add(mapperType);

        AppendCompilationContext(
            signature,
            attributedDeclaration,
            attribute,
            context);

        for (var current = mapperType;
             current is not null;
             current = current.BaseType)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (current.DeclaringSyntaxReferences.IsEmpty)
            {
                continue;
            }

            foreach (var declaration in current.DeclaringSyntaxReferences
                         .Where(reference => context.SyntaxTrees.Contains(
                             reference.SyntaxTree))
                         .Select(reference =>
                             reference.GetSyntax(cancellationToken))
                         .OfType<TypeDeclarationSyntax>()
                         .OrderBy(declaration =>
                             context.SyntaxTrees.GetOrder(
                                 declaration.SyntaxTree))
                         .ThenBy(static declaration =>
                             declaration.SpanStart))
            {
                AppendDeclaration(
                    signature,
                    declaration,
                    context,
                    dependencyTypes,
                    cancellationToken);
            }
        }

        var dependencies = dependencyTypes.Types
            .SelectMany(type => TypeContractDependencies.Build(
                type,
                context.Compilation,
                cancellationToken))
            .GroupBy(
                static dependency => dependency.Identity,
                StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(
                static dependency => dependency.Identity,
                StringComparer.Ordinal)
            .ToImmutableArray();

        return new MapperSemanticFingerprint(
            signature.ToString(),
            dependencies);
    }

    private static void AppendCompilationContext(
        StringBuilder result,
        ClassDeclarationSyntax attributedDeclaration,
        AttributeSyntax attribute,
        CompilationContext context)
    {
        var options = context.Compilation.Options;

        result
            .Append(attributedDeclaration.SyntaxTree.FilePath)
            .Append('|')
            .Append(attribute.SpanStart)
            .Append('|')
            .Append(attribute.Span.Length)
            .Append('|')
            .Append(context.Compilation.Assembly.Identity)
            .Append('|')
            .Append(context.LanguageVersion)
            .Append('|')
            .Append(options.NullableContextOptions)
            .Append('|')
            .Append(options.MetadataImportOptions)
            .Append('|')
            .Append(options.AllowUnsafe)
            .Append('|')
            .Append(options.CheckOverflow)
            .Append('|')
            .Append(options.OutputKind)
            .Append('|')
            .Append(options.Platform)
            .Append('|')
            .Append(context.Compatibility.IsLanguageCompatible)
            .Append('|')
            .Append(context.Compatibility.RuntimeContract.Kind)
            .Append('|')
            .Append(context.Compatibility.RuntimeContract.Reason)
            .AppendLine();
    }

    private static void AppendDeclaration(
        StringBuilder result,
        TypeDeclarationSyntax declaration,
        CompilationContext context,
        DependencyTypeSet dependencyTypes,
        CancellationToken cancellationToken)
    {
        var semanticModel = context.Compilation.GetSemanticModel(
            declaration.SyntaxTree);

        result
            .Append(declaration.SyntaxTree.FilePath)
            .Append('|')
            .Append(declaration.SpanStart)
            .Append('|')
            .Append(declaration.Span.Length)
            .AppendLine();

        foreach (var diagnostic in semanticModel
                     .GetDiagnostics(
                         declaration.FullSpan,
                         cancellationToken)
                     .OrderBy(static diagnostic =>
                         diagnostic.Location.SourceSpan.Start)
                     .ThenBy(static diagnostic => diagnostic.Id))
        {
            result
                .Append(diagnostic.Id)
                .Append('|')
                .Append(diagnostic.Severity)
                .Append('|')
                .Append(diagnostic.WarningLevel)
                .Append('|')
                .Append(diagnostic.Location.SourceSpan.Start)
                .Append('|')
                .Append(diagnostic.Location.SourceSpan.Length)
                .Append('|')
                .Append(diagnostic.IsSuppressed)
                .Append('|')
                .Append(diagnostic.GetMessage(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        foreach (var configureMethod in declaration.Members
                     .OfType<MethodDeclarationSyntax>()
                     .Where(static method =>
                         method.Identifier.ValueText == "Configure"))
        {
            var operation = configureMethod.Body is { } body
                ? semanticModel.GetOperation(body, cancellationToken)
                : configureMethod.ExpressionBody is { } expressionBody
                    ? semanticModel.GetOperation(
                        expressionBody.Expression,
                        cancellationToken)
                    : null;

            if (operation is not null)
            {
                AppendOperations(
                    result,
                    operation,
                    dependencyTypes,
                    cancellationToken);
            }
        }
    }

    private static void AppendOperations(
        StringBuilder result,
        IOperation root,
        DependencyTypeSet dependencyTypes,
        CancellationToken cancellationToken)
    {
        foreach (var operation in root.DescendantsAndSelf())
        {
            cancellationToken.ThrowIfCancellationRequested();

            result
                .Append(operation.Kind)
                .Append('|')
                .Append(operation.Syntax.SpanStart)
                .Append('|')
                .Append(operation.IsImplicit)
                .Append('|');
            AppendType(result, operation.Type, dependencyTypes);
            result.Append('|').Append(FormatConstant(
                operation.ConstantValue));

            var symbol = GetReferencedSymbol(operation);

            if (symbol is not null)
            {
                result.Append('|').Append(symbol.Kind).Append(':')
                    .Append(symbol.ToDisplayString(
                        SymbolDisplayFormat.CSharpErrorMessageFormat));
                AddSymbolDependencies(symbol, dependencyTypes);
            }

            if (operation is IConversionOperation conversion)
            {
                result
                    .Append('|')
                    .Append(conversion.Conversion.Exists)
                    .Append('|')
                    .Append(conversion.Conversion.IsImplicit)
                    .Append('|')
                    .Append(conversion.Conversion.IsIdentity)
                    .Append('|')
                    .Append(conversion.Conversion.IsNumeric)
                    .Append('|')
                    .Append(conversion.Conversion.IsReference)
                    .Append('|')
                    .Append(conversion.Conversion.IsUserDefined);
            }

            result.AppendLine();
        }
    }

    private static ISymbol? GetReferencedSymbol(IOperation operation)
    {
        return operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IObjectCreationOperation creation => creation.Constructor,
            IMethodReferenceOperation method => method.Method,
            IPropertyReferenceOperation property => property.Property,
            IFieldReferenceOperation field => field.Field,
            IEventReferenceOperation eventReference =>
                eventReference.Event,
            IConversionOperation conversion => conversion.OperatorMethod,
            IBinaryOperation binary => binary.OperatorMethod,
            IUnaryOperation unary => unary.OperatorMethod,
            IIncrementOrDecrementOperation increment =>
                increment.OperatorMethod,
            IArgumentOperation argument => argument.Parameter,
            _ => null
        };
    }

    private static void AddSymbolDependencies(
        ISymbol symbol,
        DependencyTypeSet result)
    {
        result.AddNamed(symbol.ContainingType);

        if (symbol is IMethodSymbol method)
        {
            foreach (var typeArgument in method.TypeArguments)
            {
                result.Add(typeArgument);
            }
        }
    }

    private static void AppendType(
        StringBuilder result,
        ITypeSymbol? type,
        DependencyTypeSet dependencyTypes)
    {
        if (type is null)
        {
            result.Append("<unresolved>");
            return;
        }

        result.Append(
            type.ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable));
        dependencyTypes.Add(type);
    }

    private sealed class DependencyTypeSet
    {
        private readonly HashSet<ITypeSymbol> _visited = new(
            SymbolEqualityComparer.Default);

        public HashSet<INamedTypeSymbol> Types { get; } = new(
            SymbolEqualityComparer.Default);

        public void Add(ITypeSymbol type)
        {
            if (!_visited.Add(type))
            {
                return;
            }

            switch (type)
            {
                case INamedTypeSymbol namedType:
                    AddNamed(namedType);

                    foreach (var typeArgument in namedType.TypeArguments)
                    {
                        Add(typeArgument);
                    }

                    break;

                case IArrayTypeSymbol arrayType:
                    Add(arrayType.ElementType);
                    break;

                case IPointerTypeSymbol pointerType:
                    Add(pointerType.PointedAtType);
                    break;

                case ITypeParameterSymbol typeParameter:
                    foreach (var constraint in
                             typeParameter.ConstraintTypes)
                    {
                        Add(constraint);
                    }

                    break;

                case IFunctionPointerTypeSymbol functionPointer:
                    Add(functionPointer.Signature.ReturnType);

                    foreach (var parameter in
                             functionPointer.Signature.Parameters)
                    {
                        Add(parameter.Type);
                    }

                    break;
            }
        }

        public void AddNamed(INamedTypeSymbol? type)
        {
            for (var current = type?.OriginalDefinition;
                 current is not null;
                 current = current.ContainingType)
            {
                Types.Add(current);
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
}

internal sealed class MapperSemanticInputComparer :
    IEqualityComparer<MapperSemanticInput>
{
    public static MapperSemanticInputComparer Instance { get; } = new();

    private MapperSemanticInputComparer()
    {
    }

    public bool Equals(
        MapperSemanticInput left,
        MapperSemanticInput right)
    {
        return StringComparer.Ordinal.Equals(
                   left.Fingerprint.Signature,
                   right.Fingerprint.Signature) &&
               TypeContractDependencies.Equal(
                   left.Fingerprint.Dependencies,
                   right.Fingerprint.Dependencies);
    }

    public int GetHashCode(MapperSemanticInput value)
    {
        var hash = StringComparer.Ordinal.GetHashCode(
            value.Fingerprint.Signature);

        return TypeContractDependencies.AddHash(
            hash,
            value.Fingerprint.Dependencies);
    }
}
