using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.ConstructionSurface.ConstructionPlan;

namespace Morphant.Generator.ConstructionSurface.PairConfiguration;

internal static class PairTypeParameterModelBuilder
{
    public static ImmutableArray<ConstructionTypeParameterModel> Build(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        IReadOnlyDictionary<ITypeParameterSymbol, string> typeParameterNames,
        Compilation compilation)
    {
        var constraints =
            new Dictionary<ITypeParameterSymbol, ConstraintAccumulator>(
                TypeParameterComparer.Instance);

        foreach (var typeParameter in typeParameters)
        {
            constraints.Add(
                typeParameter,
                new ConstraintAccumulator(compilation));
        }

        AddDefinitionConstraints(
            sourceType,
            typeParameterNames,
            constraints);
        AddDefinitionConstraints(
            destinationType,
            typeParameterNames,
            constraints);

        var result =
            ImmutableArray.CreateBuilder<ConstructionTypeParameterModel>(
                typeParameters.Length);

        foreach (var typeParameter in typeParameters)
        {
            result.Add(
                constraints[typeParameter].Build(
                    typeParameterNames[typeParameter]));
        }

        return result.ToImmutable();
    }

    private static void AddDefinitionConstraints(
        ITypeSymbol type,
        IReadOnlyDictionary<ITypeParameterSymbol, string>
            typeParameterNames,
        IReadOnlyDictionary<ITypeParameterSymbol, ConstraintAccumulator>
            constraints)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            AddDefinitionConstraints(
                arrayType.ElementType,
                typeParameterNames,
                constraints);
            return;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return;
        }

        if (namedType.ContainingType is { } containingType)
        {
            AddDefinitionConstraints(
                containingType,
                typeParameterNames,
                constraints);
        }

        var constraintTypeNames = BuildConstraintTypeNames(
            namedType,
            typeParameterNames);

        for (var index = 0;
             index < namedType.TypeArguments.Length;
             index++)
        {
            var argument = namedType.TypeArguments[index];

            if (argument is ITypeParameterSymbol typeParameter &&
                constraints.TryGetValue(
                    typeParameter,
                    out var accumulator))
            {
                accumulator.Add(
                    namedType.TypeParameters[index],
                    constraintTypeNames);
            }

            AddDefinitionConstraints(
                argument,
                typeParameterNames,
                constraints);
        }
    }

    private static IReadOnlyDictionary<ITypeParameterSymbol, string>
        BuildConstraintTypeNames(
            INamedTypeSymbol type,
            IReadOnlyDictionary<ITypeParameterSymbol, string>
                typeParameterNames)
    {
        var result = new Dictionary<ITypeParameterSymbol, string>(
            TypeParameterComparer.Instance);

        foreach (var pair in typeParameterNames)
        {
            result.Add(
                pair.Key,
                EscapeIdentifier(pair.Value));
        }

        var containingTypes = new Stack<INamedTypeSymbol>();

        for (var current = type;
             current is not null;
             current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        while (containingTypes.Count > 0)
        {
            var current = containingTypes.Pop();

            for (var index = 0;
                 index < current.TypeArguments.Length;
                 index++)
            {
                result[current.TypeParameters[index]] =
                    GeneratedTypeNameBuilder.Build(
                        current.TypeArguments[index],
                        typeParameterNames);
            }
        }

        return result;
    }

    private static string EscapeIdentifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
    }

    private sealed class ConstraintAccumulator
    {
        private readonly Compilation _compilation;
        private readonly List<TypeConstraint> _typeConstraints = new();
        private readonly HashSet<string> _typeConstraintNames =
            new(StringComparer.Ordinal);

        private PrimaryConstraint _primaryConstraint;
        private bool _hasConstructorConstraint;
        private bool _requiresNullableAnnotationsDisabled;

        public ConstraintAccumulator(Compilation compilation)
        {
            _compilation = compilation;
        }

        public void Add(
            ITypeParameterSymbol definitionParameter,
            IReadOnlyDictionary<ITypeParameterSymbol, string>
                constraintTypeNames)
        {
            MergePrimaryConstraint(
                GetPrimaryConstraint(definitionParameter));

            foreach (var constraintType in
                     definitionParameter.ConstraintTypes)
            {
                AddTypeConstraint(
                    constraintType,
                    GeneratedTypeNameBuilder.Build(
                        constraintType,
                        constraintTypeNames,
                        escapeTypeParameterNames: false));

                _requiresNullableAnnotationsDisabled |=
                    HasObliviousTopLevelAnnotation(constraintType);
            }

            _hasConstructorConstraint |=
                definitionParameter.HasConstructorConstraint;
        }

        public ConstructionTypeParameterModel Build(string name)
        {
            var result = ImmutableArray.CreateBuilder<string>();

            if (_primaryConstraint != PrimaryConstraint.None)
            {
                result.Add(BuildPrimaryConstraint(_primaryConstraint));
            }

            var baseConstraint = _typeConstraints.FirstOrDefault(
                static constraint => constraint.IsClass);

            if (baseConstraint is not null)
            {
                result.Add(baseConstraint.Name);
            }

            foreach (var constraint in _typeConstraints)
            {
                if (!constraint.IsClass)
                {
                    result.Add(constraint.Name);
                }
            }

            if (_hasConstructorConstraint &&
                _primaryConstraint is not
                    (PrimaryConstraint.Struct or
                     PrimaryConstraint.Unmanaged))
            {
                result.Add("new()");
            }

            return new ConstructionTypeParameterModel(
                name,
                result.ToImmutable(),
                _requiresNullableAnnotationsDisabled ||
                _primaryConstraint == PrimaryConstraint.ClassOblivious);
        }

        private void AddTypeConstraint(
            ITypeSymbol type,
            string name)
        {
            var candidate = new TypeConstraint(
                name,
                type,
                type.TypeKind == TypeKind.Class);
            var identity = BuildTypeConstraintIdentity(name);

            if (!_typeConstraintNames.Add(identity))
            {
                var duplicateIndex = _typeConstraints.FindIndex(
                    constraint =>
                        BuildTypeConstraintIdentity(constraint.Name) ==
                        identity);

                if (duplicateIndex >= 0 &&
                    IsTopLevelNullable(
                        _typeConstraints[duplicateIndex].Name) &&
                    !IsTopLevelNullable(candidate.Name))
                {
                    _typeConstraints[duplicateIndex] = candidate;
                }

                return;
            }

            if (!candidate.IsClass)
            {
                _typeConstraints.Add(candidate);
                return;
            }

            var existingIndex = _typeConstraints.FindIndex(
                static constraint => constraint.IsClass);

            if (existingIndex < 0)
            {
                _typeConstraints.Add(candidate);
                return;
            }

            var existing = _typeConstraints[existingIndex];

            if (IsMoreSpecific(candidate.Type, existing.Type))
            {
                _typeConstraintNames.Remove(
                    BuildTypeConstraintIdentity(existing.Name));
                _typeConstraints[existingIndex] = candidate;
                return;
            }

            _typeConstraintNames.Remove(identity);
        }

        private static string BuildTypeConstraintIdentity(string name)
        {
            return IsTopLevelNullable(name)
                ? name.Substring(0, name.Length - 1)
                : name;
        }

        private static bool IsTopLevelNullable(string name)
        {
            return name.EndsWith("?", StringComparison.Ordinal);
        }

        private bool IsMoreSpecific(
            ITypeSymbol candidate,
            ITypeSymbol existing)
        {
            var candidateConversion = _compilation.ClassifyCommonConversion(
                candidate,
                existing);
            var existingConversion = _compilation.ClassifyCommonConversion(
                existing,
                candidate);

            if (candidateConversion.IsImplicit !=
                existingConversion.IsImplicit)
            {
                return candidateConversion.IsImplicit;
            }

            if (candidate is not INamedTypeSymbol candidateNamed ||
                existing is not INamedTypeSymbol existingNamed)
            {
                return false;
            }

            for (var current = candidateNamed.BaseType;
                 current is not null;
                 current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(
                        current.OriginalDefinition,
                        existingNamed.OriginalDefinition))
                {
                    return true;
                }
            }

            return false;
        }

        private void MergePrimaryConstraint(PrimaryConstraint candidate)
        {
            if (candidate == PrimaryConstraint.None ||
                candidate == _primaryConstraint)
            {
                return;
            }

            if (_primaryConstraint == PrimaryConstraint.None)
            {
                _primaryConstraint = candidate;
                return;
            }

            if (_primaryConstraint == PrimaryConstraint.NotNull)
            {
                _primaryConstraint = candidate switch
                {
                    PrimaryConstraint.ClassNullable or
                    PrimaryConstraint.ClassOblivious =>
                        PrimaryConstraint.Class,
                    _ => candidate
                };
                return;
            }

            if (candidate == PrimaryConstraint.NotNull)
            {
                if (_primaryConstraint is
                    PrimaryConstraint.ClassNullable or
                    PrimaryConstraint.ClassOblivious)
                {
                    _primaryConstraint = PrimaryConstraint.Class;
                }

                return;
            }

            if (IsReferenceConstraint(_primaryConstraint) &&
                IsReferenceConstraint(candidate))
            {
                _primaryConstraint =
                    _primaryConstraint == PrimaryConstraint.Class ||
                    candidate == PrimaryConstraint.Class
                        ? PrimaryConstraint.Class
                        : _primaryConstraint ==
                          PrimaryConstraint.ClassNullable ||
                          candidate == PrimaryConstraint.ClassNullable
                            ? PrimaryConstraint.ClassNullable
                            : PrimaryConstraint.ClassOblivious;
                return;
            }

            if (IsValueConstraint(_primaryConstraint) &&
                IsValueConstraint(candidate))
            {
                _primaryConstraint =
                    _primaryConstraint == PrimaryConstraint.Unmanaged ||
                    candidate == PrimaryConstraint.Unmanaged
                        ? PrimaryConstraint.Unmanaged
                        : PrimaryConstraint.Struct;
            }
        }

        private static PrimaryConstraint GetPrimaryConstraint(
            ITypeParameterSymbol typeParameter)
        {
            if (typeParameter.HasUnmanagedTypeConstraint)
            {
                return PrimaryConstraint.Unmanaged;
            }

            if (typeParameter.HasValueTypeConstraint)
            {
                return PrimaryConstraint.Struct;
            }

            if (typeParameter.HasReferenceTypeConstraint)
            {
                return typeParameter
                           .ReferenceTypeConstraintNullableAnnotation switch
                       {
                           NullableAnnotation.Annotated =>
                               PrimaryConstraint.ClassNullable,
                           NullableAnnotation.None =>
                               PrimaryConstraint.ClassOblivious,
                           _ => PrimaryConstraint.Class
                       };
            }

            return typeParameter.HasNotNullConstraint
                ? PrimaryConstraint.NotNull
                : PrimaryConstraint.None;
        }

        private static string BuildPrimaryConstraint(
            PrimaryConstraint constraint)
        {
            return constraint switch
            {
                PrimaryConstraint.NotNull => "notnull",
                PrimaryConstraint.ClassNullable => "class?",
                PrimaryConstraint.ClassOblivious or
                PrimaryConstraint.Class => "class",
                PrimaryConstraint.Struct => "struct",
                PrimaryConstraint.Unmanaged => "unmanaged",
                _ => throw new InvalidOperationException(
                    "A primary constraint is required.")
            };
        }

        private static bool IsReferenceConstraint(
            PrimaryConstraint constraint)
        {
            return constraint is
                PrimaryConstraint.ClassNullable or
                PrimaryConstraint.ClassOblivious or
                PrimaryConstraint.Class;
        }

        private static bool IsValueConstraint(
            PrimaryConstraint constraint)
        {
            return constraint is
                PrimaryConstraint.Struct or
                PrimaryConstraint.Unmanaged;
        }

        private static bool HasObliviousTopLevelAnnotation(
            ITypeSymbol type)
        {
            return (type.IsReferenceType ||
                    type.TypeKind == TypeKind.TypeParameter) &&
                   type.NullableAnnotation == NullableAnnotation.None;
        }
    }

    private sealed record TypeConstraint(
        string Name,
        ITypeSymbol Type,
        bool IsClass);

    private sealed class TypeParameterComparer :
        IEqualityComparer<ITypeParameterSymbol>
    {
        public static TypeParameterComparer Instance { get; } = new();

        public bool Equals(
            ITypeParameterSymbol? x,
            ITypeParameterSymbol? y)
        {
            return SymbolEqualityComparer.Default.Equals(x, y);
        }

        public int GetHashCode(ITypeParameterSymbol obj)
        {
            return SymbolEqualityComparer.Default.GetHashCode(obj);
        }
    }

    private enum PrimaryConstraint
    {
        None,
        NotNull,
        ClassNullable,
        ClassOblivious,
        Class,
        Struct,
        Unmanaged
    }
}
