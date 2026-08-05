using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal sealed class ConstructExpressionRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly INamedTypeSymbol _mapperType;
    private readonly IParameterSymbol _sourceParameter;
    private readonly string _sourceName;
    private readonly IParameterSymbol? _previousParameter;
    private readonly PreviousExpressionSubstitution? _previousSubstitution;
    private readonly IParameterSymbol? _resultParameter;
    private readonly string? _resultName;
    private readonly SyntaxNode _transferScope;
    private readonly IReadOnlyDictionary<ISymbol, string>?
        _localSubstitutions;
    private readonly IReadOnlyDictionary<SyntaxNode, SyntaxAnnotation>?
        _dependencyAnnotations;

    private ConstructExpressionRewriter(
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
        IReadOnlyDictionary<SyntaxNode, SyntaxAnnotation>?
            dependencyAnnotations)
    {
        _semanticModel = semanticModel;
        _mapperType = mapperType;
        _sourceParameter = sourceParameter;
        _sourceName = sourceName;
        _previousParameter = previousParameter;
        _previousSubstitution = previousSubstitution;
        _resultParameter = resultParameter;
        _resultName = resultName;
        _transferScope = transferScope;
        _localSubstitutions = localSubstitutions;
        _dependencyAnnotations = dependencyAnnotations;
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
                    transferScope,
                    localSubstitutions,
                    dependencyAnnotations: null)
                .Visit(syntax)!;
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
                    transferScope,
                    localSubstitutions,
                    dependencyAnnotations)
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

            var containingType = SyntaxFactory.ParseExpression(
                extensionMethod.ContainingType.ToDisplayString(
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

        return base.VisitInvocationExpression(node);
    }

    public override SyntaxNode? VisitObjectCreationExpression(
        ObjectCreationExpressionSyntax node)
    {
        if (_semanticModel.GetTypeInfo(node).Type is not
            INamedTypeSymbol createdType)
        {
            return base.VisitObjectCreationExpression(node);
        }

        var rewritten = node.WithType(
            SyntaxFactory.ParseTypeName(
                createdType.ToDisplayString(
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
                    type.ToDisplayString(
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

        if (symbol is INamedTypeSymbol type)
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
                    type.ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable))
                .WithTriviaFrom(node);
        }

        if (symbol is { IsStatic: true, ContainingType: { } staticType } &&
            symbol is not INamedTypeSymbol &&
            !IsMemberName(node))
        {
            var containingType = IsMapperMember(symbol)
                ? _mapperType
                : staticType;

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

    public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    {
        var symbol = GetReferencedSymbol(node);

        if (symbol is IMethodSymbol
            {
                MethodKind: MethodKind.LocalFunction
            })
        {
            return node;
        }

        if (symbol is INamedTypeSymbol type)
        {
            return SyntaxFactory.ParseTypeName(
                    type.ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable))
                .WithTriviaFrom(node);
        }

        if (symbol is { IsStatic: true, ContainingType: { } staticType } &&
            !IsMemberName(node))
        {
            var containingType = IsMapperMember(symbol)
                ? _mapperType
                : staticType;

            return SyntaxFactory.ParseExpression(
                    containingType.ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable) +
                    "." +
                    node.WithoutTrivia().ToFullString())
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
                    TypeMapperMappingTypePolicy.GetGeneratedTypeName(type))
                .WithTriviaFrom(syntax)
            : (TypeSyntax)base.Visit(syntax)!;
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
        for (var type = _mapperType;
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
        CancellationToken cancellationToken)
    {
        foreach (var name in expression
                     .DescendantNodesAndSelf()
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                    resultParameter))
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
