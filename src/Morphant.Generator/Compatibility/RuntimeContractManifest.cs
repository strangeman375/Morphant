using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.Compatibility;

internal static class RuntimeContractManifest
{
    private static readonly TypePattern Void = Named("System.Void");
    private static readonly TypePattern Boolean = Named("System.Boolean");
    private static readonly TypePattern Object = Named("System.Object");
    private static readonly TypePattern String = Named("System.String");
    private static readonly TypePattern SystemType = Named("System.Type");
    private static readonly TypePattern ServiceProvider =
        Named("System.IServiceProvider");

    private static readonly ImmutableArray<TypeRequirement> Requirements =
    ImmutableArray.Create<TypeRequirement>(
        // Attribute contract.
        Requirement(
            "Morphant.MorphantMapperAttribute",
            TypeKind.Class,
            IsMorphantMapperAttribute),

        // TypeMapper and compile-time intrinsics.
        Requirement(
            "Morphant.TypeMapper",
            TypeKind.Class,
            IsTypeMapper),

        // Builder contract, ordered by metadata name.
        Requirement(
            "Morphant.MapperBuilder",
            TypeKind.Class,
            IsMapperBuilder),
        Requirement(
            "Morphant.MapperBuilderBase`1",
            TypeKind.Class,
            IsMapperBuilderBase),
        Requirement(
            "Morphant.MapperBuilder`2",
            TypeKind.Class,
            IsMappingBuilder),

        // Runtime contracts and mapping-scope entry points.
        EnumRequirement(
            "Morphant.ConstructorSelection",
            ("Default", 0),
            ("Explicit", 1),
            ("Parameterless", 2),
            ("Single", 3),
            ("Unambiguous", 4),
            ("Greediest", 5),
            ("Largest", 6)),
        Requirement(
            "Morphant.Context.MappingContext",
            TypeKind.Struct,
            IsMappingContext),
        Requirement(
            "Morphant.Context.MappingContextMarker",
            TypeKind.Class,
            IsMappingContextMarker),
        EnumRequirement(
            "Morphant.Context.MappingOperation",
            ("Create", 1),
            ("Update", 2)),
        Requirement("Morphant.IMapper", TypeKind.Interface, IsMapperInterface),
        Requirement(
            "Morphant.ITypeMapper`2",
            TypeKind.Interface,
            IsTypeMapperInterface),
        Requirement("Morphant.Mapper", TypeKind.Class, IsMapper),
        EnumRequirement(
            "Morphant.MappingMode",
            ("Default", 0),
            ("Create", 1),
            ("Update", 2),
            ("CreateAndUpdate", 3)),
        EnumRequirement(
            "Morphant.MemberSelection",
            ("Default", 0),
            ("Auto", 1),
            ("Explicit", 2)),
        EnumRequirement(
            "Morphant.NullDestinationHandling",
            ("Default", 0),
            ("Create", 1),
            ("Throw", 2)),
        EnumRequirement(
            "Morphant.NullSourceHandling",
            ("Default", 0),
            ("ReturnNull", 1),
            ("ReturnDestination", 2),
            ("Throw", 3)),
        Requirement("Morphant.Option`1", TypeKind.Struct, IsOption),
        Requirement(
            "Morphant.TypeMapperExtensions",
            TypeKind.Class,
            IsTypeMapperExtensions),
        EnumRequirement(
            "Morphant.UnmappedMemberValidation",
            ("Default", 0),
            ("None", 1),
            ("Source", 2),
            ("Destination", 3),
            ("Strict", 4)),

        // Delegate families, ordered by metadata name and arity.
        DelegateRequirement(
            "Morphant.Delegates.ConstructUsing`2",
            [VarianceKind.In, VarianceKind.Out],
            TypeParameter(1),
            TypeParameter(0)),
        DelegateRequirement(
            "Morphant.Delegates.ConstructUsing`3",
            [VarianceKind.In, VarianceKind.In, VarianceKind.Out],
            TypeParameter(2),
            TypeParameter(0),
            TypeParameter(1)),
        DelegateRequirement(
            "Morphant.Delegates.Construct`2",
            [VarianceKind.In, VarianceKind.Out],
            TypeParameter(1),
            TypeParameter(0)),
        DelegateRequirement(
            "Morphant.Delegates.Construct`3",
            [VarianceKind.In, VarianceKind.In, VarianceKind.Out],
            TypeParameter(2),
            TypeParameter(0),
            TypeParameter(1)),
        DelegateRequirement(
            "Morphant.Delegates.Convert`2",
            [VarianceKind.In, VarianceKind.Out],
            TypeParameter(1),
            TypeParameter(0)),
        DelegateRequirement(
            "Morphant.Delegates.Convert`3",
            [VarianceKind.In, VarianceKind.None, VarianceKind.Out],
            TypeParameter(2),
            TypeParameter(0),
            Named("Morphant.Option`1", TypeParameter(1))),
        DelegateRequirement(
            "Morphant.Delegates.Convert`4",
            [
                VarianceKind.In,
                VarianceKind.None,
                VarianceKind.In,
                VarianceKind.Out
            ],
            TypeParameter(3),
            TypeParameter(0),
            Named("Morphant.Option`1", TypeParameter(1)),
            TypeParameter(2)),
        DelegateRequirement(
            "Morphant.Delegates.Members`2",
            [VarianceKind.In, VarianceKind.Out],
            TypeParameter(1),
            TypeParameter(0)),
        DelegateRequirement(
            "Morphant.Delegates.Members`3",
            [VarianceKind.In, VarianceKind.None, VarianceKind.Out],
            TypeParameter(2),
            TypeParameter(0),
            Named("Morphant.Option`1", TypeParameter(1))),
        DelegateRequirement(
            "Morphant.Delegates.Members`4",
            [
                VarianceKind.In,
                VarianceKind.None,
                VarianceKind.In,
                VarianceKind.Out
            ],
            TypeParameter(3),
            TypeParameter(0),
            Named("Morphant.Option`1", TypeParameter(1)),
            TypeParameter(2)),
        DelegateRequirement(
            "Morphant.Delegates.Members`5",
            [
                VarianceKind.In,
                VarianceKind.None,
                VarianceKind.In,
                VarianceKind.In,
                VarianceKind.Out
            ],
            TypeParameter(4),
            TypeParameter(0),
            Named("Morphant.Option`1", TypeParameter(1)),
            TypeParameter(2),
            TypeParameter(3)),
        DelegateRequirement(
            "Morphant.Delegates.ResolveUsing`3",
            [VarianceKind.In, VarianceKind.None, VarianceKind.Out],
            TypeParameter(2),
            TypeParameter(0),
            Named("Morphant.Option`1", TypeParameter(1))),
        DelegateRequirement(
            "Morphant.Delegates.ResolveUsing`4",
            [
                VarianceKind.In,
                VarianceKind.None,
                VarianceKind.In,
                VarianceKind.Out
            ],
            TypeParameter(3),
            TypeParameter(0),
            Named("Morphant.Option`1", TypeParameter(1)),
            TypeParameter(2)),
        DelegateRequirement(
            "Morphant.Delegates.Resolve`3",
            [VarianceKind.In, VarianceKind.None, VarianceKind.Out],
            TypeParameter(2),
            TypeParameter(0),
            Named("Morphant.Option`1", TypeParameter(1))),
        DelegateRequirement(
            "Morphant.Delegates.Resolve`4",
            [
                VarianceKind.In,
                VarianceKind.None,
                VarianceKind.In,
                VarianceKind.Out
            ],
            TypeParameter(3),
            TypeParameter(0),
            Named("Morphant.Option`1", TypeParameter(1)),
            TypeParameter(2)),

        // Constructor/member wrappers and markers.
        Requirement(
            "Morphant.Markers.AutoMarker",
            TypeKind.Class,
            symbol => IsSealedMarker(
                symbol,
                "Morphant.Markers.MemberMarker")),
        Requirement(
            "Morphant.Markers.AutoMarker`1",
            TypeKind.Class,
            symbol =>
                IsSealedMarker(symbol, "Morphant.Markers.MemberMarker") &&
                HasImplicitConversion(
                    symbol,
                    TypeParameter(0),
                    Named(
                        "Morphant.Markers.AutoMarker`1",
                        TypeParameter(0)))),
        Requirement(
            "Morphant.Markers.ByConventionMarker",
            TypeKind.Class,
            symbol => IsSealedMarker(
                symbol,
                "Morphant.Markers.ConstructorMarker")),
        Requirement(
            "Morphant.Markers.ConstructorMarker",
            TypeKind.Class,
            symbol => symbol.IsAbstract && !symbol.IsSealed),
        Requirement(
            "Morphant.Markers.IgnoreMarker",
            TypeKind.Class,
            symbol => IsSealedMarker(
                symbol,
                "Morphant.Markers.MemberMarker")),
        Requirement(
            "Morphant.Markers.IgnoreMarker`1",
            TypeKind.Class,
            symbol =>
                IsSealedMarker(symbol, "Morphant.Markers.MemberMarker") &&
                HasImplicitConversion(
                    symbol,
                    TypeParameter(0),
                    Named(
                        "Morphant.Markers.IgnoreMarker`1",
                        TypeParameter(0)))),
        Requirement(
            "Morphant.Markers.MapMarker",
            TypeKind.Class,
            symbol =>
                symbol.IsAbstract &&
                !symbol.IsSealed &&
                HasBaseType(symbol, "Morphant.Markers.MemberMarker")),
        Requirement(
            "Morphant.Markers.MapMarker`1",
            TypeKind.Class,
            symbol =>
                IsSealedMarker(symbol, "Morphant.Markers.MapMarker") &&
                HasImplicitConversion(
                    symbol,
                    TypeParameter(0),
                    Named(
                        "Morphant.Markers.MapMarker`1",
                        TypeParameter(0)))),
        Requirement(
            "Morphant.Markers.MemberMarker",
            TypeKind.Class,
            symbol => symbol.IsAbstract && !symbol.IsSealed),
        Requirement(
            "Morphant.Markers.ValueMarker`1",
            TypeKind.Class,
            symbol => symbol.IsSealed && !symbol.IsAbstract),
        Requirement(
            "Morphant.Members.ConstructorParameter`1",
            TypeKind.Class,
            IsConstructorParameterWrapper),
        Requirement(
            "Morphant.Members.Member`1",
            TypeKind.Class,
            IsMemberWrapper),

        // Exception hierarchy and generated/runtime failure shapes.
        ExceptionRequirement(
            "Morphant.Exceptions.AmbiguousMappingException",
            "Morphant.Exceptions.MappingException",
            MappingFailureConstructor()),
        ExceptionRequirement(
            "Morphant.Exceptions.InvalidMappingContextException",
            "Morphant.Exceptions.MorphantException",
            Constructor()),
        ExceptionRequirement(
            "Morphant.Exceptions.InvalidMappingRegistrationException",
            "Morphant.Exceptions.MappingException",
            MappingFailureConstructor()),
        ExceptionRequirement(
            "Morphant.Exceptions.MappingConfigurationException",
            "Morphant.Exceptions.MappingException",
            Constructor(
                Named("Morphant.Context.MappingOperation"),
                SystemType,
                SystemType,
                String),
            Property("Reason", String)),
        Requirement(
            "Morphant.Exceptions.MappingException",
            TypeKind.Class,
            IsMappingException),
        ExceptionRequirement(
            "Morphant.Exceptions.MappingNotFoundException",
            "Morphant.Exceptions.MappingException",
            MappingFailureConstructor()),
        ExceptionRequirement(
            "Morphant.Exceptions.MappingOperationNotSupportedException",
            "Morphant.Exceptions.MappingException",
            Constructor(
                Named("Morphant.Context.MappingOperation"),
                SystemType,
                SystemType,
                Named("Morphant.MappingMode")),
            Property(
                "EffectiveMappingMode",
                Named("Morphant.MappingMode"))),
        ExceptionRequirement(
            "Morphant.Exceptions.MappingScopeCompletedException",
            "Morphant.Exceptions.MappingException",
            MappingFailureConstructor()),
        Requirement(
            "Morphant.Exceptions.MorphantException",
            TypeKind.Class,
            symbol =>
                symbol.IsAbstract &&
                !symbol.IsSealed &&
                HasBaseType(symbol, "System.Exception")),
        ExceptionRequirement(
            "Morphant.Exceptions.NestedDestinationTypeMismatchException",
            "Morphant.Exceptions.MappingException",
            Constructor(
                Named("Morphant.Context.MappingOperation"),
                SystemType,
                SystemType,
                SystemType,
                SystemType),
            Property("ActualDestinationType", SystemType),
            Property("ExpectedDestinationType", SystemType)),
        ExceptionRequirement(
            "Morphant.Exceptions.NullDestinationException",
            "Morphant.Exceptions.MappingException",
            MappingFailureConstructor()),
        ExceptionRequirement(
            "Morphant.Exceptions.NullSourceException",
            "Morphant.Exceptions.MappingException",
            MappingFailureConstructor()),
        ExceptionRequirement(
            "Morphant.Exceptions.OptionValueMissingException",
            "Morphant.Exceptions.MorphantException",
            Constructor()),
        ExceptionRequirement(
            "Morphant.Exceptions.RuntimeInvocationNotSupportedException",
            "Morphant.Exceptions.MorphantException",
            Constructor()),
        ExceptionRequirement(
            "Morphant.Exceptions.UnmatchedMappingSwitchException",
            "Morphant.Exceptions.MappingException",
            MappingFailureConstructor())
    );

