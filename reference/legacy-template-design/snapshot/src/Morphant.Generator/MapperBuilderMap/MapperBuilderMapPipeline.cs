using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.Settings;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.MapperBuilderMap;

internal static class MapperBuilderMapPipeline
{
    public static IncrementalValuesProvider<MapperBuilderMapInfo> Build(
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<TypeMapperConfigureInfo> configureInfos)
    {
        return configureInfos
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuild(source, cancellationToken))
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMapperBuilderMapInfos);
    }

    private static MapperBuilderMapInfo? TryBuild(
        (
            TypeMapperConfigureInfo ConfigureInfo,
            CompilationContext Context
        ) source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (configureInfo, context) = source;

        if (context.KnownSymbols is not { } knownSymbols)
        {
            return null;
        }

        var semanticModel = context.Compilation.GetSemanticModel(
            configureInfo.Syntax.SyntaxTree);

        if (configureInfo.Syntax.ParameterList.Parameters.Count != 1)
        {
            return null;
        }

        var builderParameterSyntax =
            configureInfo.Syntax.ParameterList.Parameters[0];

        if (semanticModel.GetDeclaredSymbol(
                builderParameterSyntax,
                cancellationToken) is not IParameterSymbol builderParameter ||
            !TryGetLinearInvocations(
                configureInfo.Syntax,
                semanticModel,
                builderParameter,
                knownSymbols,
                cancellationToken,
                out var invocations))
        {
            return null;
        }

        var registrations =
            ImmutableArray.CreateBuilder<MapperBuilderMapRegistrationInfo>();
        var seen = new HashSet<MapperBuilderMapIdentity>();
        var settings = MappingSettings.Default;

        for (var invocationIndex = 0;
             invocationIndex < invocations.Length;
             invocationIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = invocations[invocationIndex];

            if (semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken).Symbol is not IMethodSymbol method)
            {
                continue;
            }

            if (IsMapperBuilderMappingModeMethod(
                    method,
                    knownSymbols))
            {
                settings = settings with
                {
                    MappingMode =
                        TryGetMappingMode(
                            invocation,
                            method,
                            semanticModel,
                            cancellationToken,
                            out var rootMappingMode)
                            ? rootMappingMode
                            : null
                };
                continue;
            }

            if (IsMapperBuilderNullSourceHandlingMethod(
                    method,
                    knownSymbols))
            {
                var nullSourceHandling =
                    TryGetNullSourceHandling(
                        invocation,
                        method,
                        semanticModel,
                        cancellationToken,
                        out var parsedNullSourceHandling)
                        ? parsedNullSourceHandling
                        : (NullSourceHandlingValue?)null;

                ApplyNullSourceHandling(
                    invocation,
                    nullSourceHandling,
                    settings,
                    registrations,
                    out settings);
                continue;
            }

            if (IsMapperBuilderNullDestinationHandlingMethod(
                    method,
                    knownSymbols))
            {
                var nullDestinationHandling =
                    TryGetNullDestinationHandling(
                        invocation,
                        method,
                        semanticModel,
                        cancellationToken,
                        out var parsedNullDestinationHandling)
                        ? parsedNullDestinationHandling
                        : (NullDestinationHandlingValue?)null;

                ApplyNullDestinationHandling(
                    invocation,
                    nullDestinationHandling,
                    settings,
                    registrations,
                    out settings);
                continue;
            }

            if (IsMapperBuilderTemplateModeMethod(
                    method,
                    knownSymbols))
            {
                var templateMode =
                    TryGetTemplateMode(
                        invocation,
                        method,
                        semanticModel,
                        cancellationToken,
                        out var parsedTemplateMode)
                        ? parsedTemplateMode
                        : (TemplateModeValue?)null;

                ApplyTemplateMode(
                    invocation,
                    templateMode,
                    settings,
                    registrations,
                    out settings);
                continue;
            }

            if (!IsMapInvocationCandidate(invocation) ||
                !IsMapperBuilderMapMethod(method, knownSymbols))
            {
                continue;
            }

            var mappingSettings = MappingSettings.Default with
            {
                MappingMode =
                    TryGetMappingMode(
                        invocation,
                        method,
                        semanticModel,
                        cancellationToken,
                        out var mappingMode)
                        ? mappingMode
                        : null
            };

            var sourceType = method.TypeArguments[0];
            var destinationType = method.TypeArguments[1];

            var identity = new MapperBuilderMapIdentity(
                sourceType.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable),
                destinationType.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable));

            if (seen.Add(identity))
            {
                registrations.Add(
                    new MapperBuilderMapRegistrationInfo(
                        invocation,
                        FindTemplateInvocations(
                            invocations,
                            invocationIndex + 1,
                            invocation),
                        sourceType,
                        destinationType,
                        mappingSettings));
            }
        }

        return new MapperBuilderMapInfo(
            configureInfo.Syntax,
            settings,
            registrations.ToImmutable());
    }

    private static void ApplyNullSourceHandling(
        InvocationExpressionSyntax invocation,
        NullSourceHandlingValue? value,
        MappingSettings rootSettings,
        ImmutableArray<MapperBuilderMapRegistrationInfo>.Builder
            registrations,
        out MappingSettings updatedRootSettings)
    {
        if (TryFindRegistration(
                invocation,
                registrations,
                out var registrationIndex))
        {
            var registration = registrations[registrationIndex];

            registrations[registrationIndex] = registration with
            {
                Settings = registration.Settings with
                {
                    NullSourceHandling = value
                }
            };
            updatedRootSettings = rootSettings;
            return;
        }

        if (invocation
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(IsMapInvocationCandidate))
        {
            updatedRootSettings = rootSettings;
            return;
        }

        updatedRootSettings = rootSettings with
        {
            NullSourceHandling = value
        };
    }

    private static void ApplyNullDestinationHandling(
        InvocationExpressionSyntax invocation,
        NullDestinationHandlingValue? value,
        MappingSettings rootSettings,
        ImmutableArray<MapperBuilderMapRegistrationInfo>.Builder
            registrations,
        out MappingSettings updatedRootSettings)
    {
        if (TryFindRegistration(
                invocation,
                registrations,
                out var registrationIndex))
        {
            var registration = registrations[registrationIndex];

            registrations[registrationIndex] = registration with
            {
                Settings = registration.Settings with
                {
                    NullDestinationHandling = value
                }
            };
            updatedRootSettings = rootSettings;
            return;
        }

        if (invocation
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(IsMapInvocationCandidate))
        {
            updatedRootSettings = rootSettings;
            return;
        }

        updatedRootSettings = rootSettings with
        {
            NullDestinationHandling = value
        };
    }

    private static void ApplyTemplateMode(
        InvocationExpressionSyntax invocation,
        TemplateModeValue? value,
        MappingSettings rootSettings,
        ImmutableArray<MapperBuilderMapRegistrationInfo>.Builder
            registrations,
        out MappingSettings updatedRootSettings)
    {
        if (TryFindRegistration(
                invocation,
                registrations,
                out var registrationIndex))
        {
            var registration = registrations[registrationIndex];

            registrations[registrationIndex] = registration with
            {
                Settings = registration.Settings with
                {
                    TemplateMode = value
                }
            };
            updatedRootSettings = rootSettings;
            return;
        }

        if (invocation
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(IsMapInvocationCandidate))
        {
            updatedRootSettings = rootSettings;
            return;
        }

        updatedRootSettings = rootSettings with
        {
            TemplateMode = value
        };
    }

    private static bool TryFindRegistration(
        InvocationExpressionSyntax settingInvocation,
        ImmutableArray<MapperBuilderMapRegistrationInfo>.Builder
            registrations,
        out int registrationIndex)
    {
        for (var index = registrations.Count - 1;
             index >= 0;
             index--)
        {
            if (settingInvocation
                .DescendantNodesAndSelf()
                .Contains(registrations[index].Syntax))
            {
                registrationIndex = index;
                return true;
            }
        }

        registrationIndex = -1;
        return false;
    }

    private static bool TryGetMappingMode(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out MappingModeValue mappingMode)
    {
        if (!TryGetInt32Constant(
                invocation,
                method,
                semanticModel,
                cancellationToken,
                out var numericValue) ||
            (numericValue &
             ~(int)MappingModeValue.CreateAndUpdate) != 0)
        {
            mappingMode = default;
            return false;
        }

        mappingMode = (MappingModeValue)numericValue;
        return true;
    }

    private static bool TryGetNullSourceHandling(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out NullSourceHandlingValue nullSourceHandling)
    {
        if (!TryGetInt32Constant(
                invocation,
                method,
                semanticModel,
                cancellationToken,
                out var numericValue) ||
            !Enum.IsDefined(
                typeof(NullSourceHandlingValue),
                numericValue))
        {
            nullSourceHandling = default;
            return false;
        }

        nullSourceHandling =
            (NullSourceHandlingValue)numericValue;
        return true;
    }

    private static bool TryGetNullDestinationHandling(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out NullDestinationHandlingValue nullDestinationHandling)
    {
        if (!TryGetInt32Constant(
                invocation,
                method,
                semanticModel,
                cancellationToken,
                out var numericValue) ||
            !Enum.IsDefined(
                typeof(NullDestinationHandlingValue),
                numericValue))
        {
            nullDestinationHandling = default;
            return false;
        }

        nullDestinationHandling =
            (NullDestinationHandlingValue)numericValue;
        return true;
    }

    private static bool TryGetTemplateMode(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out TemplateModeValue templateMode)
    {
        if (!TryGetInt32Constant(
                invocation,
                method,
                semanticModel,
                cancellationToken,
                out var numericValue) ||
            !Enum.IsDefined(
                typeof(TemplateModeValue),
                numericValue))
        {
            templateMode = default;
            return false;
        }

        templateMode = (TemplateModeValue)numericValue;
        return true;
    }

    private static bool TryGetInt32Constant(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out int numericValue)
    {
        object? value;

        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            if (method.Parameters.Length != 1 ||
                !method.Parameters[0].HasExplicitDefaultValue ||
                method.Parameters[0].ExplicitDefaultValue is not
                    { } defaultValue)
            {
                numericValue = default;
                return false;
            }

            value = defaultValue;
        }
        else if (invocation.ArgumentList.Arguments.Count == 1)
        {
            var expression =
                invocation.ArgumentList.Arguments[0].Expression;
            var constantValue =
                semanticModel.GetConstantValue(
                    expression,
                    cancellationToken);

            if (!constantValue.HasValue)
            {
                numericValue = default;
                return false;
            }

            value = constantValue.Value;
        }
        else
        {
            numericValue = default;
            return false;
        }

        if (value is not int parsedValue)
        {
            numericValue = default;
            return false;
        }

        numericValue = parsedValue;
        return true;
    }

    private static MapperBuilderMapTemplateInfo FindTemplateInvocations(
        ImmutableArray<InvocationExpressionSyntax> invocations,
        int startIndex,
        InvocationExpressionSyntax mapInvocation)
    {
        InvocationExpressionSyntax? sourceTemplate = null;
        InvocationExpressionSyntax? destinationTemplate = null;
        var hasDuplicateSourceTemplate = false;
        var hasDuplicateDestinationTemplate = false;

        for (var index = startIndex;
             index < invocations.Length;
             index++)
        {
            var invocation = invocations[index];

            if (IsMapInvocationCandidate(invocation))
            {
                break;
            }

            if (invocation.Expression is MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "Template"
                } &&
                invocation.DescendantNodes().Contains(mapInvocation) &&
                TryGetTemplateParameterCount(
                    invocation,
                    out var parameterCount))
            {
                if (parameterCount == 1)
                {
                    if (sourceTemplate is null)
                    {
                        sourceTemplate = invocation;
                    }
                    else
                    {
                        hasDuplicateSourceTemplate = true;
                    }
                }
                else if (destinationTemplate is null)
                {
                    destinationTemplate = invocation;
                }
                else
                {
                    hasDuplicateDestinationTemplate = true;
                }
            }
        }

        return new MapperBuilderMapTemplateInfo(
            sourceTemplate,
            destinationTemplate,
            hasDuplicateSourceTemplate,
            hasDuplicateDestinationTemplate);
    }

    private static bool TryGetTemplateParameterCount(
        InvocationExpressionSyntax invocation,
        out int parameterCount)
    {
        parameterCount = default;

        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        switch (invocation.ArgumentList.Arguments[0].Expression)
        {
            case SimpleLambdaExpressionSyntax:
                parameterCount = 1;
                return true;

            case ParenthesizedLambdaExpressionSyntax lambda
                when lambda.ParameterList.Parameters.Count is 1 or 2:
                parameterCount = lambda.ParameterList.Parameters.Count;
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetLinearInvocations(
        MethodDeclarationSyntax configureSyntax,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken,
        out ImmutableArray<InvocationExpressionSyntax> invocations)
    {
        var result =
            ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();

        if (configureSyntax.Body is { } body)
        {
            foreach (var statement in body.Statements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (statement is
                    LocalDeclarationStatementSyntax or
                    LocalFunctionStatementSyntax)
                {
                    continue;
                }

                if (statement is not ExpressionStatementSyntax
                    {
                        Expression: var expression
                    } ||
                    !TryAddInvocationChain(
                        expression,
                        semanticModel,
                        builderParameter,
                        knownSymbols,
                        cancellationToken,
                        result))
                {
                    invocations = default;
                    return false;
                }
            }
        }
        else if (configureSyntax.ExpressionBody is
                 {
                     Expression: var expression
                 })
        {
            if (!TryAddInvocationChain(
                    expression,
                    semanticModel,
                    builderParameter,
                    knownSymbols,
                    cancellationToken,
                    result))
            {
                invocations = default;
                return false;
            }
        }
        else
        {
            invocations = default;
            return false;
        }

        invocations = result.ToImmutable();
        return true;
    }

    private static bool TryAddInvocationChain(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken,
        ImmutableArray<InvocationExpressionSyntax>.Builder result)
    {
        if (ContainsLogicalBranchingOutsideLambdas(
                expression,
                cancellationToken))
        {
            return false;
        }

        var chain = new Stack<InvocationExpressionSyntax>();
        var current = UnwrapParentheses(expression);

        while (current is InvocationExpressionSyntax invocation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            chain.Push(invocation);

            if (invocation.Expression is not MemberAccessExpressionSyntax
                {
                    Expression: var receiver
                })
            {
                return false;
            }

            receiver = UnwrapParentheses(receiver);

            if (receiver is InvocationExpressionSyntax)
            {
                current = receiver;
                continue;
            }

            if (receiver is not IdentifierNameSyntax builderIdentifier ||
                !SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(
                        builderIdentifier,
                        cancellationToken).Symbol,
                    builderParameter) ||
                !IsMapperBuilderRootInvocation(
                    invocation,
                    semanticModel,
                    knownSymbols,
                    cancellationToken))
            {
                return false;
            }

            if (ContainsBuilderReferenceInArguments(
                    chain,
                    semanticModel,
                    builderParameter,
                    cancellationToken))
            {
                return false;
            }

            while (chain.Count > 0)
            {
                result.Add(chain.Pop());
            }

            return true;
        }

        return false;
    }

    private static bool ContainsBuilderReferenceInArguments(
        IEnumerable<InvocationExpressionSyntax> invocations,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in invocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var identifier in argument.Expression
                             .DescendantNodesAndSelf()
                             .OfType<IdentifierNameSyntax>())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (identifier.Identifier.ValueText ==
                            builderParameter.Name &&
                        SymbolEqualityComparer.Default.Equals(
                            semanticModel.GetSymbolInfo(
                                identifier,
                                cancellationToken).Symbol,
                            builderParameter) &&
                        !IsInsideByFactoryArgument(
                            identifier,
                            semanticModel,
                            cancellationToken))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsInsideByFactoryArgument(
        IdentifierNameSyntax identifier,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var argument in identifier
                     .Ancestors()
                     .OfType<ArgumentSyntax>())
        {
            if (argument.Parent is not ArgumentListSyntax
                {
                    Parent:
                        InvocationExpressionSyntax invocation
                } ||
                semanticModel.GetSymbolInfo(
                        invocation,
                        cancellationToken)
                    .Symbol is not IMethodSymbol
                    {
                        Name: "ByFactory",
                        ContainingType: { } containingType
                    } ||
                !StringComparer.Ordinal.Equals(
                    SymbolNameHelper.GetFullMetadataName(
                        containingType),
                    "Morphant.TypeMapper"))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool ContainsLogicalBranchingOutsideLambdas(
        ExpressionSyntax expression,
        CancellationToken cancellationToken)
    {
        foreach (var node in expression.DescendantNodesAndSelf(
                     static node =>
                         node is not AnonymousFunctionExpressionSyntax))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node is ConditionalExpressionSyntax or
                SwitchExpressionSyntax or
                ConditionalAccessExpressionSyntax ||
                node.IsKind(SyntaxKind.LogicalAndExpression) ||
                node.IsKind(SyntaxKind.LogicalOrExpression) ||
                node.IsKind(SyntaxKind.CoalesceExpression) ||
                node.IsKind(SyntaxKind.CoalesceAssignmentExpression))
            {
                return true;
            }
        }

        return false;
    }

    private static ExpressionSyntax UnwrapParentheses(
        ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static bool IsMapperBuilderRootInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetSymbolInfo(
                invocation,
                cancellationToken).Symbol is not IMethodSymbol method ||
            method.IsStatic)
        {
            return false;
        }

        for (var type = knownSymbols.MapperBuilder;
             type is not null &&
             type.SpecialType != SpecialType.System_Object;
             type = type.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    method.ContainingType.OriginalDefinition,
                    type.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMapInvocationCandidate(
        InvocationExpressionSyntax invocation)
    {
        return invocation is
        {
            ArgumentList.Arguments.Count: <= 1,
            Expression: MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax
                {
                    Identifier.ValueText: "Map",
                    TypeArgumentList.Arguments.Count: 2
                }
            }
        };
    }

    private static bool IsMapperBuilderMapMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        return method.Name == "Map" &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 1 &&
               method.TypeArguments.Length == 2 &&
               SymbolEqualityComparer.Default.Equals(
                   method.ContainingType,
                   knownSymbols.MapperBuilder);
    }

    private static bool IsMapperBuilderMappingModeMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        return method.Name == "MappingMode" &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 1 &&
               method.TypeArguments.Length == 0 &&
               SymbolEqualityComparer.Default.Equals(
                   method.ContainingType,
                   knownSymbols.MapperBuilder);
    }

    private static bool IsMapperBuilderNullSourceHandlingMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        return IsMapperBuilderBaseSettingMethod(
            method,
            knownSymbols,
            "NullSourceHandling");
    }

    private static bool IsMapperBuilderNullDestinationHandlingMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        return IsMapperBuilderBaseSettingMethod(
            method,
            knownSymbols,
            "NullDestinationHandling");
    }

    private static bool IsMapperBuilderTemplateModeMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        return IsMapperBuilderBaseSettingMethod(
            method,
            knownSymbols,
            "TemplateMode");
    }

    private static bool IsMapperBuilderBaseSettingMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols,
        string methodName)
    {
        return method.Name == methodName &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 1 &&
               method.TypeArguments.Length == 0 &&
               SymbolEqualityComparer.Default.Equals(
                   method.ContainingType.OriginalDefinition,
                   knownSymbols.MapperBuilderBase);
    }

    private readonly record struct MapperBuilderMapIdentity(
        string SourceType,
        string DestinationType);
}
