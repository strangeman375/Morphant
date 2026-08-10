using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.TypeMapperGeneration;

internal sealed class ConstructExpressionRewriter : CSharpSyntaxRewriter
{
    private const string CallerArgumentExpressionAttributeMetadataName =
        "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute";

    private const string CallerFilePathAttributeMetadataName =
        "System.Runtime.CompilerServices.CallerFilePathAttribute";

    private const string CallerLineNumberAttributeMetadataName =
        "System.Runtime.CompilerServices.CallerLineNumberAttribute";

    private const string CallerMemberNameAttributeMetadataName =
        "System.Runtime.CompilerServices.CallerMemberNameAttribute";

    private readonly SemanticModel _semanticModel;
    private readonly INamedTypeSymbol _mapperType;
    private readonly INamedTypeSymbol _semanticMapperType;
    private readonly IParameterSymbol _sourceParameter;
    private readonly string _sourceName;
    private readonly IParameterSymbol? _previousParameter;
    private readonly PreviousExpressionSubstitution? _previousSubstitution;
    private readonly IParameterSymbol? _resultParameter;
    private readonly string? _resultName;
    private readonly IParameterSymbol? _contextParameter;
    private readonly string? _contextName;
    private readonly SyntaxNode _transferScope;
    private readonly IReadOnlyDictionary<ISymbol, string>?
        _localSubstitutions;
    private readonly IReadOnlyDictionary<SyntaxNode, SyntaxAnnotation>?
        _dependencyAnnotations;
    private readonly IReadOnlyDictionary<
        InvocationExpressionSyntax,
        TypeMapperNestedMapExpressionModel>? _nestedMapMappings;
    private readonly IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
        _mapperTypeSubstitutions;
    private readonly bool _lowerDeclarativeValues;
    private readonly HashSet<string> _usedGeneratedNames;

    private ConstructExpressionRewriter(
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        IParameterSymbol? contextParameter,
        string? contextName,
        SyntaxNode transferScope,
        IReadOnlyDictionary<ISymbol, string>? localSubstitutions,
        IReadOnlyDictionary<SyntaxNode, SyntaxAnnotation>?
            dependencyAnnotations,
        IReadOnlyDictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel>? nestedMapMappings,
        bool lowerDeclarativeValues)
    {
        _semanticModel = semanticModel;
        _mapperType = mapperType;
        _semanticMapperType = semanticModel.Compilation
                .GetTypeByMetadataName(
                    SymbolNameHelper.GetFullMetadataName(mapperType)) ??
            mapperType;
        _sourceParameter = sourceParameter;
        _sourceName = sourceName;
        _previousParameter = previousParameter;
        _previousSubstitution = previousSubstitution;
        _resultParameter = resultParameter;
        _resultName = resultName;
        _contextParameter = contextParameter;
        _contextName = contextName;
        _transferScope = transferScope;
        _localSubstitutions = localSubstitutions;
        _dependencyAnnotations = dependencyAnnotations;
        _nestedMapMappings = nestedMapMappings;
        _lowerDeclarativeValues = lowerDeclarativeValues;
        _mapperTypeSubstitutions =
            MapperTypeSubstitution.BuildForHierarchy(
                _semanticMapperType);
        _usedGeneratedNames = new HashSet<string>(
            transferScope.DescendantTokens()
                .Where(token => token.IsKind(
                    SyntaxKind.IdentifierToken))
                .Select(token => token.ValueText),
            StringComparer.Ordinal)
        {
            sourceName
        };

        if (resultName is not null)
        {
            _usedGeneratedNames.Add(resultName);
        }

        if (contextName is not null)
        {
            _usedGeneratedNames.Add(contextName);
        }
    }

    public static bool TryRewrite(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        SyntaxNode transferScope,
        CancellationToken cancellationToken,
        out string rewrittenExpression)
    {
        return TryRewrite(
            expression,
            semanticModel,
            mapperType,
            sourceParameter,
            sourceName,
            previousParameter,
            previousSubstitution,
            resultParameter: null,
            resultName: null,
            transferScope,
            localSubstitutions: null,
            cancellationToken,
            out rewrittenExpression);
    }

    public static bool TryRewrite(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        SyntaxNode transferScope,
        CancellationToken cancellationToken,
        out string rewrittenExpression)
    {
        return TryRewrite(
            expression,
            semanticModel,
            mapperType,
            sourceParameter,
            sourceName,
            previousParameter,
            previousSubstitution,
            resultParameter,
            resultName,
            transferScope,
            localSubstitutions: null,
            cancellationToken,
            out rewrittenExpression);
    }

    public static bool TryRewrite(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        SyntaxNode transferScope,
        IReadOnlyDictionary<ISymbol, string>? localSubstitutions,
        CancellationToken cancellationToken,
        out string rewrittenExpression)
    {
        if (!TryRewriteSyntax(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                sourceName,
                previousParameter,
                previousSubstitution,
                resultParameter,
                resultName,
                transferScope,
                localSubstitutions,
                cancellationToken,
                out var rewritten))
        {
            rewrittenExpression = string.Empty;
            return false;
        }

        rewrittenExpression = rewritten
            .WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString();
        return true;
    }

    public static bool TryRewriteWithContext(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        IParameterSymbol? contextParameter,
        string? contextName,
        SyntaxNode transferScope,
        IReadOnlyDictionary<ISymbol, string>? localSubstitutions,
        CancellationToken cancellationToken,
        out string rewrittenExpression)
    {
        if (!TryRewriteSyntaxWithContext(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                sourceName,
                previousParameter,
                previousSubstitution,
                resultParameter,
                resultName,
                contextParameter,
                contextName,
                transferScope,
                localSubstitutions,
                cancellationToken,
                out var rewritten))
        {
            rewrittenExpression = string.Empty;
            return false;
        }

        rewrittenExpression = rewritten
            .WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString();
        return true;
    }

    public static bool TryRewriteSyntax<TNode>(
        TNode syntax,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        SyntaxNode transferScope,
        CancellationToken cancellationToken,
        out TNode rewrittenSyntax)
        where TNode : CSharpSyntaxNode
    {
        return TryRewriteSyntax(
            syntax,
            semanticModel,
            mapperType,
            sourceParameter,
            sourceName,
            previousParameter,
            previousSubstitution,
            resultParameter: null,
            resultName: null,
            transferScope,
            localSubstitutions: null,
            cancellationToken,
            out rewrittenSyntax);
    }

    public static bool TryRewriteSyntax<TNode>(
        TNode syntax,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        SyntaxNode transferScope,
        CancellationToken cancellationToken,
        out TNode rewrittenSyntax)
        where TNode : CSharpSyntaxNode
    {
        return TryRewriteSyntax(
            syntax,
            semanticModel,
            mapperType,
            sourceParameter,
            sourceName,
            previousParameter,
            previousSubstitution,
            resultParameter,
            resultName,
            transferScope,
            localSubstitutions: null,
            cancellationToken,
            out rewrittenSyntax);
    }