    public static bool DeclaresAnySymbol(IAssemblySymbol assembly)
    {
        return assembly.GlobalNamespace.GetNamespaceMembers()
                   .Any(static namespaceSymbol =>
                       namespaceSymbol.Name == "Morphant") &&
               Requirements.Any(requirement =>
                   !FindOwnedTypes(assembly, requirement.MetadataName).IsEmpty);
    }

    public static bool HasAmbiguousSymbol(IAssemblySymbol assembly)
    {
        return Requirements.Any(requirement =>
            FindOwnedTypes(assembly, requirement.MetadataName).Length > 1);
    }

    public static string? FindFirstFailure(IAssemblySymbol assembly)
    {
        foreach (var requirement in Requirements)
        {
            var symbols = FindOwnedTypes(assembly, requirement.MetadataName);

            if (symbols.IsEmpty)
            {
                return $"required symbol '{requirement.MetadataName}' is missing";
            }

            if (symbols.Length != 1 ||
                !requirement.HasCompatibleShape(symbols[0]))
            {
                return $"required symbol '{requirement.MetadataName}' has an incompatible shape";
            }
        }

        return null;
    }

    public static string GetFullMetadataName(INamedTypeSymbol symbol)
    {
        if (symbol.ContainingType is { } containingType)
        {
            return GetFullMetadataName(containingType) + "+" +
                   symbol.MetadataName;
        }

        var namespaceName = symbol.ContainingNamespace?.ToDisplayString();

        return string.IsNullOrEmpty(namespaceName)
            ? symbol.MetadataName
            : namespaceName + "." + symbol.MetadataName;
    }

