using System.Collections.Immutable;

namespace Morphant.Generator.TemplateSurface.TemplateType;

internal sealed class TemplateTypeModelResultComparer :
    IEqualityComparer<TemplateTypeModelResult>
{
    public static TemplateTypeModelResultComparer Instance { get; } = new();

    private TemplateTypeModelResultComparer()
    {
    }

    public bool Equals(
        TemplateTypeModelResult x,
        TemplateTypeModelResult y)
    {
        return StringComparer.Ordinal.Equals(x.HintName, y.HintName) &&
               ModelEquals(x.Model, y.Model);
    }

    public int GetHashCode(TemplateTypeModelResult obj)
    {
        var hash = StringComparer.Ordinal.GetHashCode(obj.HintName);

        hash = AddHash(hash, obj.Model.TemplateNamespace);
        hash = AddHash(hash, obj.Model.TemplateTypeName);
        hash = AddHash(hash, obj.Model.DestinationTypeName);
        hash = AddHash(hash, obj.Model.CanConstructDestination);
        hash = AddHash(hash, obj.Model.DestinationDocumentation);

        foreach (var typeParameter in obj.Model.TypeParameters)
        {
            hash = AddHash(hash, typeParameter.Name);

            foreach (var constraint in typeParameter.Constraints)
            {
                hash = AddHash(hash, constraint);
            }
        }

        foreach (var constructor in obj.Model.Constructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                hash = AddHash(hash, parameter);
            }
        }

        foreach (var field in obj.Model.ConstructorFields)
        {
            hash = AddHash(hash, field);
        }

        foreach (var member in obj.Model.Members)
        {
            hash = AddHash(hash, member);
        }

        return hash;
    }

    private static bool ModelEquals(
        TemplateTypeModel x,
        TemplateTypeModel y)
    {
        return StringComparer.Ordinal.Equals(
                   x.TemplateNamespace,
                   y.TemplateNamespace) &&
               StringComparer.Ordinal.Equals(
                   x.TemplateTypeName,
                   y.TemplateTypeName) &&
               StringComparer.Ordinal.Equals(
                   x.DestinationTypeName,
                   y.DestinationTypeName) &&
               x.CanConstructDestination == y.CanConstructDestination &&
               x.DestinationDocumentation == y.DestinationDocumentation &&
               SequenceEqual(
                   x.TypeParameters,
                   y.TypeParameters,
                   TypeParameterEquals) &&
               SequenceEqual(
                   x.Constructors,
                   y.Constructors,
                   ConstructorEquals) &&
               x.ConstructorFields.SequenceEqual(y.ConstructorFields) &&
               x.Members.SequenceEqual(y.Members);
    }

    private static bool TypeParameterEquals(
        TemplateTypeParameterModel x,
        TemplateTypeParameterModel y)
    {
        return StringComparer.Ordinal.Equals(x.Name, y.Name) &&
               x.Constraints.SequenceEqual(
                   y.Constraints,
                   StringComparer.Ordinal);
    }

    private static bool ConstructorEquals(
        TemplateConstructorModel x,
        TemplateConstructorModel y)
    {
        return x.Parameters.SequenceEqual(y.Parameters);
    }

    private static bool SequenceEqual<T>(
        ImmutableArray<T> x,
        ImmutableArray<T> y,
        Func<T, T, bool> equals)
    {
        if (x.Length != y.Length)
        {
            return false;
        }

        for (var i = 0; i < x.Length; i++)
        {
            if (!equals(x[i], y[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static int AddHash<T>(int hash, T value)
    {
        return unchecked(
            hash * 31 +
            EqualityComparer<T>.Default.GetHashCode(value!));
    }
}