    public static bool TryRewriteSyntax<TNode>(
        TNode syntax,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        SyntaxNode transferScope,
        IReadOnlyDictionary<ISymbol, string>? localSubstitutions,
        CancellationToken cancellationToken,
        out TNode rewrittenSyntax)
        where TNode : CSharpSyntaxNode
    {
        if (!HasOnlyTransferableCaptures(
                syntax,
                transferScope,
                semanticModel,
                sourceParameter,
                previousParameter,
                resultParameter,
                contextParameter: null,
                nestedMapMappings: null,
                cancellationToken))
        {
            rewrittenSyntax = null!;
            return false;
        }

        rewrittenSyntax =
            (TNode)new ConstructExpressionRewriter(
                    semanticModel,
                    mapperType,
                    sourceParameter,
                    sourceName,
                    previousParameter,
                    previousSubstitution,
                    resultParameter,
                    resultName,
                    contextParameter: null,
                    contextName: null,
                    transferScope,
                    localSubstitutions,
                    dependencyAnnotations: null,
                    nestedMapMappings: null,
                    lowerDeclarativeValues: false)
                .Visit(syntax)!;
        return true;
    }

    public static bool TryRewriteSyntaxWithContext<TNode>(
        TNode syntax,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        IParameterSymbol? contextParameter,
        string? contextName,
        SyntaxNode transferScope,
        IReadOnlyDictionary<ISymbol, string>? localSubstitutions,
        CancellationToken cancellationToken,
        out TNode rewrittenSyntax)
        where TNode : CSharpSyntaxNode
    {
        if (!HasOnlyTransferableCaptures(
                syntax,
                transferScope,
                semanticModel,
                sourceParameter,
                previousParameter,
                resultParameter,
                contextParameter,
                nestedMapMappings: null,
                cancellationToken))
        {
            rewrittenSyntax = null!;
            return false;
        }

        rewrittenSyntax =
            (TNode)new ConstructExpressionRewriter(
                    semanticModel,
                    mapperType,
                    sourceParameter,
                    sourceName,
                    previousParameter,
                    previousSubstitution,
                    resultParameter,
                    resultName,
                    contextParameter,
                    contextName,
                    transferScope,
                    localSubstitutions,
                    dependencyAnnotations: null,
                    nestedMapMappings: null,
                    lowerDeclarativeValues: false)
                .Visit(syntax)!;
        return true;
    }

    internal static bool TryRewriteSyntaxWithAnnotationsAndContext(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        IParameterSymbol? contextParameter,
        string? contextName,
        SyntaxNode transferScope,
        IReadOnlyDictionary<ISymbol, string>? localSubstitutions,
        IReadOnlyDictionary<SyntaxNode, SyntaxAnnotation>
            dependencyAnnotations,
        IReadOnlyDictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel> nestedMapMappings,
        CancellationToken cancellationToken,
        out ExpressionSyntax rewrittenExpression)
    {
        if (!HasOnlyTransferableCaptures(
                expression,
                transferScope,
                semanticModel,
                sourceParameter,
                previousParameter,
                resultParameter,
                contextParameter,
                nestedMapMappings,
                cancellationToken))
        {
            rewrittenExpression = null!;
            return false;
        }

        rewrittenExpression =
            (ExpressionSyntax)new ConstructExpressionRewriter(
                    semanticModel,
                    mapperType,
                    sourceParameter,
                    sourceName,
                    previousParameter,
                    previousSubstitution,
                    resultParameter,
                    resultName,
                    contextParameter,
                    contextName,
                    transferScope,
                    localSubstitutions,
                    dependencyAnnotations,
                    nestedMapMappings,
                    lowerDeclarativeValues: true)
                .Visit(expression)!;
        return true;
    }

    internal static bool TryRewriteSyntaxWithAnnotations(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        SyntaxNode transferScope,
        IReadOnlyDictionary<ISymbol, string>? localSubstitutions,
        IReadOnlyDictionary<SyntaxNode, SyntaxAnnotation>
            dependencyAnnotations,
        IReadOnlyDictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel> nestedMapMappings,
        CancellationToken cancellationToken,
        out ExpressionSyntax rewrittenExpression)
    {
        if (!HasOnlyTransferableCaptures(
                expression,
                transferScope,
                semanticModel,
                sourceParameter,
                previousParameter,
                resultParameter,
                contextParameter: null,
                nestedMapMappings,
                cancellationToken))
        {
            rewrittenExpression = null!;
            return false;
        }

        rewrittenExpression =
            (ExpressionSyntax)new ConstructExpressionRewriter(
                    semanticModel,
                    mapperType,
                    sourceParameter,
                    sourceName,
                    previousParameter,
                    previousSubstitution,
                    resultParameter,
                    resultName,
                    contextParameter: null,
                    contextName: null,
                    transferScope,
                    localSubstitutions,
                    dependencyAnnotations,
                    nestedMapMappings,
                    lowerDeclarativeValues: true)
                .Visit(expression)!;
        return true;
    }

    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        var rewritten = base.Visit(node);

