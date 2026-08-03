using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ConventionConstructorMappingPlanner
{
    private const string SetsRequiredMembersAttributeMetadataName =
        "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

    public static ConventionConstructorMappingPlan? Build(
        ITypeSymbol sourceType,
        ITypeSymbol? destination,
        ConventionMemberMappingPlan memberMappings,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        return Build(
            sourceType,
            destination,
            memberMappings,
            [],
            [],
            compilation,
            mapperType,
            cancellationToken);
    }

    public static ConventionConstructorMappingPlan? Build(
        ITypeSymbol sourceType,
        ITypeSymbol? destination,
        ConventionMemberMappingPlan memberMappings,
        ImmutableArray<TemplateConstructorParameterMappingModel>
            explicitMappings,
        ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (destination is ITypeParameterSymbol typeParameter)
        {
            if (!explicitMappings.IsEmpty)
            {
                return null;
            }

            return BuildTypeParameterPlan(
                typeParameter,
                memberMappings);
        }

        if (destination is not INamedTypeSymbol namedDestination ||
            namedDestination.IsAbstract ||
            TrySelectConstructor(
                namedDestination,
                compilation,
                mapperType,
                cancellationToken) is not { } constructor)
        {
            return null;
        }

        var setsRequiredMembers =
            HasSetsRequiredMembersAttribute(constructor);

        if (memberMappings.HasUnmappedRequiredMembers &&
            !setsRequiredMembers)
        {
            return null;
        }

        var explicitArguments =
            ImmutableArray.CreateBuilder<
                TypeMapperConstructorArgumentMappingModel>(
                explicitMappings.Length);
        var explicitParameterNames =
            new HashSet<string>(StringComparer.Ordinal);
        var ignoredParameterNames =
            new HashSet<string>(StringComparer.Ordinal);
        var configuredParameterNames =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (var mapping in explicitMappings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryFindParameter(
                    constructor,
                    mapping.ParameterName) is not { } parameter)
            {
                return null;
            }

            if (!configuredParameterNames.Add(parameter.Name))
            {
                return null;
            }

            if (mapping.MarkerKind ==
                TemplateMemberMarkerKind.Ignore)
            {
                if (!CanOmit(parameter))
                {
                    return null;
                }

                ignoredParameterNames.Add(parameter.Name);
                continue;
            }

            if (mapping.MarkerKind ==
                TemplateMemberMarkerKind.Auto)
            {
                continue;
            }

            explicitParameterNames.Add(parameter.Name);

            string explicitValueExpression;

            if (mapping.NestedMap is { } nestedMap)
            {
                if (!TemplateNestedMapMappingPlanner
                    .TryBuildValueExpression(
                        nestedMap,
                        parameter.Type.WithNullableAnnotation(
                            parameter.NullableAnnotation),
                        out explicitValueExpression))
                {
                    return null;
                }
            }
            else
            {
                explicitValueExpression =
                    mapping.ExplicitValueExpression ??
                    throw new InvalidOperationException(
                        "Explicit constructor mapping requires a value.");
            }

            explicitArguments.Add(
                new TypeMapperConstructorArgumentMappingModel(
                    parameter.Name,
                    SourceMemberName: string.Empty,
                    ValueLocalName: null,
                    explicitValueExpression,
                    ValueLocalTypeName:
                        BuildExplicitValueLocalTypeName(
                            parameter),
                    TargetTypeName:
                        BuildTargetValueLocalTypeName(
                            parameter)));
        }

        var explicitArgumentArray =
            explicitArguments.ToImmutable();
        var sourceMembers =
            ConventionMemberMappingPlanner.BuildReadableMembers(
                sourceType,
                compilation,
                mapperType,
                cancellationToken);
        var candidates =
            ImmutableArray.CreateBuilder<
                ConstructorArgumentCandidate>();

        foreach (var parameter in constructor.Parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (explicitParameterNames.Contains(parameter.Name))
            {
                continue;
            }

            if (ignoredParameterNames.Contains(parameter.Name))
            {
                continue;
            }

            if (TryFindSourceMember(
                    sourceMembers,
                    parameter.Name) is not { } sourceMember ||
                !MappingExpressionCompatibility
                    .HasPotentiallyCompatibleConversion(
                        sourceMember.Type,
                        parameter.Type,
                        compilation))
            {
                if (!CanOmit(parameter))
                {
                    return null;
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
            explicitArgumentArray,
            candidateArray,
            runtimeLocals,
            compilation,
            mapperType,
            cancellationToken);

        if (compatibility is null)
        {
            return null;
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
                return null;
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
                    explicitArgumentArray,
                    argumentArray,
                    runtimeLocals,
                    compilation,
                    mapperType,
                    cancellationToken))
            {
                return null;
            }
        }
        else if (explicitArgumentArray.IsEmpty &&
                 compatibility.Value
                     .HasInvocationNullableWarning)
        {
            return null;
        }

        return BuildPlan(
            explicitArgumentArray,
            argumentArray,
            memberMappings.MapNew,
            setsRequiredMembers,
            mapperType,
            namedDestination);
    }

    private static IParameterSymbol? TryFindParameter(
        IMethodSymbol constructor,
        string parameterName)
    {
        foreach (var parameter in constructor.Parameters)
        {
            if (StringComparer.Ordinal.Equals(
                    parameter.Name,
                    parameterName))
            {
                return parameter;
            }
        }

        return null;
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

    internal static string BuildTargetValueLocalTypeName(
        IParameterSymbol parameter)
    {
        return parameter.Type
            .WithNullableAnnotation(
                parameter.NullableAnnotation)
            .ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable);
    }

    private static ConventionConstructorMappingPlan?
        BuildTypeParameterPlan(
            ITypeParameterSymbol destination,
            ConventionMemberMappingPlan memberMappings)
    {
        if ((!destination.HasValueTypeConstraint &&
             !destination.HasUnmanagedTypeConstraint &&
             !destination.HasConstructorConstraint) ||
            memberMappings.HasUnmappedRequiredMembers)
        {
            return null;
        }

        return new ConventionConstructorMappingPlan(
            new TypeMapperConstructorMappingModel(
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    destination),
                []),
            memberMappings.MapNew);
    }

    private static IMethodSymbol? TrySelectConstructor(
        INamedTypeSymbol destination,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        IMethodSymbol? parameterlessConstructor = null;
        IMethodSymbol? parameterizedConstructor = null;

        foreach (var constructor in destination.InstanceConstructors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!compilation.IsSymbolAccessibleWithin(
                    constructor,
                    mapperType) ||
                !IsSupported(constructor))
            {
                continue;
            }

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

    private static bool IsSupported(
        IMethodSymbol constructor)
    {
        return !constructor.Parameters.Any(
            static parameter =>
                parameter.RefKind != RefKind.None ||
                parameter.Type.IsRefLikeType);
    }

    internal static bool CanOmit(IParameterSymbol parameter)
    {
        return parameter.IsOptional ||
               parameter.IsParams;
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

    private static ConstructorCandidateCompatibility?
        FindCompatibleCandidates(
            ITypeSymbol sourceType,
            INamedTypeSymbol destination,
            IMethodSymbol constructor,
            ImmutableArray<TypeMapperConstructorArgumentMappingModel>
                explicitArguments,
            ImmutableArray<ConstructorArgumentCandidate> candidates,
            ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        var probe = BindProbe(
            sourceType,
            destination,
            explicitArguments,
            candidates,
            runtimeLocals,
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
                    .Arguments[
                        explicitArguments.Length + index];
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
        ImmutableArray<TypeMapperConstructorArgumentMappingModel>
            explicitArguments,
        ImmutableArray<ConstructorArgumentCandidate> arguments,
        ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var probe = BindProbe(
            sourceType,
            destination,
            explicitArguments,
            arguments,
            runtimeLocals,
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

        if (explicitArguments.IsEmpty)
        {
            return !MappingExpressionCompatibility.HasNullableWarning(
                value.Diagnostics,
                value.ObjectCreation.Span);
        }

        for (var index = 0;
             index < arguments.Length;
             index++)
        {
            var argument =
                value.ObjectCreation.ArgumentList!
                    .Arguments[
                        explicitArguments.Length + index];

            if (MappingExpressionCompatibility.HasNullableWarning(
                    value.Diagnostics,
                    argument.Span))
            {
                return false;
            }
        }

        return true;
    }

    private static ConstructorProbeBinding? BindProbe(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        ImmutableArray<TypeMapperConstructorArgumentMappingModel>
            explicitArguments,
        ImmutableArray<ConstructorArgumentCandidate> arguments,
        ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var probeTree = BuildProbeTree(
            sourceType,
            destination,
            explicitArguments,
            arguments,
            runtimeLocals,
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
        ImmutableArray<TypeMapperConstructorArgumentMappingModel>
            explicitArguments,
        ImmutableArray<ConstructorArgumentCandidate> arguments,
        ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
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
                    "__MorphantConstructorTypeCompatibilityProbe(");
                writer.Indent();
                writer.Line($"{sourceTypeName} source,");
                writer.Line(
                    "global::Morphant.MappingContext context)");
                writer.Unindent();
                writer.Line("{");
                writer.Indent();

                foreach (var local in runtimeLocals)
                {
                    writer.Line(
                        $"{local.DeclarationType} " +
                        $"{local.PlaceholderName} = " +
                        $"{local.MapNewExpression};");
                }

                if (!runtimeLocals.IsEmpty)
                {
                    writer.Line();
                }

                var argumentCount =
                    explicitArguments.Length + arguments.Length;

                if (argumentCount == 0)
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
                         index < explicitArguments.Length;
                         index++)
                    {
                        var argument = explicitArguments[index];
                        var suffix =
                            index < argumentCount - 1
                                ? ","
                                : ");";

                        writer.Line(
                            $"{Identifier(argument.ParameterName)}: " +
                            (argument.ExplicitValueExpression ??
                             throw new InvalidOperationException(
                                 "Explicit constructor mapping requires a value.")) +
                            suffix);
                    }

                    for (var index = 0;
                         index < arguments.Length;
                         index++)
                    {
                        var argument = arguments[index];
                        var suffix =
                            explicitArguments.Length + index <
                                argumentCount - 1
                                ? ","
                                : ");";

                        writer.Line(
                            $"{Identifier(argument.Parameter.Name)}: " +
                            $"source!.{Identifier(argument.SourceMember.Name)}" +
                            suffix);
                    }

                    writer.Unindent();
                }

                writer.Unindent();
                writer.Line("}");
            });
    }

    private static ConventionConstructorMappingPlan BuildPlan(
        ImmutableArray<TypeMapperConstructorArgumentMappingModel>
            explicitArguments,
        ImmutableArray<ConstructorArgumentCandidate> arguments,
        ImmutableArray<TypeMapperMemberMappingModel> memberMappings,
        bool setsRequiredMembers,
        INamedTypeSymbol mapperType,
        INamedTypeSymbol destination)
    {
        var correspondingArguments =
            new List<int>[memberMappings.Length];

        var argumentModels =
            explicitArguments
                .AddRange(
                    arguments.Select(
                        static argument =>
                            new TypeMapperConstructorArgumentMappingModel(
                                argument.Parameter.Name,
                                argument.SourceMember.Name,
                                ValueLocalName: null,
                                TargetTypeName:
                                    BuildTargetValueLocalTypeName(
                                        argument.Parameter))))
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

            if (memberMapping.ExplicitValueExpression is not null)
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
                var automaticArgumentIndex =
                    argumentIndex - explicitArguments.Length;

                if (automaticArgumentIndex >= 0 &&
                    StringComparer.Ordinal.Equals(
                        arguments[automaticArgumentIndex]
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
                            argument.ExplicitValueExpression is not null
                                ? MakeUniqueValueLocalName(
                                    "template",
                                    argument.ParameterName,
                                    usedValueLocalNames)
                                : MakeUniqueSourceValueLocalName(
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
            memberModels.ToImmutable());
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

    private static int? FindCorrespondingMemberIndex(
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
}

internal readonly record struct ConventionConstructorMappingPlan(
    TypeMapperConstructorMappingModel Constructor,
    ImmutableArray<TypeMapperMemberMappingModel> MapNewMemberMappings);
