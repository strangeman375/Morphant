using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.Compatibility;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.ConstructionSurface.PairConfiguration;

namespace Morphant.Generator.Incrementality;

internal readonly record struct MapperSemanticFingerprint(
    string Signature,
    ImmutableArray<TypeContractDependency> Dependencies);

internal readonly record struct MapperSemanticInput(
    ClassDeclarationSyntax AttributedDeclaration,
    AttributeSyntax Attribute,
    INamedTypeSymbol MapperType,
    CSharpCompilation Compilation,
    MapperSemanticFingerprint Fingerprint);

// Narrows the semantic cohort supplied by ForAttributeWithMetadataName to the
// declarations and contracts that can affect one mapper. An equal fingerprint
// deliberately keeps that entire, internally consistent cohort alive.
internal static class MapperSemanticFingerprintBuilder
{
    public static MapperSemanticFingerprint Build(
        ClassDeclarationSyntax attributedDeclaration,
        AttributeSyntax attribute,
        INamedTypeSymbol mapperType,
        CSharpCompilation compilation,
        LanguageVersion languageVersion,
        CompilationCompatibility compatibility,
        CancellationToken cancellationToken)
    {
        var signature = new StringBuilder();
        var dependencyTypes = new DependencyTypeSet();
        var registrations = new List<IInvocationOperation>();
        var syntaxTrees = new SyntaxTreeOrdering(
            compilation.SyntaxTrees);
        dependencyTypes.Add(mapperType);

        AppendCompilationInputs(
            signature,
            attributedDeclaration,
            attribute,
            compilation,
            languageVersion,
            compatibility);

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
                         .Where(reference => syntaxTrees.Contains(
                             reference.SyntaxTree))
                         .Select(reference =>
                             reference.GetSyntax(cancellationToken))
                         .OfType<TypeDeclarationSyntax>()
                         .OrderBy(declaration =>
                             syntaxTrees.GetOrder(
                                 declaration.SyntaxTree))
                         .ThenBy(static declaration =>
                             declaration.SpanStart))
            {
                AppendDeclaration(
                    signature,
                    declaration,
                    compilation,
                    dependencyTypes,
                    registrations,
                    cancellationToken);
            }
        }

        FlatteningSemanticDependencyBuilder.Add(
            registrations, compilation, mapperType, dependencyTypes, cancellationToken);
        var lookupTypes = new DependencyTypeSet();
        ExtensionLookupDependencies.Add(
            compilation,
            mapperType,
            lookupTypes.Add,
            cancellationToken);

        // Mapper declarations contain the user code we transfer. Other types
        // contribute declaration contracts, not ordinary method bodies/trivia.
        var dependencies = TypeContractDependencies.Build(
                mapperType,
                compilation,
                cancellationToken)
            .Concat(TypeContractDependencies.Build(dependencyTypes.Types, compilation,
                cancellationToken, declarationsOnly: true))
            .Concat(TypeContractDependencies.Build(lookupTypes.Types, compilation,
                cancellationToken, declarationsOnly: true))
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

    private static void AppendCompilationInputs(
        StringBuilder result,
        ClassDeclarationSyntax attributedDeclaration,
        AttributeSyntax attribute,
        CSharpCompilation compilation,
        LanguageVersion languageVersion,
        CompilationCompatibility compatibility)
    {
        var options = compilation.Options;

        result
            .Append(attributedDeclaration.SyntaxTree.FilePath)
            .Append('|')
            .Append(attribute.SpanStart)
            .Append('|')
            .Append(attribute.Span.Length)
            .Append('|')
            .Append(compilation.Assembly.Identity)
            .Append('|')
            .Append(languageVersion)
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
            .Append(compatibility.IsLanguageCompatible)
            .Append('|')
            .Append(compatibility.RuntimeContract.Kind)
            .Append('|')
            .Append(compatibility.RuntimeContract.Reason)
            .AppendLine();
    }

    private static void AppendDeclaration(
        StringBuilder result,
        TypeDeclarationSyntax declaration,
        CSharpCompilation compilation,
        DependencyTypeSet dependencyTypes,
        List<IInvocationOperation> registrations,
        CancellationToken cancellationToken)
    {
        var semanticModel = compilation.GetSemanticModel(
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
                    compilation,
                    dependencyTypes,
                    registrations,
                    cancellationToken);
            }
        }
    }

    private static void AppendOperations(
        StringBuilder result,
        IOperation root,
        CSharpCompilation compilation,
        DependencyTypeSet dependencyTypes,
        List<IInvocationOperation> registrations,
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
                if (symbol is IMethodSymbol { Name: "Map", TypeArguments.Length: 2 } map &&
                    SymbolNameHelper.GetFullMetadataName(map.ContainingType.OriginalDefinition) == MetadataNames.MapperBuilder)
                {
                    if (operation is IInvocationOperation invocation)
                        registrations.Add(invocation);
                    var friends = MappingPair.FriendMappingExtensions.Find(
                        compilation, map.TypeArguments[0], map.TypeArguments[1], operation.Syntax, cancellationToken);
                    result.Append('|').Append(friends.ExactAssemblies);
                    if (!friends.OpenReceivers.IsDefaultOrEmpty)
                        foreach (var receiver in friends.OpenReceivers) result.Append('|').Append(receiver.Key);
                    AppendPairConstraints(result, map.TypeArguments[0], map.TypeArguments[1], compilation);
                }
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

    private static void AppendPairConstraints(StringBuilder result, ITypeSymbol source, ITypeSymbol destination,
        Compilation compilation)
    {
        var parameters = GeneratedTypeNameBuilder.CollectTypeParameters(source, destination);
        if (parameters.IsEmpty) return;

        // Roslyn's unmanagedness check observes transitive instance storage.
        // Only a change to the inferred contract needs to invalidate the mapper.
        var constraints = PairTypeParameterModelBuilder.Build(source, destination, parameters,
            GeneratedTypeNameBuilder.AllocateTypeParameterNames(parameters), compilation);
        foreach (var parameter in constraints)
            result.Append('|').Append(parameter.Name).Append(':').Append(parameter.RequiresNullableAnnotationsDisabled)
                .Append(':').Append(string.Join(", ", parameter.Constraints));
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
        if (symbol.ContainingType is { } containingType)
        {
            result.Add(containingType);
        }

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
