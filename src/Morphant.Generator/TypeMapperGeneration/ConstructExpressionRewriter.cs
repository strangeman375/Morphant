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
    private readonly string _previousName;
    private readonly SyntaxNode _transferScope;

    private ConstructExpressionRewriter(
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        string previousName,
        SyntaxNode transferScope)
    {
        _semanticModel = semanticModel;
        _mapperType = mapperType;
        _sourceParameter = sourceParameter;
        _sourceName = sourceName;
        _previousParameter = previousParameter;
        _previousName = previousName;
        _transferScope = transferScope;
    }

    public static bool TryRewrite(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        string previousName,
        SyntaxNode transferScope,
        CancellationToken cancellationToken,
        out string rewrittenExpression)
    {
        if (!HasOnlyTransferableCaptures(
                expression,
                transferScope,
                semanticModel,
                sourceParameter,
                previousParameter,
                cancellationToken))
        {
            rewrittenExpression = string.Empty;
            return false;
        }

        var rewritten =
            (ExpressionSyntax)new ConstructExpressionRewriter(
                    semanticModel,
                    mapperType,
                    sourceParameter,
                    sourceName,
                    previousParameter,
                    previousName,
                    transferScope)
                .Visit(expression)!;

        rewrittenExpression = rewritten
            .WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString();
        return true;
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

    public override SyntaxNode? VisitMemberAccessExpression(
        MemberAccessExpressionSyntax node)
    {
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

    public override SyntaxNode? VisitIdentifierName(
        IdentifierNameSyntax node)
    {
        var symbol = GetReferencedSymbol(node);

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
            return SyntaxFactory.IdentifierName(_previousName)
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

    public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    {
        var symbol = GetReferencedSymbol(node);

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

    private static bool HasOnlyTransferableCaptures(
        SyntaxNode expression,
        SyntaxNode transferScope,
        SemanticModel semanticModel,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
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
                    previousParameter))
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
