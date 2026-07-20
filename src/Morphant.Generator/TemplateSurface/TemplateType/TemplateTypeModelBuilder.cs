using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.TemplateSurface.TemplateType;

internal static class TemplateTypeModelBuilder
{
    private const string AllowNullAttributeMetadataName =
        "System.Diagnostics.CodeAnalysis.AllowNullAttribute";

    private const string DisallowNullAttributeMetadataName =
        "System.Diagnostics.CodeAnalysis.DisallowNullAttribute";

    private const string ObsoleteAttributeMetadataName =
        "System.ObsoleteAttribute";

    private static readonly SymbolDisplayFormat DocumentationCrefFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static TemplateTypeModel Build(
        INamedTypeSymbol destinationType,
        TemplateDestinationTypeInfo destinationTypeInfo,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var canConstructDestination =
            destinationType.TypeKind != TypeKind.Interface &&
            !destinationType.IsAbstract;

        var constructors = canConstructDestination
            ? BuildConstructors(
                destinationType,
                compilation,
                cancellationToken)
            : ImmutableArray<TemplateConstructorModel>.Empty;

        return new TemplateTypeModel(
            destinationTypeInfo.TemplateNamespace,
            destinationTypeInfo.TemplateTypeName,
            destinationType.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable),
            canConstructDestination,
            BuildDocumentation(destinationType, cancellationToken),
            constructors,
            BuildConstructorFields(constructors),
            BuildMembers(
                destinationType,
                destinationTypeInfo.TemplateTypeName,
                compilation,
                cancellationToken));
    }

    private static ImmutableArray<TemplateConstructorModel> BuildConstructors(
        INamedTypeSymbol destinationType,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<TemplateConstructorModel>();

        foreach (var constructor in destinationType.InstanceConstructors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsAccessible(constructor, compilation))
            {
                continue;
            }

            // ref/out/in и ref-like параметры требуют отдельного
            // представления в template surface.
            if (constructor.Parameters.Any(
                    static parameter =>
                        parameter.RefKind != RefKind.None ||
                        parameter.Type.IsRefLikeType))
            {
                continue;
            }

            var parameters =
                ImmutableArray.CreateBuilder<TemplateConstructorParameterModel>(
                    constructor.Parameters.Length);

            foreach (var parameter in constructor.Parameters)
            {
                cancellationToken.ThrowIfCancellationRequested();

                parameters.Add(
                    new TemplateConstructorParameterModel(
                        parameter.Name,
                        parameter.Type.ToDisplayString(
                            SymbolDisplayFormats.FullyQualifiedNullable),
                        BuildTypeSuffix(parameter.Type),
                        parameter.IsOptional || parameter.IsParams,
                        BuildDefaultValueDisplay(parameter)));
            }

            result.Add(new TemplateConstructorModel(parameters.ToImmutable()));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<TemplateConstructorFieldModel>
        BuildConstructorFields(
            ImmutableArray<TemplateConstructorModel> constructors)
    {
        var uniqueParameters =
            new List<TemplateConstructorParameterModel>();

        var parameterKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var constructor in constructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                var key = parameter.Name + "\0" + parameter.TypeName;

                if (parameterKeys.Add(key))
                {
                    uniqueParameters.Add(parameter);
                }
            }
        }

        var nameCounts = new Dictionary<string, int>(
            StringComparer.Ordinal);

        foreach (var parameter in uniqueParameters)
        {
            nameCounts.TryGetValue(parameter.Name, out var count);
            nameCounts[parameter.Name] = count + 1;
        }

        var usedFieldNames = new HashSet<string>(StringComparer.Ordinal);
        var result =
            ImmutableArray.CreateBuilder<TemplateConstructorFieldModel>();

        foreach (var parameter in uniqueParameters)
        {
            var fieldName = nameCounts[parameter.Name] == 1
                ? parameter.Name
                : parameter.Name + parameter.TypeSuffix;

            fieldName = MakeUnique(fieldName, usedFieldNames);

            result.Add(
                new TemplateConstructorFieldModel(
                    fieldName,
                    parameter.Name,
                    parameter.TypeName));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<TemplateMemberModel> BuildMembers(
        INamedTypeSymbol destinationType,
        string templateTypeName,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<TemplateMemberModel>();

        if (destinationType.TypeKind == TypeKind.Interface)
        {
            AddInterfaceMembers(
                destinationType,
                templateTypeName,
                compilation,
                result,
                cancellationToken);
        }
        else
        {
            AddClassMembers(
                destinationType,
                templateTypeName,
                compilation,
                result,
                cancellationToken);
        }

        return result.ToImmutable();
    }

    private static void AddClassMembers(
        INamedTypeSymbol destinationType,
        string templateTypeName,
        Compilation compilation,
        ImmutableArray<TemplateMemberModel>.Builder result,
        CancellationToken cancellationToken)
    {
        var hiddenMemberNames =
            new HashSet<string>(StringComparer.Ordinal);

        var memberGroups =
            new List<ImmutableArray<TemplateMemberModel>>();

        // Сначала обходим и разрешаем скрытие от производного типа к
        // базовому, затем выводим получившиеся группы в обратном порядке.
        // Так most-derived семантика сочетается с base-first выводом.
        for (var currentType = destinationType;
             currentType is not null;
             currentType = currentType.BaseType)
        {
            cancellationToken.ThrowIfCancellationRequested();

            memberGroups.Add(
                BuildDeclaredMembers(
                    currentType,
                    templateTypeName,
                    compilation,
                    hiddenMemberNames,
                    cancellationToken));
        }

        for (var i = memberGroups.Count - 1; i >= 0; i--)
        {
            result.AddRange(memberGroups[i]);
        }
    }

    private static ImmutableArray<TemplateMemberModel> BuildDeclaredMembers(
        INamedTypeSymbol declaringType,
        string templateTypeName,
        Compilation compilation,
        HashSet<string> hiddenMemberNames,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<TemplateMemberModel>();
        var declaredMembers = declaringType.GetMembers();

        foreach (var member in declaredMembers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (hiddenMemberNames.Contains(member.Name))
            {
                continue;
            }

            if (TryBuildMemberModel(
                    member,
                    templateTypeName,
                    compilation,
                    cancellationToken) is { } memberModel)
            {
                result.Add(memberModel);
            }
        }

        // Любое объявление в производном типе скрывает одноимённые
        // мемберы базового типа, даже если само объявление недоступно
        // или не может участвовать в маппинге.
        foreach (var member in declaredMembers)
        {
            hiddenMemberNames.Add(member.Name);
        }

        return result.ToImmutable();
    }

    private static void AddInterfaceMembers(
        INamedTypeSymbol destinationType,
        string templateTypeName,
        Compilation compilation,
        ImmutableArray<TemplateMemberModel>.Builder result,
        CancellationToken cancellationToken)
    {
        var interfaces = BuildBaseFirstInterfaceOrder(
            destinationType,
            cancellationToken);

        var winningDeclarations = BuildWinningInterfaceDeclarations(
            interfaces,
            cancellationToken);

        var emittedMemberNames =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (var currentInterface in interfaces)
        {
            foreach (var member in currentInterface.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!winningDeclarations.TryGetValue(
                        member.Name,
                        out var winningInterface) ||
                    !SymbolEqualityComparer.Default.Equals(
                        currentInterface,
                        winningInterface) ||
                    emittedMemberNames.Contains(member.Name))
                {
                    continue;
                }

                if (TryBuildMemberModel(
                        member,
                        templateTypeName,
                        compilation,
                        cancellationToken) is not { } memberModel)
                {
                    continue;
                }

                result.Add(memberModel);
                emittedMemberNames.Add(member.Name);
            }
        }
    }

    private static ImmutableArray<INamedTypeSymbol>
        BuildBaseFirstInterfaceOrder(
            INamedTypeSymbol destinationType,
            CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var visitedInterfaces =
            new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        AddInterfaceBaseFirst(
            destinationType,
            visitedInterfaces,
            result,
            cancellationToken);

        return result.ToImmutable();
    }

    private static void AddInterfaceBaseFirst(
        INamedTypeSymbol currentInterface,
        HashSet<ISymbol> visitedInterfaces,
        ImmutableArray<INamedTypeSymbol>.Builder result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!visitedInterfaces.Add(currentInterface))
        {
            return;
        }

        foreach (var baseInterface in currentInterface.Interfaces)
        {
            AddInterfaceBaseFirst(
                baseInterface,
                visitedInterfaces,
                result,
                cancellationToken);
        }

        result.Add(currentInterface);
    }

    private static Dictionary<string, INamedTypeSymbol>
        BuildWinningInterfaceDeclarations(
            ImmutableArray<INamedTypeSymbol> interfaces,
            CancellationToken cancellationToken)
    {
        var declarations =
            new Dictionary<string, List<INamedTypeSymbol>>(
                StringComparer.Ordinal);

        foreach (var currentInterface in interfaces)
        {
            foreach (var member in currentInterface.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!declarations.TryGetValue(
                        member.Name,
                        out var declaringInterfaces))
                {
                    declaringInterfaces = new List<INamedTypeSymbol>();
                    declarations.Add(member.Name, declaringInterfaces);
                }

                if (!ContainsSymbol(
                        declaringInterfaces,
                        currentInterface))
                {
                    declaringInterfaces.Add(currentInterface);
                }
            }
        }

        var result =
            new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);

        foreach (var declaration in declarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FindUniqueMostDerivedInterface(
                    declaration.Value,
                    cancellationToken) is { } winningInterface)
            {
                result.Add(declaration.Key, winningInterface);
            }
        }

        return result;
    }

    private static INamedTypeSymbol? FindUniqueMostDerivedInterface(
        List<INamedTypeSymbol> declaringInterfaces,
        CancellationToken cancellationToken)
    {
        INamedTypeSymbol? result = null;

        foreach (var candidate in declaringInterfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isHiddenByDerivedDeclaration = false;

            foreach (var other in declaringInterfaces)
            {
                if (!SymbolEqualityComparer.Default.Equals(candidate, other) &&
                    InheritsFromInterface(other, candidate))
                {
                    isHiddenByDerivedDeclaration = true;
                    break;
                }
            }

            if (isHiddenByDerivedDeclaration)
            {
                continue;
            }

            // Несколько unrelated most-derived объявлений делают имя
            // неоднозначным для плоской template surface.
            if (result is not null)
            {
                return null;
            }

            result = candidate;
        }

        return result;
    }

    private static bool InheritsFromInterface(
        INamedTypeSymbol derivedInterface,
        INamedTypeSymbol baseInterface)
    {
        foreach (var inheritedInterface in derivedInterface.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    inheritedInterface,
                    baseInterface))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSymbol(
        List<INamedTypeSymbol> symbols,
        INamedTypeSymbol candidate)
    {
        foreach (var symbol in symbols)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static TemplateMemberModel? TryBuildMemberModel(
        ISymbol member,
        string templateTypeName,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        if (IsTemplateRecordMemberName(member.Name, templateTypeName))
        {
            return null;
        }

        if (member is IPropertySymbol property)
        {
            if (property.IsStatic ||
                property.IsIndexer ||
                property.SetMethod is not { } setter ||
                !IsAccessible(property, compilation) ||
                !IsAccessible(setter, compilation) ||
                !IsSupportedMemberType(property.Type))
            {
                return null;
            }

            var setterParameter =
                setter.Parameters[setter.Parameters.Length - 1];

            var typeName = BuildMemberTypeName(
                property.Type,
                setterParameter.NullableAnnotation,
                property,
                setterParameter,
                out var requiresNullableAnnotationsDisabled);

            return new TemplateMemberModel(
                property.Name,
                typeName,
                BuildDocumentation(
                    property,
                    cancellationToken),
                requiresNullableAnnotationsDisabled,
                BuildObsoleteAttributeSource(property));
        }

        if (member is IFieldSymbol field)
        {
            if (field.IsStatic ||
                field.IsConst ||
                field.IsReadOnly ||
                field.IsImplicitlyDeclared ||
                !IsAccessible(field, compilation) ||
                !IsSupportedMemberType(field.Type))
            {
                return null;
            }

            var typeName = BuildMemberTypeName(
                field.Type,
                field.NullableAnnotation,
                field,
                null,
                out var requiresNullableAnnotationsDisabled);

            return new TemplateMemberModel(
                field.Name,
                typeName,
                BuildDocumentation(
                    field,
                    cancellationToken),
                requiresNullableAnnotationsDisabled,
                BuildObsoleteAttributeSource(field));
        }

        return null;
    }

    private static string BuildMemberTypeName(
        ITypeSymbol type,
        NullableAnnotation nullableAnnotation,
        ISymbol member,
        ISymbol? inputSymbol,
        out bool requiresNullableAnnotationsDisabled)
    {
        if (CanApplyNullableAnnotation(type))
        {
            if (HasAttribute(
                    member,
                    DisallowNullAttributeMetadataName) ||
                HasAttribute(
                    inputSymbol,
                    DisallowNullAttributeMetadataName))
            {
                nullableAnnotation = NullableAnnotation.NotAnnotated;
            }
            else if (HasAttribute(
                         member,
                         AllowNullAttributeMetadataName) ||
                     HasAttribute(
                         inputSymbol,
                         AllowNullAttributeMetadataName))
            {
                nullableAnnotation = NullableAnnotation.Annotated;
            }
        }

        requiresNullableAnnotationsDisabled =
            nullableAnnotation == NullableAnnotation.None &&
            CanApplyNullableAnnotation(type);

        return type
            .WithNullableAnnotation(nullableAnnotation)
            .ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable);
    }

    private static bool CanApplyNullableAnnotation(ITypeSymbol type)
    {
        return type.IsReferenceType ||
               type.TypeKind == TypeKind.TypeParameter;
    }

    private static bool IsSupportedMemberType(ITypeSymbol type)
    {
        return !type.IsRefLikeType &&
               type.TypeKind != TypeKind.Pointer &&
               type.TypeKind != TypeKind.FunctionPointer &&
               type.TypeKind != TypeKind.Error;
    }

    private static bool IsTemplateRecordMemberName(
        string memberName,
        string templateTypeName)
    {
        return memberName == templateTypeName ||
               memberName == "Clone" ||
               memberName == "EqualityContract" ||
               memberName == "Equals" ||
               memberName == "GetHashCode" ||
               memberName == "PrintMembers" ||
               memberName == "ToString";
    }

    private static bool HasAttribute(
        ISymbol? symbol,
        string attributeMetadataName)
    {
        if (symbol is null)
        {
            return false;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            if (HasMetadataName(
                    attribute.AttributeClass,
                    attributeMetadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static string? BuildObsoleteAttributeSource(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!HasMetadataName(
                    attribute.AttributeClass,
                    ObsoleteAttributeMetadataName))
            {
                continue;
            }

            var arguments = new List<string>();

            foreach (var argument in attribute.ConstructorArguments)
            {
                arguments.Add(FormatAttributeArgument(argument));
            }

            foreach (var argument in attribute.NamedArguments)
            {
                arguments.Add(
                    EscapeIdentifier(argument.Key) +
                    " = " +
                    FormatAttributeArgument(argument.Value));
            }

            const string attributeTypeName =
                "global::System.ObsoleteAttribute";

            return arguments.Count == 0
                ? attributeTypeName
                : attributeTypeName +
                  "(" +
                  string.Join(", ", arguments) +
                  ")";
        }

        return null;
    }

    private static string FormatAttributeArgument(TypedConstant argument)
    {
        if (argument.IsNull)
        {
            return "null";
        }

        return SymbolDisplay.FormatPrimitive(
                   argument.Value!,
                   quoteStrings: true,
                   useHexadecimalNumbers: false)
               ?? "default";
    }

    private static bool HasMetadataName(
        INamedTypeSymbol? type,
        string metadataName)
    {
        return type is not null &&
               SymbolNameHelper.GetFullMetadataName(type) == metadataName;
    }

    private static TemplateDocumentationModel BuildDocumentation(
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        var xml = symbol.GetDocumentationCommentXml(
            preferredCulture: CultureInfo.InvariantCulture,
            expandIncludes: false,
            cancellationToken: cancellationToken);

        return new TemplateDocumentationModel(
            symbol.ToDisplayString(DocumentationCrefFormat),
            !string.IsNullOrWhiteSpace(xml));
    }

    private static bool IsAccessible(
        ISymbol symbol,
        Compilation compilation)
    {
        return compilation.IsSymbolAccessibleWithin(
            symbol,
            compilation.Assembly);
    }

    private static string MakeUnique(
        string candidate,
        HashSet<string> usedNames)
    {
        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        for (var suffix = 2;; suffix++)
        {
            var name =
                candidate + suffix.ToString(CultureInfo.InvariantCulture);

            if (usedNames.Add(name))
            {
                return name;
            }
        }
    }

    private static string BuildTypeSuffix(ITypeSymbol type)
    {
        var displayName = type.ToDisplayString(
            SymbolDisplayFormat.MinimallyQualifiedFormat);

        var result = new StringBuilder(displayName.Length);
        var uppercaseNext = true;

        for (var i = 0; i < displayName.Length; i++)
        {
            var character = displayName[i];

            if (char.IsLetterOrDigit(character) || character == '_')
            {
                result.Append(
                    uppercaseNext
                        ? char.ToUpperInvariant(character)
                        : character);

                uppercaseNext = false;
                continue;
            }

            if (character == '?')
            {
                result.Append("Nullable");
                uppercaseNext = true;
                continue;
            }

            if (character == '[')
            {
                var rank = 1;

                while (++i < displayName.Length &&
                       displayName[i] != ']')
                {
                    if (displayName[i] == ',')
                    {
                        rank++;
                    }
                }

                result.Append("Array");

                if (rank > 1)
                {
                    result.Append(
                        rank.ToString(CultureInfo.InvariantCulture));
                }
            }

            uppercaseNext = true;
        }

        return result.Length == 0
            ? "Value"
            : result.ToString();
    }

    private static string? BuildDefaultValueDisplay(
        IParameterSymbol parameter)
    {
        if (!parameter.HasExplicitDefaultValue)
        {
            return null;
        }

        var value = parameter.ExplicitDefaultValue;

        if (value is null)
        {
            return CanRepresentNull(parameter.Type)
                ? "null"
                : "default";
        }

        if (parameter.Type is INamedTypeSymbol
            {
                TypeKind: TypeKind.Enum
            } enumType)
        {
            return BuildEnumDefaultValueDisplay(enumType, value);
        }

        return SymbolDisplay.FormatPrimitive(
                   value,
                   quoteStrings: true,
                   useHexadecimalNumbers: false)
               ?? Convert.ToString(
                   value,
                   CultureInfo.InvariantCulture);
    }

    private static string BuildEnumDefaultValueDisplay(
        INamedTypeSymbol enumType,
        object value)
    {
        foreach (var member in enumType
                     .GetMembers()
                     .OfType<IFieldSymbol>())
        {
            if (member.HasConstantValue &&
                object.Equals(member.ConstantValue, value))
            {
                return enumType.ToDisplayString(
                           SymbolDisplayFormat.MinimallyQualifiedFormat) +
                       "." +
                       EscapeIdentifier(member.Name);
            }
        }

        var numericValue =
            SymbolDisplay.FormatPrimitive(
                value,
                quoteStrings: true,
                useHexadecimalNumbers: false)
            ?? Convert.ToString(
                value,
                CultureInfo.InvariantCulture)
            ?? "0";

        return
            $"({enumType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)})" +
            numericValue;
    }

    private static bool CanRepresentNull(ITypeSymbol type)
    {
        return type.IsReferenceType ||
               type is IPointerTypeSymbol ||
               type is INamedTypeSymbol
               {
                   OriginalDefinition.SpecialType:
                       SpecialType.System_Nullable_T
               };
    }

    private static string EscapeIdentifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
    }
}
