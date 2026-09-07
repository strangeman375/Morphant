using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator;

internal enum MapperMemberReceiver
{
    Unchanged,
    DeclaringType,
    Base,
    Unavailable
}

internal static class MapperMemberBinding
{
    public static bool UsesCurrentInstance(SimpleNameSyntax name)
    {
        if (name.Parent is MemberAccessExpressionSyntax access &&
            ReferenceEquals(access.Name, name))
        {
            ExpressionSyntax receiver = access.Expression;
            while (receiver is ParenthesizedExpressionSyntax parentheses)
            {
                receiver = parentheses.Expression;
            }

            return receiver is ThisExpressionSyntax;
        }

        return name.Parent is not (MemberBindingExpressionSyntax or
            QualifiedNameSyntax or AliasQualifiedNameSyntax or NameColonSyntax or
            NameEqualsSyntax) &&
            !(name.Parent is AssignmentExpressionSyntax assignment &&
              ReferenceEquals(assignment.Left, name) &&
              assignment.Parent is InitializerExpressionSyntax);
    }

    public static MapperMemberReceiver GetReceiver(
        ISymbol member,
        INamedTypeSymbol mapperType,
        Compilation compilation,
        out INamedTypeSymbol? declaringType)
    {
        declaringType = null;
        if (member is not (IMethodSymbol or IPropertySymbol or
                IFieldSymbol or IEventSymbol) ||
            member is IMethodSymbol { MethodKind: MethodKind.LocalFunction } ||
            member.ContainingType is null)
        {
            return MapperMemberReceiver.Unchanged;
        }

        for (var current = mapperType; current is not null;
             current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    current.OriginalDefinition,
                    member.ContainingType.OriginalDefinition))
            {
                declaringType = current;
                break;
            }
        }

        if (declaringType is null ||
            !HasConflictingMember(mapperType, declaringType, member))
        {
            return MapperMemberReceiver.Unchanged;
        }

        var closedMember = declaringType.GetMembers(member.Name)
            .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(
                candidate.OriginalDefinition, member.OriginalDefinition));
        if (closedMember is null ||
            !compilation.IsSymbolAccessibleWithin(closedMember, mapperType))
        {
            return MapperMemberReceiver.Unavailable;
        }

        if (member.IsStatic ||
            compilation.IsSymbolAccessibleWithin(
                closedMember, mapperType, throughType: declaringType))
        {
            return MapperMemberReceiver.DeclaringType;
        }

        // A base call suppresses virtual dispatch, so it is only valid when
        // the original selected member cannot dispatch to another override.
        if (!RequiresVirtualDispatch(member) &&
            mapperType.BaseType is { } baseType &&
            !HasConflictingMember(baseType, declaringType, member))
        {
            return MapperMemberReceiver.Base;
        }

        return MapperMemberReceiver.Unavailable;
    }

    private static bool HasConflictingMember(
        INamedTypeSymbol start, INamedTypeSymbol declaringType, ISymbol member)
    {
        for (var current = start;
             current is not null && !SymbolEqualityComparer.Default.Equals(
                 current, declaringType);
             current = current.BaseType)
        {
            foreach (var candidate in current.GetMembers(member.Name))
            {
                if (!SameVirtualSlot(candidate, member))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool RequiresVirtualDispatch(ISymbol member) =>
        !member.IsSealed &&
        (member.IsVirtual || member.IsOverride || member.IsAbstract);

    public static ISymbol? GetOverride(ISymbol member, INamedTypeSymbol mapperType)
    {
        if (!RequiresVirtualDispatch(member))
        {
            return null;
        }

        for (var current = mapperType; current is not null; current = current.BaseType)
        {
            foreach (var candidate in current.GetMembers(member.Name))
            {
                if (SameVirtualSlot(candidate, member))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static bool SameVirtualSlot(ISymbol left, ISymbol right)
    {
        if (!RequiresVirtualDispatch(right))
        {
            return false;
        }
        return SymbolEqualityComparer.Default.Equals(
            GetRoot(left).OriginalDefinition, GetRoot(right).OriginalDefinition);

        static ISymbol GetRoot(ISymbol symbol)
        {
            while (true)
            {
                ISymbol? overridden = symbol switch
                {
                    IMethodSymbol method => method.OverriddenMethod,
                    IPropertySymbol property => property.OverriddenProperty,
                    IEventSymbol @event => @event.OverriddenEvent,
                    _ => null
                };
                if (overridden is null)
                {
                    return symbol;
                }
                symbol = overridden;
            }
        }
    }
}
