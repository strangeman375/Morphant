using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class IncludedSourceMemberSet
{
    public static IncludedSourceMemberSetResult Build(
        ITypeSymbol sourceType,
        ImmutableArray<IncludeMembersConfigurationModel> configurations,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var rootMembers = ConventionMemberMappingPlanner.BuildReadableMembers(
            sourceType,
            compilation,
            mapperType,
            cancellationToken);

        if (configurations.IsEmpty)
        {
            return new IncludedSourceMemberSetResult(
                rootMembers,
                ImmutableArray<ISymbol>.Empty,
                ImmutableArray<IncludeMembersIssueObservation>.Empty,
                ImmutableArray<IncludedSourceScope>.Empty);
        }

        var issues =
            ImmutableArray.CreateBuilder<IncludeMembersIssueObservation>();
        var scopes =
            ImmutableArray.CreateBuilder<IncludedSourceScope>();
        var seenPaths = new Dictionary<string, IncludedSourceScope>(
            StringComparer.Ordinal);
        var scopeIndex = 0;

        foreach (var configuration in configurations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetSelectedPaths(
                    configuration,
                    cancellationToken,
                    out var sourceParameter,
                    out var selectedPaths,
                    out var selectorReason))
            {
                issues.Add(new IncludeMembersIssueObservation(
                    IncludeMembersIssueKind.InvalidSelector,
                    configuration.Invocation,
                    configuration.Expression.Syntax.GetLocation(),
                    selectorReason,
                    ImmutableArray<Location>.Empty));
                continue;
            }

            foreach (var selectedPath in selectedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var diagnosticOrigin = selectedPaths.Length == 1
                    ? configuration.Expression.Syntax
                    : selectedPath;

                if (!TryBuildScope(
                        sourceType,
                        configuration,
                        selectedPath,
                        sourceParameter,
                        diagnosticOrigin,
                        scopeIndex++,
                        compilation,
                        mapperType,
                        cancellationToken,
                        out var scope,
                        out var reason))
                {
                    issues.Add(new IncludeMembersIssueObservation(
                        IncludeMembersIssueKind.InvalidSelector,
                        configuration.Invocation,
                        diagnosticOrigin.GetLocation(),
                        reason,
                        ImmutableArray<Location>.Empty));
                    continue;
                }

                if (seenPaths.TryGetValue(
                        scope.PathIdentity,
                        out var first))
                {
                    issues.Add(new IncludeMembersIssueObservation(
                        IncludeMembersIssueKind.InvalidSelector,
                        configuration.Invocation,
                        diagnosticOrigin.GetLocation(),
                        $"path '{scope.PathDisplay}' is included more than once",
                        ImmutableArray.Create(
                            first.DiagnosticOrigin.GetLocation())));
                    continue;
                }

                seenPaths.Add(scope.PathIdentity, scope);
                scopes.Add(scope);
            }
        }

        if (issues.Count > 0)
        {
            return new IncludedSourceMemberSetResult(
                rootMembers,
                ImmutableArray<ISymbol>.Empty,
                issues.ToImmutable(),
                scopes.ToImmutable());
        }

        var pathMembers = scopes
            .SelectMany(static scope => scope.Access.Path)
            .Select(static segment => segment.Symbol)
            .Distinct(SymbolEqualityComparer.Default)
            .ToImmutableArray();

        var rootNames = new HashSet<string>(
            rootMembers.Select(static member => member.Name),
            StringComparer.Ordinal);
        var includedByName = new Dictionary<string,
            List<(ConventionReadableMember Member, IncludedSourceScope Scope)>>(
            StringComparer.Ordinal);

        foreach (var scope in scopes)
        {
            foreach (var member in scope.Members)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (rootNames.Contains(member.Name))
                {
                    continue;
                }

                if (!includedByName.TryGetValue(member.Name, out var matches))
                {
                    matches = [];
                    includedByName.Add(member.Name, matches);
                }

                matches.Add((
                    member with
                    {
                        Type = scope.Access.CanProduceMissingValue
                            ? ConventionSourceAccessModel.LiftMissingValueType(
                                member.Type,
                                compilation)
                            : member.Type,
                        SourceAccess = scope.Access,
                        PathDisplay = scope.PathDisplay + "." + member.Name,
                        PathIdentity = scope.PathIdentity + "/" +
                            ConventionSourceAccessModel.MemberIdentity(
                                member.Symbol),
                        SourcePathMembers = scope.Access.Path
                            .Select(static segment => segment.Symbol)
                            .Append(member.Symbol)
                            .ToImmutableArray()
                    },
                    scope));
            }
        }

        var result = ImmutableArray.CreateBuilder<ConventionReadableMember>();
        result.AddRange(rootMembers);

        foreach (var pair in includedByName.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (pair.Value.Count == 1)
            {
                result.Add(pair.Value[0].Member);
                continue;
            }

            var ordered = pair.Value
                .OrderBy(static candidate =>
                    candidate.Scope.Access.ScopeIndex)
                .ToImmutableArray();
            var paths = string.Join(
                ", ",
                ordered.Select(static candidate =>
                    $"'{candidate.Scope.PathDisplay}'"));

            issues.Add(new IncludeMembersIssueObservation(
                IncludeMembersIssueKind.AmbiguousMember,
                ordered[1].Scope.Configuration.Invocation,
                ordered[1].Scope.DiagnosticOrigin.GetLocation(),
                $"member '{pair.Key}' is available from {paths}",
                ordered.Select(static candidate =>
                        candidate.Scope.DiagnosticOrigin.GetLocation())
                    .ToImmutableArray()));
        }

        return new IncludedSourceMemberSetResult(
            result.ToImmutable(),
            pathMembers,
            issues.ToImmutable(),
            scopes.ToImmutable());
    }

    private static bool TryGetSelectedPaths(
        IncludeMembersConfigurationModel configuration,
        CancellationToken cancellationToken,
        out IParameterSymbol sourceParameter,
        out ImmutableArray<ExpressionSyntax> selectedPaths,
        out string reason)
    {
        if (configuration.Expression.Syntax is not
                LambdaExpressionSyntax lambda ||
            lambda.AsyncKeyword.RawKind != 0 ||
            lambda.ExpressionBody is not { } expressionBody ||
            GetSingleParameter(lambda) is not { } parameterSyntax ||
            configuration.Expression.SemanticModel.GetDeclaredSymbol(
                parameterSyntax,
                cancellationToken) is not IParameterSymbol parameter)
        {
            sourceParameter = null!;
            selectedPaths = default;
            reason = "the selector must be an inline property or field path " +
                     "rooted in source, or an anonymous object of such paths";
            return false;
        }

        expressionBody = UnwrapParentheses(expressionBody);
        sourceParameter = parameter;
        selectedPaths = expressionBody is
            AnonymousObjectCreationExpressionSyntax anonymous
                ? anonymous.Initializers
                    .Select(static initializer => initializer.Expression)
                    .ToImmutableArray()
                : ImmutableArray.Create(expressionBody);
        reason = string.Empty;
        return true;
    }

    private static bool TryBuildScope(
        ITypeSymbol currentSourceType,
        IncludeMembersConfigurationModel configuration,
        ExpressionSyntax selectedPath,
        IParameterSymbol sourceParameter,
        SyntaxNode diagnosticOrigin,
        int scopeIndex,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken,
        out IncludedSourceScope scope,
        out string reason)
    {
        if (!TryBuildPath(
                selectedPath,
                sourceParameter,
                configuration.Expression.SemanticModel,
                cancellationToken,
                out var path))
        {
            scope = default;
            reason = "the selector must be an inline property or field path " +
                     "rooted in source, or an anonymous object of such paths";
            return false;
        }

        if (path.IsEmpty)
        {
            scope = default;
            reason = "the selector must select a nested source object";
            return false;
        }

        var rootType = MappingTypeNormalization.NormalizeDeclarativeSource(
                configuration.SourceType,
                compilation)
            .WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var parsedPath = path;

        if (MappingTypeNormalization.IsNullableValue(
                configuration.SourceType) &&
            parsedPath.Length > 0 &&
            StringComparer.Ordinal.Equals(parsedPath[0].Name, "Value"))
        {
            parsedPath = parsedPath.RemoveAt(0);
        }

        if (parsedPath.IsEmpty)
        {
            scope = default;
            reason = "the selector must select a nested source object";
            return false;
        }

        var resolvedPath =
            ImmutableArray.CreateBuilder<ConventionSourcePathSegment>(
                parsedPath.Length);
        var receiverType = rootType;

        foreach (var segment in parsedPath)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readableMember = ConventionMemberMappingPlanner
                .BuildReadableMembers(
                    NormalizeSelectedType(receiverType),
                    compilation,
                    mapperType,
                    cancellationToken)
                .FirstOrDefault(candidate =>
                    StringComparer.Ordinal.Equals(
                        candidate.Name,
                        segment.Name));

            if (readableMember.Symbol is null)
            {
                scope = default;
                reason =
                    $"member '{segment.Name}' is not readable from generated code";
                return false;
            }

            resolvedPath.Add(new ConventionSourcePathSegment(
                readableMember.Name,
                readableMember.Type,
                readableMember.Symbol,
                segment.SuppressesNull,
                segment.RequiresNullGuard));
            receiverType = readableMember.Type;
        }

        var immutablePath = resolvedPath.ToImmutable();
        var selectedType = NormalizeSelectedType(
            immutablePath[immutablePath.Length - 1].Type);
        var members = ConventionMemberMappingPlanner.BuildReadableMembers(
            selectedType,
            compilation,
            mapperType,
            cancellationToken);

        if (members.IsEmpty)
        {
            scope = default;
            reason =
                $"selected type '{MapperContractDisplay.CreateType(selectedType)}' " +
                "has no readable instance members";
            return false;
        }

        var requiresRootCast = !SymbolEqualityComparer.Default.Equals(
            rootType,
            currentSourceType.WithNullableAnnotation(
                NullableAnnotation.NotAnnotated));
        var pathDisplay = string.Join(
            ".",
            immutablePath.Select(static segment => segment.Name));
        var pathIdentity = string.Join(
            "/",
            immutablePath.Select(static segment =>
                SymbolNameHelper.GetFullMetadataName(
                    segment.Symbol.ContainingType!) + "." +
                segment.Symbol.MetadataName));

        scope = new IncludedSourceScope(
            configuration,
            diagnosticOrigin,
            pathDisplay,
            pathIdentity,
            members,
            selectedType,
            new ConventionSourceAccessModel(
                scopeIndex,
                rootType,
                requiresRootCast,
                immutablePath));
        reason = string.Empty;
        return true;
    }

    private static bool TryBuildPath(
        ExpressionSyntax expression,
        IParameterSymbol sourceParameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ImmutableArray<ParsedIncludeMembersPathSegment> path)
    {
        var segments =
            ImmutableArray.CreateBuilder<ParsedIncludeMembersPathSegment>();

        bool Visit(ExpressionSyntax current, bool allowMemberBinding)
        {
            current = UnwrapParentheses(current);

            if (current is PostfixUnaryExpressionSyntax postfix &&
                postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            {
                if (!Visit(postfix.Operand, allowMemberBinding))
                {
                    return false;
                }

                if (segments.Count > 0)
                {
                    segments[segments.Count - 1] =
                        segments[segments.Count - 1] with
                        {
                            SuppressesNull = true
                        };
                }

                return true;
            }

            if (current is IdentifierNameSyntax identifier)
            {
                return SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken)
                        .Symbol,
                    sourceParameter);
            }

            if (current is ConditionalAccessExpressionSyntax conditional)
            {
                if (!Visit(conditional.Expression, allowMemberBinding))
                {
                    return false;
                }

                if (segments.Count > 0)
                {
                    segments[segments.Count - 1] =
                        segments[segments.Count - 1] with
                        {
                            RequiresNullGuard = true
                        };
                }

                return Visit(
                    conditional.WhenNotNull,
                    allowMemberBinding: true);
            }

            return current switch
            {
                MemberAccessExpressionSyntax memberAccess =>
                    Visit(memberAccess.Expression, allowMemberBinding) &&
                    AddMember(memberAccess.Name),
                MemberBindingExpressionSyntax memberBinding
                    when allowMemberBinding => AddMember(memberBinding.Name),
                _ => false
            };
        }

        bool AddMember(SimpleNameSyntax memberName)
        {
            if (semanticModel.GetSymbolInfo(
                    memberName,
                    cancellationToken).Symbol is not { } symbol ||
                !TryGetReadableInstanceMemberType(symbol, out _))
            {
                return false;
            }

            segments.Add(new ParsedIncludeMembersPathSegment(
                memberName.Identifier.ValueText,
                SuppressesNull: false,
                RequiresNullGuard: false));
            return true;
        }

        if (!Visit(expression, allowMemberBinding: false))
        {
            path = default;
            return false;
        }

        path = segments.ToImmutable();
        return true;
    }

    private static bool TryGetReadableInstanceMemberType(
        ISymbol symbol,
        out ITypeSymbol type)
    {
        switch (symbol)
        {
            case IPropertySymbol
            {
                IsStatic: false,
                IsIndexer: false,
                GetMethod: not null
            } property:
                type = property.Type.WithNullableAnnotation(
                    property.NullableAnnotation);
                return true;

            case IFieldSymbol
            {
                IsStatic: false
            } field:
                type = field.Type.WithNullableAnnotation(
                    field.NullableAnnotation);
                return true;

            default:
                type = null!;
                return false;
        }
    }

    private static ParameterSyntax? GetSingleParameter(
        LambdaExpressionSyntax lambda)
    {
        return lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax parenthesized
                when parenthesized.ParameterList.Parameters.Count == 1 =>
                parenthesized.ParameterList.Parameters[0],
            _ => null
        };
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

    private static ITypeSymbol NormalizeSelectedType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T)
        {
            return named.TypeArguments[0]
                .WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        }

        return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
    }

}

