using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
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
        Action<ITypeSymbol> addDependency,
        CancellationToken cancellationToken)
    {
        var walker = new FlatteningDependencyWalker(
            compilation,
            mapperType,
            addDependency);

        foreach (var invocation in root.DescendantsAndSelf()
                     .OfType<IInvocationOperation>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryGetRegisteredPair(
                    invocation.TargetMethod,
                    out var source,
                    out var destination))
            {
                walker.AddScope(
                    source,
                    destination,
                    cancellationToken);
                continue;
            }

            if (!TryGetIncludeMembersPair(
                    invocation.TargetMethod,
                    out source,
                    out destination))
            {
                continue;
            }

            walker.AddScope(
                source,
                destination,
                cancellationToken);

            foreach (var argument in invocation.Arguments)
            {
                foreach (var operation in argument.Value
                             .DescendantsAndSelf())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if ((operation is IPropertyReferenceOperation ||
                         operation is IFieldReferenceOperation) &&
                        operation.Type is { } selectedType)
                    {
                        walker.AddScope(
                            selectedType,
                            destination,
                            cancellationToken);
                    }
                }
            }
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

    private static bool TryGetIncludeMembersPair(
        IMethodSymbol method,
        out ITypeSymbol source,
        out ITypeSymbol destination)
    {
        var containingType = method.ContainingType;

        if (method.Name == "IncludeMembers" &&
            method.MethodKind == MethodKind.Ordinary &&
            !method.IsStatic &&
            method.Parameters.Length == 1 &&
            containingType.TypeArguments.Length == 2 &&
            StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    containingType.OriginalDefinition),
                MetadataNames.PairMapperBuilder))
        {
            source = containingType.TypeArguments[0];
            destination = containingType.TypeArguments[1];
            return true;
        }

        source = null!;
        destination = null!;
        return false;
    }

    private sealed class FlatteningDependencyWalker
    {
        private readonly CSharpCompilation _compilation;
        private readonly INamedTypeSymbol _mapperType;
        private readonly Action<ITypeSymbol> _addDependency;
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
            Action<ITypeSymbol> addDependency)
        {
            _compilation = compilation;
            _mapperType = mapperType;
            _addDependency = addDependency;
        }

        public void AddScope(
            ITypeSymbol source,
            ITypeSymbol destination,
            CancellationToken cancellationToken)
        {
            _addDependency(source);

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
                if (member.Name.Length >= remainingName.Length ||
                    !remainingName.StartsWith(
                        member.Name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _addDependency(member.Type);
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