    private static ImmutableArray<INamedTypeSymbol> FindOwnedTypes(
        IAssemblySymbol assembly,
        string metadataName)
    {
        var lastDot = metadataName.LastIndexOf('.');
        var namespaceName = lastDot < 0
            ? string.Empty
            : metadataName.Substring(0, lastDot);
        var typeMetadataName = lastDot < 0
            ? metadataName
            : metadataName.Substring(lastDot + 1);
        var tick = typeMetadataName.LastIndexOf('`');
        var typeName = tick < 0
            ? typeMetadataName
            : typeMetadataName.Substring(0, tick);
        var arity = tick < 0
            ? 0
            : int.Parse(typeMetadataName.Substring(tick + 1));
        IEnumerable<INamespaceSymbol> namespaces = [assembly.GlobalNamespace];

        if (namespaceName.Length != 0)
        {
            foreach (var segment in namespaceName.Split('.'))
            {
                namespaces = namespaces
                    .SelectMany(namespaceSymbol =>
                        namespaceSymbol.GetNamespaceMembers()
                            .Where(candidate => candidate.Name == segment))
                    .ToArray();
            }
        }

        return namespaces
            .SelectMany(namespaceSymbol =>
                namespaceSymbol.GetTypeMembers(typeName, arity))
            .Where(symbol =>
                SymbolEqualityComparer.Default.Equals(
                    symbol.ContainingAssembly,
                    assembly) &&
                GetFullMetadataName(symbol) == metadataName)
            .ToImmutableArray();
    }

