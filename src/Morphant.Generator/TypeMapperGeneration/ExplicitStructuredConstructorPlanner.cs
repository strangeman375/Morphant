using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MappingPair;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ExplicitStructuredConstructorPlanner
{
    public static ExplicitStructuredConstructorPlanningResult Build(
        ImmutableArray<StructuredObjectArgument> planArguments,
        ITypeSymbol sourceType,
        ConventionSourceMemberContext sourceContext,
        INamedTypeSymbol destination,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string?> rewriteExpression,
        Func<ExpressionSyntax, IParameterSymbol,
            TypeMapperRewrittenDependencyExpression?>
            rewriteDependencyExpression,
        string nonNullSourceName,
        SyntaxNode strategyOrigin,
        CancellationToken cancellationToken)
    {
        var constructors =
            DestinationCapabilityPolicy.GetSupportedConstructors(
                destination,
                compilation,
                cancellationToken);
        var flatteningIssues =
            ImmutableArray.CreateBuilder<FlatteningIssueObservation>();

        ExplicitStructuredConstructorPlanningResult Unsupported(
            ConstructorCandidateRejectionReason rejection,
            IMethodSymbol? selectedConstructor = null,
            ImmutableArray<ConstructorParameterRuleObservation>
                selectedRules = default) =>
            new(
                Plan: null,
                BuildObservation(
                    constructors,
                    strategyOrigin,
                    selectedConstructor,
                    selectedRules,
                    rejection,
                    flatteningIssues.ToImmutable()));

        if (destination.TypeKind == TypeKind.Interface ||
            destination.IsAbstract)
        {
            return Unsupported(
                destination.IsAbstract
                    ? ConstructorCandidateRejectionReason
                        .AbstractDestination
                    : ConstructorCandidateRejectionReason.StrategyShape);
        }

        if (planArguments.Any(argument =>
                !argument.Syntax.RefKindKeyword.IsKind(
                    SyntaxKind.None)))
        {
            return Unsupported(
                ConstructorCandidateRejectionReason.ExplicitRule);
        }

        if (constructors.IsEmpty)
        {
            return Unsupported(
                ConstructorCandidateRejectionReason.StrategyShape);
        }

        var usedNames = BuildUsedProbeNames(mapperType);
        var probeTypeName = MakeUnique(
            "__MorphantConstructProbe",
            usedNames);
        var probeMethodName = MakeUnique(
            "__MorphantBindConstruct",
            usedNames);
        var destinationProbeMethodName = MakeUnique(
            "__MorphantBindDestinationConstructor",
            usedNames);
        var probeTree = BuildProbeTree(
            sourceType,
            destination,
            constructors,
            planArguments,
            mapperType,
            probeTypeName,
            probeMethodName,
            expression => BuildProbeArgumentExpression(
                expression,
                compilation,
                semanticModel,
                rewriteExpression,
                cancellationToken));

        if (probeTree is null)
        {
            return Unsupported(
                ConstructorCandidateRejectionReason.ExplicitRule);
        }

        var probeCompilation = compilation
            .WithOptions(
                compilation.Options
                    .WithReportSuppressedDiagnostics(true))
            .AddSyntaxTrees(probeTree);
        var probeSemanticModel =
            probeCompilation.GetSemanticModel(probeTree);
        var probeRoot = probeTree.GetRoot(cancellationToken);
        var probeType = probeRoot
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(type =>
                type.Identifier.ValueText == probeTypeName);
        var probeMethod = probeRoot
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method =>
                method.Identifier.ValueText == probeMethodName);

        if (probeMethod.Body?.Statements.LastOrDefault() is not
            ReturnStatementSyntax
            {
                Expression:
                    ObjectCreationExpressionSyntax probeObjectCreation
            })
        {
            return Unsupported(
                ConstructorCandidateRejectionReason.InvocationBinding);
        }

        var selectedProbeConstructor = probeSemanticModel
            .GetSymbolInfo(
                probeObjectCreation,
                cancellationToken)
            .Symbol as IMethodSymbol;

        if (selectedProbeConstructor?.DeclaringSyntaxReferences
                .FirstOrDefault()?
                .GetSyntax(cancellationToken) is not
            ConstructorDeclarationSyntax selectedSyntax)
        {
            return Unsupported(
                ConstructorCandidateRejectionReason.AmbiguousStrategy);
        }

        var probeConstructors = probeType.Members
            .OfType<ConstructorDeclarationSyntax>()
            .ToImmutableArray();
        var selectedConstructorIndex = -1;

        for (var index = 0;
             index < probeConstructors.Length;
             index++)
        {
            if (probeConstructors[index].SpanStart ==
                selectedSyntax.SpanStart)
            {
                selectedConstructorIndex = index;
                break;
            }
        }

        if (selectedConstructorIndex < 0 ||
            selectedConstructorIndex >= constructors.Length)
        {
            return Unsupported(
                ConstructorCandidateRejectionReason.InvocationBinding);
        }

        var destinationConstructor =
            constructors[selectedConstructorIndex];
        var probeArgumentList = probeObjectCreation.ArgumentList;

        if (probeArgumentList is null ||
            probeArgumentList.Arguments.Count != planArguments.Length)
        {
            return Unsupported(
                ConstructorCandidateRejectionReason.InvocationBinding,
                destinationConstructor);
        }

        var arguments =
            ImmutableArray.CreateBuilder<
                TypeMapperConstructorArgumentMappingModel>(
                planArguments.Length);
        var destinationMembers =
            ConventionConstructorMappingPlanner
                .BuildConstructorDestinationMembers(
                    destination,
                    memberObservation: null,
                    compilation,
                    mapperType,
                    cancellationToken);
        var parameterRules = destinationConstructor.Parameters
            .Select(parameter =>
                new ConstructorParameterRuleObservation(
                    parameter,
                    parameter.Name,
                    ConstructorParameterRuleOrigin.Omitted,
                    OriginNode: null,
                    SourceMember: null,
                    ConventionConstructorMappingPlanner
                        .FindAssociatedDestinationMember(
                            destinationMembers,
                            parameter.Name),
                    ConventionConstructorMappingPlanner.CanOmit(parameter),
                    ConventionConstructorMappingPlanner.CanOmit(parameter)
                        ? ConstructorCandidateRejectionReason.None
                        : ConstructorCandidateRejectionReason.ExplicitRule))
            .ToArray();

        for (var index = 0;
             index < probeArgumentList.Arguments.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var probeArgument = probeArgumentList.Arguments[index];

            if (probeSemanticModel.GetOperation(
                    probeArgument,
                    cancellationToken) is not IArgumentOperation
                {
                    Parameter: { } probeParameter
                } ||
                probeParameter.Ordinal < 0 ||
                probeParameter.Ordinal >=
                    destinationConstructor.Parameters.Length)
            {
                return Unsupported(
                    ConstructorCandidateRejectionReason.InvocationBinding,
                    destinationConstructor,
                    parameterRules.ToImmutableArray());
            }

            var destinationParameter =
                destinationConstructor.Parameters[
                    probeParameter.Ordinal];
            var destinationMember =
                parameterRules[probeParameter.Ordinal]
                    .DestinationMember;
            var planArgument = planArguments[index];
            var targetType = DeclarativeIntrinsic
                    .TryGetWrapperTargetType(
                        planArgument.Value,
                        MetadataNames.ConstructorParameter,
                        semanticModel,
                        cancellationToken,
                        out var contextualTargetType)
                ? contextualTargetType
                : destinationParameter.Type.WithNullableAnnotation(
                    destinationParameter.NullableAnnotation);

            if (DeclarativeConstructorMarker.TryGetKind(
                    planArgument.Value,
                    targetType,
                    semanticModel,
                    mapperType,
                    cancellationToken,
                    out var markerKind))
            {
                if (markerKind ==
                    DeclarativeConstructorMarkerKind.Ignore)
                {
                    var canOmit =
                        ConventionConstructorMappingPlanner.CanOmit(
                            destinationParameter);
                    parameterRules[probeParameter.Ordinal] =
                        new ConstructorParameterRuleObservation(
                            destinationParameter,
                            destinationParameter.Name,
                            ConstructorParameterRuleOrigin.Ignore,
                            planArgument.Value,
                            SourceMember: null,
                            destinationMember,
                            canOmit,
                            canOmit
                                ? ConstructorCandidateRejectionReason.None
                                : ConstructorCandidateRejectionReason
                                    .ExplicitRule,
                            planArgument.Syntax.NameColon?.Name ??
                            planArgument.Value);

                    if (!canOmit)
                    {
                        return Unsupported(
                            ConstructorCandidateRejectionReason.ExplicitRule,
                            destinationConstructor,
                            parameterRules.ToImmutableArray());
                    }

                    continue;
                }

                if (markerKind ==
                    DeclarativeConstructorMarkerKind.Auto)
                {
                    var sourceMember =
                        ConventionConstructorMappingPlanner
                            .TryResolveSourceMember(
                                sourceContext,
                                destinationParameter,
                                compilation,
                                mapperType,
                                cancellationToken,
                                out var flatteningIssue,
                                planArgument.Value);

                    if (flatteningIssue is { } issue)
                    {
                        flatteningIssues.Add(issue);
                    }

                    var compatible = sourceMember is { } candidate &&
                        MappingExpressionCompatibility
                            .HasPotentiallyCompatibleConversion(
                                candidate.Type,
                                destinationParameter.Type,
                                compilation);
                    var rejection = sourceMember is null
                        ? ConstructorCandidateRejectionReason
                            .MissingSourceMember
                        : compatible
                            ? ConstructorCandidateRejectionReason.None
                            : ConstructorCandidateRejectionReason
                                .IncompatibleArgument;
                    parameterRules[probeParameter.Ordinal] =
                        new ConstructorParameterRuleObservation(
                            destinationParameter,
                            destinationParameter.Name,
                            ConstructorParameterRuleOrigin.Auto,
                            planArgument.Value,
                            sourceMember?.Symbol,
                            destinationMember,
                            compatible,
                            rejection,
                            planArgument.Syntax.NameColon?.Name ??
                            planArgument.Value,
                            SourcePathMembers: sourceMember is { } resolved
                                ? resolved.GetSourcePathMembers()
                                : default);

                    if (!compatible || sourceMember is null)
                    {
                        return Unsupported(
                            rejection,
                            destinationConstructor,
                            parameterRules.ToImmutableArray());
                    }

                    arguments.Add(
                        new TypeMapperConstructorArgumentMappingModel(
                            destinationParameter.Name,
                            sourceMember.Value.Name,
                            ValueLocalName: null,
                            ConventionValueExpression:
                                sourceMember.Value
                                    .BuildConventionValueExpression(
                                        nonNullSourceName),
                            ConventionProbeValueExpression:
                                sourceMember.Value
                                    .BuildConventionValueExpression(
                                        "source!"),
                            TargetTypeName:
                                ConventionConstructorMappingPlanner
                                    .BuildTargetValueLocalTypeName(
                                        destinationParameter),
                            ParameterSymbol: destinationParameter,
                            SourceMemberSymbol: sourceMember.Value.Symbol,
                            RuleOriginNode: planArgument.Syntax,
                            RuleOrigin:
                                ConstructorParameterRuleOrigin.Auto));
                    continue;
                }
            }

            var rewrittenDependency =
                rewriteDependencyExpression(
                    planArgument.Value,
                    destinationParameter);
            var explicitValueExpression =
                rewrittenDependency?.Expression;

            if (explicitValueExpression is null)
            {
                parameterRules[probeParameter.Ordinal] =
                    new ConstructorParameterRuleObservation(
                        destinationParameter,
                        destinationParameter.Name,
                        ConstructorParameterRuleOrigin.Value,
                        planArgument.Value,
                        SourceMember: null,
                        destinationMember,
                        IsApplicable: false,
                        ConstructorCandidateRejectionReason.ExplicitRule,
                        planArgument.Syntax.NameColon?.Name ??
                        planArgument.Value);

                return Unsupported(
                    ConstructorCandidateRejectionReason.ExplicitRule,
                    destinationConstructor,
                    parameterRules.ToImmutableArray());
            }

            parameterRules[probeParameter.Ordinal] =
                new ConstructorParameterRuleObservation(
                    destinationParameter,
                    destinationParameter.Name,
                    ConstructorParameterRuleOrigin.Value,
                    planArgument.Value,
                    SourceMember: null,
                    destinationMember,
                    IsApplicable: true,
                    ConstructorCandidateRejectionReason.None,
                    planArgument.Syntax.NameColon?.Name ??
                    planArgument.Value);

            arguments.Add(
                new TypeMapperConstructorArgumentMappingModel(
                    destinationParameter.Name,
                    SourceMemberName: string.Empty,
                    ValueLocalName: null,
                    explicitValueExpression,
                    ValueLocalTypeName:
                        ConventionConstructorMappingPlanner
                            .BuildExplicitValueLocalTypeName(
                                destinationParameter),
                    TargetTypeName:
                        ConventionConstructorMappingPlanner
                            .BuildTargetValueLocalTypeName(
                                destinationParameter),
                    DependencyExpression:
                        rewrittenDependency?.DependencyExpression,
                    ParameterSymbol: destinationParameter,
                    RuleOriginNode: planArgument.Syntax,
                    RuleOrigin:
                        ConstructorParameterRuleOrigin.Value));
        }

        var argumentModels = arguments.ToImmutable();

        if (!BindsDestinationConstructor(
                sourceType,
                destination,
                destinationConstructor,
                argumentModels,
                compilation,
                mapperType,
                destinationProbeMethodName,
                cancellationToken))
        {
            return Unsupported(
                ConstructorCandidateRejectionReason.InvocationBinding,
                destinationConstructor,
                parameterRules.ToImmutableArray());
        }

        var observation = BuildObservation(
            constructors,
            strategyOrigin,
            destinationConstructor,
            parameterRules.ToImmutableArray(),
            ConstructorCandidateRejectionReason.None,
            flatteningIssues.ToImmutable());

        return new ExplicitStructuredConstructorPlanningResult(
            new ExplicitStructuredConstructorPlan(
                destinationConstructor,
                argumentModels),
            observation);
    }

    private static SyntaxTree? BuildProbeTree(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        ImmutableArray<IMethodSymbol> constructors,
        ImmutableArray<StructuredObjectArgument> arguments,
        INamedTypeSymbol mapperType,
        string probeTypeName,
        string probeMethodName,
        Func<ExpressionSyntax, string?> rewriteExpression)
    {
        var rewrittenArguments = new string[arguments.Length];

        for (var index = 0; index < arguments.Length; index++)
        {
            if (rewriteExpression(arguments[index].Value) is not
                { } rewritten)
            {
                return null;
            }

            rewrittenArguments[index] = rewritten;
        }

        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                sourceType);
        var destinationTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destination);

        return MapperProbeSyntax.Build(
            mapperType,
            "Morphant.StructuredConstructProbe.g.cs",
            writer =>
            {
                writer.OpenBlock(
                    $"private sealed class {probeTypeName}");

                foreach (var constructor in constructors)
                {
                    var parameters = constructor.Parameters
                        .Select(BuildProbeParameter)
                        .ToArray();

                    writer.Line(
                        $"public {probeTypeName}(" +
                        string.Join(", ", parameters) +
                        ") { }");
                }

                writer.CloseBlock();
                writer.Line();
                writer.Line(
                    $"private {probeTypeName} {probeMethodName}(" +
                    $"{sourceTypeName} source, " +
                    "global::Morphant.Option<" +
                    destinationTypeName +
                    "> previous, " +
                    destinationTypeName +
                    " destination)");
                writer.Line("{");
                writer.Indent();

                if (arguments.IsEmpty)
                {
                    writer.Line(
                        $"return new {probeTypeName}();");
                }
                else
                {
                    writer.Line(
                        $"return new {probeTypeName}(");
                    writer.Indent();

                    for (var index = 0;
                         index < arguments.Length;
                         index++)
                    {
                        var argument = arguments[index];
                        var prefix =
                            argument.Syntax.NameColon is { } nameColon
                                ? nameColon.Name.Identifier.Text + ": "
                                : string.Empty;
                        var suffix = index < arguments.Length - 1
                            ? ","
                            : ");";

                        writer.Line(
                            prefix +
                            rewrittenArguments[index] +
                            suffix);
                    }

                    writer.Unindent();
                }

                writer.Unindent();
                writer.Line("}");
            },
            requiresSystemLinq: arguments.Any(argument =>
                argument.Value.DescendantNodesAndSelf()
                    .OfType<QueryExpressionSyntax>()
                    .Any()));
    }

    private static string BuildProbeParameter(
        IParameterSymbol parameter)
    {
        var parameterType = parameter.Type
            .WithNullableAnnotation(parameter.NullableAnnotation)
            .ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable);
        var optionalSuffix =
            parameter.IsOptional || parameter.IsParams
                ? " = null!"
                : string.Empty;

        return
            "global::Morphant.Members.ConstructorParameter<" +
            parameterType +
            "> " +
            Identifier(parameter.Name) +
            optionalSuffix;
    }

    private static bool BindsDestinationConstructor(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        IMethodSymbol selectedConstructor,
        ImmutableArray<TypeMapperConstructorArgumentMappingModel> arguments,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        string probeMethodName,
        CancellationToken cancellationToken)
    {
        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                sourceType);
        var destinationTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destination);
        var probeTree = MapperProbeSyntax.Build(
            mapperType,
            "Morphant.StructuredDestinationConstructorProbe.g.cs",
            writer =>
            {
                writer.Line(
                    $"private {destinationTypeName} {probeMethodName}(" +
                    $"{sourceTypeName} source, " +
                    "global::Morphant.Option<" +
                    destinationTypeName +
                    "> previous)");
                writer.Line("{");
                writer.Indent();
                var localNames = new GeneratedLocalNameAllocator(
                    mapperType,
                    "source",
                    "previous");

                if (arguments.IsEmpty)
                {
                    writer.Line(
                        $"return new {destinationTypeName}();");
                }
                else
                {
                    writer.Line(
                        $"return new {destinationTypeName}(");
                    writer.Indent();

                    for (var index = 0;
                         index < arguments.Length;
                         index++)
                    {
                        var argument = arguments[index];
                        var suffix = index < arguments.Length - 1
                            ? ","
                            : ");";

                        writer.Line(
                            $"{Identifier(argument.ParameterName)}: " +
                            (argument.ExplicitValueExpression is not null
                                ? "default(" +
                                  (argument.TargetTypeName ?? "object") +
                                  ")"
                                : argument.ConventionProbeValueExpression
                                      ?.Render(localNames) ??
                                  "source." +
                                  Identifier(argument.SourceMemberName)) +
                            suffix);
                    }

                    writer.Unindent();
                }

                writer.Unindent();
                writer.Line("}");
            });
        var probeCompilation = compilation
            .WithOptions(
                compilation.Options
                    .WithReportSuppressedDiagnostics(true))
            .AddSyntaxTrees(probeTree);
        var semanticModel =
            probeCompilation.GetSemanticModel(probeTree);
        var probeMethod = probeTree
            .GetRoot(cancellationToken)
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method =>
                method.Identifier.ValueText == probeMethodName);

        if (probeMethod.Body?.Statements.LastOrDefault() is not
            ReturnStatementSyntax
            {
                Expression:
                    ObjectCreationExpressionSyntax objectCreation
            })
        {
            return false;
        }

        var boundConstructor = semanticModel
            .GetSymbolInfo(
                objectCreation,
                cancellationToken)
            .Symbol as IMethodSymbol;

        if (boundConstructor is null ||
            !ConventionConstructorMappingPlanner.AreSameConstructor(
                boundConstructor,
                selectedConstructor))
        {
            return false;
        }

        var diagnostics = semanticModel.GetDiagnostics(
            cancellationToken: cancellationToken);

        for (var index = 0;
             index < arguments.Length;
             index++)
        {
            if (arguments[index].ExplicitValueExpression is null &&
                MappingExpressionCompatibility.HasNullableWarning(
                    diagnostics,
                    objectCreation.ArgumentList!.Arguments[index].Span))
            {
                return false;
            }
        }

        return true;
    }

    private static string? BuildProbeArgumentExpression(
        ExpressionSyntax expression,
        CSharpCompilation compilation,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string?> rewriteExpression,
        CancellationToken cancellationToken)
    {
        if (expression.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>()
                .Any(identifier =>
                    semanticModel.GetSymbolInfo(
                            identifier,
                            cancellationToken)
                        .Symbol is ILocalSymbol
                    {
                        IsConst: false
                    }))
        {
            var expressionType = semanticModel.GetTypeInfo(
                    expression,
                    cancellationToken)
                .Type;

            if (expressionType is null ||
                expressionType.TypeKind == TypeKind.Error)
            {
                return null;
            }

            return "default(" +
                   TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                       expressionType) +
                   ")";
        }

        if (!TryGetConstructorParameterCast(
                expression,
                compilation,
                semanticModel,
                cancellationToken,
                out var constructorParameterCast,
                out var castType))
        {
            return rewriteExpression(expression);
        }

        if (rewriteExpression(constructorParameterCast.Expression) is not
            { } rewrittenOperand)
        {
            return null;
        }

        return SyntaxFactory.ParseExpression(
                "(" +
                castType.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable) +
                ")" +
                rewrittenOperand)
            .WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString();
    }

    private static bool TryGetConstructorParameterCast(
        ExpressionSyntax expression,
        CSharpCompilation compilation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out CastExpressionSyntax constructorParameterCast,
        out INamedTypeSymbol castType)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        if (expression is not CastExpressionSyntax cast)
        {
            constructorParameterCast = null!;
            castType = null!;
            return false;
        }

        if (compilation.GetTypeByMetadataName(
                MetadataNames.ConstructorParameter) is not
            { } constructorParameterDefinition ||
            semanticModel.GetTypeInfo(
                    cast.Type,
                    cancellationToken)
                .Type is not INamedTypeSymbol resolvedCastType ||
            !SymbolEqualityComparer.Default.Equals(
                resolvedCastType.OriginalDefinition,
                constructorParameterDefinition))
        {
            constructorParameterCast = null!;
            castType = null!;
            return false;
        }

        constructorParameterCast = cast;
        castType = resolvedCastType;
        return true;
    }

    private static string MakeUnique(
        string candidate,
        HashSet<string> usedNames)
    {
        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        for (var suffix = 1;; suffix++)
        {
            var name = candidate + suffix;

            if (usedNames.Add(name))
            {
                return name;
            }
        }
    }

    private static ConstructorPlanningObservation BuildObservation(
        ImmutableArray<IMethodSymbol> constructors,
        SyntaxNode strategyOrigin,
        IMethodSymbol? selectedConstructor,
        ImmutableArray<ConstructorParameterRuleObservation> selectedRules,
        ConstructorCandidateRejectionReason rejection,
        ImmutableArray<FlatteningIssueObservation> flatteningIssues)
    {
        var rules = selectedRules.IsDefault
            ? ImmutableArray<ConstructorParameterRuleObservation>.Empty
            : selectedRules;
        var candidates = constructors.Select(constructor =>
            {
                var isSelected = selectedConstructor is not null &&
                    SymbolEqualityComparer.Default.Equals(
                        constructor,
                        selectedConstructor);

                return new ConstructorCandidateObservation(
                    constructor,
                    isSelected ? rules : ImmutableArray<ConstructorParameterRuleObservation>.Empty,
                    isSelected || selectedConstructor is null
                        ? rejection
                        : ConstructorCandidateRejectionReason.StrategyShape);
            })
            .ToImmutableArray();

        return new ConstructorPlanningObservation(
            ConstructorSelectionValue.Explicit,
            strategyOrigin,
            candidates,
            selectedConstructor,
            Terminals: ImmutableArray<StructuredTerminalObservation>.Empty,
            FlatteningIssues: flatteningIssues);
    }

    private static HashSet<string> BuildUsedProbeNames(
        INamedTypeSymbol mapperType)
    {
        var result = new HashSet<string>(
            mapperType.GetMembers().Select(member => member.Name),
            StringComparer.Ordinal);

        for (var type = mapperType;
             type is not null;
             type = type.ContainingType)
        {
            foreach (var typeParameter in type.TypeParameters)
            {
                result.Add(typeParameter.Name);
            }
        }

        return result;
    }

    private static string Identifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) !=
                   SyntaxKind.None
            ? "@" + value
            : value;
    }
}

internal readonly record struct ExplicitStructuredConstructorPlan(
    IMethodSymbol Constructor,
    ImmutableArray<TypeMapperConstructorArgumentMappingModel> Arguments);

internal readonly record struct ExplicitStructuredConstructorPlanningResult(
    ExplicitStructuredConstructorPlan? Plan,
    ConstructorPlanningObservation Observation);
