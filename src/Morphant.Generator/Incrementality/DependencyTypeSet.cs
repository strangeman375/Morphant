using Microsoft.CodeAnalysis;

namespace Morphant.Generator.Incrementality;

internal sealed class DependencyTypeSet
{
    private readonly HashSet<ITypeSymbol> _visited = new(
        SymbolEqualityComparer.Default);

    public HashSet<INamedTypeSymbol> Types { get; } = new(
        SymbolEqualityComparer.Default);

    public void Add(ITypeSymbol type)
    {
        if (!_visited.Add(type))
        {
            return;
        }

        switch (type)
        {
            case INamedTypeSymbol namedType:
                AddNamed(namedType);

                foreach (var typeArgument in namedType.TypeArguments)
                {
                    Add(typeArgument);
                }

                break;

            case IArrayTypeSymbol arrayType:
                Add(arrayType.ElementType);
                break;

            case IPointerTypeSymbol pointerType:
                Add(pointerType.PointedAtType);
                break;

            case ITypeParameterSymbol typeParameter:
                foreach (var constraint in
                         typeParameter.ConstraintTypes)
                {
                    Add(constraint);
                }

                break;

            case IFunctionPointerTypeSymbol functionPointer:
                Add(functionPointer.Signature.ReturnType);

                foreach (var parameter in
                         functionPointer.Signature.Parameters)
                {
                    Add(parameter.Type);
                }

                break;
        }
    }

    public void AddNamed(INamedTypeSymbol? type)
    {
        for (var current = type?.OriginalDefinition;
             current is not null;
             current = current.ContainingType)
        {
            if (!Types.Add(current))
            {
                continue;
            }

            foreach (var parameter in current.TypeParameters)
            {
                Add(parameter);
            }

            if (current.BaseType is { } baseType)
            {
                Add(baseType);
            }

            // Interface edits can change extension applicability without
            // changing the receiver declaration. Expand each definition
            // only once: C<T> : I<C<C<T>>> otherwise grows indefinitely.
            foreach (var implementedInterface in current.AllInterfaces)
            {
                Add(implementedInterface);
            }
        }
    }

    // Only expand members of the requested surface, never the whole object
    // graph. Leaf contracts still include conversions, bases and constraints.
    public void AddDeclarations(ITypeSymbol type, CancellationToken cancellationToken)
    {
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        Visit(type);

        void Visit(ITypeSymbol current)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(current)) return;
            Add(current);

            if (current is ITypeParameterSymbol parameter)
            {
                foreach (var constraint in parameter.ConstraintTypes) Visit(constraint);
                return;
            }

            if (current is not INamedTypeSymbol named) return;
            foreach (var member in named.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (member)
                {
                    case IPropertySymbol property:
                        Add(property.Type);
                        foreach (var indexParameter in property.Parameters) Add(indexParameter.Type);
                        break;
                    case IFieldSymbol field:
                        Add(field.Type);
                        break;
                    case IMethodSymbol method:
                        Add(method.ReturnType);
                        foreach (var argument in method.Parameters) Add(argument.Type);
                        break;
                }
            }

            if (named.BaseType is { } baseType) Visit(baseType);
            if (named.TypeKind == TypeKind.Interface)
            {
                foreach (var baseInterface in named.Interfaces) Visit(baseInterface);
            }
        }
    }
}