    private static TypeRequirement Requirement(
        string metadataName,
        TypeKind typeKind,
        Func<INamedTypeSymbol, bool> hasCompatibleShape)
    {
        return new TypeRequirement(
            metadataName,
            symbol =>
                symbol.TypeKind == typeKind &&
                symbol.DeclaredAccessibility == Accessibility.Public &&
                hasCompatibleShape(symbol));
    }

    private static TypeRequirement EnumRequirement(
        string metadataName,
        params (string Name, int Value)[] values)
    {
        return Requirement(
            metadataName,
            TypeKind.Enum,
            symbol =>
                symbol.EnumUnderlyingType?.SpecialType ==
                    SpecialType.System_Int32 &&
                values.All(value => HasEnumValue(
                    symbol,
                    value.Name,
                    value.Value)));
    }

    private static TypeRequirement DelegateRequirement(
        string metadataName,
        VarianceKind[] variance,
        TypePattern returnType,
        params TypePattern[] parameters)
    {
        return Requirement(
            metadataName,
            TypeKind.Delegate,
            symbol =>
                HasVariance(symbol, variance) &&
                symbol.DelegateInvokeMethod is { } invoke &&
                returnType.Matches(invoke.ReturnType) &&
                ParametersMatch(
                    invoke.Parameters,
                    parameters.Select(static type => Parameter(type)).ToArray()));
    }

    private static TypeRequirement ExceptionRequirement(
        string metadataName,
        string baseMetadataName,
        params MemberRequirement[] members)
    {
        return Requirement(
            metadataName,
            TypeKind.Class,
            symbol =>
                symbol.IsSealed &&
                !symbol.IsAbstract &&
                HasBaseType(symbol, baseMetadataName) &&
                members.All(member => member.Matches(symbol)));
    }

    private static bool IsMorphantMapperAttribute(INamedTypeSymbol symbol)
    {
        return symbol.IsSealed &&
               !symbol.IsAbstract &&
               HasBaseType(symbol, "System.Attribute") &&
               HasConstructor(symbol, Accessibility.Public);
    }

