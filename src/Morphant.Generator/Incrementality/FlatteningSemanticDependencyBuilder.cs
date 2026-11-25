using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.Incrementality;

internal static class FlatteningSemanticDependencyBuilder
{
    // Flattening observes contracts beyond the registered root source type.
    // Follow only prefixes of actual destination names so an unrelated object
    // graph does not become an incremental dependency of every mapper.
    public static void Add(
        IReadOnlyList<IInvocationOperation> registrations,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        DependencyTypeSet dependencies,
        CancellationToken cancellationToken)
    {
        if (registrations.Count == 0)
            return;

        var walker = new FlatteningDependencyWalker(compilation, mapperType, dependencies);
        var substitutions = MapperTypeSubstitution.BuildForHierarchy(mapperType);
        var levels = new Dictionary<INamedTypeSymbol, Dictionary<MappingPairKey, Registration>>(SymbolEqualityComparer.Default);
        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntax = registration.Syntax;
            var model = compilation.GetSemanticModel(syntax.SyntaxTree);
            var declaredSource = registration.TargetMethod.TypeArguments[0];
            var source = MapperTypeSubstitution.Substitute(declaredSource, substitutions, compilation);
            var destination = MapperTypeSubstitution.Substitute(registration.TargetMethod.TypeArguments[1], substitutions, compilation);
            walker.AddScope(source, destination, cancellationToken);

            if (model.GetEnclosingSymbol(syntax.SpanStart, cancellationToken)?.ContainingType is not { } declaringMapper)
                continue;

            var scopes = ImmutableArray.CreateBuilder<ITypeSymbol>();
            MappingPairKey? includedBase = null;
            while (BuilderFlowAnalyzer.TryGetOuterReceiverInvocation(syntax, out var call))
            {
                cancellationToken.ThrowIfCancellationRequested();
                syntax = call;
                if (call.Expression is not MemberAccessExpressionSyntax access)
                    continue;
                if (access.Name.Identifier.ValueText == "IncludeBase" &&
                    PairConfigurationModelBuilder.TryBuildIncludeBaseConfiguration(
                        call, model, substitutions, cancellationToken, out var parent))
                    includedBase = MappingPairKey.Create(parent.SourceType, parent.DestinationType);
                if (access.Name.Identifier.ValueText == "IncludeMembers" && call.ArgumentList.Arguments.Count == 1 &&
                    call.ArgumentList.Arguments[0].Expression is LambdaExpressionSyntax selector)
                {
                    foreach (var selected in GetSelectedTypes(selector, declaredSource, model, cancellationToken))
                        scopes.Add(MapperTypeSubstitution.Substitute(selected, substitutions, compilation));
                }
            }

            if (!levels.TryGetValue(declaringMapper, out var pairs))
                levels.Add(declaringMapper, pairs = new Dictionary<MappingPairKey, Registration>());
            var key = MappingPairKey.Create(source, destination);
            pairs[key] = new Registration(declaringMapper, key, destination, includedBase, scopes.ToImmutable());
        }

