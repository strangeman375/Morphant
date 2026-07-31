using System.Collections.Immutable;

namespace Morphant.Generator.TemplateSurface.TemplateExtension;

public readonly record struct TemplateExtensionRequest
(
    TemplateExtensionGenerationKind GenerationKind,
    TemplateDestinationTypeInfo CanonicalDestinationType,
    ImmutableArray<TemplateDestinationTypeInfo> MappingTypes,
    string HintName
)
{
    public bool Equals(TemplateExtensionRequest other)
    {
        return GenerationKind == other.GenerationKind &&
               CanonicalDestinationType.Equals(
                   other.CanonicalDestinationType) &&
               StringComparer.Ordinal.Equals(
                   HintName,
                   other.HintName) &&
               MappingTypes.SequenceEqual(other.MappingTypes);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (int)GenerationKind;
            hashCode =
                hashCode * 397 ^
                CanonicalDestinationType.GetHashCode();
            hashCode =
                hashCode * 397 ^
                StringComparer.Ordinal.GetHashCode(HintName);

            foreach (var mappingType in MappingTypes)
            {
                hashCode =
                    hashCode * 397 ^
                    mappingType.GetHashCode();
            }

            return hashCode;
        }
    }
}

public enum TemplateExtensionGenerationKind
{
    Generic,
    PairSpecific
}
