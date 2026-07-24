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
        INamedTypeSymbol? destination,
        ConventionMemberMappingPlan memberMappings,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (destination is null ||
            destination.IsAbstract ||
            TrySelectConstructor(
                destination,
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
            destination,
            constructor,
            candidateArray,
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
                    destination,
                    constructor,
                    argumentArray,
                    compilation,
                    mapperType,
                    cancellationToken))
            {
                return null;
            }
        }
        else if (compatibility.Value
            .HasInvocationNullableWarning)
        {
            return null;
        }

        return BuildPlan(
            argumentArray,
            memberMappings.MapNew,
            setsRequiredMembers,
            mapperType);
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

    private static bool CanOmit(IParameterSymbol parameter)
    {
        return parameter.IsOptional ||
               parameter.IsParams;
    }

    private static ConventionReadableMember?
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

        return probe is { } value &&
               AreSameConstructor(
                   value.Constructor,
                   constructor) &&
               !MappingExpressionCompatibility.HasNullableWarning(
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
        var objectCreation = probeTree
            .GetRoot(cancellationToken)
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Single();
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
                    "__MorphantConstructorTypeCompatibilityProbe(");
                writer.Indent();
                writer.Line($"{sourceTypeName} source)");
                writer.Unindent();
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
        ImmutableArray<ConstructorArgumentCandidate> arguments,
        ImmutableArray<TypeMapperMemberMappingModel> memberMappings,
        bool setsRequiredMembers,
        INamedTypeSymbol mapperType)
    {
        var correspondingArguments =
            new List<int>[memberMappings.Length];

        for (var argumentIndex = 0;
             argumentIndex < arguments.Length;
             argumentIndex++)
        {
            if (FindCorrespondingMemberIndex(
                    memberMappings,
                    arguments[argumentIndex].Parameter.Name) is not
                { } memberIndex)
            {
                continue;
            }

            correspondingArguments[memberIndex] ??=
                new List<int>();
            correspondingArguments[memberIndex]!
                .Add(argumentIndex);
        }

        var argumentModels =
            arguments
                .Select(
                    static argument =>
                        new TypeMapperConstructorArgumentMappingModel(
                            argument.Parameter.Name,
                            argument.SourceMember.Name,
                            SourceValueLocalName: null))
                .ToArray();
        var memberModels =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();
        var usedSourceValueLocalNames =
            BuildUsedSourceValueLocalNames(mapperType);

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

            string? sourceValueLocalName = null;

            if (matchingArguments.Count == 1)
            {
                var argumentIndex = matchingArguments[0];
                var argument = arguments[argumentIndex];

                if (StringComparer.Ordinal.Equals(
                        argument.SourceMember.Name,
                        memberMapping.SourceMemberName))
                {
                    sourceValueLocalName =
                        MakeUniqueSourceValueLocalName(
                            argument.SourceMember.Name,
                            usedSourceValueLocalNames);
                    argumentModels[argumentIndex] =
                        argumentModels[argumentIndex] with
                        {
                            SourceValueLocalName =
                                sourceValueLocalName
                        };
                }
            }

            memberModels.Add(
                memberMapping with
                {
                    SourceValueLocalName =
                        sourceValueLocalName
                });
        }

        return new ConventionConstructorMappingPlan(
            new TypeMapperConstructorMappingModel(
                argumentModels.ToImmutableArray()),
            memberModels.ToImmutable());
    }

    private static HashSet<string> BuildUsedSourceValueLocalNames(
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

    private static string MakeUniqueSourceValueLocalName(
        string sourceMemberName,
        HashSet<string> usedNames)
    {
        var candidate =
            "source" +
            char.ToUpperInvariant(sourceMemberName[0]) +
            sourceMemberName.Substring(1);

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

    private static bool HasSetsRequiredMembersAttribute(
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

    private static bool AreSameConstructor(
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
