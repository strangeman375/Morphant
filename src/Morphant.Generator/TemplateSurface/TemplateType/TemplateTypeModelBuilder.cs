using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.TemplateSurface.TemplateType;

internal static class TemplateTypeModelBuilder
{
    public static TemplateTypeModel Build(
        INamedTypeSymbol destinationType,
        TemplateDestinationTypeInfo destinationTypeInfo,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var constructors = BuildConstructors(
            destinationType,
            compilation,
            cancellationToken);

        return new TemplateTypeModel(
            destinationTypeInfo.TemplateNamespace,
            destinationTypeInfo.TemplateTypeName,
            destinationType.ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable),
            constructors,
            BuildConstructorFields(constructors),
            BuildMembers(
                destinationType,
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

            // ref/out/in-конструкторы требуют отдельной поддержки
            // на этапе генерации настоящего вызова конструктора.
            if (constructor.Parameters.Any(
                    static parameter => parameter.RefKind != RefKind.None))
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
                        parameter.IsOptional || parameter.IsParams));
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
                    parameter.TypeName));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<TemplateMemberModel> BuildMembers(
        INamedTypeSymbol destinationType,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<TemplateMemberModel>();

        // GetMembers сохраняет естественный порядок объявления.
        foreach (var member in destinationType.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (member is IPropertySymbol property)
            {
                if (property.IsStatic ||
                    property.IsIndexer ||
                    property.SetMethod is not { } setter ||
                    !IsAccessible(property, compilation) ||
                    !IsAccessible(setter, compilation))
                {
                    continue;
                }

                result.Add(
                    new TemplateMemberModel(
                        property.Name,
                        property.Type.ToDisplayString(
                            SymbolDisplayFormats.FullyQualifiedNullable)));

                continue;
            }

            if (member is IFieldSymbol field)
            {
                if (field.IsStatic ||
                    field.IsConst ||
                    field.IsReadOnly ||
                    field.IsImplicitlyDeclared ||
                    !IsAccessible(field, compilation))
                {
                    continue;
                }

                result.Add(
                    new TemplateMemberModel(
                        field.Name,
                        field.Type.ToDisplayString(
                            SymbolDisplayFormats.FullyQualifiedNullable)));
            }
        }

        return result.ToImmutable();
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
}
