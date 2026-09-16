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

        var destinationMembers = BuildConstructorDestinationMembers(
            namedDestination,
            memberMappings.Observation,
            compilation,
            mapperType,
            cancellationToken);
        var resolvedParameters = ResolveParameters(constructors, memberMappings, sourceContext,
            compilation, mapperType, cancellationToken);
        var candidates = constructors.Select(constructor =>
        {
            var parameters = constructor.Parameters.Select(parameter => resolvedParameters[parameter])
                .ToImmutableArray();
            return (Constructor: constructor, Parameters: parameters,
                Preparation: PrepareConstructor(memberMappings, constructor, parameters, cancellationToken));
        }).ToImmutableArray();
        var probes = BindProbes(sourceType, namedDestination,
            candidates.Select(candidate => candidate.Preparation.Arguments).ToImmutableArray(),
            compilation, mapperType, cancellationToken);
        var plannedCandidates = candidates.Select((candidate, index) =>
        {
            var planning = BuildPlanForConstructor(sourceType, namedDestination, memberMappings,
                candidate.Constructor, candidate.Preparation, probes[index],
                compilation, mapperType, nonNullSourceName, cancellationToken);
            return (candidate.Constructor, candidate.Parameters, planning.Plan, planning.FlatteningIssues);
        }).ToImmutableArray();
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
            ? selectedPlan is null
                ? FindSharedFlatteningIssues(
                    plannedCandidates.Select(static candidate =>
                        candidate.FlatteningIssues))
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
                        candidate.Parameters,
                        destinationMembers,
                        memberMappings,
                        cancellationToken))
                .ToImmutableArray(),
            selectedConstructor,
            Terminals: ImmutableArray<StructuredTerminalObservation>.Empty,
            FlatteningIssues: selectedFlatteningIssues);

        return new ConventionConstructorPlanningResult(
            selectedPlan is { } plan
                ? memberMappings.Prepare(plan) with
                {
                    Observation = observation
                }
                : null,
            observation);
    }

    private static Dictionary<IParameterSymbol, ResolvedConstructorParameter> ResolveParameters(
        ImmutableArray<IMethodSymbol> constructors,
        ConstructorInitializationMappingPlan members,
        ConventionSourceMemberContext sourceContext,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<IParameterSymbol, ResolvedConstructorParameter>(SymbolEqualityComparer.Default);
        var automatic = ImmutableArray.CreateBuilder<IParameterSymbol>();
        foreach (var parameter in constructors.SelectMany(constructor => constructor.Parameters))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BuildMemberArgument(members, parameter, compilation, mapperType,
                    out var compatible) is { } memberArgument)
                result.Add(parameter, new ResolvedConstructorParameter(
                    parameter, memberArgument, SourceMember: null, compatible, FlatteningIssue: null));
            else
                automatic.Add(parameter);
        }

        var sources = ConventionConstructorSourceResolver.Resolve(sourceContext,
            automatic.ToImmutable(), compilation, mapperType, cancellationToken);
        for (var index = 0; index < automatic.Count; index++)
        {
            var parameter = automatic[index];
            var source = sources[index];
            result.Add(parameter, new ResolvedConstructorParameter(
                parameter, MemberArgument: null, source.Member,
                source.Member is { } member && MappingExpressionCompatibility.HasPotentiallyCompatibleConversion(
                    member.Type, parameter.Type, compilation), source.Issue));
        }
        return result;
    }

    private static ConstructorPreparation PrepareConstructor(
            ConstructorInitializationMappingPlan memberMappings,
            IMethodSymbol constructor,
            ImmutableArray<ResolvedConstructorParameter> parameters,
            CancellationToken cancellationToken)
    {
        var flatteningIssues =
            ImmutableArray.CreateBuilder<FlatteningIssueObservation>();
        var setsRequiredMembers =
            HasSetsRequiredMembersAttribute(constructor);

        if (memberMappings.HasResultDependency(constructor) ||
            !memberMappings.RequiredObligations.IsEmpty &&
            !setsRequiredMembers)
        {
            return new ConstructorPreparation(
                Arguments: default, setsRequiredMembers,
                flatteningIssues.ToImmutable());
        }

        var candidates =
            ImmutableArray.CreateBuilder<
                ConstructorArgumentCandidate>();

        foreach (var resolved in parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parameter = resolved.Parameter;

            if (resolved.MemberArgument is { } memberArgument)
            {
                if (!resolved.Compatible)
                {
                    return new ConstructorPreparation(
                        Arguments: default, setsRequiredMembers, flatteningIssues.ToImmutable());
                }
                candidates.Add(new ConstructorArgumentCandidate(
                    parameter, SourceMember: null, memberArgument));
                continue;
            }

            if (resolved.SourceMember is not { } sourceMember || !resolved.Compatible)
            {
                if (resolved.FlatteningIssue is { } flatteningIssue)
                {
                    flatteningIssues.Add(flatteningIssue);
                }

                if (!CanOmit(parameter))
                {
                    return new ConstructorPreparation(
                        Arguments: default, setsRequiredMembers,
                        flatteningIssues.ToImmutable());
                }

                continue;
            }

            candidates.Add(
                new ConstructorArgumentCandidate(
                    parameter,
                    sourceMember));
        }

        return new ConstructorPreparation(candidates.ToImmutable(), setsRequiredMembers,
            flatteningIssues.ToImmutable());
    }

    private static ConventionConstructorCandidatePlan BuildPlanForConstructor(
        ITypeSymbol sourceType,
        INamedTypeSymbol namedDestination,
        ConstructorInitializationMappingPlan memberMappings,
        IMethodSymbol constructor,
        ConstructorPreparation preparation,
        ConstructorProbeBinding? probe,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        string nonNullSourceName,
        CancellationToken cancellationToken)
    {
        var candidateArray = preparation.Arguments;
        if (candidateArray.IsDefault)
            return new ConventionConstructorCandidatePlan(Plan: null, preparation.FlatteningIssues);
        var compatibility = FindCompatibleCandidates(constructor, candidateArray, probe, cancellationToken);

        if (compatibility is null)
        {
            return new ConventionConstructorCandidatePlan(
                Plan: null,
                preparation.FlatteningIssues);
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
                    preparation.FlatteningIssues);
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
                    preparation.FlatteningIssues);
            }
        }
        else if (compatibility.Value.HasInvocationNullableWarning)
        {
            return new ConventionConstructorCandidatePlan(
                Plan: null,
                preparation.FlatteningIssues);
        }

        return new ConventionConstructorCandidatePlan(
            BuildPlan(
                argumentArray,
                memberMappings.InitializerMappings,
                memberMappings.PostMappings,
                preparation.SetsRequiredMembers,
                mapperType,
                nonNullSourceName,
                namedDestination),
            preparation.FlatteningIssues);
    }

    private static ConstructorCandidateObservation
        BuildCandidateObservation(
            IMethodSymbol constructor,
            ConventionConstructorMappingPlan? plan,
            ImmutableArray<ResolvedConstructorParameter> parameters,
            ImmutableArray<ISymbol> destinationMembers,
            ConstructorInitializationMappingPlan memberMappings,
            CancellationToken cancellationToken)
    {
        var parameterRules =
            ImmutableArray.CreateBuilder<
                ConstructorParameterRuleObservation>();
        var rejection = ConstructorCandidateRejectionReason.None;

        if (memberMappings.HasResultDependency(constructor))
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

        foreach (var resolved in parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parameter = resolved.Parameter;
            if (resolved.MemberArgument is { } memberArgument)
            {
                var memberRejection = !resolved.Compatible
                    ? ConstructorCandidateRejectionReason.IncompatibleArgument
                    : plan is null
                        ? ConstructorCandidateRejectionReason.InvocationBinding
                        : ConstructorCandidateRejectionReason.None;
                parameterRules.Add(new ConstructorParameterRuleObservation(
                    parameter, parameter.Name, ConstructorParameterRuleOrigin.Value,
                    memberArgument.RuleOriginNode, memberArgument.SourceMemberSymbol,
                    FindAssociatedDestinationMember(destinationMembers, parameter.Name),
                    memberRejection == ConstructorCandidateRejectionReason.None,
                    memberRejection, SourcePathMembers: memberArgument.SourcePathMembers));
                if (rejection == ConstructorCandidateRejectionReason.None)
                {
                    rejection = memberRejection;
                }
                continue;
            }

            var sourceMember = resolved.SourceMember;
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
            else if (!resolved.Compatible)
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
            if (arguments[index].MemberValueTypeName is not null ||
                arguments[index].ExplicitValueExpression is not null)
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
                var localNames = new GeneratedLocalNameAllocator(
                    mapperType,
                    "source");

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
                            argument.MemberValueTypeName is not null
                                ? "default(" + argument.TargetTypeName + ")!"
                                : argument.ExplicitValueExpression is null
                                ? argument.ConventionProbeValueExpression
                                      ?.Render(localNames) ??
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
        var resolved = ConventionConstructorSourceResolver.Resolve(sourceContext,
            ImmutableArray.Create(parameter), compilation, mapperType, cancellationToken, originNode)[0];
        flatteningIssue = resolved.Issue;
        return resolved.Member;
    }

    private static ConstructorCandidateCompatibility? FindCompatibleCandidates(
        IMethodSymbol constructor,
        ImmutableArray<ConstructorArgumentCandidate> candidates,
        ConstructorProbeBinding? probe,
        CancellationToken cancellationToken)
    {
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
        var probe = BindProbes(sourceType, destination, ImmutableArray.Create(arguments),
            compilation, mapperType, cancellationToken)[0];

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

    private static ImmutableArray<ConstructorProbeBinding?> BindProbes(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        ImmutableArray<ImmutableArray<ConstructorArgumentCandidate>> candidates,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var result = new ConstructorProbeBinding?[candidates.Length];
        if (candidates.All(static arguments => arguments.IsDefault))
            return result.ToImmutableArray();

        var tree = BuildProbeTree(sourceType, destination, candidates, mapperType);
        var semanticModel = compilation
            .WithOptions(compilation.Options.WithReportSuppressedDiagnostics(true))
            .AddSyntaxTrees(tree).GetSemanticModel(tree);
        var methods = tree.GetRoot(cancellationToken).DescendantNodes()
            .OfType<MethodDeclarationSyntax>().ToArray();
        var diagnostics = semanticModel.GetDiagnostics(cancellationToken: cancellationToken);
        var methodIndex = 0;
        for (var index = 0; index < candidates.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidates[index].IsDefault) continue;
            var method = methods[methodIndex++];
            if (method.Body?.Statements.LastOrDefault() is ReturnStatementSyntax
                { Expression: ObjectCreationExpressionSyntax creation } &&
                semanticModel.GetSymbolInfo(creation, cancellationToken).Symbol is IMethodSymbol constructor)
                result[index] = new ConstructorProbeBinding(constructor, creation, semanticModel, diagnostics);
        }
        return result.ToImmutableArray();
    }

    private static SyntaxTree BuildProbeTree(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        ImmutableArray<ImmutableArray<ConstructorArgumentCandidate>> candidates,
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
                for (var probeIndex = 0; probeIndex < candidates.Length; probeIndex++)
                {
                    var arguments = candidates[probeIndex];
                    if (arguments.IsDefault) continue;
                    writer.Line(
                        $"private static {destinationTypeName} " +
                        $"__MorphantConstructorTypeCompatibilityProbe{probeIndex}(" +
                        $"{sourceTypeName} source)");
                    writer.Line("{");
                    writer.Indent();
                    var localNames = new GeneratedLocalNameAllocator(
                        mapperType,
                        "source");

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
                                (argument.MemberArgument is { } memberArgument
                                    ? "default(" + memberArgument.MemberValueTypeName + ")!"
                                    : SourceExpression(argument.SourceMember!.Value, "source!", localNames)) +
                                suffix);
                        }

                        writer.Unindent();
                    }

                    writer.Unindent();
                    writer.Line("}");
                }
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
                    argument.MemberArgument ?? new TypeMapperConstructorArgumentMappingModel(
                        argument.Parameter.Name,
                        argument.SourceMember!.Value.Name,
                        ValueLocalName: null,
                        ConventionValueExpression:
                            argument.SourceMember!.Value
                                .BuildConventionValueExpression(
                                    nonNullSourceName),
                        ConventionProbeValueExpression:
                            argument.SourceMember!.Value
                                .BuildConventionValueExpression(
                                    "source!"),
                        TargetTypeName:
                            BuildTargetValueLocalTypeName(
                                argument.Parameter),
                        ParameterSymbol: argument.Parameter,
                        SourceMemberSymbol: argument.SourceMember!.Value.Symbol,
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

            if (matchingArguments.Any(argumentIndex =>
                    argumentModels[argumentIndex].MemberValueTypeName is not null))
            {
                var sharedMember = MoveMemberValueToArguments(
                    argumentModels, memberMapping, matchingArguments,
                    usedValueLocalNames);
                if (memberMapping.IsRequired)
                {
                    memberModels.Add(sharedMember);
                }
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
                            .SourceMember?.Name,
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
                            argument.ValueLocalName ??
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

    internal static TypeMapperConstructorArgumentMappingModel? BuildMemberArgument(
        ConstructorInitializationMappingPlan members,
        IParameterSymbol parameter,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        out bool compatible)
    {
        compatible = true;
        if (FindCorrespondingMemberIndex(members.InitializerMappings, parameter.Name)
                is not { } index)
        {
            return null;
        }

        var member = members.InitializerMappings[index];
        var rule = members.Observation.Rules.LastOrDefault(candidate =>
            candidate.InvalidReason == MemberRuleInvalidReason.None &&
            StringComparer.Ordinal.Equals(candidate.DestinationMember.Name,
                member.DestinationMemberName));
        if (rule?.Origin is not (MemberRuleOrigin.Auto or
                MemberRuleOrigin.ExplicitValue or MemberRuleOrigin.NestedMapping))
        {
            return null;
        }

        var memberType = rule.TargetType ?? rule.DestinationMember switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => parameter.Type
        };
        var semanticMapperType = compilation.GetTypeByMetadataName(
            SymbolNameHelper.GetFullMetadataName(mapperType)) ?? mapperType;
        memberType = MapperTypeSubstitution.Substitute(memberType,
            MapperTypeSubstitution.BuildForHierarchy(semanticMapperType), compilation);
        var conversion = compilation.ClassifyConversion(memberType, parameter.Type);
        compatible = conversion.IsImplicit && !conversion.IsDynamic;
        var valueTypeName = member.ExplicitValueTypeName ??
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(memberType);

        return new TypeMapperConstructorArgumentMappingModel(
            parameter.Name, member.SourceMemberName, ValueLocalName: null,
            ExplicitValueExpression: member.ExplicitValueExpression,
            ConventionValueExpression: member.ConventionValueExpression,
            ValueLocalTypeName: valueTypeName,
            TargetTypeName: BuildTargetValueLocalTypeName(parameter),
            DependencyExpression: member.DependencyExpression,
            EvaluationLocals: member.EvaluationLocals,
            ParameterSymbol: parameter, SourceMemberSymbol: rule.SourceMember,
            RuleOriginNode: rule.OriginNode,
            RuleOrigin: ConstructorParameterRuleOrigin.Value,
            SourcePathMembers: rule.SourcePathMembers,
            MemberValueTypeName: valueTypeName,
            ArgumentCastTypeName: SymbolEqualityComparer.Default.Equals(
                memberType, parameter.Type) ? null : BuildTargetValueLocalTypeName(parameter));
    }

    private static TypeMapperMemberMappingModel MoveMemberValueToArguments(
        TypeMapperConstructorArgumentMappingModel[] arguments,
        TypeMapperMemberMappingModel member,
        IReadOnlyList<int> argumentIndexes,
        HashSet<string> usedNames)
    {
        var firstIndex = argumentIndexes[0];
        // A single unshared argument needs no supporting value local. Keep
        // locals for required initializers, conversions and argument ordering.
        var localName = arguments.Length == 1 && argumentIndexes.Count == 1 &&
            !member.IsRequired && arguments[0].ArgumentCastTypeName is null
                ? null
                : MakeUniqueSourceValueLocalName(member.DestinationMemberName, usedNames);

        foreach (var index in argumentIndexes)
        {
            arguments[index] = arguments[index] with
            {
                SourceMemberName = member.SourceMemberName,
                ValueLocalName = index == firstIndex ? localName : null,
                ExplicitValueExpression = index == firstIndex
                    ? member.ExplicitValueExpression : localName,
                ConventionValueExpression = index == firstIndex ? member.ConventionValueExpression : null,
                ConventionProbeValueExpression = null,
                ValueLocalTypeName = arguments[index].MemberValueTypeName ?? member.ExplicitValueTypeName,
                DependencyExpression = index == firstIndex ? member.DependencyExpression : null,
                EvaluationLocals = index == firstIndex ? member.EvaluationLocals : default,
                SourceMemberSymbol = null,
                SourcePathMembers = default,
                RuleOrigin = ConstructorParameterRuleOrigin.Value
            };
        }

        // Earlier constructor arguments must not move after the new value local.
        for (var index = 0; index < firstIndex; index++)
        {
            if (arguments[index].ValueLocalName is null)
            {
                arguments[index] = arguments[index] with
                {
                    ValueLocalName = MakeUniqueSourceValueLocalName(
                        arguments[index].ParameterName, usedNames)
                };
            }
        }

        return member with
        {
            SourceValueLocalName = localName,
            ExplicitValueExpression = null,
            ValueLocalName = null,
            ConventionValueExpression = null,
            DependencyExpression = null,
            EvaluationLocals = default
        };
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

        if (memberMappings.HasResultDependency(constructor) ||
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
        var argumentModels = arguments.ToArray();
        var usedValueLocalNames = BuildUsedValueLocalNames(mapperType);
        usedValueLocalNames.UnionWith(new[] { nonNullSourceName, "destination", "previous" });
        usedValueLocalNames.UnionWith(arguments.Where(argument => argument.ValueLocalName is not null)
            .Select(argument => argument.ValueLocalName!));

        for (var index = 0;
             index < memberMappings.InitializerMappings.Length;
             index++)
        {
            var mapping = memberMappings.InitializerMappings[index];

            if (correspondingArgumentIndexes[index] is { } memberArguments &&
                memberArguments.Any(argumentIndex =>
                    argumentModels[argumentIndex].MemberValueTypeName is not null))
            {
                var sharedMember = MoveMemberValueToArguments(
                    argumentModels, mapping, memberArguments, usedValueLocalNames);
                if (mapping.IsRequired)
                {
                    create.Add(sharedMember);
                }
                continue;
            }

            var explicitMemberRule = memberMappings.Observation.Rules.Any(rule =>
                rule.InvalidReason == MemberRuleInvalidReason.None &&
                rule.Origin is MemberRuleOrigin.Auto or MemberRuleOrigin.ExplicitValue or MemberRuleOrigin.NestedMapping &&
                StringComparer.Ordinal.Equals(rule.DestinationMember.Name, mapping.DestinationMemberName));

            if (!correspondingMemberIndexes.Contains(index) ||
                explicitMemberRule ||
                mapping.IsRequired && !setsRequiredMembers)
            {
                if (correspondingMemberIndexes.Contains(index) &&
                    !explicitMemberRule &&
                    mapping.IsRequired &&
                    !setsRequiredMembers &&
                    correspondingArgumentIndexes[index] is
                        { Count: 1 } argumentIndexes)
                {
                    var argumentIndex = argumentIndexes[0];
                    var argument = arguments[argumentIndex];

                    var destinationMember = destinationMembers.FirstOrDefault(member =>
                        StringComparer.Ordinal.Equals(member.Name, mapping.DestinationMemberName));
                    var memberType = destinationMember switch
                    {
                        IPropertySymbol property => property.Type,
                        IFieldSymbol field => field.Type,
                        _ => null
                    };
                    if (argument.ParameterSymbol is { } parameter && memberType is not null &&
                        compilation.ClassifyConversion(parameter.Type, memberType) is
                            { IsImplicit: true, IsDynamic: false } &&
                        CanReuseRequiredValue(parameter, mapping, memberType,
                            compilation, mapperType, cancellationToken))
                    {
                        sharedValues.Add(
                            (create.Count, argumentIndex));
                    }
                }

                create.Add(mapping);
            }
        }

        if (sharedValues.Count > 0)
        {
            var lastSharedArgumentIndex =
                sharedValues.Max(static value => value.ArgumentIndex);
            for (var argumentIndex = 0;
                 argumentIndex <= lastSharedArgumentIndex;
                 argumentIndex++)
            {
                var argument = argumentModels[argumentIndex];

                argumentModels[argumentIndex] =
                    argument with
                    {
                        ValueLocalName =
                            argument.ValueLocalName ??
                            (argument.ExplicitValueExpression is not null
                                ? MakeUniqueValueLocalName(
                                    "construct",
                                    argument.ParameterName,
                                    usedValueLocalNames)
                                : MakeUniqueSourceValueLocalName(
                                    argument.SourceMemberName,
                                    usedValueLocalNames))
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

        return memberMappings.Prepare(new ConventionConstructorMappingPlan(
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
                Terminals: ImmutableArray<StructuredTerminalObservation>.Empty)));
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

    internal static ITypeSymbol GetParameterInputType(
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

    private static bool CanReuseRequiredValue(
        IParameterSymbol parameter,
        TypeMapperMemberMappingModel member,
        ITypeSymbol memberType,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var parameterTypeName = GetParameterInputType(parameter)
            .ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable);
        var memberTypeName = member.ExplicitValueTypeName ??
            memberType.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable);
        if (StringComparer.Ordinal.Equals(parameterTypeName, memberTypeName))
            return true;

        var tree = MapperProbeSyntax.Build(mapperType,
            "Morphant.RequiredValueCompatibilityProbe.g.cs",
            writer => writer.Line(
                $"private static {memberTypeName} __MorphantRequiredValueProbe({parameterTypeName} value) => value;"));
        var model = compilation.WithOptions(compilation.Options.WithReportSuppressedDiagnostics(true))
            .AddSyntaxTrees(tree).GetSemanticModel(tree);
        var expression = tree.GetRoot(cancellationToken).DescendantNodes()
            .OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        return model.GetConversion(expression, cancellationToken) is { IsImplicit: true, IsDynamic: false } &&
            !MappingExpressionCompatibility.HasNullableWarning(
                model.GetDiagnostics(cancellationToken: cancellationToken), expression.Span);
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

    internal static ImmutableArray<FlatteningIssueObservation>
        FindSharedFlatteningIssues(
            IEnumerable<ImmutableArray<FlatteningIssueObservation>>
                issueSets)
    {
        var sets = issueSets.Select(static issues =>
                issues.IsDefault
                    ? ImmutableArray<FlatteningIssueObservation>.Empty
                    : issues)
            .ToImmutableArray();

        if (sets.IsEmpty || sets[0].IsEmpty)
        {
            return ImmutableArray<FlatteningIssueObservation>.Empty;
        }

        var result =
            ImmutableArray.CreateBuilder<FlatteningIssueObservation>();

        foreach (var issue in sets[0])
        {
            if (result.Any(candidate =>
                    AreSameFlatteningIssue(candidate, issue)) ||
                sets.Skip(1).Any(issues =>
                    !issues.Any(candidate =>
                        AreSameFlatteningIssue(candidate, issue))))
            {
                continue;
            }

            result.Add(issue);
        }

        return result.ToImmutable();
    }

    private static bool AreSameFlatteningIssue(
        FlatteningIssueObservation left,
        FlatteningIssueObservation right)
    {
        if (!StringComparer.Ordinal.Equals(
                left.TargetName,
                right.TargetName) ||
            left.CandidatePaths.Length != right.CandidatePaths.Length)
        {
            return false;
        }

        for (var index = 0; index < left.CandidatePaths.Length; index++)
        {
            if (!StringComparer.Ordinal.Equals(
                    left.CandidatePaths[index],
                    right.CandidatePaths[index]))
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
        GeneratedLocalNameAllocator localNames) =>
        member.BuildConventionValueExpression(
            sourceName)?.Render(localNames) ??
        sourceName + "." + Identifier(member.Name);

    private readonly record struct ConstructorPreparation(
        ImmutableArray<ConstructorArgumentCandidate> Arguments,
        bool SetsRequiredMembers,
        ImmutableArray<FlatteningIssueObservation> FlatteningIssues);

    private readonly record struct ResolvedConstructorParameter(
        IParameterSymbol Parameter,
        TypeMapperConstructorArgumentMappingModel? MemberArgument,
        ConventionReadableMember? SourceMember,
        bool Compatible,
        FlatteningIssueObservation? FlatteningIssue);

    private readonly record struct ConstructorArgumentCandidate(
        IParameterSymbol Parameter,
        ConventionReadableMember? SourceMember,
        TypeMapperConstructorArgumentMappingModel? MemberArgument = null);

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
    ConstructorPlanningObservation? Observation = null,
    TypeMapperTupleReconstructionModel? TupleReconstruction = null);

internal readonly record struct ConventionConstructorPlanningResult(
    ConventionConstructorMappingPlan? Plan,
    ConstructorPlanningObservation Observation);
