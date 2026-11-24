using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.Incrementality;

internal static class FlatteningSemanticDependencyBuilder
{
    // Flattening observes contracts beyond the registered root source type.
    // Follow only prefixes of actual destination names so an unrelated object
    // graph does not become an incremental dependency of every mapper.
    public static void Add(
        IOperation root,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        DependencyTypeSet dependencies,
        CancellationToken cancellationToken)
    {
        var walker = new FlatteningDependencyWalker(
            compilation,
            mapperType,
            dependencies);

        foreach (var invocation in root.DescendantsAndSelf()
                     .OfType<IInvocationOperation>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetRegisteredPair(
                    invocation.TargetMethod,
                    out var source,
                    out var destination))
                continue;

            walker.AddScope(source, destination, cancellationToken);
            AddIncludedScopes(invocation.Syntax, source, destination, walker, compilation, cancellationToken);
        }
    }

    private static bool TryGetRegisteredPair(
        IMethodSymbol method,
        out ITypeSymbol source,
        out ITypeSymbol destination)
    {
        if (method.Name == "Map" &&
            method.MethodKind == MethodKind.Ordinary &&
            !method.IsStatic &&
            method.Parameters.Length == 1 &&
            method.TypeArguments.Length == 2 &&
            StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    method.ContainingType.OriginalDefinition),
                MetadataNames.MapperBuilder))
        {
            source = method.TypeArguments[0];
            destination = method.TypeArguments[1];
            return true;
        }

        source = null!;
        destination = null!;
        return false;
    }

    private static void AddIncludedScopes(SyntaxNode registration, ITypeSymbol source, ITypeSymbol destination,
        FlatteningDependencyWalker walker, Compilation compilation, CancellationToken token)
    {
        var current = registration;
        while (BuilderFlowAnalyzer.TryGetOuterReceiverInvocation(current, out var call))
        {
            token.ThrowIfCancellationRequested();
            current = call;
            if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "IncludeMembers" } ||
                call.ArgumentList.Arguments.Count != 1 ||
                call.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax selector)
                continue;

            // After a generated callback, the call has no bound receiver in the
            // input compilation. Bind the selector in its original scope to
            // observe the selected field and property types.
            var type = SyntaxFactory.ParseTypeName("global::System.Func<" +
                source.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable) + ", object?>");
            var statement = SyntaxFactory.LocalDeclarationStatement(SyntaxFactory.VariableDeclaration(type)
                .AddVariables(SyntaxFactory.VariableDeclarator("__selector").WithInitializer(SyntaxFactory.EqualsValueClause(selector))));
            var model = compilation.GetSemanticModel(call.SyntaxTree);
            if (!model.TryGetSpeculativeSemanticModel(call.SpanStart, statement, out var binding) ||
                binding.GetOperation(statement, token) is not { } operation)
                continue;

            foreach (var read in operation.DescendantsAndSelf())
                if (read is IPropertyReferenceOperation or IFieldReferenceOperation && read.Type is { } selected)
                    walker.AddScope(selected, destination, token);
        }
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