        return node is not null &&
               rewritten is not null &&
               _dependencyAnnotations is not null &&
               _dependencyAnnotations.TryGetValue(
                   node,
                   out var annotation)
            ? rewritten.WithAdditionalAnnotations(annotation)
            : rewritten;
    }

    public override SyntaxNode? VisitInvocationExpression(
        InvocationExpressionSyntax node)
    {
        if (_lowerDeclarativeValues &&
            DeclarativeIntrinsic.TryGetKind(
                node,
                _semanticModel,
                default,
                out var intrinsicKind,
                out _) &&
            intrinsicKind == DeclarativeIntrinsicKind.Value &&
            _semanticModel.GetOperation(node) is IInvocationOperation
            {
                TargetMethod:
                {
                    IsGenericMethod: true,
                    TypeArguments.Length: 1
                } valueMethod,
                Arguments: var valueArguments
            } &&
            valueArguments.FirstOrDefault(argument =>
                argument.Parameter?.Name == "value")?.Syntax is
                ArgumentSyntax valueArgument)
        {
            var valueType = SubstituteMapperType(
                valueMethod.TypeArguments[0]
                    .WithNullableAnnotation(
                        valueMethod
                            .TypeArgumentNullableAnnotations[0]));
            var rewrittenValue = (ExpressionSyntax)
                Visit(valueArgument.Expression)!;

            return SyntaxFactory.CastExpression(
                    SyntaxFactory.ParseTypeName(
                        TypeMapperMappingTypePolicy
                            .GetGeneratedTypeName(valueType)),
                    SyntaxFactory.ParenthesizedExpression(
                        rewrittenValue.WithoutTrivia()))
                .WithTriviaFrom(node);
        }

        if (_nestedMapMappings is not null &&
            _nestedMapMappings.TryGetValue(
                node,
                out var nestedMap))
        {
            var method = SyntaxFactory.ParseExpression(
                "context.Mapper.Map<" +
                nestedMap.SourceTypeName +
                ", " +
                nestedMap.DestinationTypeName +
                ">");
            SeparatedSyntaxList<ArgumentSyntax> arguments;

            if (nestedMap.InferredSourceMemberName is { } sourceMember)
            {
                arguments = SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(
                        SyntaxFactory.ParseExpression(
                            _sourceName + "." +
                            Identifier(sourceMember))));
            }
            else if (nestedMap.GuardNullDestination)
            {
                if (GetSourceArgument(node) is not { } sourceArgument)
                {
                    return node;
                }

                arguments = SyntaxFactory.SingletonSeparatedList(
                    (ArgumentSyntax)Visit(sourceArgument)!);
            }
            else
            {
                arguments = SyntaxFactory.SeparatedList(
                    node.ArgumentList.Arguments.Select(
                        argument => (ArgumentSyntax)Visit(argument)!));
            }

            if (nestedMap.GeneratedDestinationExpression is
                    { } destinationExpression &&
                nestedMap.GeneratedDestinationType is
                    { } destinationType)
            {
                var generatedDestination =
                    nestedMap.GuardNullDestination
                        ? BuildDestinationConversion(
                            nestedMap.GuardVariableName!,
                            destinationType,
                            nestedMap.DestinationType,
                            nestedMap.Operation,
                            nestedMap.RuntimeSourceTypeName,
                            nestedMap.DestinationTypeName,
                            nestedMap.RuntimeDestinationTypeName,
                            nestedMap.CompatibleDestinationName!,
                            nestedMap.IncompatibleDestinationName!,
                            allowNull: false)
                        : BuildDestinationConversion(
                            destinationExpression,
                            destinationType,
                            nestedMap.DestinationType,
                            nestedMap.Operation,
                            nestedMap.RuntimeSourceTypeName,
                            nestedMap.DestinationTypeName,
                            nestedMap.RuntimeDestinationTypeName,
                            nestedMap.CompatibleDestinationName!,
                            nestedMap.IncompatibleDestinationName!,
                            allowNull: true);
                arguments = arguments.Add(
                    SyntaxFactory.Argument(generatedDestination)
                        .WithNameColon(
                            SyntaxFactory.NameColon("destination")));
            }

            var rewrittenInvocation = node
                .WithExpression(method)
                .WithArgumentList(
                    node.ArgumentList.WithArguments(arguments));

            if (nestedMap.GuardNullDestination &&
                nestedMap.GeneratedDestinationExpression is
                    { } guardedDestination &&
                nestedMap.GuardVariableName is { } guardVariable)
            {
                return SyntaxFactory.ParseExpression(
                        guardedDestination +
                        " is { } " +
                        Identifier(guardVariable) +
                        " ? " +
                        rewrittenInvocation.WithoutTrivia()
                            .NormalizeWhitespace()
                            .ToFullString() +
                        " : default(" +
                        nestedMap.DestinationTypeName +
                        ")")
                    .WithTriviaFrom(node);
            }

            return rewrittenInvocation
                .WithTriviaFrom(node);
        }

        if (node.Expression is IdentifierNameSyntax
            {
                Identifier.ValueText: "nameof"
            } &&
            _semanticModel.GetConstantValue(node) is
            {
                HasValue: true,
                Value: string value
            })
        {
            return SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(value))
                .WithTriviaFrom(node);
        }

        if (node.Expression is MemberAccessExpressionSyntax
            {
                Expression: var receiver,
                Name: var methodName
            } &&
            TryGetExtensionMethod(node, methodName) is
                { } extensionMethod)
        {
            var rewrittenReceiver =
                (ExpressionSyntax)Visit(receiver)!;

            return RewriteExtensionInvocation(
                node,
                methodName,
                extensionMethod,
                rewrittenReceiver);
        }

        var rewrittenOrdinaryInvocation =
            (InvocationExpressionSyntax)base
                .VisitInvocationExpression(node)!;
        var rewrittenArguments = rewrittenOrdinaryInvocation
            .ArgumentList.Arguments
            .ToList();

        AppendCallerInfoArguments(
            rewrittenArguments,
            _semanticModel.GetOperation(node) as
                IInvocationOperation);

        return rewrittenOrdinaryInvocation.WithArgumentList(
            rewrittenOrdinaryInvocation.ArgumentList.WithArguments(
                SyntaxFactory.SeparatedList(rewrittenArguments)));
    }

    public override SyntaxNode? VisitConditionalAccessExpression(
        ConditionalAccessExpressionSyntax node)
    {
        if (!TryRewriteConditionalExtensionInvocation(
                node,
                out var condition,
                out var invocation,
                out var resultType) ||
            resultType.SpecialType == SpecialType.System_Void)
        {
            return base.VisitConditionalAccessExpression(node);
        }

        return SyntaxFactory.ConditionalExpression(
                condition,
                invocation,
                SyntaxFactory.DefaultExpression(
                    SyntaxFactory.ParseTypeName(
                        TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                            SubstituteMapperType(resultType)))))
            .WithTriviaFrom(node);
    }

    public override SyntaxNode? VisitExpressionStatement(
        ExpressionStatementSyntax node)
    {
        if (node.Expression is ConditionalAccessExpressionSyntax
                conditionalAccess &&
            (_semanticModel.GetTypeInfo(conditionalAccess).Type ??
             _semanticModel.GetTypeInfo(conditionalAccess).ConvertedType) is
                {
                    SpecialType: SpecialType.System_Void
                } &&
            TryRewriteConditionalExtensionInvocation(
                conditionalAccess,
                out var condition,
                out var invocation,
                out _))
        {
            return SyntaxFactory.IfStatement(
                    condition,
                    SyntaxFactory.ExpressionStatement(invocation))
                .WithTriviaFrom(node);
        }

        return base.VisitExpressionStatement(node);
    }

    private bool TryRewriteConditionalExtensionInvocation(
        ConditionalAccessExpressionSyntax node,
        out ExpressionSyntax condition,
        out InvocationExpressionSyntax invocation,
        out ITypeSymbol resultType)
    {
        if (node.WhenNotNull is not InvocationExpressionSyntax
            {
                Expression: MemberBindingExpressionSyntax
                {
                    Name: var methodName
                }
            } conditionalInvocation ||
            TryGetExtensionMethod(
                conditionalInvocation,
                methodName) is not { } extensionMethod ||
            (_semanticModel.GetTypeInfo(node).Type ??
             _semanticModel.GetTypeInfo(node).ConvertedType) is not
                { } conditionalResultType)
        {
            condition = null!;
            invocation = null!;
            resultType = null!;
            return false;
        }

        var receiverName = UserResultMappingPlanner.AllocateName(
            "conditionalReceiver",
            _usedGeneratedNames);
        var rewrittenReceiver =
            (ExpressionSyntax)Visit(node.Expression)!;
        condition = SyntaxFactory.IsPatternExpression(
            SyntaxFactory.ParenthesizedExpression(
                rewrittenReceiver.WithoutTrivia()),
            SyntaxFactory.RecursivePattern()
                .WithPropertyPatternClause(
                    SyntaxFactory.PropertyPatternClause())
                .WithDesignation(
                    SyntaxFactory.SingleVariableDesignation(
                        SyntaxFactory.Identifier(
                            Identifier(receiverName)))));
        invocation = RewriteExtensionInvocation(
            conditionalInvocation,
            methodName,
            extensionMethod,
            SyntaxFactory.IdentifierName(
                Identifier(receiverName)));
        resultType = conditionalResultType;
        return true;
    }

    private InvocationExpressionSyntax RewriteExtensionInvocation(
        InvocationExpressionSyntax node,
        SimpleNameSyntax methodName,
        IMethodSymbol extensionMethod,
        ExpressionSyntax rewrittenReceiver)
    {
        var rewrittenMethodName =
            (SimpleNameSyntax)Visit(methodName)!;
        var receiverArgument =
            SyntaxFactory.Argument(rewrittenReceiver);

        if (extensionMethod.Parameters[0].RefKind == RefKind.Ref)
        {
            receiverArgument = receiverArgument.WithRefKindKeyword(
                SyntaxFactory.Token(SyntaxKind.RefKeyword));
        }

        var arguments = new List<ArgumentSyntax>
        {
            receiverArgument
        };

        arguments.AddRange(
            node.ArgumentList.Arguments.Select(argument =>
                (ArgumentSyntax)Visit(argument)!));

        AppendCallerInfoArguments(
            arguments,
            _semanticModel.GetOperation(node) as
                IInvocationOperation);

        var containingType = SyntaxFactory.ParseExpression(
            SubstituteMapperType(
                    extensionMethod.ContainingType)
                .ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable));

        return node
            .WithExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    containingType,
                    rewrittenMethodName))
            .WithArgumentList(
                node.ArgumentList.WithArguments(
                    SyntaxFactory.SeparatedList(arguments)))
            .WithTriviaFrom(node);
    }

    public override SyntaxNode? VisitImplicitObjectCreationExpression(
        ImplicitObjectCreationExpressionSyntax node)
    {
        var rewritten =
            (ImplicitObjectCreationExpressionSyntax)base
                .VisitImplicitObjectCreationExpression(node)!;
        var arguments = rewritten.ArgumentList.Arguments.ToList();

        AppendCallerInfoArguments(
            arguments,
            _semanticModel.GetOperation(node) as
                IObjectCreationOperation);

        return rewritten.WithArgumentList(
            rewritten.ArgumentList.WithArguments(
                SyntaxFactory.SeparatedList(arguments)));
    }

    public override SyntaxNode? VisitCastExpression(
        CastExpressionSyntax node)
    {
        if (!_lowerDeclarativeValues)
        {
            return base.VisitCastExpression(node);
        }

        if (!DeclarativeIntrinsic.TryGetWrapperCast(
                node,
                MetadataNames.Member,
                _semanticModel,
                default,
                out _,
                out var targetType) &&
            !DeclarativeIntrinsic.TryGetWrapperCast(
                node,
                MetadataNames.ConstructorParameter,
                _semanticModel,
                default,
                out _,
                out targetType))
        {
            return base.VisitCastExpression(node);
        }

        var rewrittenValue = (ExpressionSyntax)
            Visit(node.Expression)!;

        return SyntaxFactory.CastExpression(
                SyntaxFactory.ParseTypeName(
                    TypeMapperMappingTypePolicy
                        .GetGeneratedTypeName(
                            SubstituteMapperType(targetType))),
                SyntaxFactory.ParenthesizedExpression(
                    rewrittenValue.WithoutTrivia()))
            .WithTriviaFrom(node);
    }

    private ArgumentSyntax? GetSourceArgument(
        InvocationExpressionSyntax invocation)
    {
        return (_semanticModel.GetOperation(invocation) as
                IInvocationOperation)?
            .Arguments.FirstOrDefault(argument =>
                argument.Parameter?.Name == "source")?
            .Syntax as ArgumentSyntax;
    }

    private static void AppendCallerInfoArguments(
        ICollection<ArgumentSyntax> arguments,
        IOperation? operation)
    {
        var operationArguments = operation switch
        {
            IInvocationOperation invocation => invocation.Arguments,
            IObjectCreationOperation creation => creation.Arguments,
            _ => default
        };

        if (operationArguments.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var argument in operationArguments)
        {
            if (argument.ArgumentKind != ArgumentKind.DefaultValue ||
                argument.Parameter is not { } parameter ||
                !HasCallerInfoAttribute(parameter) ||
                !TryBuildCallerInfoExpression(
                    argument.Value,
                    out var expression))
            {
                continue;
            }

            arguments.Add(
                SyntaxFactory.Argument(expression)
                    .WithNameColon(
                        SyntaxFactory.NameColon(
                            SyntaxFactory.IdentifierName(
                                Identifier(parameter.Name)))));
        }
    }

    private static bool HasCallerInfoAttribute(
        IParameterSymbol parameter)
    {
        return parameter.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { } attributeType &&
            SymbolNameHelper.GetFullMetadataName(attributeType) is
                CallerArgumentExpressionAttributeMetadataName or
                CallerFilePathAttributeMetadataName or
                CallerLineNumberAttributeMetadataName or
                CallerMemberNameAttributeMetadataName);
    }

    private static bool TryBuildCallerInfoExpression(
        IOperation operation,
        out ExpressionSyntax expression)
    {
        while (operation is IConversionOperation
               {
                   Operand: var operand
               })
        {
            operation = operand;
        }

        if (operation.ConstantValue is not
            {
                HasValue: true
            } constant)
        {
            expression = null!;
            return false;
        }

        expression = constant.Value switch
        {
            null => SyntaxFactory.LiteralExpression(
                SyntaxKind.NullLiteralExpression),
            string value => SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(value)),
            int value => SyntaxFactory.LiteralExpression(
                SyntaxKind.NumericLiteralExpression,
                SyntaxFactory.Literal(value)),
            _ => null!
        };

        return expression is not null;
    }

    private static ExpressionSyntax BuildDestinationConversion(
        string expression,
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        DeclarativeNestedMapOperation operation,
        string runtimeSourceTypeName,
        string destinationTypeName,
        string runtimeDestinationTypeName,
        string compatibleDestinationName,
        string incompatibleDestinationName,
        bool allowNull)
    {
        if (SymbolEqualityComparer.Default.Equals(
                sourceType,
                destinationType) ||
            string.Equals(
                TypeMapperMappingTypePolicy.GetGeneratedRuntimeTypeName(
                    sourceType),
                runtimeDestinationTypeName,
                StringComparison.Ordinal))
        {
            return SyntaxFactory.ParseExpression(expression);
        }

        var castTypeName = allowNull
            ? BuildMaybeNullTypeName(
                destinationType,
                destinationTypeName)
            : destinationTypeName;

        var expectedType = $"typeof({runtimeDestinationTypeName})";
        var mappingOperation =
            "global::Morphant.Context.MappingOperation." + operation;
        var mappingSourceType = $"typeof({runtimeSourceTypeName})";
        var mappingDestinationType = expectedType;
        var actualTypeExpression = allowNull
            ? Identifier(incompatibleDestinationName) + ".GetType()"
            : expression + ".GetType()";
        var mismatch =
            "throw new global::Morphant.Exceptions." +
            "NestedDestinationTypeMismatchException(" +
            mappingOperation + ", " +
            mappingSourceType + ", " +
            mappingDestinationType + ", " +
            expectedType + ", " +
            actualTypeExpression + ")";

        if (!allowNull)
        {
            return SyntaxFactory.ParseExpression(
                expression + " is " + runtimeDestinationTypeName + " " +
                Identifier(compatibleDestinationName) + " ? " +
                Identifier(compatibleDestinationName) + " : " +
                mismatch);
        }

        var nullResult = destinationType.IsValueType &&
                         destinationType is not INamedTypeSymbol
                         {
                             OriginalDefinition.SpecialType:
                                 SpecialType.System_Nullable_T
                         }
            ? "throw new global::Morphant.Exceptions." +
              "NestedDestinationTypeMismatchException(" +
              mappingOperation + ", " +
              mappingSourceType + ", " +
              mappingDestinationType + ", " +
              expectedType + ", null)"
            : $"default({castTypeName})";

        return SyntaxFactory.ParseExpression(
            expression + " switch { " +
            "null => " + nullResult + ", " +
            runtimeDestinationTypeName + " " +
            Identifier(compatibleDestinationName) + " => " +
            Identifier(compatibleDestinationName) + ", " +
            "var " + Identifier(incompatibleDestinationName) + " => " +
            mismatch + " }");
    }

    private static string BuildMaybeNullTypeName(
        ITypeSymbol type,
        string typeName)
    {
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T)
        {
            return typeName;
        }

        if (type.IsValueType)
        {
            return typeName;
        }

        return TypeMapperMappingTypePolicy.GetGeneratedTypeName(
            type.WithNullableAnnotation(NullableAnnotation.Annotated));
    }

    private static string Identifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
    }

    public override SyntaxNode? VisitObjectCreationExpression(
        ObjectCreationExpressionSyntax node)
    {
        ObjectCreationExpressionSyntax rewritten;

        if (_semanticModel.GetTypeInfo(node).Type is
            INamedTypeSymbol createdType)
        {
            rewritten = node.WithType(
                SyntaxFactory.ParseTypeName(
                    SubstituteMapperType(createdType).ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable)));

            if (node.ArgumentList is { } argumentList)
            {
                rewritten = rewritten.WithArgumentList(
                    (ArgumentListSyntax)Visit(argumentList)!);
            }

            if (node.Initializer is { } initializer)
            {
                rewritten = rewritten.WithInitializer(
                    (InitializerExpressionSyntax)Visit(initializer)!);
            }
        }
        else
        {
            rewritten =
                (ObjectCreationExpressionSyntax)base
                    .VisitObjectCreationExpression(node)!;
        }

        var arguments = rewritten.ArgumentList?.Arguments
            .ToList() ?? [];

        AppendCallerInfoArguments(
            arguments,
            _semanticModel.GetOperation(node) as
                IObjectCreationOperation);

        if (arguments.Count > 0 || rewritten.ArgumentList is not null)
        {
            rewritten = rewritten.WithArgumentList(
                (rewritten.ArgumentList ?? SyntaxFactory.ArgumentList())
                .WithArguments(
                    SyntaxFactory.SeparatedList(arguments)));
        }

        return rewritten.WithTriviaFrom(node);
    }

    public override SyntaxNode? VisitConditionalExpression(
        ConditionalExpressionSyntax node)
    {
        if (TryEvaluateKnownBoolean(node.Condition, out var condition))
        {
            return Visit(
                    condition
                        ? node.WhenTrue
                        : node.WhenFalse)!
                .WithTriviaFrom(node);
        }

        return base.VisitConditionalExpression(node);
    }

    public override SyntaxNode? VisitMemberAccessExpression(
        MemberAccessExpressionSyntax node)
    {
        if (_previousParameter is not null &&
            _previousSubstitution is { } previous &&
            IsPreviousParameterExpression(node.Expression))
        {
            if (node.Name.Identifier.ValueText == "HasValue")
            {
                return SyntaxFactory.ParseExpression(
                        previous.HasValueExpression)
                    .WithTriviaFrom(node);
            }

            if (node.Name.Identifier.ValueText == "Value")
            {
                return SyntaxFactory.ParseExpression(
                        previous.ValueExpression)
                    .WithTriviaFrom(node);
            }
        }

        var symbol = GetReferencedSymbol(node);

        if (symbol is IAliasSymbol
            {
                Target: INamedTypeSymbol aliasType
            })
        {
            symbol = aliasType;
        }

        if (symbol is INamedTypeSymbol type)
        {
            return SyntaxFactory.ParseExpression(
                    SubstituteMapperType(type).ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable))
                .WithTriviaFrom(node);
        }

        return base.VisitMemberAccessExpression(node);
    }

    public override SyntaxNode? VisitDeclarationPattern(
        DeclarationPatternSyntax node)
    {
        return node
            .WithType(RewriteType(node.Type))
            .WithDesignation(
                (VariableDesignationSyntax)Visit(node.Designation)!);
    }

    public override SyntaxNode? VisitRecursivePattern(
        RecursivePatternSyntax node)
    {
        return node
            .WithType(
                node.Type is { } type
                    ? RewriteType(type)
                    : null)
            .WithPositionalPatternClause(
                node.PositionalPatternClause is { } positional
                    ? (PositionalPatternClauseSyntax)Visit(positional)!
                    : null)
            .WithPropertyPatternClause(
                node.PropertyPatternClause is { } property
                    ? (PropertyPatternClauseSyntax)Visit(property)!
                    : null)
            .WithDesignation(
                node.Designation is { } designation
                    ? (VariableDesignationSyntax)Visit(designation)!
                    : null);
    }

    public override SyntaxNode? VisitTypePattern(
        TypePatternSyntax node)
    {
        return node.WithType(RewriteType(node.Type));
    }

    public override SyntaxNode? VisitVariableDeclaration(
        VariableDeclarationSyntax node)
    {
        if (node.Type.IsVar ||
            node.Type is RefTypeSyntax
            {
                Type: var referencedType
            } && referencedType.IsVar)
        {
            return node.WithVariables(VisitList(node.Variables));
        }

        return node
            .WithType(RewriteType(node.Type))
            .WithVariables(VisitList(node.Variables));
    }

    public override SyntaxNode? VisitLocalFunctionStatement(
        LocalFunctionStatementSyntax node)
    {
        if (_semanticModel.GetDeclaredSymbol(node) is not
            IMethodSymbol function)
        {
            return base.VisitLocalFunctionStatement(node);
        }

        var returnType = SyntaxFactory.ParseTypeName(
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    function.ReturnType.WithNullableAnnotation(
                        function.ReturnNullableAnnotation)))
            .WithTriviaFrom(node.ReturnType);

        if (node.ReturnType is RefTypeSyntax refReturnType)
        {
            returnType = refReturnType.WithType(returnType);
        }

        var parameters = node.ParameterList.Parameters
            .Select((parameter, index) =>
            {
                var parameterSymbol = function.Parameters[index];
                var rewritten = parameter.WithType(
                    SyntaxFactory.ParseTypeName(
                            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                                parameterSymbol.Type.WithNullableAnnotation(
                                    parameterSymbol.NullableAnnotation)))
                        .WithTriviaFrom(parameter.Type!));

                return parameter.Default is { } defaultValue
                    ? rewritten.WithDefault(
                        defaultValue.WithValue(
                            (ExpressionSyntax)Visit(defaultValue.Value)!))
                    : rewritten;
            });
        var constraints = node.ConstraintClauses.Select(clause =>
            clause.WithConstraints(
                SyntaxFactory.SeparatedList(
                    clause.Constraints.Select(RewriteConstraint))));

        return node
            .WithReturnType(returnType)
            .WithParameterList(
                node.ParameterList.WithParameters(
                    SyntaxFactory.SeparatedList(parameters)))
            .WithConstraintClauses(SyntaxFactory.List(constraints))
            .WithBody(
                node.Body is null
                    ? null
                    : (BlockSyntax)Visit(node.Body)!)
            .WithExpressionBody(
                node.ExpressionBody is null
                    ? null
                    : node.ExpressionBody.WithExpression(
                        (ExpressionSyntax)Visit(
                            node.ExpressionBody.Expression)!));
    }

    public override SyntaxNode? VisitIdentifierName(
        IdentifierNameSyntax node)
    {
        var symbol = GetReferencedSymbol(node);

        if (symbol is not null &&
            _localSubstitutions is not null &&
            _localSubstitutions.TryGetValue(
                symbol,
                out var localName))
        {
            return SyntaxFactory.IdentifierName(localName)
                .WithTriviaFrom(node);
        }

        if (symbol is IMethodSymbol
            {
                MethodKind: MethodKind.LocalFunction
            })
        {
            return node;
        }

        if (SymbolEqualityComparer.Default.Equals(
                symbol,
                _sourceParameter))
        {
            return SyntaxFactory.IdentifierName(_sourceName)
                .WithTriviaFrom(node);
        }

        if (_previousParameter is not null &&
            SymbolEqualityComparer.Default.Equals(
                symbol,
                _previousParameter))
        {
            return SyntaxFactory.ParseExpression(
                    _previousSubstitution?.OptionExpression ??
                    node.Identifier.Text)
                .WithTriviaFrom(node);
        }

        if (_resultParameter is not null &&
            SymbolEqualityComparer.Default.Equals(
                symbol,
                _resultParameter))
        {
            return SyntaxFactory.IdentifierName(
                    _resultName ?? node.Identifier.Text)
                .WithTriviaFrom(node);
        }

        if (_contextParameter is not null &&
            SymbolEqualityComparer.Default.Equals(
                symbol,
                _contextParameter))
        {
            return SyntaxFactory.IdentifierName(
                    _contextName ?? node.Identifier.Text)
                .WithTriviaFrom(node);
        }

        if (symbol is ILocalSymbol
            {
                IsConst: true,
                HasConstantValue: true
            } constantLocal &&
            !IsDeclaredWithin(constantLocal, _transferScope))
        {
            return BuildConstantExpression(constantLocal)
                .WithTriviaFrom(node);
        }

        if (symbol is IAliasSymbol
            {
                Target: INamedTypeSymbol aliasType
            })
        {
            symbol = aliasType;
        }

        if (symbol is ITypeSymbol type)
        {
            if (node.Parent is MemberAccessExpressionSyntax
                {
                    Name: var memberName
                } &&
                ReferenceEquals(memberName, node))
            {
                return node;
            }

            return SyntaxFactory.ParseTypeName(
                    SubstituteMapperType(type).ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable))
                .WithTriviaFrom(node);
        }

        if (symbol is { IsStatic: true, ContainingType: { } staticType } &&
            symbol is not INamedTypeSymbol &&
            !IsMemberName(node))
        {
            var containingType = IsMapperMember(symbol)
                ? _mapperType
                : (INamedTypeSymbol)SubstituteMapperType(staticType);

            return SyntaxFactory.ParseExpression(
                    containingType.ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable) +
                    "." +
                    node.Identifier.Text)
                .WithTriviaFrom(node);
        }

        if (symbol is not null &&
            IsMapperInstanceMember(symbol) &&
            !IsMemberName(node))
        {
            return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(),
                    node.WithoutTrivia())
                .WithTriviaFrom(node);
        }

        return base.VisitIdentifierName(node);
    }

    public override SyntaxNode? VisitSingleVariableDesignation(
        SingleVariableDesignationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);

        return symbol is not null &&
               _localSubstitutions is not null &&
               _localSubstitutions.TryGetValue(
                   symbol,
                   out var localName)
            ? node.WithIdentifier(
                SyntaxFactory.Identifier(localName)
                    .WithTriviaFrom(node.Identifier))
            : base.VisitSingleVariableDesignation(node);
    }

    public override SyntaxNode? VisitFromClause(
        FromClauseSyntax node)
    {
        var rewritten = (FromClauseSyntax)base.VisitFromClause(node)!;
        return rewritten.WithIdentifier(
            RewriteDeclaredIdentifier(node, rewritten.Identifier));
    }

    public override SyntaxNode? VisitLetClause(
        LetClauseSyntax node)
    {
        var rewritten = (LetClauseSyntax)base.VisitLetClause(node)!;
        return rewritten.WithIdentifier(
            RewriteDeclaredIdentifier(node, rewritten.Identifier));
    }

    public override SyntaxNode? VisitJoinClause(
        JoinClauseSyntax node)
    {
        var rewritten = (JoinClauseSyntax)base.VisitJoinClause(node)!;
        return rewritten.WithIdentifier(
            RewriteDeclaredIdentifier(node, rewritten.Identifier));
    }

    public override SyntaxNode? VisitJoinIntoClause(
        JoinIntoClauseSyntax node)
    {
        var rewritten =
            (JoinIntoClauseSyntax)base.VisitJoinIntoClause(node)!;
        return rewritten.WithIdentifier(
            RewriteDeclaredIdentifier(node, rewritten.Identifier));
    }

    public override SyntaxNode? VisitQueryContinuation(
        QueryContinuationSyntax node)
    {
        var rewritten =
            (QueryContinuationSyntax)base.VisitQueryContinuation(node)!;
        return rewritten.WithIdentifier(
            RewriteDeclaredIdentifier(node, rewritten.Identifier));
    }

    private SyntaxToken RewriteDeclaredIdentifier(
        SyntaxNode declaration,
        SyntaxToken identifier)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(declaration);

        return symbol is not null &&
               _localSubstitutions is not null &&
               _localSubstitutions.TryGetValue(
                   symbol,
                   out var localName)
            ? SyntaxFactory.Identifier(localName)
                .WithTriviaFrom(identifier)
            : identifier;
    }

    public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    {
        var symbol = GetReferencedSymbol(node);
        var rewrittenName = node.WithTypeArgumentList(
            node.TypeArgumentList.WithArguments(
                SyntaxFactory.SeparatedList(
                    node.TypeArgumentList.Arguments.Select(
                        RewriteType))));

        if (symbol is IMethodSymbol
            {
                MethodKind: MethodKind.LocalFunction
            })
        {
            return rewrittenName;
        }

        if (symbol is INamedTypeSymbol type)
        {
            return SyntaxFactory.ParseTypeName(
                    SubstituteMapperType(type).ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable))
                .WithTriviaFrom(node);
        }

        if (symbol is { IsStatic: true, ContainingType: { } staticType } &&
            !IsMemberName(node))
        {
            var containingType = IsMapperMember(symbol)
                ? _mapperType
                : (INamedTypeSymbol)SubstituteMapperType(staticType);
            return SyntaxFactory.ParseExpression(
                    containingType.ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable) +
                    "." +
                    rewrittenName.WithoutTrivia().ToFullString())
                .WithTriviaFrom(node);
        }

        if (symbol is not null &&
            IsMapperInstanceMember(symbol) &&
            !IsMemberName(node))
        {
            return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(),
                    rewrittenName.WithoutTrivia())
                .WithTriviaFrom(node);
        }

        return base.VisitGenericName(node);
    }

    private bool IsMapperInstanceMember(ISymbol symbol)
    {
        if (symbol.IsStatic ||
            symbol is not (IFieldSymbol or IPropertySymbol or
                IEventSymbol or IMethodSymbol))
        {
            return false;
        }

        return IsMapperMember(symbol);
    }

    private TypeSyntax RewriteType(TypeSyntax syntax)
    {
        return _semanticModel.GetTypeInfo(syntax).Type is { } type
            ? SyntaxFactory.ParseTypeName(
                    TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                        SubstituteMapperType(type)))
                .WithTriviaFrom(syntax)
            : (TypeSyntax)base.Visit(syntax)!;
    }

    private ITypeSymbol SubstituteMapperType(ITypeSymbol type)
    {
        return MapperTypeSubstitution.Substitute(
            type,
            _mapperTypeSubstitutions,
            _semanticModel.Compilation);
    }

    private TypeParameterConstraintSyntax RewriteConstraint(
        TypeParameterConstraintSyntax constraint)
    {
        return constraint is TypeConstraintSyntax typeConstraint
            ? typeConstraint.WithType(
                RewriteType(typeConstraint.Type))
            : constraint;
    }

    private bool IsMapperMember(ISymbol symbol)
    {
        for (var type = _semanticMapperType;
             type is not null;
             type = type.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    type,
                    symbol.ContainingType))
            {
                return true;
            }
        }

        return false;
    }

    private IMethodSymbol? TryGetExtensionMethod(
        InvocationExpressionSyntax invocation,
        SimpleNameSyntax methodName)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(invocation);
        var method = symbolInfo.Symbol as IMethodSymbol ??
                     GetReferencedSymbol(methodName) as IMethodSymbol;

        if (method is null)
        {
            return null;
        }

        return method.ReducedFrom ??
               (method.IsExtensionMethod ? method : null);
    }

    private ISymbol? GetReferencedSymbol(SyntaxNode node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);

        if (symbolInfo.Symbol is { } symbol)
        {
            return symbol;
        }

        if (symbolInfo.CandidateSymbols.IsEmpty)
        {
            return null;
        }

        var candidate = symbolInfo.CandidateSymbols[0];

        return symbolInfo.CandidateSymbols.All(other =>
                other.IsStatic == candidate.IsStatic &&
                other.Name == candidate.Name &&
                SymbolEqualityComparer.Default.Equals(
                    other.ContainingType,
                    candidate.ContainingType))
            ? candidate
            : null;
    }

    private bool IsPreviousParameterExpression(
        ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;

                case PostfixUnaryExpressionSyntax
                {
                    RawKind:
                        (int)SyntaxKind.SuppressNullableWarningExpression,
                    Operand: var operand
                }:
                    expression = operand;
                    continue;
            }

            break;
        }

        return expression is IdentifierNameSyntax identifier &&
               SymbolEqualityComparer.Default.Equals(
                   GetReferencedSymbol(identifier),
                   _previousParameter);
    }

    private bool TryEvaluateKnownBoolean(
        ExpressionSyntax expression,
        out bool value)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        if (_semanticModel.GetConstantValue(expression) is
            {
                HasValue: true,
                Value: bool constant
            })
        {
            value = constant;
            return true;
        }

        if (_previousSubstitution is { } previous &&
            expression is MemberAccessExpressionSyntax
            {
                Expression: var receiver,
                Name.Identifier.ValueText: "HasValue"
            } &&
            IsPreviousParameterExpression(receiver) &&
            bool.TryParse(
                previous.HasValueExpression,
                out value))
        {
            return true;
        }

        if (expression is PrefixUnaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.LogicalNotExpression,
                Operand: var operand
            } &&
            TryEvaluateKnownBoolean(operand, out var operandValue))
        {
            value = !operandValue;
            return true;
        }

        if (expression is BinaryExpressionSyntax binary &&
            TryEvaluateKnownBoolean(
                binary.Left,
                out var leftValue))
        {
            switch ((SyntaxKind)binary.RawKind)
            {
                case SyntaxKind.LogicalAndExpression
                    when !leftValue:
                    value = false;
                    return true;

                case SyntaxKind.LogicalOrExpression
                    when leftValue:
                    value = true;
                    return true;
            }

            if (TryEvaluateKnownBoolean(
                    binary.Right,
                    out var rightValue))
            {
                switch ((SyntaxKind)binary.RawKind)
                {
                    case SyntaxKind.LogicalAndExpression:
                        value = leftValue && rightValue;
                        return true;

                    case SyntaxKind.LogicalOrExpression:
                        value = leftValue || rightValue;
                        return true;

                    case SyntaxKind.EqualsExpression:
                        value = leftValue == rightValue;
                        return true;

                    case SyntaxKind.NotEqualsExpression:
                        value = leftValue != rightValue;
                        return true;
                }
            }
        }

        value = false;
        return false;
    }

    private static bool HasOnlyTransferableCaptures(
        SyntaxNode expression,
        SyntaxNode transferScope,
        SemanticModel semanticModel,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        IParameterSymbol? resultParameter,
        IParameterSymbol? contextParameter,
        IReadOnlyDictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel>? nestedMapMappings,
        CancellationToken cancellationToken)
    {
        if (!DeclarativeQueryExpressionPolicy.IsSupported(
                expression,
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        foreach (var name in expression
                     .DescendantNodesAndSelf()
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (name.Parent is NameColonSyntax)
            {
                continue;
            }

            if (IsGeneratedDestinationReference(
                    name,
                    nestedMapMappings,
                    semanticModel,
                    cancellationToken))
            {
                continue;
            }

            var symbol = semanticModel.GetSymbolInfo(
                    name,
                    cancellationToken)
                .Symbol;

            if (symbol is null &&
                name.Parent is InvocationExpressionSyntax invocation &&
                ReferenceEquals(invocation.Expression, name))
            {
                symbol = semanticModel.GetSymbolInfo(
                        invocation,
                        cancellationToken)
                    .Symbol;
            }

            if (ContainsFileLocalSymbol(symbol))
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(
                    symbol,
                    sourceParameter) ||
                previousParameter is not null &&
                SymbolEqualityComparer.Default.Equals(
                    symbol,
                    previousParameter) ||
                resultParameter is not null &&
                SymbolEqualityComparer.Default.Equals(
                    symbol,
                    resultParameter) ||
                contextParameter is not null &&
                SymbolEqualityComparer.Default.Equals(
                    symbol,
                    contextParameter))
            {
                continue;
            }

            if (symbol is ILocalSymbol
                {
                    IsConst: true
                })
            {
                continue;
            }

            if (symbol is ILocalSymbol or IParameterSymbol ||
                symbol is IMethodSymbol
                {
                    MethodKind: MethodKind.LocalFunction
                })
            {
                if (!IsDeclaredWithin(symbol, transferScope))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ContainsFileLocalSymbol(ISymbol? symbol)
    {
        if (symbol is IAliasSymbol alias)
        {
            symbol = alias.Target;
        }

        if (ContainsFileLocalType(symbol?.ContainingType))
        {
            return true;
        }

        return symbol switch
        {
            ITypeSymbol type => ContainsFileLocalType(type),
            IFieldSymbol field => ContainsFileLocalType(field.Type),
            IPropertySymbol property => ContainsFileLocalType(property.Type),
            IEventSymbol @event => ContainsFileLocalType(@event.Type),
            ILocalSymbol local => ContainsFileLocalType(local.Type),
            IParameterSymbol parameter =>
                ContainsFileLocalType(parameter.Type),
            IMethodSymbol method =>
                ContainsFileLocalType(method.ReturnType) ||
                method.Parameters.Any(parameter =>
                    ContainsFileLocalType(parameter.Type)) ||
                method.TypeArguments.Any(ContainsFileLocalType),
            _ => false
        };
    }

    private static bool ContainsFileLocalType(ITypeSymbol? type)
    {
        switch (type)
        {
            case null:
                return false;

            case IArrayTypeSymbol array:
                return ContainsFileLocalType(array.ElementType);

            case IPointerTypeSymbol pointer:
                return ContainsFileLocalType(pointer.PointedAtType);

            case IFunctionPointerTypeSymbol functionPointer:
                return ContainsFileLocalType(
                           functionPointer.Signature.ReturnType) ||
                       functionPointer.Signature.Parameters.Any(parameter =>
                           ContainsFileLocalType(parameter.Type));

            case INamedTypeSymbol named:
                for (var current = named;
                     current is not null;
                     current = current.ContainingType)
                {
                    if (current.IsFileLocal)
                    {
                        return true;
                    }
                }

                return named.TypeArguments.Any(ContainsFileLocalType);

            default:
                return false;
        }
    }

    private static bool IsGeneratedDestinationReference(
        SimpleNameSyntax name,
        IReadOnlyDictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel>? nestedMapMappings,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (nestedMapMappings is null)
        {
            return false;
        }

        var invocation = name.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(candidate =>
                nestedMapMappings.TryGetValue(
                    candidate,
                    out var mapping) &&
                mapping.GuardNullDestination);

        if (invocation is null ||
            semanticModel.GetOperation(
                invocation,
                cancellationToken) is not IInvocationOperation operation ||
            operation.Arguments.FirstOrDefault(argument =>
                argument.Parameter?.Name == "destination")?.Syntax is not
                ArgumentSyntax destinationArgument)
        {
            return false;
        }

        return destinationArgument.Span.Contains(name.Span);
    }

    private static bool IsDeclaredWithin(
        ISymbol symbol,
        SyntaxNode scope)
    {
        return symbol.DeclaringSyntaxReferences.Any(reference =>
            ReferenceEquals(reference.SyntaxTree, scope.SyntaxTree) &&
            scope.FullSpan.Contains(reference.Span));
    }

    private static ExpressionSyntax BuildConstantExpression(
        ILocalSymbol constant)
    {
        var literal = constant.ConstantValue is null
            ? SyntaxFactory.LiteralExpression(
                SyntaxKind.NullLiteralExpression)
            : SyntaxFactory.ParseExpression(
                SymbolDisplay.FormatPrimitive(
                    constant.ConstantValue,
                    quoteStrings: true,
                    useHexadecimalNumbers: false));

        if (constant.ConstantValue is null ||
            constant.Type.TypeKind == TypeKind.Enum ||
            constant.Type.SpecialType is
                SpecialType.System_SByte or
                SpecialType.System_Byte or
                SpecialType.System_Int16 or
                SpecialType.System_UInt16 or
                SpecialType.System_IntPtr or
                SpecialType.System_UIntPtr)
        {
            return SyntaxFactory.CastExpression(
                SyntaxFactory.ParseTypeName(
                    TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                        constant.Type)),
                literal);
        }

        return literal;
    }

    private static bool IsMemberName(SimpleNameSyntax node)
    {
        return node.Parent is MemberAccessExpressionSyntax
        {
            Name: var memberName
        } && ReferenceEquals(memberName, node);
    }
}

internal readonly record struct PreviousExpressionSubstitution(
    string OptionExpression,
    string ValueExpression,
    string HasValueExpression);