        // An included selector keeps its declared binding, but its members are
        // matched against the consuming destination, including its constructor names.
        var visited = new HashSet<Registration>();
        foreach (var consumer in levels.Values.SelectMany(static pairs => pairs.Values))
        {
            visited.Clear();
            for (Registration? current = consumer; current is not null && visited.Add(current); current = FindBase(current))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var scope in current.Scopes)
                    walker.AddScope(scope, consumer.Destination, cancellationToken);
            }
        }

        Registration? FindBase(Registration current)
        {
            if (current.IncludedBase is not { } key)
                return null;
            var level = key == current.Key ? current.Mapper.BaseType : current.Mapper;
            for (; level is not null; level = level.BaseType)
                if (levels.TryGetValue(level.OriginalDefinition, out var pairs) && pairs.TryGetValue(key, out var parent))
                    return parent;
            return null;
        }
    }

    private sealed record Registration(INamedTypeSymbol Mapper, MappingPairKey Key, ITypeSymbol Destination,
        MappingPairKey? IncludedBase, ImmutableArray<ITypeSymbol> Scopes);

    private static IEnumerable<ITypeSymbol> GetSelectedTypes(
        LambdaExpressionSyntax selector, ITypeSymbol source, SemanticModel model, CancellationToken token)
    {
        // Bind the future extension's delegate in the selector's original
        // scope. A closed mapper substitutes its own type arguments afterwards.
        var selectorSource = MappingTypeNormalization.NormalizeDeclarativeSource(
            source, model.Compilation, normalizeDynamic: false);
        var type = SyntaxFactory.ParseTypeName("global::System.Func<" +
            selectorSource.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable) + ", object?>");
        var statement = SyntaxFactory.LocalDeclarationStatement(SyntaxFactory.VariableDeclaration(type)
            .AddVariables(SyntaxFactory.VariableDeclarator("__selector").WithInitializer(SyntaxFactory.EqualsValueClause(selector))));
        if (!model.TryGetSpeculativeSemanticModel(selector.SpanStart, statement, out var binding) ||
            binding.GetOperation(statement, token) is not { } operation)
            yield break;

        foreach (var read in operation.DescendantsAndSelf())
            if (read is IPropertyReferenceOperation or IFieldReferenceOperation && read.Type is { } selected)
                yield return selected;
    }

    private sealed class FlatteningDependencyWalker
    {
        private readonly CSharpCompilation _compilation;
        private readonly INamedTypeSymbol _mapperType;
        private readonly DependencyTypeSet _dependencies;
        private readonly Dictionary<ITypeSymbol,
                ImmutableArray<ConventionReadableMember>>
            _readableMembers = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<ITypeSymbol, ImmutableArray<string>>
            _targetNames = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<ITypeSymbol, HashSet<string>> _visited =
            new(SymbolEqualityComparer.Default);

        public FlatteningDependencyWalker(
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            DependencyTypeSet dependencies)
        {
            _compilation = compilation;
            _mapperType = mapperType;
            _dependencies = dependencies;
        }

        public void AddScope(
            ITypeSymbol source,
            ITypeSymbol destination,
            CancellationToken cancellationToken)
        {
            _dependencies.AddDeclarations(source, cancellationToken);
            _dependencies.AddDeclarations(destination, cancellationToken);

            foreach (var targetName in GetTargetNames(destination))
            {
                AddPathDependencies(
                    source,
                    targetName,
                    cancellationToken);
            }
        }

        private void AddPathDependencies(
            ITypeSymbol source,
            string remainingName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var receiver = NormalizeReceiverType(source);

            if (!_visited.TryGetValue(receiver, out var visitedNames))
            {
                visitedNames = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                _visited.Add(receiver, visitedNames);
            }

            if (!visitedNames.Add(remainingName))
            {
                return;
            }

            foreach (var member in GetReadableMembers(
                         receiver,
                         cancellationToken))
            {
                if (member.Name.Length > remainingName.Length ||
                    !remainingName.StartsWith(
                        member.Name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _dependencies.Add(member.Type);
                if (member.Name.Length == remainingName.Length) continue;
                AddPathDependencies(
                    member.Type,
                    remainingName.Substring(member.Name.Length),
                    cancellationToken);
            }
        }

        private ImmutableArray<ConventionReadableMember>
            GetReadableMembers(
                ITypeSymbol source,
                CancellationToken cancellationToken)
        {
            if (_readableMembers.TryGetValue(source, out var members))
            {
                return members;
            }

            members = ConventionMemberMappingPlanner.BuildReadableMembers(
                source,
                _compilation,
                _mapperType,
                cancellationToken);
            _readableMembers.Add(source, members);
            return members;
        }

        private ImmutableArray<string> GetTargetNames(
            ITypeSymbol destination)
        {
            destination = NormalizeReceiverType(destination);

            if (_targetNames.TryGetValue(destination, out var names))
            {
                return names;
            }

            var result = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            AddDestinationTargetNames(destination, result);
            names = result.OrderBy(
                    static name => name,
                    StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();
            _targetNames.Add(destination, names);
            return names;
        }

        private static void AddDestinationTargetNames(
            ITypeSymbol destination,
            HashSet<string> result)
        {
            if (destination is ITypeParameterSymbol typeParameter)
            {
                foreach (var constraint in typeParameter.ConstraintTypes)
                {
                    AddDestinationTargetNames(constraint, result);
                }

                return;
            }

            if (destination is not INamedTypeSymbol namedType)
            {
                return;
            }

            if (namedType.TypeKind == TypeKind.Interface)
            {
                AddMemberNames(namedType, result);

                foreach (var baseInterface in namedType.AllInterfaces)
                {
                    AddMemberNames(baseInterface, result);
                }
            }
            else
            {
                for (var current = namedType;
                     current is not null;
                     current = current.BaseType)
                {
                    AddMemberNames(current, result);
                }
            }

            foreach (var constructor in namedType.InstanceConstructors)
            {
                foreach (var parameter in constructor.Parameters)
                {
                    if (parameter.Name.Length > 0)
                    {
                        result.Add(parameter.Name);
                    }
                }
            }
        }

        private static void AddMemberNames(
            INamedTypeSymbol type,
            HashSet<string> result)
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IPropertySymbol
                    {
                        IsStatic: false,
                        IsIndexer: false
                    } property)
                {
                    result.Add(property.Name);
                }
                else if (member is IFieldSymbol
                         {
                             IsStatic: false,
                             IsImplicitlyDeclared: false
                         } field)
                {
                    result.Add(field.Name);
                }
            }
        }

        private static ITypeSymbol NormalizeReceiverType(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol
                {
                    OriginalDefinition.SpecialType:
                    SpecialType.System_Nullable_T
                } nullable)
            {
                return nullable.TypeArguments[0].WithNullableAnnotation(
                    NullableAnnotation.NotAnnotated);
            }

            return type.WithNullableAnnotation(
                NullableAnnotation.NotAnnotated);
        }
    }
}