    private static bool IsTypeMapper(INamedTypeSymbol symbol)
    {
        return symbol.IsAbstract &&
               !symbol.IsSealed &&
               HasMethod(
                   symbol,
                   "Configure",
                   Accessibility.Protected,
                   isStatic: false,
                   arity: 0,
                   Void,
                   [Parameter(Named("Morphant.MapperBuilder"))],
                   method => method.IsAbstract) &&
               HasMethod(
                   symbol,
                   "Supports",
                   Accessibility.ProtectedOrInternal,
                   isStatic: false,
                   arity: 0,
                   Boolean,
                   [Parameter(SystemType), Parameter(SystemType)],
                   method => method.IsVirtual && !method.IsAbstract) &&
               HasMethod(
                   symbol,
                   "ByConvention",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 0,
                   Named("Morphant.Markers.ByConventionMarker")) &&
               HasMethod(
                   symbol,
                   "Auto",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 0,
                   Named("Morphant.Markers.AutoMarker")) &&
               HasMethod(
                   symbol,
                   "Auto",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 1,
                   Named(
                       "Morphant.Markers.AutoMarker`1",
                       MethodTypeParameter(0))) &&
               HasMethod(
                   symbol,
                   "Ignore",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 0,
                   Named("Morphant.Markers.IgnoreMarker")) &&
               HasMethod(
                   symbol,
                   "Ignore",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 1,
                   Named(
                       "Morphant.Markers.IgnoreMarker`1",
                       MethodTypeParameter(0))) &&
               HasMethod(
                   symbol,
                   "Value",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 1,
                   Named(
                       "Morphant.Markers.ValueMarker`1",
                       MethodTypeParameter(0)),
                   [Parameter(MethodTypeParameter(0))]) &&
               HasMapIntrinsics(symbol);
    }

    private static bool HasMapIntrinsics(INamedTypeSymbol symbol)
    {
        var marker = Named("Morphant.Markers.MapMarker");
        var genericMarker = Named(
            "Morphant.Markers.MapMarker`1",
            MethodTypeParameter(0));

        return HasMethod(
                   symbol,
                   "Map",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 0,
                   marker) &&
               HasMethod(
                   symbol,
                   "Map",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 0,
                   marker,
                   [Parameter(Object)]) &&
               HasMethod(
                   symbol,
                   "Map",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 1,
                   genericMarker) &&
               HasMethod(
                   symbol,
                   "Map",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 1,
                   genericMarker,
                   [Parameter(Object)]) &&
               HasMethod(
                   symbol,
                   "Create",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 0,
                   marker,
                   [Parameter(Object)]) &&
               HasMethod(
                   symbol,
                   "Create",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 1,
                   genericMarker,
                   [Parameter(Object)]) &&
               HasMethod(
                   symbol,
                   "Update",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 0,
                   marker,
                   [Parameter(Object), Parameter(Object)]) &&
               HasMethod(
                   symbol,
                   "Update",
                   Accessibility.Protected,
                   isStatic: true,
                   arity: 1,
                   genericMarker,
                   [Parameter(Object), Parameter(Object)]);
    }

    private static bool IsMapperBuilderBase(INamedTypeSymbol symbol)
    {
        var self = TypeParameter(0);

        return symbol.IsAbstract &&
               !symbol.IsSealed &&
               symbol.TypeParameters.Length == 1 &&
               symbol.TypeParameters[0].ConstraintTypes.Any(type =>
                   Named("Morphant.MapperBuilderBase`1", self).Matches(type)) &&
               HasBuilderSetting(
                   symbol,
                   "NullSourceHandling",
                   "Morphant.NullSourceHandling") &&
               HasBuilderSetting(
                   symbol,
                   "NullDestinationHandling",
                   "Morphant.NullDestinationHandling") &&
               HasBuilderSetting(
                   symbol,
                   "ConstructorSelection",
                   "Morphant.ConstructorSelection") &&
               HasBuilderSetting(
                   symbol,
                   "MemberSelection",
                   "Morphant.MemberSelection") &&
               HasBuilderSetting(
                   symbol,
                   "UnmappedMemberValidation",
                   "Morphant.UnmappedMemberValidation");
    }

    private static bool HasBuilderSetting(
        INamedTypeSymbol symbol,
        string methodName,
        string parameterMetadataName)
    {
        return HasMethod(
            symbol,
            methodName,
            Accessibility.Public,
            isStatic: false,
            arity: 0,
            TypeParameter(0),
            [Parameter(Named(parameterMetadataName))]);
    }

    private static bool IsMapperBuilder(INamedTypeSymbol symbol)
    {
        return symbol.IsSealed &&
               !symbol.IsAbstract &&
               HasBaseType(
                   symbol,
                   Named(
                       "Morphant.MapperBuilderBase`1",
                       Named("Morphant.MapperBuilder"))) &&
               HasMethod(
                   symbol,
                   "MappingMode",
                   Accessibility.Public,
                   isStatic: false,
                   arity: 0,
                   Named("Morphant.MapperBuilder"),
                   [Parameter(Named("Morphant.MappingMode"))]) &&
               HasMethod(
                   symbol,
                   "Map",
                   Accessibility.Public,
                   isStatic: false,
                   arity: 2,
                   Named(
                       "Morphant.MapperBuilder`2",
                       MethodTypeParameter(0),
                       MethodTypeParameter(1)),
                   [Parameter(Named("Morphant.MappingMode"))],
                   method =>
                       method.Parameters[0].IsOptional &&
                       method.Parameters[0].HasExplicitDefaultValue &&
                       Equals(method.Parameters[0].ExplicitDefaultValue, 0));
    }

