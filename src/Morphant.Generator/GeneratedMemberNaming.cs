using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator;

internal static class GeneratedMemberNaming
{
    public static IReadOnlyDictionary<string, string> BuildNames(
        INamedTypeSymbol destination)
    {
        var tuple = BclTupleShapePolicy.TryCreate(destination);
        destination = tuple is null ? destination.OriginalDefinition : destination;
        var names = new HashSet<string>(StringComparer.Ordinal);

        if (tuple is not null)
        {
            foreach (var element in tuple.Elements)
            {
                names.Add(element.Name);
            }
        }
        else
        {
            AddNames(destination);
            foreach (var parent in destination.AllInterfaces)
            {
                AddNames(parent);
            }
        }

        var reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            tuple is null
                ? GeneratedPlanNaming.BuildMembersTypeName(destination)
                : "TupleMembers",
            "Clone", "EqualityContract", "Equals", "GetHashCode",
            "PrintMembers", "ToString"
        };
        var typeParameters = GeneratedTypeNameBuilder.CollectTypeParameters(destination);
        reserved.UnionWith(GeneratedTypeNameBuilder.AllocateTypeParameterNames(typeParameters).Values);
        var occupied = new HashSet<string>(names, StringComparer.Ordinal);
        occupied.UnionWith(reserved);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in names.OrderBy(static name => name, StringComparer.Ordinal))
        {
            var generated = name;
            if (reserved.Contains(name))
            {
                do
                {
                    generated += "_";
                }
                while (!occupied.Add(generated));
            }

            result.Add(name, generated);
        }

        return result;

        void AddNames(INamedTypeSymbol type)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    names.Add(member.Name);
                }
            }
        }
    }

    public static string GetDestinationName(
        INamedTypeSymbol destination,
        string generatedName)
    {
        foreach (var name in BuildNames(destination))
        {
            if (name.Value == generatedName)
            {
                return name.Key;
            }
        }

        return generatedName;
    }
}
