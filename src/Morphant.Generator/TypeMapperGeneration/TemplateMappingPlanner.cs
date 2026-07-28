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

    public static TemplateMappingPlanResult? Build(
        MapperBuilderMapRegistrationInfo registration,
        ITypeSymbol? memberType,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (!TryGetLambda(
                registration.TemplateSyntax,
                out var lambda,
                out var sourceParameter,
                out var destinationParameter))
        {
            return null;
        }

        var semanticModel = compilation.GetSemanticModel(
            registration.Syntax.SyntaxTree);

        if (semanticModel.GetDeclaredSymbol(
                sourceParameter,
                cancellationToken) is not
                IParameterSymbol sourceParameterSymbol)
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

        var localFunctionResult =
            TemplateLocalFunctionPlanner.Build(
                lambda,
                semanticModel,
                cancellationToken);

        if (localFunctionResult is
            UnsupportedTemplateLocalFunctions
            unsupportedLocalFunctions)
        {
            return new UnsupportedTemplateMappingPlanResult(
                unsupportedLocalFunctions.Message);
        }

        var localFunctions =
            (SupportedTemplateLocalFunctions)
            localFunctionResult;
        var localFunctionPlaceholderNames =
            localFunctions.Functions
                .Select(static function =>
                    function.PlaceholderName)
                .ToImmutableArray();
        var directTemplate =
            registration.DestinationType is
                INamedTypeSymbol namedDestination &&
            DirectDestinationTypePolicy.IsDirect(
                namedDestination);
        var controlFlowResult = TemplateControlFlowPlanner.Build(
            lambda,
            semanticModel,
            directTemplate,
            cancellationToken);

        if (controlFlowResult is UnsupportedTemplateControlFlow
            unsupportedControlFlow)
        {
            return new UnsupportedTemplateMappingPlanResult(
                unsupportedControlFlow.Message);
        }

        if (controlFlowResult is not TemplateControlFlowProgram
            controlFlow)
        {
            return null;
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
        var runtimeLocalPlaceholderNames =
            controlFlow.RuntimeLocals
                .Select(
                    static local =>
                        local.PlaceholderName)
                .ToImmutableArray();
        var allowedFactoryCaptureSymbols =
            new HashSet<ISymbol>(
                controlFlow.RuntimeLocalPlaceholders.Keys,
                SymbolEqualityComparer.Default)
            {
                sourceParameterSymbol
            };

        if (destinationParameterSymbol is not null)
        {
            allowedFactoryCaptureSymbols.Add(
                destinationParameterSymbol);
        }

        string RewriteMapNew(ExpressionSyntax expression) =>
            RewriteParameters(
                expression,
                sourceParameterSymbol,
                destinationParameterSymbol,
                mapNewDestinationExpression,
                semanticModel,
                controlFlow.RuntimeLocalPlaceholders,
                runtimeLocalPlaceholderNames,
                localFunctions.Placeholders,
                localFunctionPlaceholderNames);

        string RewriteMapExisting(ExpressionSyntax expression) =>
            RewriteParameters(
                expression,
                sourceParameterSymbol,
                destinationParameterSymbol,
                mapExistingDestinationExpression,
                semanticModel,
                controlFlow.RuntimeLocalPlaceholders,
                runtimeLocalPlaceholderNames,
                localFunctions.Placeholders,
                localFunctionPlaceholderNames);

        var runtimeLocals =
            controlFlow.RuntimeLocals
                .Select(local =>
                    new TemplateRuntimeLocalPlan(
                        local.PlaceholderName,
                        local.PreferredName,
                        local.DeclarationType,
                        RewriteMapNew(
                            local.Initializer),
                        destinationParameterSymbol is null
                            ? RewriteMapNew(
                                local.Initializer)
                            : RewriteMapExisting(
                                local.Initializer)))
                .ToImmutableArray();

        bool IsMapNewDestinationKnownAbsent(
            ExpressionSyntax expression) =>
            destinationParameterSymbol is not null &&
            mapNewDestinationIsKnownNull &&
            IsKnownNullFromDestinationParameter(
                expression,
                destinationParameterSymbol,
                semanticModel);

        TemplateMappingPlan? BuildLeaf(
            TemplateLeafSyntaxNode leaf)
        {
            if (directTemplate)
            {
                if (leaf.DirectExpression is not
                    { } directExpression)
                {
                    return null;
                }

                var mapNewDirectExpression =
                    RewriteMapNew(directExpression);

                return new TemplateMappingPlan(
                    mapNewDirectExpression,
                    destinationParameterSymbol is null
                        ? mapNewDirectExpression
                        : RewriteMapExisting(
                            directExpression),
                    [],
                    TemplateConstructionKind.None,
                    Constructor: null,
                    Factory: null,
                    ConventionConstructorMappings: [],
                    HasDestinationParameter:
                        destinationParameterSymbol is not null);
            }

            if (memberType is null ||
                leaf.ObjectCreation is not
                    { } objectCreation)
            {
                return null;
            }

            var memberMappings =
                ImmutableArray.CreateBuilder<
                    TemplateMemberMappingModel>();
            var seenNames =
                new HashSet<string>(StringComparer.Ordinal);

            foreach (var initializerExpression in
                     leaf.MemberAssignments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var value = initializerExpression.Value;

                if (!seenNames.Add(
                        initializerExpression.MemberName) ||
                    TryFindWritableMember(
                        memberType,
                        initializerExpression.MemberName,
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
                    TypeMapperMappingTypePolicy
                        .GetGeneratedTypeName(
                            member.Type);
                var mapNewMapping =
                    new TypeMapperMemberMappingModel(
                        SourceMemberName: string.Empty,
                        member.Name,
                        member.IsRequired,
                        SourceValueLocalName: null,
                        mapNewValueExpression,
                        explicitValueTypeName);
                TypeMapperMemberMappingModel?
                    mapExistingMapping =
                        member.CanAssign
                            ? new TypeMapperMemberMappingModel(
                                SourceMemberName:
                                    string.Empty,
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
            TemplateFactoryPlan? factory = null;
            ImmutableArray<
                    TemplateConstructorMemberMappingModel>
                conventionConstructorMappings = [];

            if (TemplateByConventionMappingPlanner.TryBuild(
                    leaf.Arguments,
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
                         leaf.Arguments,
                         semanticModel,
                         allowedFactoryCaptureSymbols,
                         cancellationToken,
                         out var factorySyntax))
            {
                constructionKind =
                    TemplateConstructionKind.ByFactory;
                factory = BuildFactoryPlan(
                    factorySyntax,
                    sourceParameterSymbol,
                    destinationParameterSymbol,
                    mapNewDestinationExpression,
                    semanticModel,
                    controlFlow.RuntimeLocalPlaceholders,
                    runtimeLocalPlaceholderNames,
                    localFunctions.Placeholders,
                    localFunctionPlaceholderNames);
            }
            else if (memberType is ITypeParameterSymbol &&
                     leaf.Arguments.IsEmpty)
            {
                constructionKind =
                    TemplateConstructionKind
                        .TypeParameterParameterless;
            }
            else if (memberType is
                     INamedTypeSymbol constructorDestination)
            {
                constructor =
                    TemplateConstructorMappingPlanner.Build(
                        leaf.Arguments,
                        runtimeLocals,
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
                factory,
                conventionConstructorMappings,
                HasDestinationParameter:
                    destinationParameterSymbol is not null);
        }

        if (!TryBuildPlanNode(
                controlFlow.Root,
                BuildLeaf,
                RewriteMapNew,
                RewriteMapExisting,
                out var root))
        {
            return null;
        }

        var transferredLocalFunctions =
            localFunctions.Functions
                .Select(function =>
                    new TemplateLocalFunctionPlan(
                        function.PlaceholderName,
                        function.PreferredName,
                        function.UnsupportedMessage is null
                            ? RewriteLocalFunction(
                                function.Syntax,
                                sourceParameterSymbol,
                                destinationParameterSymbol,
                                mapNewDestinationExpression,
                                semanticModel,
                                controlFlow
                                    .RuntimeLocalPlaceholders,
                                runtimeLocalPlaceholderNames,
                                localFunctions.Placeholders,
                                localFunctionPlaceholderNames)
                            : RewriteUnsupportedLocalFunction(
                                function.Syntax,
                                semanticModel,
                                localFunctions.Placeholders),
                        function.UnsupportedMessage))
                .ToImmutableArray();

        return new SupportedTemplateMappingPlanResult(
            root,
            runtimeLocals,
            transferredLocalFunctions);
    }

    private static TemplateFactoryPlan? BuildFactoryPlan(
        TemplateFactorySyntaxPlan? syntax,
        IParameterSymbol sourceParameter,
        IParameterSymbol? destinationParameter,
        ExpressionSyntax? mapNewDestinationExpression,
        SemanticModel semanticModel,
        IReadOnlyDictionary<ISymbol, string>
            templateRuntimeLocalPlaceholders,
        IReadOnlyCollection<string>
            templateRuntimeLocalPlaceholderNames,
        IReadOnlyDictionary<ISymbol, string>
            localFunctionPlaceholders,
        IReadOnlyCollection<string>
            localFunctionPlaceholderNames)
    {
        if (syntax is null)
        {
            return null;
        }

        if (syntax.UnsupportedMessage is
            { } unsupportedMessage)
        {
            return new TemplateFactoryPlan(
                [],
                ValueExpression: null,
                unsupportedMessage);
        }

        var localPlaceholders =
            new Dictionary<ISymbol, string>(
                SymbolEqualityComparer.Default);

        foreach (var pair in
                 templateRuntimeLocalPlaceholders)
        {
            localPlaceholders.Add(
                pair.Key,
                pair.Value);
        }

        foreach (var pair in
                 syntax.RuntimeLocalPlaceholders)
        {
            localPlaceholders.Add(
                pair.Key,
                pair.Value);
        }

        var localPlaceholderNames =
            templateRuntimeLocalPlaceholderNames
                .Concat(
                    syntax.RuntimeLocals.Select(
                        static local =>
                            local.PlaceholderName))
                .ToImmutableArray();

        string Rewrite(ExpressionSyntax expression) =>
            RewriteParameters(
                expression,
                sourceParameter,
                destinationParameter,
                mapNewDestinationExpression,
                semanticModel,
                localPlaceholders,
                localPlaceholderNames,
                localFunctionPlaceholders,
                localFunctionPlaceholderNames);

        var runtimeLocals =
            syntax.RuntimeLocals
                .Select(local =>
                    new TemplateFactoryRuntimeLocalPlan(
                        local.PlaceholderName,
                        local.PreferredName,
                        local.DeclarationType,
                        Rewrite(local.Initializer)))
                .ToImmutableArray();
        var valueExpression =
            syntax.ResultExpression is { } resultExpression
                ? Rewrite(resultExpression)
                : syntax.InvokedLocalPlaceholder + "()";

        return new TemplateFactoryPlan(
            runtimeLocals,
            valueExpression,
            UnsupportedMessage: null);
    }

    private static bool TryGetLambda(
        InvocationExpressionSyntax? templateInvocation,
        out LambdaExpressionSyntax lambda,
        out ParameterSyntax sourceParameter,
        out ParameterSyntax? destinationParameter)
    {
        lambda = null!;
        sourceParameter = null!;
        destinationParameter = null;

        if (templateInvocation is null ||
            templateInvocation.ArgumentList.Arguments.Count != 1 ||
            templateInvocation.ArgumentList.Arguments[0].Expression is not
                LambdaExpressionSyntax lambdaExpression)
        {
            return false;
        }

        lambda = lambdaExpression;

        switch (lambdaExpression)
        {
            case SimpleLambdaExpressionSyntax
                {
                    Parameter: var simpleParameter
                }:
                sourceParameter = simpleParameter;
                return true;

            case ParenthesizedLambdaExpressionSyntax parenthesized
                when parenthesized.ParameterList.Parameters.Count
                         is 1 or 2:
                sourceParameter =
                    parenthesized.ParameterList.Parameters[0];
                destinationParameter =
                    parenthesized.ParameterList.Parameters.Count == 2
                        ? parenthesized.ParameterList.Parameters[1]
                        : null;
                return true;

            default:
                return false;
        }
    }

    private static bool TryBuildPlanNode(
        TemplateControlFlowSyntaxNode syntax,
        Func<TemplateLeafSyntaxNode, TemplateMappingPlan?>
            buildLeaf,
        Func<ExpressionSyntax, string> rewriteMapNew,
        Func<ExpressionSyntax, string> rewriteMapExisting,
        out TemplateMappingPlanNode node)
    {
        if (syntax is TemplateLeafSyntaxNode leaf)
        {
            if (buildLeaf(leaf) is not { } leafPlan)
            {
                node = null!;
                return false;
            }

            node = new TemplateLeafMappingPlanNode(
                leafPlan);
            return true;
        }

        var conditional =
            (TemplateConditionalSyntaxNode)syntax;

        if (!TryBuildPlanNode(
                conditional.WhenTrue,
                buildLeaf,
                rewriteMapNew,
                rewriteMapExisting,
                out var whenTrue) ||
            !TryBuildPlanNode(
                conditional.WhenFalse,
                buildLeaf,
                rewriteMapNew,
                rewriteMapExisting,
                out var whenFalse))
        {
            node = null!;
            return false;
        }

        node = new TemplateConditionalMappingPlanNode(
            rewriteMapNew(conditional.Condition),
            rewriteMapExisting(conditional.Condition),
            whenTrue,
            whenFalse);
        return true;
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
        SemanticModel semanticModel,
        IReadOnlyDictionary<ISymbol, string>
            runtimeLocalPlaceholders,
        IReadOnlyCollection<string>
            runtimeLocalPlaceholderNames,
        IReadOnlyDictionary<ISymbol, string>
            localFunctionPlaceholders,
        IReadOnlyCollection<string>
            localFunctionPlaceholderNames)
    {
        var rewritten = new TemplateParameterRewriter(
                sourceParameter,
                destinationParameter,
                destinationExpression,
                semanticModel,
                runtimeLocalPlaceholders,
                runtimeLocalPlaceholderNames,
                localFunctionPlaceholders,
                localFunctionPlaceholderNames)
            .Visit(expression)!
            .WithoutTrivia()
            .NormalizeWhitespace();

        return new NullableSyntaxTriviaRewriter()
            .Visit(rewritten)!
            .ToFullString();
    }

    private static string RewriteLocalFunction(
        LocalFunctionStatementSyntax localFunction,
        IParameterSymbol sourceParameter,
        IParameterSymbol? destinationParameter,
        ExpressionSyntax? destinationExpression,
        SemanticModel semanticModel,
        IReadOnlyDictionary<ISymbol, string>
            runtimeLocalPlaceholders,
        IReadOnlyCollection<string>
            runtimeLocalPlaceholderNames,
        IReadOnlyDictionary<ISymbol, string>
            localFunctionPlaceholders,
        IReadOnlyCollection<string>
            localFunctionPlaceholderNames)
    {
        var rewritten = new TemplateParameterRewriter(
                sourceParameter,
                destinationParameter,
                destinationExpression,
                semanticModel,
                runtimeLocalPlaceholders,
                runtimeLocalPlaceholderNames,
                localFunctionPlaceholders,
                localFunctionPlaceholderNames)
            .Visit(localFunction)!
            .WithoutTrivia()
            .NormalizeWhitespace(
                indentation: "    ",
                eol: "\r\n");

        return new NullableSyntaxTriviaRewriter()
            .Visit(rewritten)!
            .ToFullString();
    }

    private static string RewriteUnsupportedLocalFunction(
        LocalFunctionStatementSyntax localFunction,
        SemanticModel semanticModel,
        IReadOnlyDictionary<ISymbol, string>
            localFunctionPlaceholders)
    {
        var rewritten =
            new UnsupportedLocalFunctionRewriter(
                    semanticModel,
                    localFunctionPlaceholders)
                .Visit(localFunction)!
                .WithoutTrivia()
                .NormalizeWhitespace(
                    indentation: "    ",
                    eol: "\r\n");

        return rewritten.ToFullString();
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
        SemanticModel semanticModel,
        IReadOnlyDictionary<ISymbol, string>
            runtimeLocalPlaceholders,
        IReadOnlyCollection<string>
            runtimeLocalPlaceholderNames,
        IReadOnlyDictionary<ISymbol, string>
            localFunctionPlaceholders,
        IReadOnlyCollection<string>
            localFunctionPlaceholderNames)
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

        public override SyntaxNode? VisitMemberAccessExpression(
            MemberAccessExpressionSyntax node)
        {
            if (semanticModel.GetSymbolInfo(node).Symbol is
                INamedTypeSymbol type)
            {
                return SyntaxFactory.ParseExpression(
                        type.ToDisplayString(
                            SymbolDisplayFormats.FullyQualifiedNullable))
                    .WithTriviaFrom(node);
            }

            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode? VisitLocalFunctionStatement(
            LocalFunctionStatementSyntax node)
        {
            if (semanticModel.GetDeclaredSymbol(node) is not
                IMethodSymbol function)
            {
                return node;
            }

            var returnType =
                SyntaxFactory.ParseTypeName(
                    TypeMapperMappingTypePolicy
                        .GetGeneratedTypeName(
                            function.ReturnType
                                .WithNullableAnnotation(
                                    function
                                        .ReturnNullableAnnotation)))
                    .WithTriviaFrom(node.ReturnType);

            if (node.ReturnType is RefTypeSyntax refReturnType)
            {
                returnType = refReturnType.WithType(
                    returnType);
            }

            var parameters =
                node.ParameterList.Parameters
                    .Select((parameter, index) =>
                    {
                        var parameterSymbol =
                            function.Parameters[index];
                        var rewritten = parameter.WithType(
                            SyntaxFactory.ParseTypeName(
                                    TypeMapperMappingTypePolicy
                                        .GetGeneratedTypeName(
                                            parameterSymbol.Type
                                                .WithNullableAnnotation(
                                                    parameterSymbol
                                                        .NullableAnnotation)))
                                .WithTriviaFrom(
                                    parameter.Type!));

                        if (parameter.Default is { } defaultValue)
                        {
                            rewritten = rewritten.WithDefault(
                                defaultValue.WithValue(
                                    (ExpressionSyntax)
                                    Visit(
                                        defaultValue.Value)!));
                        }

                        return rewritten;
                    });
            var constraints =
                node.ConstraintClauses
                    .Select(clause =>
                        clause.WithConstraints(
                            SyntaxFactory.SeparatedList<
                                TypeParameterConstraintSyntax>(
                                clause.Constraints.Select(
                                    constraint =>
                                        RewriteConstraint(
                                            constraint)))));
            var rewritten = node
                .WithReturnType(returnType)
                .WithParameterList(
                    node.ParameterList.WithParameters(
                        SyntaxFactory.SeparatedList(
                            parameters)))
                .WithConstraintClauses(
                    SyntaxFactory.List(constraints))
                .WithBody(
                    node.Body is null
                        ? null
                        : (BlockSyntax)Visit(node.Body)!)
                .WithExpressionBody(
                    node.ExpressionBody is null
                        ? null
                        : node.ExpressionBody.WithExpression(
                            (ExpressionSyntax)
                            Visit(
                                node.ExpressionBody.Expression)!));

            if (localFunctionPlaceholders.TryGetValue(
                    function,
                    out var placeholder))
            {
                rewritten = rewritten.WithIdentifier(
                    SyntaxFactory.Identifier(placeholder)
                        .WithTriviaFrom(
                            rewritten.Identifier));
            }

            return rewritten;
        }

        public override SyntaxNode? VisitVariableDeclaration(
            VariableDeclarationSyntax node)
        {
            if (node.Type.IsVar)
            {
                return node.WithVariables(
                    VisitList(node.Variables));
            }

            if (semanticModel.GetTypeInfo(node.Type).Type is not
                    { } type)
            {
                return base.VisitVariableDeclaration(node);
            }

            return node
                .WithType(
                    SyntaxFactory.ParseTypeName(
                            TypeMapperMappingTypePolicy
                                .GetGeneratedTypeName(type))
                        .WithTriviaFrom(node.Type))
                .WithVariables(
                    VisitList(node.Variables));
        }

        private TypeParameterConstraintSyntax RewriteConstraint(
            TypeParameterConstraintSyntax constraint)
        {
            if (constraint is not TypeConstraintSyntax typeConstraint ||
                semanticModel.GetTypeInfo(
                        typeConstraint.Type)
                    .Type is not { } type)
            {
                return constraint;
            }

            return typeConstraint.WithType(
                SyntaxFactory.ParseTypeName(
                        TypeMapperMappingTypePolicy
                            .GetGeneratedTypeName(type))
                    .WithTriviaFrom(
                        typeConstraint.Type));
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
            if (runtimeLocalPlaceholderNames.Contains(
                    node.Identifier.ValueText,
                    StringComparer.Ordinal) ||
                localFunctionPlaceholderNames.Contains(
                    node.Identifier.ValueText,
                    StringComparer.Ordinal))
            {
                return node;
            }

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

            if (symbol is not null &&
                localFunctionPlaceholders.TryGetValue(
                    symbol,
                    out var localFunctionPlaceholder))
            {
                return SyntaxFactory.IdentifierName(
                        localFunctionPlaceholder)
                    .WithTriviaFrom(node);
            }

            if (symbol is ILocalSymbol
                {
                    IsConst: true,
                    HasConstantValue: true
                } constantLocal &&
                !runtimeLocalPlaceholders.ContainsKey(
                    constantLocal) &&
                !IsDeclaredInEnclosingLocalFunction(
                    constantLocal,
                    node))
            {
                var literal =
                    constantLocal.ConstantValue is null
                        ? SyntaxFactory.LiteralExpression(
                            SyntaxKind.NullLiteralExpression)
                        : SyntaxFactory.ParseExpression(
                            SymbolDisplay.FormatPrimitive(
                                constantLocal.ConstantValue,
                                quoteStrings: true,
                                useHexadecimalNumbers: false));
                var constantExpression =
                    RequiresConstantCast(constantLocal)
                        ? SyntaxFactory.CastExpression(
                            SyntaxFactory.ParseTypeName(
                                TypeMapperMappingTypePolicy
                                    .GetGeneratedTypeName(
                                        constantLocal.Type)),
                            literal)
                        : literal;

                return constantExpression.WithTriviaFrom(node);
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

            if (symbol is not null &&
                runtimeLocalPlaceholders.TryGetValue(
                    symbol,
                    out var localPlaceholder))
            {
                return SyntaxFactory.IdentifierName(
                        localPlaceholder)
                    .WithTriviaFrom(node);
            }

            if (symbol is INamedTypeSymbol type)
            {
                if (node.Parent is MemberAccessExpressionSyntax
                    {
                        Name: var typeMemberName
                    } &&
                    ReferenceEquals(typeMemberName, node))
                {
                    return node;
                }

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

        private static bool RequiresConstantCast(
            ILocalSymbol constant)
        {
            if (constant.ConstantValue is null ||
                constant.Type.TypeKind == TypeKind.Enum)
            {
                return true;
            }

            return constant.Type.SpecialType is
                SpecialType.System_SByte or
                SpecialType.System_Byte or
                SpecialType.System_Int16 or
                SpecialType.System_UInt16 or
                SpecialType.System_IntPtr or
                SpecialType.System_UIntPtr;
        }

        private static bool IsDeclaredInEnclosingLocalFunction(
            ILocalSymbol local,
            IdentifierNameSyntax reference)
        {
            if (reference.Ancestors()
                    .OfType<LocalFunctionStatementSyntax>()
                    .FirstOrDefault() is not
                { } localFunction)
            {
                return false;
            }

            return local.DeclaringSyntaxReferences.Any(
                declaration =>
                    ReferenceEquals(
                        declaration.SyntaxTree,
                        localFunction.SyntaxTree) &&
                    localFunction.FullSpan.Contains(
                        declaration.Span));
        }

        public override SyntaxNode? VisitGenericName(
            GenericNameSyntax node)
        {
            var symbol = GetReferencedSymbol(node);

            if (symbol is not null &&
                localFunctionPlaceholders.TryGetValue(
                    symbol,
                    out var localFunctionPlaceholder))
            {
                return node.WithIdentifier(
                    SyntaxFactory.Identifier(
                            localFunctionPlaceholder)
                        .WithTriviaFrom(
                            node.Identifier));
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

    private sealed class UnsupportedLocalFunctionRewriter(
        SemanticModel semanticModel,
        IReadOnlyDictionary<ISymbol, string>
            localFunctionPlaceholders)
        : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitLocalFunctionStatement(
            LocalFunctionStatementSyntax node)
        {
            var rewritten =
                (LocalFunctionStatementSyntax)
                base.VisitLocalFunctionStatement(node)!;

            return semanticModel.GetDeclaredSymbol(node) is
                    { } function &&
                localFunctionPlaceholders.TryGetValue(
                    function,
                    out var placeholder)
                ? rewritten.WithIdentifier(
                    SyntaxFactory.Identifier(placeholder)
                        .WithTriviaFrom(
                            rewritten.Identifier))
                : rewritten;
        }

        public override SyntaxNode? VisitIdentifierName(
            IdentifierNameSyntax node)
        {
            return TryGetPlaceholder(
                    node,
                    out var placeholder)
                ? SyntaxFactory.IdentifierName(placeholder)
                    .WithTriviaFrom(node)
                : base.VisitIdentifierName(node);
        }

        public override SyntaxNode? VisitGenericName(
            GenericNameSyntax node)
        {
            return TryGetPlaceholder(
                    node,
                    out var placeholder)
                ? node.WithIdentifier(
                    SyntaxFactory.Identifier(placeholder)
                        .WithTriviaFrom(
                            node.Identifier))
                : base.VisitGenericName(node);
        }

        private bool TryGetPlaceholder(
            SimpleNameSyntax name,
            out string placeholder)
        {
            placeholder = string.Empty;
            var symbol =
                semanticModel.GetSymbolInfo(name).Symbol;

            if (symbol is null &&
                name.Parent is InvocationExpressionSyntax
                {
                    Expression: var invocationExpression
                } invocation &&
                ReferenceEquals(
                    invocationExpression,
                    name))
            {
                symbol =
                    semanticModel.GetSymbolInfo(invocation)
                        .Symbol;
            }

            return symbol is not null &&
                   localFunctionPlaceholders.TryGetValue(
                       symbol,
                       out placeholder);
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

internal abstract record TemplateMappingPlanResult;

internal sealed record UnsupportedTemplateMappingPlanResult(
    string Message)
    : TemplateMappingPlanResult;

internal sealed record SupportedTemplateMappingPlanResult(
    TemplateMappingPlanNode Root,
    ImmutableArray<TemplateRuntimeLocalPlan> RuntimeLocals,
    ImmutableArray<TemplateLocalFunctionPlan> LocalFunctions)
    : TemplateMappingPlanResult;

internal abstract record TemplateMappingPlanNode;

internal sealed record TemplateConditionalMappingPlanNode(
    string MapNewCondition,
    string MapExistingCondition,
    TemplateMappingPlanNode WhenTrue,
    TemplateMappingPlanNode WhenFalse)
    : TemplateMappingPlanNode;

internal sealed record TemplateLeafMappingPlanNode(
    TemplateMappingPlan Plan)
    : TemplateMappingPlanNode;

internal readonly record struct TemplateRuntimeLocalPlan(
    string PlaceholderName,
    string PreferredName,
    string DeclarationType,
    string MapNewExpression,
    string MapExistingExpression);

internal readonly record struct TemplateLocalFunctionPlan(
    string PlaceholderName,
    string PreferredName,
    string Declaration,
    string? UnsupportedMessage);

internal readonly record struct TemplateMappingPlan(
    string? MapNewDirectExpression,
    string? MapExistingDirectExpression,
    ImmutableArray<TemplateMemberMappingModel> MemberMappings,
    TemplateConstructionKind ConstructionKind,
    TemplateConstructorMappingPlan? Constructor,
    TemplateFactoryPlan? Factory,
    ImmutableArray<TemplateConstructorMemberMappingModel>
        ConventionConstructorMappings,
    bool HasDestinationParameter);

internal readonly record struct TemplateFactoryPlan(
    ImmutableArray<TemplateFactoryRuntimeLocalPlan> RuntimeLocals,
    string? ValueExpression,
    string? UnsupportedMessage);

internal readonly record struct TemplateFactoryRuntimeLocalPlan(
    string PlaceholderName,
    string PreferredName,
    string DeclarationType,
    string ValueExpression);

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