    private static bool IsMappingBuilder(INamedTypeSymbol symbol)
    {
        var self = Named(
            "Morphant.MapperBuilder`2",
            TypeParameter(0),
            TypeParameter(1));

        return symbol.IsSealed &&
               !symbol.IsAbstract &&
               HasBaseType(
                   symbol,
                   Named("Morphant.MapperBuilderBase`1", self)) &&
               HasMethod(
                   symbol,
                   "IncludeBase",
                   Accessibility.Public,
                   isStatic: false,
                   arity: 2,
                   self) &&
               HasMethod(
                   symbol,
                   "IncludeMembers",
                   Accessibility.Public,
                   isStatic: false,
                   arity: 1,
                   self,
                   [
                       Parameter(Named(
                           "System.Func`2",
                           TypeParameter(0),
                           MethodTypeParameter(0)))
                   ]);
    }

    private static bool IsTypeMapperInterface(INamedTypeSymbol symbol)
    {
        return HasVariance(
                   symbol,
                   [VarianceKind.In, VarianceKind.None]) &&
               HasMethod(
                   symbol,
                   "Create",
                   Accessibility.Public,
                   isStatic: false,
                   arity: 0,
                   TypeParameter(1),
                   [
                       Parameter(TypeParameter(0)),
                       Parameter(Named("Morphant.Context.MappingContext"))
                   ]) &&
               HasMethod(
                   symbol,
                   "Update",
                   Accessibility.Public,
                   isStatic: false,
                   arity: 0,
                   TypeParameter(1),
                   [
                       Parameter(TypeParameter(0)),
                       Parameter(TypeParameter(1)),
                       Parameter(Named("Morphant.Context.MappingContext"))
                   ]);
    }

    private static bool IsMapperInterface(INamedTypeSymbol symbol)
    {
        return HasMapMethods(symbol, isStatic: false);
    }

    private static bool IsMapper(INamedTypeSymbol symbol)
    {
        return symbol.IsSealed &&
               !symbol.IsAbstract &&
               symbol.AllInterfaces.Any(@interface =>
                   GetFullMetadataName(@interface.OriginalDefinition) ==
                       "Morphant.IMapper") &&
               HasConstructor(
                   symbol,
                   Accessibility.Public,
                   Parameter(ServiceProvider)) &&
               HasMapMethods(symbol, isStatic: false);
    }

    private static bool HasMapMethods(
        INamedTypeSymbol symbol,
        bool isStatic)
    {
        return HasMethod(
                   symbol,
                   "Map",
                   Accessibility.Public,
                   isStatic,
                   arity: 2,
                   MethodTypeParameter(1),
                   [Parameter(MethodTypeParameter(0))]) &&
               HasMethod(
                   symbol,
                   "Map",
                   Accessibility.Public,
                   isStatic,
                   arity: 2,
                   MethodTypeParameter(1),
                   [
                       Parameter(MethodTypeParameter(0)),
                       Parameter(MethodTypeParameter(1))
                   ]);
    }

    private static bool IsOption(INamedTypeSymbol symbol)
    {
        var self = Named("Morphant.Option`1", TypeParameter(0));

        return symbol.IsReadOnly &&
               HasProperty(
                   symbol,
                   "None",
                   Accessibility.Public,
                   isStatic: true,
                   self) &&
               HasProperty(
                   symbol,
                   "HasValue",
                   Accessibility.Public,
                   isStatic: false,
                   Boolean) &&
               HasProperty(
                   symbol,
                   "Value",
                   Accessibility.Public,
                   isStatic: false,
                   TypeParameter(0)) &&
               HasMethod(
                   symbol,
                   "Some",
                   Accessibility.Public,
                   isStatic: true,
                   arity: 0,
                   self,
                   [Parameter(TypeParameter(0))]) &&
               HasMethod(
                   symbol,
                   "TryGetValue",
                   Accessibility.Public,
                   isStatic: false,
                   arity: 0,
                   Boolean,
                   [Parameter(TypeParameter(0), RefKind.Out)]);
    }

    private static bool IsMappingContext(INamedTypeSymbol symbol)
    {
        return symbol.IsReadOnly &&
               HasProperty(
                   symbol,
                   "Operation",
                   Accessibility.Public,
                   isStatic: false,
                   Named("Morphant.Context.MappingOperation")) &&
               HasProperty(
                   symbol,
                   "Mapper",
                   Accessibility.Public,
                   isStatic: false,
                   Named("Morphant.IMapper"));
    }

