using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ConventionConstructorMappingPlanner
{
    private const string AllowNullAttributeMetadataName =
        "System.Diagnostics.CodeAnalysis.AllowNullAttribute";

    private const string DisallowNullAttributeMetadataName =
        "System.Diagnostics.CodeAnalysis.DisallowNullAttribute";

    private const string SetsRequiredMembersAttributeMetadataName =
        "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

    public static ConventionConstructorPlanningResult Build(
        ITypeSymbol sourceType,
        ITypeSymbol? destination,
        ConstructorInitializationMappingPlan memberMappings,
        MappingPairCapabilities capabilities,
        ConstructorSelectionValue? constructorSelection,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        string nonNullSourceName,
        CancellationToken cancellationToken)
    {
        var sourceMembers =
            ConventionMemberMappingPlanner.BuildReadableMembers(
                sourceType,
                compilation,
                mapperType,
                cancellationToken);

        return Build(
            sourceType,
            destination,
            memberMappings,
            capabilities,
            constructorSelection,
            new ConventionSourceMemberContext(
                sourceType,
                sourceMembers,
                ImmutableArray<IncludedSourceScope>.Empty,
                FlatteningValue.None),
            compilation,
            mapperType,
            nonNullSourceName,
            cancellationToken);
    }

    public static ConventionConstructorPlanningResult Build(
        ITypeSymbol sourceType,
        ITypeSymbol? destination,
        ConstructorInitializationMappingPlan memberMappings,
        MappingPairCapabilities capabilities,
        ConstructorSelectionValue? constructorSelection,
        ConventionSourceMemberContext sourceContext,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        string nonNullSourceName,
        CancellationToken cancellationToken)
    {
        ConstructorPlanningObservation EmptyObservation() =>
            new(
                constructorSelection,
                StrategyOrigin: null,
                Candidates: ImmutableArray<ConstructorCandidateObservation>.Empty,
                SelectedConstructor: null,
                Terminals: ImmutableArray<StructuredTerminalObservation>.Empty);

        if (!capabilities.StructuredConstruction)
        {
            return new ConventionConstructorPlanningResult(
                Plan: null,
                EmptyObservation());
        }

        if (destination is not INamedTypeSymbol namedDestination ||
            namedDestination.IsAbstract ||
            constructorSelection is null)
        {
            return new ConventionConstructorPlanningResult(
                Plan: null,
                EmptyObservation());
        }

        var constructors =
            DestinationCapabilityPolicy.GetSupportedConstructors(
                namedDestination,
                compilation,
                cancellationToken);

        var sourceMembers = sourceContext.DirectMembers;
        var destinationMembers = BuildConstructorDestinationMembers(
            namedDestination,
            memberMappings.Observation,
            compilation,
            mapperType,
            cancellationToken);
        var plannedCandidates = constructors.Select(constructor =>
            {
                var planning = BuildPlanForConstructor(
                    sourceType,
                    namedDestination,
                    memberMappings,
                    constructor,
                    sourceContext,
                    compilation,
                    mapperType,
                    nonNullSourceName,
                    cancellationToken);

                return (
                    Constructor: constructor,
                    planning.Plan,
                    planning.FlatteningIssues);
            })
            .ToImmutableArray();
        ConventionConstructorMappingPlan? selectedPlan = null;
        IMethodSymbol? selectedConstructor = null;

        if (constructorSelection == ConstructorSelectionValue.Greediest)
        {
            var selectedArgumentCount = -1;
            var hasTie = false;

            foreach (var candidate in plannedCandidates)
            {
                if (candidate.Plan is not { } candidatePlan)
                {
                    continue;
                }

                var argumentCount =
                    candidatePlan.Constructor.Arguments.Length;

                if (argumentCount > selectedArgumentCount)
                {
                    selectedPlan = candidatePlan;
                    selectedConstructor = candidate.Constructor;
                    selectedArgumentCount = argumentCount;
                    hasTie = false;
                }
                else if (argumentCount == selectedArgumentCount)
                {
                    hasTie = true;
                }
            }

            if (hasTie)
            {
                selectedPlan = null;
                selectedConstructor = null;
            }
        }
        else if (TrySelectConstructor(
                     constructors,
                     constructorSelection.Value) is { } constructor)
        {
            selectedConstructor = constructor;
            selectedPlan = plannedCandidates.First(candidate =>
                    AreSameConstructor(
                        candidate.Constructor,
                        constructor))
                .Plan;
        }

        var selectedFlatteningIssues = selectedConstructor is null
            ? selectedPlan is null && plannedCandidates.Length == 1
                ? plannedCandidates[0].FlatteningIssues
                : ImmutableArray<FlatteningIssueObservation>.Empty
            : plannedCandidates.First(candidate =>
                    AreSameConstructor(
                        candidate.Constructor,
                        selectedConstructor))
                .FlatteningIssues;
        var observation = new ConstructorPlanningObservation(
            constructorSelection,
            StrategyOrigin: null,
            plannedCandidates.Select(candidate =>
                    BuildCandidateObservation(
                        candidate.Constructor,
                        candidate.Plan,
                        sourceContext,
                        destinationMembers,
                        memberMappings,
                        compilation,
                        mapperType,
                        cancellationToken))
                .ToImmutableArray(),
            selectedConstructor,
            Terminals: ImmutableArray<StructuredTerminalObservation>.Empty,
            FlatteningIssues: selectedFlatteningIssues);

        return new ConventionConstructorPlanningResult(
            selectedPlan is { } plan
                ? plan with
                {
                    Observation = observation
                }
                : null,
            observation);
    }

    private static ConventionConstructorCandidatePlan
        BuildPlanForConstructor(
            ITypeSymbol sourceType,
            INamedTypeSymbol namedDestination,
            ConstructorInitializationMappingPlan memberMappings,
            IMethodSymbol constructor,
            ConventionSourceMemberContext sourceContext,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            string nonNullSourceName,
            CancellationToken cancellationToken)
    {
        var flatteningIssues =
            ImmutableArray.CreateBuilder<FlatteningIssueObservation>();
        var setsRequiredMembers =
            HasSetsRequiredMembersAttribute(constructor);

        if (!memberMappings.ResultDependentCreationOnlyRules.IsEmpty ||
            !memberMappings.RequiredObligations.IsEmpty &&
            !setsRequiredMembers)
        {
            return new ConventionConstructorCandidatePlan(
                Plan: null,
                flatteningIssues.ToImmutable());
        }

        var candidates =
            ImmutableArray.CreateBuilder<
                ConstructorArgumentCandidate>();

        foreach (var parameter in constructor.Parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryResolveSourceMember(
                    sourceContext,
                    parameter,
                    compilation,
                    mapperType,
                    cancellationToken,
                    out var flatteningIssue) is not { } sourceMember ||
                !MappingExpressionCompatibility
                    .HasPotentiallyCompatibleConversion(
                        sourceMember.Type,
                        parameter.Type,
                        compilation))
            {
                if (flatteningIssue is not null)
                {
                    flatteningIssues.Add(flatteningIssue);
                }

                if (!CanOmit(parameter))
                {
                    return new ConventionConstructorCandidatePlan(
                        Plan: null,
                        flatteningIssues.ToImmutable());
                }

                continue;
            }

            candidates.Add(
                new ConstructorArgumentCandidate(
                    parameter,
                    sourceMember));
        }

        var candidateArray = candidates.ToImmutable();
        var compatibility = FindCompatibleCandidates(
            sourceType,
            namedDestination,
            constructor,
            candidateArray,
            compilation,
            mapperType,
            cancellationToken);

        if (compatibility is null)
        {
            return new ConventionConstructorCandidatePlan(
                Plan: null,
                flatteningIssues.ToImmutable());
        }

        var compatibleArguments =
            ImmutableArray.CreateBuilder<
                ConstructorArgumentCandidate>();
        var removedOptionalArgument = false;

        for (var index = 0;
             index < candidateArray.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (compatibility.Value.Candidates[index])
            {
                compatibleArguments.Add(candidateArray[index]);
            }
            else if (!CanOmit(candidateArray[index].Parameter))
            {
                return new ConventionConstructorCandidatePlan(
                    Plan: null,
                    flatteningIssues.ToImmutable());
            }
            else
            {
                removedOptionalArgument = true;
            }
        }

        var argumentArray = compatibleArguments.ToImmutable();

        if (removedOptionalArgument)
        {
            if (!BindsSelectedConstructor(
                    sourceType,
                    namedDestination,
                    constructor,
                    argumentArray,
                    compilation,
                    mapperType,
                    cancellationToken))
            {
                return new ConventionConstructorCandidatePlan(
                    Plan: null,
                    flatteningIssues.ToImmutable());
            }
        }
        else if (compatibility.Value.HasInvocationNullableWarning)
        {
            return new ConventionConstructorCandidatePlan(
                Plan: null,
                flatteningIssues.ToImmutable());
        }

        return new ConventionConstructorCandidatePlan(
            BuildPlan(
                argumentArray,
                memberMappings.InitializerMappings,
                memberMappings.PostMappings,
                setsRequiredMembers,
                mapperType,
                nonNullSourceName,
                namedDestination),
            flatteningIssues.ToImmutable());
    }

    private static ConstructorCandidateObservation
        BuildCandidateObservation(
            IMethodSymbol constructor,
            ConventionConstructorMappingPlan? plan,
            ConventionSourceMemberContext sourceContext,
            ImmutableArray<ISymbol> destinationMembers,
            ConstructorInitializationMappingPlan memberMappings,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        var parameterRules =
            ImmutableArray.CreateBuilder<
                ConstructorParameterRuleObservation>();
        var rejection = ConstructorCandidateRejectionReason.None;

        if (!memberMappings.ResultDependentCreationOnlyRules.IsEmpty)
        {
            rejection = ConstructorCandidateRejectionReason
                .ResultDependentInitializer;
        }
        else if (!memberMappings.RequiredObligations.IsEmpty &&
                 !HasSetsRequiredMembersAttribute(constructor))
        {
            rejection = ConstructorCandidateRejectionReason.RequiredMember;
        }

        var hasPlanWideMemberRejection =
            rejection != ConstructorCandidateRejectionReason.None;

        foreach (var parameter in constructor.Parameters)
        {
            var sourceMember = TryResolveSourceMember(
                sourceContext,
                parameter,
                compilation,
                mapperType,
                cancellationToken,
                out _);
            var ruleOrigin = ConstructorParameterRuleOrigin.Convention;
            var ruleRejection = ConstructorCandidateRejectionReason.None;
            var applicable = true;

            if (sourceMember is null)
            {
                ruleOrigin = CanOmit(parameter)
                    ? ConstructorParameterRuleOrigin.Omitted
                    : ConstructorParameterRuleOrigin.Convention;
                applicable = CanOmit(parameter);
                ruleRejection = applicable
                    ? ConstructorCandidateRejectionReason.None
                    : ConstructorCandidateRejectionReason
                        .MissingSourceMember;
            }
            else if (!MappingExpressionCompatibility
                         .HasPotentiallyCompatibleConversion(
                             sourceMember.Value.Type,
                             parameter.Type,
                             compilation))
            {
                applicable = CanOmit(parameter);
                ruleOrigin = applicable
                    ? ConstructorParameterRuleOrigin.Omitted
                    : ConstructorParameterRuleOrigin.Convention;
                ruleRejection = applicable
                    ? ConstructorCandidateRejectionReason.None
                    : ConstructorCandidateRejectionReason
                        .IncompatibleArgument;
            }
            else if (plan is null &&
                     !hasPlanWideMemberRejection &&
                     rejection == ConstructorCandidateRejectionReason.None)
            {
                applicable = false;
                ruleRejection = ConstructorCandidateRejectionReason
                    .InvocationBinding;
            }

            if (rejection == ConstructorCandidateRejectionReason.None &&
                ruleRejection != ConstructorCandidateRejectionReason.None)
            {
                rejection = ruleRejection;
            }

            parameterRules.Add(
                new ConstructorParameterRuleObservation(
                    parameter,
                    parameter.Name,
                    ruleOrigin,
                    OriginNode: null,
                    sourceMember?.Symbol,
                    FindAssociatedDestinationMember(
                        destinationMembers,
                        parameter.Name),
                    applicable,
                    ruleRejection,
                    SourcePathMembers: sourceMember is { } resolvedMember
                        ? resolvedMember.GetSourcePathMembers()
                        : default));
        }

        if (plan is null &&
            rejection == ConstructorCandidateRejectionReason.None)
        {
            rejection = ConstructorCandidateRejectionReason
                .InvocationBinding;
        }

        return new ConstructorCandidateObservation(
            constructor,
            parameterRules.ToImmutable(),
            rejection);
    }

    internal static string BuildTargetValueLocalTypeName(
        IParameterSymbol parameter)
    {
        return parameter.Type
            .WithNullableAnnotation(
                parameter.NullableAnnotation)
            .ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable);
    }

    internal static IMethodSymbol? TrySelectConstructor(
        ImmutableArray<IMethodSymbol> constructors,
        ConstructorSelectionValue constructorSelection)
    {
        return constructorSelection switch
        {
            ConstructorSelectionValue.Default or
            ConstructorSelectionValue.Unambiguous =>
                TrySelectUnambiguousConstructor(constructors),
            ConstructorSelectionValue.Explicit => null,
            ConstructorSelectionValue.Parameterless =>
                constructors.FirstOrDefault(
                    static constructor =>
                        constructor.Parameters.IsEmpty),
            ConstructorSelectionValue.Single =>
                constructors.Length == 1
                    ? constructors[0]
                    : null,
            ConstructorSelectionValue.Largest =>
                TrySelectLargestConstructor(constructors),
            ConstructorSelectionValue.Greediest => null,
            _ => null
        };
    }

    internal static ConventionConstructorMappingPlan?
        TrySelectGreediestPlan(
            ImmutableArray<IMethodSymbol> constructors,
            Func<IMethodSymbol, ConventionConstructorMappingPlan?> buildPlan,
            CancellationToken cancellationToken)
    {
        ConventionConstructorMappingPlan? selectedPlan = null;
        var selectedArgumentCount = -1;
        var hasTie = false;

        foreach (var constructor in constructors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (buildPlan(constructor) is not { } plan)
            {
                continue;
            }

            var argumentCount = plan.Constructor.Arguments.Length;

            if (argumentCount > selectedArgumentCount)
            {
                selectedPlan = plan;
                selectedArgumentCount = argumentCount;
                hasTie = false;
            }
            else if (argumentCount == selectedArgumentCount)
            {
                hasTie = true;
            }
        }

        return hasTie
            ? null
            : selectedPlan;
    }

    private static IMethodSymbol? TrySelectUnambiguousConstructor(
        ImmutableArray<IMethodSymbol> constructors)
    {
        IMethodSymbol? parameterlessConstructor = null;
        IMethodSymbol? parameterizedConstructor = null;

        foreach (var constructor in constructors)
        {
            if (constructor.Parameters.IsEmpty)
            {
                parameterlessConstructor = constructor;
                continue;
            }

            if (parameterizedConstructor is not null)
            {
                return null;
            }

            parameterizedConstructor = constructor;
        }

        return parameterizedConstructor ??
               parameterlessConstructor;
    }

    private static IMethodSymbol? TrySelectLargestConstructor(
        ImmutableArray<IMethodSymbol> constructors)
    {
        IMethodSymbol? selectedConstructor = null;
        var selectedParameterCount = -1;
        var hasTie = false;

        foreach (var constructor in constructors)
        {
            var parameterCount = constructor.Parameters.Length;

            if (parameterCount > selectedParameterCount)
            {
                selectedConstructor = constructor;
                selectedParameterCount = parameterCount;
                hasTie = false;
            }
            else if (parameterCount == selectedParameterCount)
            {
                hasTie = true;
            }
        }

        return hasTie
            ? null
            : selectedConstructor;
    }

    internal static bool CanOmit(IParameterSymbol parameter)
    {
        return parameter.IsOptional ||
               parameter.IsParams;
    }

    internal static bool HasCompatibleAutomaticArguments(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        IMethodSymbol constructor,
        ImmutableArray<TypeMapperConstructorArgumentMappingModel> arguments,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var probeTree = BuildAutomaticArgumentProbeTree(
            sourceType,
            destination,
            arguments,
            mapperType);
        var probeCompilation = compilation
            .WithOptions(
                compilation.Options
                    .WithReportSuppressedDiagnostics(true))
            .AddSyntaxTrees(probeTree);
        var semanticModel =
            probeCompilation.GetSemanticModel(probeTree);
        var objectCreation = probeTree
            .GetRoot(cancellationToken)
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Single();
        var boundConstructor = semanticModel.GetSymbolInfo(
                objectCreation,
                cancellationToken)
            .Symbol as IMethodSymbol;

        if (boundConstructor is null ||
            !AreSameConstructor(boundConstructor, constructor))
        {
            return false;
        }

        var diagnostics = semanticModel.GetDiagnostics(
            cancellationToken: cancellationToken);
        var syntaxArguments = objectCreation.ArgumentList!.Arguments;

        for (var index = 0; index < arguments.Length; index++)
        {
            if (arguments[index].ExplicitValueExpression is not null)
            {
                continue;
            }

            var expression = syntaxArguments[index].Expression;
            var conversion = semanticModel.GetConversion(
                expression,
                cancellationToken);

            if (!conversion.IsImplicit ||
                conversion.IsDynamic ||
                MappingExpressionCompatibility.HasNullableWarning(
                    diagnostics,
                    expression.Span))
            {
                return false;
            }
        }

        return true;
    }

    private static SyntaxTree BuildAutomaticArgumentProbeTree(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        ImmutableArray<TypeMapperConstructorArgumentMappingModel> arguments,
        INamedTypeSymbol mapperType)
    {
        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                sourceType);
        var destinationTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destination);

        return MapperProbeSyntax.Build(
            mapperType,
            "Morphant.ByConventionTypeCompatibilityProbe.g.cs",
            writer =>
            {
                writer.Line(
                    $"private static {destinationTypeName} " +
                    "__MorphantByConventionTypeCompatibilityProbe(" +
                    $"{sourceTypeName} source)");
                writer.Line("{");
                writer.Indent();

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
                        var valueExpression =
                            argument.ExplicitValueExpression is null
                                ? argument.ConventionProbeValueExpression ??
                                  "source!." +
                                  Identifier(argument.SourceMemberName)
                                : "(" + argument.TargetTypeName +
                                  ")default!";
                        var suffix = index < arguments.Length - 1
                            ? ","
                            : ");";

                        writer.Line(
                            $"{Identifier(argument.ParameterName)}: " +
                            valueExpression + suffix);
                    }

                    writer.Unindent();
                }

                writer.Unindent();
                writer.Line("}");
            });
    }

    internal static ConventionReadableMember?
        TryFindSourceMember(
            ImmutableArray<ConventionReadableMember> sourceMembers,
            string parameterName)
    {
        foreach (var sourceMember in sourceMembers)
        {
            if (StringComparer.Ordinal.Equals(
                    sourceMember.Name,
                    parameterName))
            {
                return sourceMember;
            }
        }

        ConventionReadableMember? result = null;

        foreach (var sourceMember in sourceMembers)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(
                    sourceMember.Name,
                    parameterName))
            {
                continue;
            }

            if (result is not null)
            {
                return null;
            }

            result = sourceMember;
        }

        return result;
    }

    internal static ConventionReadableMember? TryResolveSourceMember(
        ConventionSourceMemberContext sourceContext,
        IParameterSymbol parameter,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken,
        out FlatteningIssueObservation? flatteningIssue,
        SyntaxNode? originNode = null)
    {
        var resolution =
            ConventionSourceMemberResolver.ResolveConstructor(
                sourceContext,
                parameter.Name,
                compilation,
                mapperType,
                cancellationToken);

        if (resolution.HasDirectClaim)
        {
            flatteningIssue = null;
            return resolution.Candidates.Length == 1
                ? resolution.Candidates[0]
                : null;
        }

        var compatible = FindCompatibleFlattenedCandidates(
            sourceContext,
            parameter,
            resolution,
            compilation,
            mapperType,
            cancellationToken);

        if (compatible.IsEmpty)
        {
            resolution = ConventionSourceMemberResolver
                .ResolveConstructorCaseInsensitiveFlattened(
                    sourceContext,
                    parameter.Name,
                    compilation,
                    mapperType,
                    cancellationToken);
            compatible = FindCompatibleFlattenedCandidates(
                sourceContext,
                parameter,
                resolution,
                compilation,
                mapperType,
                cancellationToken);
        }

        if (compatible.Length == 1)
        {
            flatteningIssue = null;
            return compatible[0];
        }

        if (compatible.Length > 1)
        {
            flatteningIssue = ConventionMemberMappingPlanner
                .BuildFlatteningIssue(
                    parameter,
                    parameter.Name,
                    compatible,
                    originNode);
            return null;
        }

        flatteningIssue = null;
        return null;
    }

    private static ImmutableArray<ConventionReadableMember>
        FindCompatibleFlattenedCandidates(
            ConventionSourceMemberContext sourceContext,
            IParameterSymbol parameter,
            ConventionSourceMemberResolution resolution,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        var compatible = ConventionSourceValueCompatibility
            .FindCompatibleCandidates(
                sourceContext.RootType,
                GetParameterInputType(parameter),
                resolution.Candidates,
                compilation,
                mapperType,
                cancellationToken);

        if (!compatible.IsEmpty ||
            resolution.FallbackCandidates.IsEmpty)
        {
            return compatible;
        }

        return ConventionSourceValueCompatibility
            .FindCompatibleCandidates(
                sourceContext.RootType,
                GetParameterInputType(parameter),
                resolution.FallbackCandidates,
                compilation,
                mapperType,
                cancellationToken);
    }

    private static ConstructorCandidateCompatibility?
        FindCompatibleCandidates(
            ITypeSymbol sourceType,
            INamedTypeSymbol destination,
            IMethodSymbol constructor,
            ImmutableArray<ConstructorArgumentCandidate> candidates,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        var probe = BindProbe(
            sourceType,
            destination,
            candidates,
            compilation,
            mapperType,
            cancellationToken);

        if (probe is null ||
            !AreSameConstructor(
                probe.Value.Constructor,
                constructor))
        {
            return null;
        }

        var result = ImmutableArray.CreateBuilder<bool>(
            candidates.Length);

        for (var index = 0;
             index < candidates.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var argument =
                probe.Value.ObjectCreation.ArgumentList!
                    .Arguments[index];
            var conversion =
                probe.Value.SemanticModel.GetConversion(
                    argument.Expression,
                    cancellationToken);

            result.Add(
                conversion.IsImplicit &&
                !conversion.IsDynamic &&
                !MappingExpressionCompatibility
                    .HasNullableWarning(
                        probe.Value.Diagnostics,
                        argument.Span));
        }

        return new ConstructorCandidateCompatibility(
            result.ToImmutable(),
            MappingExpressionCompatibility.HasNullableWarning(
                probe.Value.Diagnostics,
                probe.Value.ObjectCreation.Span));
    }

    private static bool BindsSelectedConstructor(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        IMethodSymbol constructor,
        ImmutableArray<ConstructorArgumentCandidate> arguments,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var probe = BindProbe(
            sourceType,
            destination,
            arguments,
            compilation,
            mapperType,
            cancellationToken);

        if (probe is not { } value ||
            !AreSameConstructor(
                value.Constructor,
                constructor))
        {
            return false;
        }

        return !MappingExpressionCompatibility.HasNullableWarning(
            value.Diagnostics,
            value.ObjectCreation.Span);
    }

    private static ConstructorProbeBinding? BindProbe(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        ImmutableArray<ConstructorArgumentCandidate> arguments,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var probeTree = BuildProbeTree(
            sourceType,
            destination,
            arguments,
            mapperType);
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
                method.Identifier.ValueText ==
                "__MorphantConstructorTypeCompatibilityProbe");

        if (probeMethod.Body?.Statements.LastOrDefault() is not
            ReturnStatementSyntax
            {
                Expression:
                    ObjectCreationExpressionSyntax objectCreation
            })
        {
            return null;
        }

        var constructor = semanticModel
            .GetSymbolInfo(
                objectCreation,
                cancellationToken)
            .Symbol as IMethodSymbol;

        if (constructor is null)
        {
            return null;
        }

        return new ConstructorProbeBinding(
            constructor,
            objectCreation,
            semanticModel,
            semanticModel.GetDiagnostics(
                cancellationToken: cancellationToken));
    }

    private static SyntaxTree BuildProbeTree(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        ImmutableArray<ConstructorArgumentCandidate> arguments,
        INamedTypeSymbol mapperType)
    {
        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                sourceType);
        var destinationTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destination);

        return MapperProbeSyntax.Build(
            mapperType,
            "Morphant.ConstructorTypeCompatibilityProbe.g.cs",
            writer =>
            {
                writer.Line(
                    $"private static {destinationTypeName} " +
                    "__MorphantConstructorTypeCompatibilityProbe(" +
                    $"{sourceTypeName} source)");
                writer.Line("{");
                writer.Indent();

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
                        var suffix =
                            index < arguments.Length - 1
                                ? ","
                                : ");";

                        writer.Line(
                            $"{Identifier(argument.Parameter.Name)}: " +
                            SourceExpression(
                                argument.SourceMember,
                                "source!",
                                index) +
                            suffix);
                    }

                    writer.Unindent();
                }

                writer.Unindent();
                writer.Line("}");
            });
    }

    private static ConventionConstructorMappingPlan BuildPlan(
        ImmutableArray<ConstructorArgumentCandidate> arguments,
        ImmutableArray<TypeMapperMemberMappingModel> memberMappings,
        ImmutableArray<TypeMapperMemberMappingModel> postMappings,
        bool setsRequiredMembers,
        INamedTypeSymbol mapperType,
        string nonNullSourceName,
        INamedTypeSymbol destination)
    {
        var correspondingArguments =
            new List<int>[memberMappings.Length];

        var argumentModels = arguments
            .Select(
                argument =>
                    new TypeMapperConstructorArgumentMappingModel(
                        argument.Parameter.Name,
                        argument.SourceMember.Name,
                        ValueLocalName: null,
                        ConventionValueExpression:
                            argument.SourceMember
                                .BuildConventionValueExpression(
                                    nonNullSourceName,
                                    argument.Parameter.Ordinal,
                                    "c"),
                        ConventionProbeValueExpression:
                            argument.SourceMember
                                .BuildConventionValueExpression(
                                    "source!",
                                    argument.Parameter.Ordinal,
                                    "c"),
                        TargetTypeName:
                            BuildTargetValueLocalTypeName(
                                argument.Parameter),
                        ParameterSymbol: argument.Parameter,
                        SourceMemberSymbol: argument.SourceMember.Symbol,
                        RuleOrigin:
                            ConstructorParameterRuleOrigin.Convention))
            .ToArray();

        for (var argumentIndex = 0;
             argumentIndex < argumentModels.Length;
             argumentIndex++)
        {
            if (FindCorrespondingMemberIndex(
                    memberMappings,
                    argumentModels[argumentIndex].ParameterName) is not
                { } memberIndex)
            {
                continue;
            }

            correspondingArguments[memberIndex] ??=
                new List<int>();
            correspondingArguments[memberIndex]!
                .Add(argumentIndex);
        }

        var memberModels =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();
        var sharedValues =
            new List<SharedConstructorValue>();
        var usedValueLocalNames =
            BuildUsedValueLocalNames(mapperType);
        usedValueLocalNames.Add(nonNullSourceName);

        for (var memberIndex = 0;
             memberIndex < memberMappings.Length;
             memberIndex++)
        {
            var memberMapping = memberMappings[memberIndex];
            var matchingArguments =
                correspondingArguments[memberIndex];

            if (matchingArguments is null)
            {
                memberModels.Add(memberMapping);
                continue;
            }

            if (!memberMapping.IsRequired ||
                setsRequiredMembers)
            {
                continue;
            }

            if (matchingArguments.Count == 1)
            {
                var argumentIndex = matchingArguments[0];

                if (StringComparer.Ordinal.Equals(
                        arguments[argumentIndex]
                            .SourceMember.Name,
                        memberMapping.SourceMemberName))
                {
                    sharedValues.Add(
                        new SharedConstructorValue(
                            memberModels.Count,
                            argumentIndex));
                }
            }

            memberModels.Add(memberMapping);
        }

        if (sharedValues.Count > 0)
        {
            var lastSharedArgumentIndex =
                sharedValues.Max(
                    static value =>
                        value.ArgumentIndex);

            for (var argumentIndex = 0;
                 argumentIndex <= lastSharedArgumentIndex;
                 argumentIndex++)
            {
                var argument = argumentModels[argumentIndex];

                argumentModels[argumentIndex] =
                    argument with
                    {
                        ValueLocalName =
                            MakeUniqueSourceValueLocalName(
                                argument.SourceMemberName,
                                usedValueLocalNames)
                    };
            }

            foreach (var sharedValue in sharedValues)
            {
                var memberMapping =
                    memberModels[sharedValue.MemberIndex];

                memberModels[sharedValue.MemberIndex] =
                    memberMapping with
                    {
                        SourceValueLocalName =
                            argumentModels[
                                sharedValue.ArgumentIndex]
                                .ValueLocalName
                    };
            }
        }

        return new ConventionConstructorMappingPlan(
            new TypeMapperConstructorMappingModel(
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    destination),
                argumentModels.ToImmutableArray()),
            memberModels.ToImmutable(),
            postMappings);
    }

    internal static HashSet<string> BuildUsedValueLocalNames(
        INamedTypeSymbol mapperType)
    {
        var result = new HashSet<string>(StringComparer.Ordinal)
        {
            "source",
            "context"
        };

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

    internal static string MakeUniqueSourceValueLocalName(
        string sourceMemberName,
        HashSet<string> usedNames)
    {
        return MakeUniqueValueLocalName(
            "source",
            sourceMemberName,
            usedNames);
    }

    internal static string MakeUniqueValueLocalName(
        string prefix,
        string valueName,
        HashSet<string> usedNames)
    {
        var candidate =
            prefix +
            char.ToUpperInvariant(valueName[0]) +
            valueName.Substring(1);

        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        for (var suffix = 1;; suffix++)
        {
            var name =
                candidate +
                suffix.ToString(CultureInfo.InvariantCulture);

            if (usedNames.Add(name))
            {
                return name;
            }
        }
    }

    internal static int? FindCorrespondingMemberIndex(
        ImmutableArray<TypeMapperMemberMappingModel> memberMappings,
        string parameterName)
    {
        for (var index = 0;
             index < memberMappings.Length;
             index++)
        {
            if (StringComparer.Ordinal.Equals(
                    memberMappings[index].DestinationMemberName,
                    parameterName))
            {
                return index;
            }
        }

        int? result = null;

        for (var index = 0;
             index < memberMappings.Length;
             index++)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(
                    memberMappings[index].DestinationMemberName,
                    parameterName))
            {
                continue;
            }

            if (result is not null)
            {
                return null;
            }

            result = index;
        }

        return result;
    }

    internal static ConventionConstructorMappingPlan? BuildExplicitPlan(
        ITypeSymbol destination,
        ConstructorInitializationMappingPlan memberMappings,
        IMethodSymbol constructor,
        ImmutableArray<TypeMapperConstructorArgumentMappingModel> arguments,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        string nonNullSourceName,
        CancellationToken cancellationToken)
    {
        var setsRequiredMembers =
            HasSetsRequiredMembersAttribute(constructor);
        var destinationMembers = BuildConstructorDestinationMembers(
            destination,
            memberMappings.Observation,
            compilation,
            mapperType,
            cancellationToken);

        if (!memberMappings.ResultDependentCreationOnlyRules.IsEmpty ||
            !memberMappings.RequiredObligations.IsEmpty &&
            !setsRequiredMembers)
        {
            return null;
        }

        var correspondingMemberIndexes = new HashSet<int>();

        foreach (var argument in arguments)
        {
            if (FindCorrespondingMemberIndex(
                    memberMappings.InitializerMappings,
                    argument.ParameterName) is { } memberIndex)
            {
                correspondingMemberIndexes.Add(memberIndex);
            }
        }

        var correspondingArgumentIndexes =
            new List<int>[memberMappings.InitializerMappings.Length];

        for (var argumentIndex = 0;
             argumentIndex < arguments.Length;
             argumentIndex++)
        {
            if (FindCorrespondingMemberIndex(
                    memberMappings.InitializerMappings,
                    arguments[argumentIndex].ParameterName) is not
                { } memberIndex)
            {
                continue;
            }

            correspondingArgumentIndexes[memberIndex] ??=
                new List<int>();
            correspondingArgumentIndexes[memberIndex]!
                .Add(argumentIndex);
        }

        var create =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var sharedValues =
            new List<(int MemberIndex, int ArgumentIndex)>();

        for (var index = 0;
             index < memberMappings.InitializerMappings.Length;
             index++)
        {
            var mapping = memberMappings.InitializerMappings[index];

            if (!correspondingMemberIndexes.Contains(index) ||
                mapping.ExplicitValueExpression is not null ||
                mapping.IsRequired && !setsRequiredMembers)
            {
                if (correspondingMemberIndexes.Contains(index) &&
                    mapping.ExplicitValueExpression is null &&
                    mapping.IsRequired &&
                    !setsRequiredMembers &&
                    correspondingArgumentIndexes[index] is
                        { Count: 1 } argumentIndexes)
                {
                    var argumentIndex = argumentIndexes[0];
                    var argument = arguments[argumentIndex];

                    if (argument.ExplicitValueExpression is null &&
                        StringComparer.Ordinal.Equals(
                            argument.SourceMemberName,
                            mapping.SourceMemberName))
                    {
                        sharedValues.Add(
                            (create.Count, argumentIndex));
                    }
                }

                create.Add(mapping);
            }
        }

        var argumentModels = arguments.ToArray();

        if (sharedValues.Count > 0)
        {
            var lastSharedArgumentIndex =
                sharedValues.Max(static value => value.ArgumentIndex);
            var usedValueLocalNames =
                BuildUsedValueLocalNames(mapperType);

            usedValueLocalNames.Add(nonNullSourceName);
            usedValueLocalNames.Add("destination");
            usedValueLocalNames.Add("previous");

            for (var argumentIndex = 0;
                 argumentIndex <= lastSharedArgumentIndex;
                 argumentIndex++)
            {
                var argument = argumentModels[argumentIndex];

                argumentModels[argumentIndex] =
                    argument with
                    {
                        ValueLocalName =
                            argument.ExplicitValueExpression is not null
                                ? MakeUniqueValueLocalName(
                                    "construct",
                                    argument.ParameterName,
                                    usedValueLocalNames)
                                : MakeUniqueSourceValueLocalName(
                                    argument.SourceMemberName,
                                    usedValueLocalNames)
                    };
            }

            foreach (var sharedValue in sharedValues)
            {
                var memberMapping = create[sharedValue.MemberIndex];

                create[sharedValue.MemberIndex] =
                    memberMapping with
                    {
                        SourceValueLocalName =
                            argumentModels[sharedValue.ArgumentIndex]
                                .ValueLocalName
                    };
            }
        }

        return new ConventionConstructorMappingPlan(
            new TypeMapperConstructorMappingModel(
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    destination),
                argumentModels.ToImmutableArray()),
            create.ToImmutable(),
            memberMappings.PostMappings,
            new ConstructorPlanningObservation(
                ConstructorSelectionValue.Explicit,
                StrategyOrigin: null,
                Candidates:
                ImmutableArray.Create<ConstructorCandidateObservation>(
                    new ConstructorCandidateObservation(
                        constructor,
                        constructor.Parameters.Select(parameter =>
                            {
                                var argument = arguments.FirstOrDefault(
                                    candidate =>
                                        StringComparer.Ordinal.Equals(
                                            candidate.ParameterName,
                                            parameter.Name));
                                var hasArgument = !String.IsNullOrEmpty(
                                    argument.ParameterName);

                                return new
                                    ConstructorParameterRuleObservation(
                                        parameter,
                                        parameter.Name,
                                        !hasArgument
                                            ? ConstructorParameterRuleOrigin
                                                .Omitted
                                            : argument.RuleOrigin ??
                                              ConstructorParameterRuleOrigin
                                                  .Value,
                                        argument.RuleOriginNode,
                                        argument.SourceMemberSymbol,
                                        FindAssociatedDestinationMember(
                                            destinationMembers,
                                            parameter.Name),
                                        IsApplicable: true,
                                        ConstructorCandidateRejectionReason
                                            .None);
                            })
                            .ToImmutableArray(),
                        ConstructorCandidateRejectionReason.None)
                ),
                constructor,
                Terminals: ImmutableArray<StructuredTerminalObservation>.Empty));
    }

    internal static string BuildExplicitValueLocalTypeName(
        IParameterSymbol parameter)
    {
        var nullableAnnotation =
            parameter.Type.IsReferenceType ||
            parameter.Type is ITypeParameterSymbol
            {
                HasValueTypeConstraint: false,
                HasUnmanagedTypeConstraint: false
            }
                ? NullableAnnotation.Annotated
                : parameter.NullableAnnotation;

        return parameter.Type
            .WithNullableAnnotation(nullableAnnotation)
            .ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable);
    }

    internal static ISymbol? FindAssociatedDestinationMember(
        ImmutableArray<ISymbol> members,
        string parameterName)
    {
        foreach (var member in members)
        {
            if (StringComparer.Ordinal.Equals(
                    member.Name,
                    parameterName))
            {
                return member;
            }
        }

        ISymbol? result = null;

        foreach (var member in members)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(
                    member.Name,
                    parameterName))
            {
                continue;
            }

            if (result is not null)
            {
                return null;
            }

            result = member;
        }

        return result;
    }

    internal static ImmutableArray<ISymbol>
        BuildConstructorDestinationMembers(
            ITypeSymbol destination,
            MemberPlanningObservation? memberObservation,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<ISymbol>();

        void Add(ISymbol member)
        {
            if (!result.Any(candidate =>
                    SymbolEqualityComparer.Default.Equals(
                        candidate,
                        member)))
            {
                result.Add(member);
            }
        }

        if (memberObservation is { } observation)
        {
            foreach (var member in observation.SupportedDestinationMembers)
            {
                Add(member);
            }
        }

        foreach (var member in ConventionMemberMappingPlanner
                     .BuildReadableMembers(
                         destination,
                         compilation,
                         mapperType,
                         cancellationToken))
        {
            Add(member.Symbol);
        }

        return result.ToImmutable();
    }

    internal static bool HasSetsRequiredMembersAttribute(
        IMethodSymbol constructor)
    {
        foreach (var attribute in constructor.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeType &&
                SymbolNameHelper.GetFullMetadataName(
                    attributeType) ==
                SetsRequiredMembersAttributeMetadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static ITypeSymbol GetParameterInputType(
        IParameterSymbol parameter)
    {
        var annotation = parameter.NullableAnnotation;

        if (parameter.Type.IsReferenceType ||
            parameter.Type.TypeKind == TypeKind.TypeParameter)
        {
            if (HasAttribute(
                    parameter,
                    DisallowNullAttributeMetadataName))
            {
                annotation = NullableAnnotation.NotAnnotated;
            }
            else if (HasAttribute(
                         parameter,
                         AllowNullAttributeMetadataName))
            {
                annotation = NullableAnnotation.Annotated;
            }
            else if (annotation == NullableAnnotation.None)
            {
                annotation = NullableAnnotation.Annotated;
            }
        }

        return parameter.Type.WithNullableAnnotation(annotation);
    }

    private static bool HasAttribute(
        ISymbol symbol,
        string metadataName) =>
        symbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { } attributeType &&
            StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(attributeType),
                metadataName));

    internal static bool AreSameConstructor(
        IMethodSymbol left,
        IMethodSymbol right)
    {
        var leftDocumentationId =
            left.GetDocumentationCommentId();
        var rightDocumentationId =
            right.GetDocumentationCommentId();

        if (leftDocumentationId is not null ||
            rightDocumentationId is not null)
        {
            return StringComparer.Ordinal.Equals(
                leftDocumentationId,
                rightDocumentationId);
        }

        if (!StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    left.ContainingType),
                SymbolNameHelper.GetFullMetadataName(
                    right.ContainingType)) ||
            left.Parameters.Length !=
            right.Parameters.Length)
        {
            return false;
        }

        for (var index = 0;
             index < left.Parameters.Length;
             index++)
        {
            var leftParameter = left.Parameters[index];
            var rightParameter = right.Parameters[index];

            if (leftParameter.RefKind !=
                    rightParameter.RefKind ||
                !StringComparer.Ordinal.Equals(
                    leftParameter.Type.ToDisplayString(
                        SymbolDisplayFormats
                            .FullyQualifiedNullable),
                    rightParameter.Type.ToDisplayString(
                        SymbolDisplayFormats
                            .FullyQualifiedNullable)))
            {
                return false;
            }
        }

        return true;
    }

    private static string Identifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) !=
                   SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) !=
                   SyntaxKind.None
            ? "@" + value
            : value;
    }

    private static string SourceExpression(
        ConventionReadableMember member,
        string sourceName,
        int expressionIndex) =>
        member.BuildConventionValueExpression(
            sourceName,
            expressionIndex,
            "c") ??
        sourceName + "." + Identifier(member.Name);

    private readonly record struct ConstructorArgumentCandidate(
        IParameterSymbol Parameter,
        ConventionReadableMember SourceMember);

    private readonly record struct SharedConstructorValue(
        int MemberIndex,
        int ArgumentIndex);

    private readonly record struct ConstructorCandidateCompatibility(
        ImmutableArray<bool> Candidates,
        bool HasInvocationNullableWarning);

    private readonly record struct ConstructorProbeBinding(
        IMethodSymbol Constructor,
        ObjectCreationExpressionSyntax ObjectCreation,
        SemanticModel SemanticModel,
        ImmutableArray<Diagnostic> Diagnostics);

    private readonly record struct ConventionConstructorCandidatePlan(
        ConventionConstructorMappingPlan? Plan,
        ImmutableArray<FlatteningIssueObservation> FlatteningIssues);
}

internal readonly record struct ConventionConstructorMappingPlan(
    TypeMapperConstructorMappingModel Constructor,
    ImmutableArray<TypeMapperMemberMappingModel> CreateMemberMappings,
    ImmutableArray<TypeMapperMemberMappingModel> CreatePostMemberMappings,
    ConstructorPlanningObservation? Observation = null);

internal readonly record struct ConventionConstructorPlanningResult(
    ConventionConstructorMappingPlan? Plan,
    ConstructorPlanningObservation Observation);
