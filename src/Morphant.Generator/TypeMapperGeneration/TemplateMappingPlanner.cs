using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperBuilderMap;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TemplateMappingPlanner
{
    private static readonly SyntaxAnnotation
        KnownNullMapNewExpressionAnnotation =
            new(nameof(KnownNullMapNewExpressionAnnotation));

    private static readonly SyntaxAnnotation
        SimplifiedMapNewExpressionAnnotation =
            new(nameof(SimplifiedMapNewExpressionAnnotation));

    public static bool HasUnmappedRequiredMembers(
        ITypeSymbol? destination,
        ImmutableArray<TypeMapperMemberMappingModel> mappings,
        CancellationToken cancellationToken)
    {
        if (destination is not INamedTypeSymbol namedDestination)
        {
            return false;
        }

        var mappedNames =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (var mapping in mappings)
        {
            mappedNames.Add(mapping.DestinationMemberName);
        }

        for (var current = namedDestination;
             current is not null;
             current = current.BaseType)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol
                    {
                        IsStatic: false,
                        IsRequired: true
                    } or IFieldSymbol
                    {
                        IsStatic: false,
                        IsRequired: true
                    } &&
                    !mappedNames.Contains(member.Name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static TemplateMappingPlan? Build(
        MapperBuilderMapRegistrationInfo registration,
        ITypeSymbol? memberType,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (!TryGetLambda(
                registration.TemplateSyntax,
                out var sourceParameter,
                out var destinationParameter,
                out var body))
        {
            return null;
        }

        var semanticModel = compilation.GetSemanticModel(
            registration.Syntax.SyntaxTree);

        if (semanticModel.GetDeclaredSymbol(
                sourceParameter,
                cancellationToken) is not
                IParameterSymbol sourceParameterSymbol ||
            ContainsConfigureLocalCapture(
                body,
                semanticModel,
                cancellationToken))
        {
            return null;
        }

        IParameterSymbol? destinationParameterSymbol = null;

        if (destinationParameter is not null)
        {
            if (semanticModel.GetDeclaredSymbol(
                    destinationParameter,
                    cancellationToken) is not
                    IParameterSymbol declaredDestinationParameter)
            {
                return null;
            }

            destinationParameterSymbol =
                declaredDestinationParameter;
        }

        var mapNewDestinationIsKnownNull =
            HasKnownNullDefault(registration.DestinationType);
        var mapNewDestinationExpression =
            destinationParameterSymbol is null
                ? null
                : BuildMapNewDestinationExpression(
                    registration.DestinationType,
                    mapNewDestinationIsKnownNull);
        var mapExistingDestinationExpression =
            destinationParameterSymbol is null
                ? null
                : SyntaxFactory.IdentifierName("destination");

        string RewriteMapNew(ExpressionSyntax expression) =>
            RewriteParameters(
                expression,
                sourceParameterSymbol,
                destinationParameterSymbol,
                mapNewDestinationExpression,
                semanticModel);

        string RewriteMapExisting(ExpressionSyntax expression) =>
            RewriteParameters(
                expression,
                sourceParameterSymbol,
                destinationParameterSymbol,
                mapExistingDestinationExpression,
                semanticModel);

        bool IsMapNewDestinationKnownAbsent(
            ExpressionSyntax expression) =>
            destinationParameterSymbol is not null &&
            mapNewDestinationIsKnownNull &&
            IsKnownNullFromDestinationParameter(
                expression,
                destinationParameterSymbol,
                semanticModel);

        if (registration.DestinationType is INamedTypeSymbol namedDestination &&
            DirectDestinationTypePolicy.IsDirect(namedDestination))
        {
            var mapNewDirectExpression =
                RewriteMapNew(body);

            return new TemplateMappingPlan(
                mapNewDirectExpression,
                destinationParameterSymbol is null
                    ? mapNewDirectExpression
                    : RewriteMapExisting(body),
                [],
                TemplateConstructionKind.None,
                Constructor: null,
                FactoryExpression: null,
                ConventionConstructorMappings: [],
                HasDestinationParameter:
                    destinationParameterSymbol is not null);
        }

        if (memberType is null ||
            body is not ImplicitObjectCreationExpressionSyntax
                objectCreation)
        {
            return null;
        }

        var initializerExpressions =
            objectCreation.Initializer?.Expressions ?? default;
        var memberMappings =
            ImmutableArray.CreateBuilder<TemplateMemberMappingModel>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var initializerExpression in initializerExpressions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (initializerExpression is not AssignmentExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: IdentifierNameSyntax memberName,
                    Right: var value
                } ||
                !seenNames.Add(memberName.Identifier.ValueText) ||
                TryFindWritableMember(
                    memberType,
                    memberName.Identifier.ValueText,
                    compilation,
                    mapperType) is not { } member)
            {
                return null;
            }

            if (TemplateMemberMarker.TryGetKind(
                    value,
                    semanticModel,
                    cancellationToken,
                    out var markerKind))
            {
                memberMappings.Add(
                    new TemplateMemberMappingModel(
                        member.Name,
                        markerKind ==
                            TemplateMemberMarkerKind.Auto
                            ? TemplateMemberMappingKind.Auto
                            : TemplateMemberMappingKind.Ignore,
                        MapNewMapping: null,
                        MapExistingMapping: null));
                continue;
            }

            if (!TryBuildExplicitValueExpression(
                    value,
                    member.Type,
                    registration.SourceType,
                    compilation,
                    mapperType,
                    semanticModel,
                    RewriteMapNew,
                    IsMapNewDestinationKnownAbsent,
                    cancellationToken,
                    out var mapNewValueExpression))
            {
                return null;
            }

            var mapExistingValueExpression =
                mapNewValueExpression;

            if (destinationParameterSymbol is not null &&
                !TryBuildExplicitValueExpression(
                    value,
                    member.Type,
                    registration.SourceType,
                    compilation,
                    mapperType,
                    semanticModel,
                    RewriteMapExisting,
                    static _ => false,
                    cancellationToken,
                    out mapExistingValueExpression))
            {
                return null;
            }

            var explicitValueTypeName =
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    member.Type);
            var mapNewMapping = new TypeMapperMemberMappingModel(
                SourceMemberName: string.Empty,
                member.Name,
                member.IsRequired,
                SourceValueLocalName: null,
                mapNewValueExpression,
                explicitValueTypeName);
            TypeMapperMemberMappingModel? mapExistingMapping =
                member.CanAssign
                    ? new TypeMapperMemberMappingModel(
                        SourceMemberName: string.Empty,
                        member.Name,
                        member.IsRequired,
                        SourceValueLocalName: null,
                        mapExistingValueExpression,
                        explicitValueTypeName,
                        RequiresPreviousDestinationValueLocal:
                            destinationParameterSymbol is not null &&
                            ReferencesParameter(
                                value,
                                destinationParameterSymbol,
                                semanticModel,
                                cancellationToken))
                    : null;

            memberMappings.Add(
                new TemplateMemberMappingModel(
                    member.Name,
                    TemplateMemberMappingKind.Explicit,
                    mapNewMapping,
                    mapExistingMapping));
        }

        var constructionKind =
            TemplateConstructionKind.DestinationConstructor;
        TemplateConstructorMappingPlan? constructor = null;
        string? factoryExpression = null;
        ImmutableArray<TemplateConstructorMemberMappingModel>
            conventionConstructorMappings = [];

        if (TemplateByConventionMappingPlanner.TryBuild(
                objectCreation,
                registration.SourceType,
                compilation,
                mapperType,
                semanticModel,
                RewriteMapNew,
                IsMapNewDestinationKnownAbsent,
                cancellationToken,
                out conventionConstructorMappings))
        {
            constructionKind =
                TemplateConstructionKind.ByConvention;
        }
        else if (TemplateByFactoryMappingPlanner.TryBuild(
                     objectCreation,
                     semanticModel,
                     RewriteMapNew,
                     cancellationToken,
                     out factoryExpression))
        {
            constructionKind =
                TemplateConstructionKind.ByFactory;
        }
        else if (memberType is ITypeParameterSymbol &&
            objectCreation.ArgumentList.Arguments.Count == 0)
        {
            constructionKind =
                TemplateConstructionKind.TypeParameterParameterless;
        }
        else if (memberType is INamedTypeSymbol constructorDestination)
        {
            constructor = TemplateConstructorMappingPlanner.Build(
                objectCreation,
                registration.SourceType,
                constructorDestination,
                compilation,
                mapperType,
                semanticModel,
                RewriteMapNew,
                IsMapNewDestinationKnownAbsent,
                cancellationToken);
        }

        return new TemplateMappingPlan(
            MapNewDirectExpression: null,
            MapExistingDirectExpression: null,
            memberMappings.ToImmutable(),
            constructionKind,
            constructor,
            factoryExpression,
            conventionConstructorMappings,
            HasDestinationParameter:
                destinationParameterSymbol is not null);
    }

    private static bool TryGetLambda(
        InvocationExpressionSyntax? templateInvocation,
        out ParameterSyntax sourceParameter,
        out ParameterSyntax? destinationParameter,
        out ExpressionSyntax body)
    {
        sourceParameter = null!;
        destinationParameter = null;
        body = null!;

        if (templateInvocation is null ||
            templateInvocation.ArgumentList.Arguments.Count != 1 ||
            templateInvocation.ArgumentList.Arguments[0].Expression is not
                LambdaExpressionSyntax lambda)
        {
            return false;
        }

        switch (lambda)
        {
            case SimpleLambdaExpressionSyntax
                {
                    Parameter: var simpleParameter,
                    ExpressionBody: { } expression
                }:
                sourceParameter = simpleParameter;
                body = expression;
                return true;

            case ParenthesizedLambdaExpressionSyntax parenthesized
                when parenthesized.ParameterList.Parameters.Count
                         is 1 or 2 &&
                     parenthesized.ExpressionBody is { } expression:
                sourceParameter =
                    parenthesized.ParameterList.Parameters[0];
                destinationParameter =
                    parenthesized.ParameterList.Parameters.Count == 2
                        ? parenthesized.ParameterList.Parameters[1]
                        : null;
                body = expression;
                return true;

            default:
                return false;
        }
    }

    private static bool TryBuildExplicitValueExpression(
        ExpressionSyntax expression,
        ITypeSymbol targetDestinationType,
        ITypeSymbol containingSourceType,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string> rewriteExpression,
        Func<ExpressionSyntax, bool>
            isKnownAbsentExistingDestination,
        CancellationToken cancellationToken,
        out string valueExpression)
    {
        valueExpression = string.Empty;

        if (!TemplateNestedMapMappingPlanner.TryRecognize(
                expression,
                containingSourceType,
                compilation,
                mapperType,
                semanticModel,
                rewriteExpression,
                isKnownAbsentExistingDestination,
                cancellationToken,
                out var nestedMap))
        {
            valueExpression = rewriteExpression(expression);
            return true;
        }

        return nestedMap is { } nestedMapValue &&
               TemplateNestedMapMappingPlanner.TryBuildValueExpression(
                   nestedMapValue,
                   targetDestinationType,
                   out valueExpression);
    }

    private static ExpressionSyntax BuildMapNewDestinationExpression(
        ITypeSymbol destinationType,
        bool isKnownNull)
    {
        var expression = SyntaxFactory.ParseExpression(
            "default(" +
            TypeMapperMappingTypePolicy
                .GetGeneratedMaybeNullTypeName(destinationType) +
            ")");

        return isKnownNull
            ? expression.WithAdditionalAnnotations(
                KnownNullMapNewExpressionAnnotation)
            : expression;
    }

    private static bool HasKnownNullDefault(ITypeSymbol type)
    {
        return type.IsReferenceType ||
               type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType ==
               SpecialType.System_Nullable_T ||
               type is ITypeParameterSymbol
               {
                   HasReferenceTypeConstraint: true
               };
    }

    private static bool IsKnownNullFromDestinationParameter(
        ExpressionSyntax expression,
        IParameterSymbol destinationParameter,
        SemanticModel semanticModel)
    {
        while (true)
        {
            if (expression is ParenthesizedExpressionSyntax
                {
                    Expression: var parenthesizedExpression
                })
            {
                expression = parenthesizedExpression;
                continue;
            }

            if (expression is PostfixUnaryExpressionSyntax
                {
                    RawKind:
                        (int)SyntaxKind
                            .SuppressNullableWarningExpression,
                    Operand: var operand
                })
            {
                expression = operand;
                continue;
            }

            break;
        }

        if (expression is IdentifierNameSyntax identifier)
        {
            return SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier).Symbol,
                destinationParameter);
        }

        return expression is ConditionalAccessExpressionSyntax
               {
                   Expression: var receiver
               } &&
               IsKnownNullFromDestinationParameter(
                   receiver,
                   destinationParameter,
                   semanticModel);
    }

    private static bool ReferencesParameter(
        ExpressionSyntax expression,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var identifier in expression
                     .DescendantNodesAndSelf()
                     .OfType<IdentifierNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsInsideNameof(identifier, expression) ||
                !SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(
                            identifier,
                            cancellationToken)
                        .Symbol,
                    parameter))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsInsideNameof(
        IdentifierNameSyntax identifier,
        ExpressionSyntax expression)
    {
        for (SyntaxNode? current = identifier.Parent;
             current is not null &&
             !ReferenceEquals(current, expression.Parent);
             current = current.Parent)
        {
            if (current is InvocationExpressionSyntax
                {
                    Expression: IdentifierNameSyntax
                    {
                        Identifier.ValueText: "nameof"
                    }
                })
            {
                return true;
            }
        }

        return false;
    }

    private static string RewriteParameters(
        ExpressionSyntax expression,
        IParameterSymbol sourceParameter,
        IParameterSymbol? destinationParameter,
        ExpressionSyntax? destinationExpression,
        SemanticModel semanticModel)
    {
        var rewritten = new TemplateParameterRewriter(
                sourceParameter,
                destinationParameter,
                destinationExpression,
                semanticModel)
            .Visit(expression)!
            .WithoutTrivia()
            .NormalizeWhitespace();

        return new NullableSyntaxTriviaRewriter()
            .Visit(rewritten)!
            .ToFullString();
    }

    private static bool ContainsConfigureLocalCapture(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var identifier in expression
                     .DescendantNodesAndSelf()
                     .OfType<IdentifierNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetSymbolInfo(
                    identifier,
                    cancellationToken)
                .Symbol;

            if (symbol is ILocalSymbol or IRangeVariableSymbol ||
                symbol is IMethodSymbol
                {
                    MethodKind: MethodKind.LocalFunction
                })
            {
                return true;
            }
        }

        return false;
    }

    private static TemplateWritableMember? TryFindWritableMember(
        ITypeSymbol destination,
        string memberName,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType)
    {
        if (destination is ITypeParameterSymbol typeParameter)
        {
            foreach (var constraint in typeParameter.ConstraintTypes)
            {
                if (TryFindWritableMember(
                        constraint,
                        memberName,
                        compilation,
                        mapperType) is { } member)
                {
                    return member;
                }
            }

            return null;
        }

        if (destination is not INamedTypeSymbol namedDestination)
        {
            return null;
        }

        for (var current = namedDestination;
             current is not null;
             current = current.BaseType)
        {
            var members = current.GetMembers(memberName);

            if (members.Length == 0)
            {
                continue;
            }

            foreach (var symbol in members)
            {
                if (symbol is IPropertySymbol
                    {
                        IsStatic: false,
                        IsIndexer: false,
                        ReturnsByRef: false,
                        ReturnsByRefReadonly: false,
                        ExplicitInterfaceImplementations.IsEmpty: true,
                        SetMethod: { } setter
                    } property &&
                    compilation.IsSymbolAccessibleWithin(
                        property,
                        mapperType,
                        destination) &&
                    compilation.IsSymbolAccessibleWithin(
                        setter,
                        mapperType,
                        destination))
                {
                    return new TemplateWritableMember(
                        property.Name,
                        property.Type.WithNullableAnnotation(
                            property.NullableAnnotation),
                        property.IsRequired,
                        CanAssign: !setter.IsInitOnly);
                }

                if (symbol is IFieldSymbol
                    {
                        IsStatic: false,
                        IsConst: false,
                        IsReadOnly: false,
                        IsImplicitlyDeclared: false,
                        IsFixedSizeBuffer: false
                    } field &&
                    compilation.IsSymbolAccessibleWithin(
                        field,
                        mapperType,
                        destination))
                {
                    return new TemplateWritableMember(
                        field.Name,
                        field.Type.WithNullableAnnotation(
                            field.NullableAnnotation),
                        field.IsRequired,
                        CanAssign: true);
                }
            }

            return null;
        }

        return null;
    }

    private sealed class TemplateParameterRewriter(
        IParameterSymbol sourceParameter,
        IParameterSymbol? destinationParameter,
        ExpressionSyntax? destinationExpression,
        SemanticModel semanticModel)
        : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitBinaryExpression(
            BinaryExpressionSyntax node)
        {
            var rewritten =
                (BinaryExpressionSyntax)
                base.VisitBinaryExpression(node)!;

            if (node.IsKind(SyntaxKind.CoalesceExpression) &&
                IsKnownNullMapNewExpression(rewritten.Left))
            {
                return MarkSimplified(
                    rewritten.Right.WithTriviaFrom(node));
            }

            return rewritten;
        }

        public override SyntaxNode? VisitParenthesizedExpression(
            ParenthesizedExpressionSyntax node)
        {
            var rewritten =
                (ParenthesizedExpressionSyntax)
                base.VisitParenthesizedExpression(node)!;
            var expression = rewritten.Expression;

            return node.Parent is BinaryExpressionSyntax &&
                   expression.HasAnnotation(
                       SimplifiedMapNewExpressionAnnotation) &&
                   CanRemoveParentheses(expression)
                ? expression.WithTriviaFrom(node)
                : rewritten;
        }

        public override SyntaxNode? VisitObjectCreationExpression(
            ObjectCreationExpressionSyntax node)
        {
            if (semanticModel.GetTypeInfo(node).Type is not
                INamedTypeSymbol createdType)
            {
                return base.VisitObjectCreationExpression(node);
            }

            var rewritten = node.WithType(
                SyntaxFactory.ParseTypeName(
                    createdType.ToDisplayString(
                        SymbolDisplayFormats
                            .FullyQualifiedNullable)));

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

        private static bool IsKnownNullMapNewExpression(
            ExpressionSyntax expression)
        {
            expression = UnwrapParentheses(expression);

            if (expression.HasAnnotation(
                    KnownNullMapNewExpressionAnnotation))
            {
                return true;
            }

            return expression is ConditionalAccessExpressionSyntax
                   {
                       Expression: var receiver
                   } &&
                   IsKnownNullMapNewExpression(receiver);
        }

        private static bool CanRemoveParentheses(
            ExpressionSyntax expression)
        {
            return expression is
                LiteralExpressionSyntax or
                IdentifierNameSyntax or
                MemberAccessExpressionSyntax or
                ConditionalAccessExpressionSyntax or
                InvocationExpressionSyntax or
                ElementAccessExpressionSyntax or
                ObjectCreationExpressionSyntax or
                ImplicitObjectCreationExpressionSyntax or
                DefaultExpressionSyntax or
                CastExpressionSyntax or
                PrefixUnaryExpressionSyntax or
                PostfixUnaryExpressionSyntax;
        }

        private static ExpressionSyntax UnwrapParentheses(
            ExpressionSyntax expression)
        {
            while (expression is ParenthesizedExpressionSyntax
                   {
                       Expression: var nested
                   })
            {
                expression = nested;
            }

            return expression;
        }

        private static ExpressionSyntax MarkSimplified(
            ExpressionSyntax expression)
        {
            return expression.WithAdditionalAnnotations(
                SimplifiedMapNewExpressionAnnotation);
        }

        public override SyntaxNode? VisitInvocationExpression(
            InvocationExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax
                {
                    Identifier.ValueText: "nameof"
                } &&
                semanticModel.GetConstantValue(node) is
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
                })
            {
                var extensionMethod = TryGetExtensionMethod(
                    node,
                    methodName);
                var extensionContainingType =
                    extensionMethod?.ContainingType ??
                    TryResolveExtensionContainer(
                        node,
                        methodName.Identifier.ValueText,
                        GetExpressionType(receiver));

                if (extensionContainingType is null)
                {
                    return base.VisitInvocationExpression(node);
                }

                var rewrittenReceiver =
                    (ExpressionSyntax)Visit(receiver)!;
                var rewrittenMethodName =
                    (SimpleNameSyntax)Visit(methodName)!;
                var rewrittenArguments = node.ArgumentList.Arguments
                    .Select(argument =>
                        (ArgumentSyntax)Visit(argument)!)
                    .ToArray();
                var receiverArgument =
                    SyntaxFactory.Argument(rewrittenReceiver);

                if (extensionMethod?.Parameters[0].RefKind ==
                    RefKind.Ref)
                {
                    receiverArgument = receiverArgument.WithRefKindKeyword(
                        SyntaxFactory.Token(SyntaxKind.RefKeyword));
                }

                var arguments =
                    new List<ArgumentSyntax>(
                        rewrittenArguments.Length + 1)
                    {
                        receiverArgument
                    };

                arguments.AddRange(rewrittenArguments);

                var containingType = SyntaxFactory.ParseExpression(
                    extensionContainingType.ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable));
                var invocation = node
                    .WithExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            containingType,
                            rewrittenMethodName))
                    .WithArgumentList(
                        node.ArgumentList.WithArguments(
                            SyntaxFactory.SeparatedList(arguments)));

                return invocation.WithTriviaFrom(node);
            }

            return base.VisitInvocationExpression(node);
        }

        private ITypeSymbol? GetExpressionType(
            ExpressionSyntax expression)
        {
            if (semanticModel.GetTypeInfo(expression).Type is
                {
                    TypeKind: not TypeKind.Error
                } type)
            {
                return type;
            }

            return semanticModel.GetSymbolInfo(expression).Symbol switch
            {
                IFieldSymbol field => field.Type,
                ILocalSymbol local => local.Type,
                IParameterSymbol expressionParameter =>
                    expressionParameter.Type,
                IPropertySymbol property => property.Type,
                IMethodSymbol method => method.ReturnType,
                _ => null
            };
        }

        private INamedTypeSymbol? TryResolveExtensionContainer(
            InvocationExpressionSyntax invocation,
            string methodName,
            ITypeSymbol? receiverType)
        {
            var symbolInfo =
                semanticModel.GetSymbolInfo(invocation);

            if (symbolInfo.Symbol is IMethodSymbol ||
                symbolInfo.CandidateSymbols
                    .OfType<IMethodSymbol>()
                    .Any(static method =>
                        !method.IsExtensionMethod &&
                        method.ReducedFrom is null))
            {
                return null;
            }

            INamedTypeSymbol? result = null;

            foreach (var usingDirective in
                     GetInScopeUsings(invocation))
            {
                if (usingDirective.StaticKeyword.IsKind(
                        SyntaxKind.StaticKeyword) ||
                    usingDirective.Alias is not null ||
                    usingDirective.Name is not { } name ||
                    semanticModel.GetSymbolInfo(name).Symbol is not
                        INamespaceSymbol namespaceSymbol)
                {
                    continue;
                }

                if (!TryAddExtensionContainer(
                        namespaceSymbol,
                        methodName,
                        receiverType,
                        ref result))
                {
                    return null;
                }
            }

            for (var containingNamespace =
                     semanticModel.GetEnclosingSymbol(
                             invocation.SpanStart)?
                         .ContainingNamespace;
                 containingNamespace is not null;
                 containingNamespace =
                     containingNamespace.ContainingNamespace)
            {
                if (!TryAddExtensionContainer(
                        containingNamespace,
                        methodName,
                        receiverType,
                        ref result))
                {
                    return null;
                }

                if (containingNamespace.IsGlobalNamespace)
                {
                    break;
                }
            }

            return result;
        }

        private static bool TryAddExtensionContainer(
            INamespaceSymbol namespaceSymbol,
            string methodName,
            ITypeSymbol? receiverType,
            ref INamedTypeSymbol? result)
        {
            foreach (var type in namespaceSymbol.GetTypeMembers())
            {
                if (!type.GetMembers(methodName)
                        .OfType<IMethodSymbol>()
                        .Any(method =>
                            method.IsExtensionMethod &&
                            (receiverType is null ||
                             method.ReduceExtensionMethod(
                                 receiverType) is not null)))
                {
                    continue;
                }

                if (result is not null &&
                    !SymbolEqualityComparer.Default.Equals(
                        result,
                        type))
                {
                    result = null;
                    return false;
                }

                result = type;
            }

            return true;
        }

        private IMethodSymbol? TryGetExtensionMethod(
            InvocationExpressionSyntax invocation,
            SimpleNameSyntax methodName)
        {
            var symbolInfo =
                semanticModel.GetSymbolInfo(invocation);
            var method = symbolInfo.Symbol as IMethodSymbol ??
                         GetReferencedSymbol(methodName) as IMethodSymbol;

            if (method is null)
            {
                method = symbolInfo.CandidateSymbols
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault();

                if (method is not null &&
                    symbolInfo.CandidateSymbols
                        .OfType<IMethodSymbol>()
                        .Any(candidate =>
                            candidate.Name != method.Name ||
                            !SymbolEqualityComparer.Default.Equals(
                                candidate.ContainingType,
                                method.ContainingType)))
                {
                    return null;
                }
            }

            return method?.ReducedFrom ??
                   (method is { IsExtensionMethod: true }
                       ? method
                       : null);
        }

        public override SyntaxNode? VisitIdentifierName(
            IdentifierNameSyntax node)
        {
            var symbol = GetReferencedSymbol(node);

            if (symbol is null &&
                node.Parent is InvocationExpressionSyntax
                {
                    Expression: var invocationExpression
                } invocation &&
                ReferenceEquals(invocationExpression, node))
            {
                symbol = semanticModel.GetSymbolInfo(invocation).Symbol;
            }

            if (SymbolEqualityComparer.Default.Equals(
                    symbol,
                    sourceParameter))
            {
                return SyntaxFactory.PostfixUnaryExpression(
                        SyntaxKind.SuppressNullableWarningExpression,
                        SyntaxFactory.IdentifierName("source"))
                    .WithTriviaFrom(node);
            }

            if (destinationExpression is not null &&
                SymbolEqualityComparer.Default.Equals(
                    symbol,
                    destinationParameter))
            {
                return destinationExpression;
            }

            if (symbol is INamedTypeSymbol type)
            {
                return SyntaxFactory.ParseExpression(
                        type.ToDisplayString(
                            SymbolDisplayFormats.FullyQualifiedNullable))
                    .WithTriviaFrom(node);
            }

            if (node.Parent is not MemberAccessExpressionSyntax
                {
                    Name: var memberName
                } ||
                !ReferenceEquals(memberName, node))
            {
                if (TryBuildQualifiedStaticMember(
                        symbol,
                        node,
                        node.Identifier.Text) is { } expression)
                {
                    return expression.WithTriviaFrom(node);
                }
            }

            return base.VisitIdentifierName(node);
        }

        public override SyntaxNode? VisitGenericName(
            GenericNameSyntax node)
        {
            if (GetReferencedSymbol(node) is
                INamedTypeSymbol type)
            {
                return SyntaxFactory.ParseExpression(
                        type.ToDisplayString(
                            SymbolDisplayFormats.FullyQualifiedNullable))
                    .WithTriviaFrom(node);
            }

            var symbol = GetReferencedSymbol(node);

            if (node.Parent is not MemberAccessExpressionSyntax
                {
                    Name: var memberName
                } ||
                !ReferenceEquals(memberName, node))
            {
                if (TryBuildQualifiedStaticMember(
                        symbol,
                        node,
                        node.ToString()) is { } expression)
                {
                    return expression.WithTriviaFrom(node);
                }
            }

            return base.VisitGenericName(node);
        }

        private ISymbol? GetReferencedSymbol(
            SimpleNameSyntax node)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is { } symbol)
            {
                return symbol;
            }

            if (symbolInfo.CandidateSymbols.IsEmpty)
            {
                return null;
            }

            var candidate = symbolInfo.CandidateSymbols[0];

            return symbolInfo.CandidateSymbols.All(
                other =>
                    other.IsStatic == candidate.IsStatic &&
                    other.Name == candidate.Name &&
                    SymbolEqualityComparer.Default.Equals(
                        other.ContainingType,
                        candidate.ContainingType))
                ? candidate
                : null;
        }

        private ExpressionSyntax? TryBuildQualifiedStaticMember(
            ISymbol? symbol,
            SimpleNameSyntax node,
            string memberSyntax)
        {
            var containingType = symbol is
                {
                    IsStatic: true,
                    ContainingType: { } symbolContainingType
                } &&
                symbol is not INamedTypeSymbol
                    ? symbolContainingType
                    : TryResolveStaticImport(
                        node,
                        node.Identifier.ValueText);

            if (containingType is null)
            {
                return null;
            }

            return SyntaxFactory.ParseExpression(
                containingType.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable) +
                "." +
                memberSyntax);
        }

        private INamedTypeSymbol? TryResolveStaticImport(
            SyntaxNode node,
            string memberName)
        {
            INamedTypeSymbol? result = null;

            foreach (var usingDirective in GetInScopeUsings(node))
            {
                if (!usingDirective.StaticKeyword.IsKind(
                        SyntaxKind.StaticKeyword) ||
                    usingDirective.Name is not { } name ||
                    semanticModel.GetSymbolInfo(name).Symbol is not
                        INamedTypeSymbol type ||
                    type.GetMembers(memberName).IsEmpty)
                {
                    continue;
                }

                if (result is not null &&
                    !SymbolEqualityComparer.Default.Equals(
                        result,
                        type))
                {
                    return null;
                }

                result = type;
            }

            return result;
        }

        private IEnumerable<UsingDirectiveSyntax>
            GetInScopeUsings(
                SyntaxNode node)
        {
            if (node.SyntaxTree.GetRoot() is
                CompilationUnitSyntax compilationUnit)
            {
                foreach (var usingDirective in
                         compilationUnit.Usings)
                {
                    yield return usingDirective;
                }
            }

            foreach (var syntaxTree in
                     semanticModel.Compilation.SyntaxTrees)
            {
                if (ReferenceEquals(
                        syntaxTree,
                        node.SyntaxTree) ||
                    syntaxTree.GetRoot() is not
                        CompilationUnitSyntax otherCompilationUnit)
                {
                    continue;
                }

                foreach (var usingDirective in
                         otherCompilationUnit.Usings)
                {
                    if (usingDirective.GlobalKeyword.IsKind(
                            SyntaxKind.GlobalKeyword))
                    {
                        yield return usingDirective;
                    }
                }
            }

            foreach (var namespaceDeclaration in
                     node.Ancestors()
                         .OfType<BaseNamespaceDeclarationSyntax>())
            {
                foreach (var usingDirective in
                         namespaceDeclaration.Usings)
                {
                    yield return usingDirective;
                }
            }
        }
    }

    private sealed class NullableSyntaxTriviaRewriter :
        CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitNullableType(
            NullableTypeSyntax node)
        {
            var rewritten =
                (NullableTypeSyntax)base.VisitNullableType(node)!;

            return rewritten.WithQuestionToken(
                rewritten.QuestionToken.WithTrailingTrivia(
                    default(SyntaxTriviaList)));
        }

        public override SyntaxNode? VisitPostfixUnaryExpression(
            PostfixUnaryExpressionSyntax node)
        {
            var rewritten =
                (PostfixUnaryExpressionSyntax)
                base.VisitPostfixUnaryExpression(node)!;

            if (!rewritten.IsKind(
                    SyntaxKind
                        .SuppressNullableWarningExpression))
            {
                return rewritten;
            }

            return rewritten
                .WithOperand(
                    rewritten.Operand.WithTrailingTrivia(
                        default(SyntaxTriviaList)))
                .WithOperatorToken(
                    rewritten.OperatorToken
                        .WithLeadingTrivia(
                            default(SyntaxTriviaList)));
        }
    }

    private readonly record struct TemplateWritableMember(
        string Name,
        ITypeSymbol Type,
        bool IsRequired,
        bool CanAssign);
}

internal readonly record struct TemplateMappingPlan(
    string? MapNewDirectExpression,
    string? MapExistingDirectExpression,
    ImmutableArray<TemplateMemberMappingModel> MemberMappings,
    TemplateConstructionKind ConstructionKind,
    TemplateConstructorMappingPlan? Constructor,
    string? FactoryExpression,
    ImmutableArray<TemplateConstructorMemberMappingModel>
        ConventionConstructorMappings,
    bool HasDestinationParameter);

internal readonly record struct TemplateMemberMappingModel(
    string MemberName,
    TemplateMemberMappingKind Kind,
    TypeMapperMemberMappingModel? MapNewMapping,
    TypeMapperMemberMappingModel? MapExistingMapping);

internal enum TemplateMemberMappingKind
{
    Explicit,
    Auto,
    Ignore
}

internal enum TemplateConstructionKind
{
    None,
    TypeParameterParameterless,
    DestinationConstructor,
    ByConvention,
    ByFactory
}