    private static bool IsMappingContextMarker(INamedTypeSymbol symbol)
    {
        return symbol.IsAbstract &&
               !symbol.IsSealed &&
               HasProperty(
                   symbol,
                   "Operation",
                   Accessibility.Public,
                   isStatic: false,
                   Named("Morphant.Context.MappingOperation"),
                   property => property.GetMethod?.IsAbstract == true);
    }

    private static bool IsTypeMapperExtensions(INamedTypeSymbol symbol)
    {
        return symbol.IsStatic &&
               HasScopeExtension(symbol, "Create", includeDestination: false) &&
               HasScopeExtension(symbol, "Update", includeDestination: true);
    }

    private static bool HasScopeExtension(
        INamedTypeSymbol symbol,
        string name,
        bool includeDestination)
    {
        var parameters = new List<ParameterRequirement>
        {
            Parameter(Named(
                "Morphant.ITypeMapper`2",
                MethodTypeParameter(0),
                MethodTypeParameter(1))),
            Parameter(MethodTypeParameter(0))
        };

        if (includeDestination)
        {
            parameters.Add(Parameter(MethodTypeParameter(1)));
        }

        return HasMethod(
            symbol,
            name,
            Accessibility.Public,
            isStatic: true,
            arity: 2,
            MethodTypeParameter(1),
            parameters.ToArray(),
            method => method.IsExtensionMethod);
    }

    private static bool IsSealedMarker(
        INamedTypeSymbol symbol,
        string baseMetadataName)
    {
        return symbol.IsSealed &&
               !symbol.IsAbstract &&
               HasBaseType(symbol, baseMetadataName);
    }

    private static bool IsConstructorParameterWrapper(INamedTypeSymbol symbol)
    {
        return IsWrapper(symbol, "Morphant.Members.ConstructorParameter`1");
    }

    private static bool IsMemberWrapper(INamedTypeSymbol symbol)
    {
        return IsWrapper(symbol, "Morphant.Members.Member`1");
    }

    private static bool IsWrapper(
        INamedTypeSymbol symbol,
        string wrapperMetadataName)
    {
        var destination = Named(wrapperMetadataName, TypeParameter(0));
        var sources = new[]
        {
            TypeParameter(0),
            Named("Morphant.Markers.AutoMarker"),
            Named(
                "Morphant.Markers.AutoMarker`1",
                TypeParameter(0)),
            Named("Morphant.Markers.IgnoreMarker"),
            Named(
                "Morphant.Markers.IgnoreMarker`1",
                TypeParameter(0)),
            Named("Morphant.Markers.MapMarker"),
            Named(
                "Morphant.Markers.ValueMarker`1",
                TypeParameter(0))
        };

        return symbol.IsSealed &&
               !symbol.IsAbstract &&
               sources.All(source =>
                   HasImplicitConversion(symbol, source, destination));
    }

    private static bool IsMappingException(INamedTypeSymbol symbol)
    {
        return symbol.IsAbstract &&
               !symbol.IsSealed &&
               HasBaseType(symbol, "Morphant.Exceptions.MorphantException") &&
               HasProperty(
                   symbol,
                   "Operation",
                   Accessibility.Public,
                   isStatic: false,
                   Named("Morphant.Context.MappingOperation")) &&
               HasProperty(
                   symbol,
                   "SourceType",
                   Accessibility.Public,
                   isStatic: false,
                   SystemType) &&
               HasProperty(
                   symbol,
                   "DestinationType",
                   Accessibility.Public,
                   isStatic: false,
                   SystemType);
    }

    private static MemberRequirement MappingFailureConstructor()
    {
        return Constructor(
            Named("Morphant.Context.MappingOperation"),
            SystemType,
            SystemType);
    }

    private static bool HasEnumValue(
        INamedTypeSymbol symbol,
        string name,
        int value)
    {
        return symbol.GetMembers(name)
            .OfType<IFieldSymbol>()
            .Any(field =>
                field.HasConstantValue &&
                Equals(field.ConstantValue, value));
    }

    private static bool HasVariance(
        INamedTypeSymbol symbol,
        IReadOnlyList<VarianceKind> variance)
    {
        return symbol.TypeParameters.Length == variance.Count &&
               symbol.TypeParameters
                   .Select(static parameter => parameter.Variance)
                   .SequenceEqual(variance);
    }

    private static bool HasBaseType(
        INamedTypeSymbol symbol,
        string metadataName)
    {
        return symbol.BaseType is { } baseType &&
               GetFullMetadataName(baseType.OriginalDefinition) == metadataName;
    }

    private static bool HasBaseType(
        INamedTypeSymbol symbol,
        TypePattern pattern)
    {
        return symbol.BaseType is { } baseType && pattern.Matches(baseType);
    }

    private static bool HasConstructor(
        INamedTypeSymbol symbol,
        Accessibility accessibility,
        params ParameterRequirement[] parameters)
    {
        return symbol.InstanceConstructors.Any(constructor =>
            constructor.DeclaredAccessibility == accessibility &&
            ParametersMatch(constructor.Parameters, parameters));
    }