internal readonly record struct IncludedSourceMemberSetResult(
    ImmutableArray<ConventionReadableMember> Members,
    ImmutableArray<ISymbol> PathMembers,
    ImmutableArray<IncludeMembersIssueObservation> Issues,
    ImmutableArray<IncludedSourceScope> Scopes);

internal readonly record struct IncludedSourceScope(
    IncludeMembersConfigurationModel Configuration,
    SyntaxNode DiagnosticOrigin,
    string PathDisplay,
    string PathIdentity,
    ImmutableArray<ConventionReadableMember> Members,
    ITypeSymbol SelectedType,
    ConventionSourceAccessModel Access);

internal readonly record struct ConventionSourceAccessModel(
    int ScopeIndex,
    ITypeSymbol RootType,
    bool RequiresRootCast,
    ImmutableArray<ConventionSourcePathSegment> Path)
{
    public bool CanProduceMissingValue =>
        Path.Any(RequiresGuard);

    public ConventionSourceValueExpressionModel BuildValueExpression(
        string sourceName,
        ISymbol member,
        ITypeSymbol memberType)
    {
        var receiverExpression = RequiresRootCast
            ? "((" +
              RootType.ToDisplayString(
                  SymbolDisplayFormats.FullyQualifiedNullable) +
              ")" + sourceName + ")"
            : sourceName;
        var typeName = memberType.ToDisplayString(
            SymbolDisplayFormats.FullyQualifiedNullable);

        return new ConventionSourceValueExpressionModel(
            receiverExpression,
            Path.Select(segment =>
                    new ConventionSourceValuePathSegmentModel(
                        segment.Name,
                        segment.SuppressesNull,
                        RequiresGuard(segment)))
                .ToImmutableArray(),
            member.Name,
            typeName,
            RequiresTypedMissingBranch(member));
    }

    // Roslyn 4.4 cannot preserve the lifted target type when conditional
    // access ends in an unconstrained T. Keep an explicit typed null branch
    // for that case so a missing path never becomes default(T).
    private bool RequiresTypedMissingBranch(ISymbol member) =>
        CanProduceMissingValue &&
        GetMemberType(member) is ITypeParameterSymbol
        {
            IsReferenceType: false,
            IsValueType: false
        };

    private static ITypeSymbol? GetMemberType(ISymbol member) =>
        member switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => null
        };

    public ConventionSourceAccessModel Append(
        ConventionReadableMember member) =>
        this with
        {
            Path = Path.Add(new ConventionSourcePathSegment(
                member.Name,
                member.Type,
                member.Symbol,
                SuppressesNull: false,
                RequiresNullGuard: false))
        };

    public static ITypeSymbol NormalizeReceiverType(ITypeSymbol type)
    {
        if (IsNullableValue(type))
        {
            return ((INamedTypeSymbol)type).TypeArguments[0]
                .WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        }

        return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
    }

    public static ITypeSymbol LiftMissingValueType(
        ITypeSymbol type,
        CSharpCompilation compilation)
    {
        if (IsNullableValue(type))
        {
            return type;
        }

        if (type is ITypeParameterSymbol typeParameter)
        {
            if (typeParameter.IsReferenceType)
            {
                return type.WithNullableAnnotation(
                    NullableAnnotation.Annotated);
            }

            if (typeParameter.IsValueType)
            {
                return compilation
                    .GetSpecialType(SpecialType.System_Nullable_T)
                    .Construct(type.WithNullableAnnotation(
                        NullableAnnotation.NotAnnotated));
            }

            return compilation
                .GetSpecialType(SpecialType.System_Object)
                .WithNullableAnnotation(NullableAnnotation.Annotated);
        }

        if (type.IsReferenceType)
        {
            return type.WithNullableAnnotation(NullableAnnotation.Annotated);
        }

        if (type.IsValueType)
        {
            return compilation
                .GetSpecialType(SpecialType.System_Nullable_T)
                .Construct(type.WithNullableAnnotation(
                    NullableAnnotation.NotAnnotated));
        }

        return type.WithNullableAnnotation(NullableAnnotation.Annotated);
    }

    public static string MemberIdentity(ISymbol member) =>
        SymbolNameHelper.GetFullMetadataName(member.ContainingType!) + "." +
        member.MetadataName;

    private static bool CanBeNull(ITypeSymbol type)
    {
        if (IsNullableValue(type))
        {
            return true;
        }

        if (type.IsReferenceType)
        {
            return type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        return type is ITypeParameterSymbol typeParameter &&
               !typeParameter.HasValueTypeConstraint &&
               !typeParameter.HasUnmanagedTypeConstraint &&
               type.NullableAnnotation == NullableAnnotation.Annotated;
    }

    private static bool RequiresGuard(
        ConventionSourcePathSegment segment) =>
        segment.RequiresNullGuard ||
        CanBeNull(segment.Type) &&
        (!segment.SuppressesNull || IsNullableValue(segment.Type));

    private static bool IsNullableValue(ITypeSymbol type) =>
        type is INamedTypeSymbol named &&
        named.OriginalDefinition.SpecialType ==
            SpecialType.System_Nullable_T;

}

internal readonly record struct ConventionSourcePathSegment(
    string Name,
    ITypeSymbol Type,
    ISymbol Symbol,
    bool SuppressesNull,
    bool RequiresNullGuard);

internal readonly record struct ParsedIncludeMembersPathSegment(
    string Name,
    bool SuppressesNull,
    bool RequiresNullGuard);

internal enum IncludeMembersIssueKind
{
    InvalidSelector,
    AmbiguousMember
}

internal readonly record struct IncludeMembersIssueObservation(
    IncludeMembersIssueKind Kind,
    InvocationExpressionSyntax Invocation,
    Location Location,
    string Detail,
    ImmutableArray<Location> AdditionalLocations);
