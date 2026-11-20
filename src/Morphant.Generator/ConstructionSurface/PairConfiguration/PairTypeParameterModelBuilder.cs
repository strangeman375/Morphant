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
        if (typeParameters.IsEmpty)
            return ImmutableArray<ConstructionTypeParameterModel>.Empty;

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
            constraints, compilation);
        AddDefinitionConstraints(
            destinationType,
            typeParameterNames,
            constraints, compilation);

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
            constraints, Compilation compilation)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            AddDefinitionConstraints(
                arrayType.ElementType,
                typeParameterNames,
                constraints, compilation);
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
                constraints, compilation);
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
            else if (argument is not ITypeParameterSymbol)
            {
                AddIndirectConstraints(argument, namedType.TypeParameters[index], namedType,
                    typeParameterNames, constraints, compilation);
            }

            AddDefinitionConstraints(
                argument,
                typeParameterNames,
                constraints, compilation);
        }
    }

    private static void AddIndirectConstraints(
        ITypeSymbol argument, ITypeParameterSymbol formal, INamedTypeSymbol owner,
        IReadOnlyDictionary<ITypeParameterSymbol, string> names,
        IReadOnlyDictionary<ITypeParameterSymbol, ConstraintAccumulator> constraints, Compilation compilation)
    {
        if (formal.HasUnmanagedTypeConstraint && argument.IsValueType)
        {
            // Ask Roslyn about the constructed layout. A phantom parameter or
            // a static field does not contribute to unmanagedness.
            var parameters = GeneratedTypeNameBuilder.CollectTypeParameters(argument);
            var value = compilation.GetSpecialType(SpecialType.System_Int32);
            var reference = compilation.GetSpecialType(SpecialType.System_Object);
            var replacements = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var parameter in parameters)
                replacements.Add(parameter, value);

            foreach (var parameter in parameters)
            {
                replacements[parameter] = reference;
                if (!MapperTypeSubstitution.Substitute(argument, replacements, compilation).IsUnmanagedType &&
                    constraints.TryGetValue(parameter, out var required))
                    required.RequireUnmanaged();
                replacements[parameter] = value;
            }
        }

        if (formal.ConstraintTypes.IsEmpty)
            return;

        var substitutions = MapperTypeSubstitution.Build(owner.OriginalDefinition, owner);
        foreach (var bound in formal.ConstraintTypes)
            AddConversion(argument, MapperTypeSubstitution.Substitute(bound, substitutions, compilation));

        void AddConversion(ITypeSymbol from, ITypeSymbol to)
        {
            if (SymbolEqualityComparer.Default.Equals(from, to))
                return;
            if (from is ITypeParameterSymbol parameter)
            {
                if (constraints.TryGetValue(parameter, out var required))
                    required.RequireReferenceConversion(to, names);
                return;
            }

            if (from is IArrayTypeSymbol array)
            {
                if (to is IArrayTypeSymbol targetArray && array.Rank == targetArray.Rank)
                    AddConversion(array.ElementType, targetArray.ElementType);
                else if (array.IsSZArray && to is INamedTypeSymbol targetInterface &&
                         targetInterface.OriginalDefinition.SpecialType is
                             SpecialType.System_Collections_Generic_IEnumerable_T or
                             SpecialType.System_Collections_Generic_IList_T or
                             SpecialType.System_Collections_Generic_ICollection_T or
                             SpecialType.System_Collections_Generic_IReadOnlyList_T or
                             SpecialType.System_Collections_Generic_IReadOnlyCollection_T)
                    AddConversion(array.ElementType, targetInterface.TypeArguments[0]);
                return;
            }

            if (to is not INamedTypeSymbol target || target.Arity == 0 || from is not INamedTypeSymbol source)
                return;

            // An exact implemented interface needs no variance conversion.
            if (source.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, target)))
                return;

            var matches = source.AllInterfaces.Where(i =>
                SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, target.OriginalDefinition));
            var implementation = SymbolEqualityComparer.Default.Equals(source.OriginalDefinition, target.OriginalDefinition)
                ? source
                : matches.FirstOrDefault(i => compilation.ClassifyCommonConversion(i, target).IsImplicit) ?? matches.FirstOrDefault();
            if (implementation is null)
                return;

            for (var i = 0; i < target.Arity; i++)
            {
                var actual = implementation.TypeArguments[i];
                var expected = target.TypeArguments[i];
                switch (target.TypeParameters[i].Variance)
                {
                    case VarianceKind.Out:
                        AddConversion(actual, expected);
                        break;
                    case VarianceKind.In:
                        AddConversion(expected, actual);
                        break;
                }
            }
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

        public void RequireUnmanaged() => MergePrimaryConstraint(PrimaryConstraint.Unmanaged);

        public void RequireReferenceConversion(ITypeSymbol target,
            IReadOnlyDictionary<ITypeParameterSymbol, string> names)
        {
            MergePrimaryConstraint(target.NullableAnnotation switch
            {
                NullableAnnotation.Annotated => PrimaryConstraint.ClassNullable,
                NullableAnnotation.None => PrimaryConstraint.ClassOblivious,
                _ => PrimaryConstraint.Class
            });
            if (target.SpecialType != SpecialType.System_Object && target is not IDynamicTypeSymbol)
            {
                AddTypeConstraint(target, GeneratedTypeNameBuilder.Build(target, names));
                _requiresNullableAnnotationsDisabled |= HasObliviousTopLevelAnnotation(target);
            }
        }

        public ConstructionTypeParameterModel Build(string name)
        {
            var result = ImmutableArray.CreateBuilder<string>();

            var baseConstraint = _typeConstraints.FirstOrDefault(static constraint => constraint.IsClass);
            var ordinaryBase = baseConstraint is not null && baseConstraint.Type.SpecialType is not
                (SpecialType.System_Enum or SpecialType.System_Delegate or SpecialType.System_MulticastDelegate);
            if (_primaryConstraint != PrimaryConstraint.None && !ordinaryBase)
                result.Add(BuildPrimaryConstraint(_primaryConstraint));

            if (baseConstraint is not null)
            {
                var nameOfBase = baseConstraint.Name;
                if (ordinaryBase && _primaryConstraint is PrimaryConstraint.Class or PrimaryConstraint.NotNull &&
                    IsTopLevelNullable(nameOfBase))
                    nameOfBase = nameOfBase.Substring(0, nameOfBase.Length - 1);

                result.Add(nameOfBase);
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