    private static bool HasMethod(
        INamedTypeSymbol symbol,
        string name,
        Accessibility accessibility,
        bool isStatic,
        int arity,
        TypePattern returnType,
        ParameterRequirement[]? parameters = null,
        Func<IMethodSymbol, bool>? additionalCheck = null)
    {
        parameters ??= [];

        return symbol.GetMembers(name)
            .OfType<IMethodSymbol>()
            .Any(method =>
                method.MethodKind == MethodKind.Ordinary &&
                method.DeclaredAccessibility == accessibility &&
                method.IsStatic == isStatic &&
                method.Arity == arity &&
                returnType.Matches(method.ReturnType) &&
                ParametersMatch(method.Parameters, parameters) &&
                (additionalCheck is null || additionalCheck(method)));
    }

    private static bool HasProperty(
        INamedTypeSymbol symbol,
        string name,
        Accessibility accessibility,
        bool isStatic,
        TypePattern type,
        Func<IPropertySymbol, bool>? additionalCheck = null)
    {
        return symbol.GetMembers(name)
            .OfType<IPropertySymbol>()
            .Any(property =>
                property.DeclaredAccessibility == accessibility &&
                property.IsStatic == isStatic &&
                property.GetMethod is not null &&
                type.Matches(property.Type) &&
                (additionalCheck is null || additionalCheck(property)));
    }

    private static bool HasImplicitConversion(
        INamedTypeSymbol symbol,
        TypePattern source,
        TypePattern destination)
    {
        return symbol.GetMembers("op_Implicit")
            .OfType<IMethodSymbol>()
            .Any(method =>
                method.MethodKind == MethodKind.Conversion &&
                method.DeclaredAccessibility == Accessibility.Public &&
                method.IsStatic &&
                destination.Matches(method.ReturnType) &&
                method.Parameters.Length == 1 &&
                source.Matches(method.Parameters[0].Type));
    }

    private static bool ParametersMatch(
        ImmutableArray<IParameterSymbol> actual,
        IReadOnlyList<ParameterRequirement> expected)
    {
        if (actual.Length != expected.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (actual[index].RefKind != expected[index].RefKind ||
                !expected[index].Type.Matches(actual[index].Type))
            {
                return false;
            }
        }

        return true;
    }

    private static MemberRequirement Constructor(
        params TypePattern[] parameterTypes)
    {
        return new MemberRequirement(symbol =>
            HasConstructor(
                symbol,
                Accessibility.Public,
                parameterTypes.Select(static type => Parameter(type)).ToArray()));
    }

    private static MemberRequirement Property(
        string name,
        TypePattern type)
    {
        return new MemberRequirement(symbol =>
            HasProperty(
                symbol,
                name,
                Accessibility.Public,
                isStatic: false,
                type));
    }

    private static ParameterRequirement Parameter(
        TypePattern type,
        RefKind refKind = RefKind.None)
    {
        return new ParameterRequirement(type, refKind);
    }

    private static TypePattern Named(
        string metadataName,
        params TypePattern[] typeArguments)
    {
        return new NamedTypePattern(metadataName, typeArguments);
    }

    private static TypePattern TypeParameter(int ordinal)
    {
        return new TypeParameterPattern(
            TypeParameterKind.Type,
            ordinal);
    }

    private static TypePattern MethodTypeParameter(int ordinal)
    {
        return new TypeParameterPattern(
            TypeParameterKind.Method,
            ordinal);
    }

    private sealed record TypeRequirement(
        string MetadataName,
        Func<INamedTypeSymbol, bool> HasCompatibleShape);

    private sealed record ParameterRequirement(
        TypePattern Type,
        RefKind RefKind);

    private sealed record MemberRequirement(
        Func<INamedTypeSymbol, bool> Matches);

    private abstract class TypePattern
    {
        public abstract bool Matches(ITypeSymbol symbol);
    }

    private sealed class NamedTypePattern(
        string metadataName,
        IReadOnlyList<TypePattern> typeArguments) : TypePattern
    {
        public override bool Matches(ITypeSymbol symbol)
        {
            if (symbol is not INamedTypeSymbol named ||
                GetFullMetadataName(named.OriginalDefinition) != metadataName ||
                named.TypeArguments.Length != typeArguments.Count)
            {
                return false;
            }

            for (var index = 0; index < typeArguments.Count; index++)
            {
                if (!typeArguments[index].Matches(named.TypeArguments[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class TypeParameterPattern(
        TypeParameterKind kind,
        int ordinal) : TypePattern
    {
        public override bool Matches(ITypeSymbol symbol)
        {
            return symbol is ITypeParameterSymbol parameter &&
                   parameter.TypeParameterKind == kind &&
                   parameter.Ordinal == ordinal;
        }
    }
}
