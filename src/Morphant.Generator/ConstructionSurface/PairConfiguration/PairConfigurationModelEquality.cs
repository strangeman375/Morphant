namespace Morphant.Generator.ConstructionSurface.PairConfiguration;

internal static class PairConfigurationModelEquality
{
    public static bool Equal(
        PairConfigurationModel left,
        PairConfigurationModel right)
    {
        return StringComparer.Ordinal.Equals(
                   left.ExtensionContainerTypeName,
                   right.ExtensionContainerTypeName) &&
               StringComparer.Ordinal.Equals(
                   left.BuilderTypeName,
                   right.BuilderTypeName) &&
               StringComparer.Ordinal.Equals(
                   left.ReceiverTypeName,
                   right.ReceiverTypeName) &&
               StringComparer.Ordinal.Equals(
                   left.DeclarativeSourceTypeName,
                   right.DeclarativeSourceTypeName) &&
               StringComparer.Ordinal.Equals(
                   left.ManualSourceTypeName,
                   right.ManualSourceTypeName) &&
               StringComparer.Ordinal.Equals(
                   left.DestinationTypeName,
                   right.DestinationTypeName) &&
               StringComparer.Ordinal.Equals(
                   left.PreviousDestinationTypeName,
                   right.PreviousDestinationTypeName) &&
               left.HasStructuredConstruction ==
                   right.HasStructuredConstruction &&
               StringComparer.Ordinal.Equals(
                   left.ConstructionResultTypeName,
                   right.ConstructionResultTypeName) &&
               StringComparer.Ordinal.Equals(
                   left.MembersPlanTypeName,
                   right.MembersPlanTypeName) &&
               EqualTypeParameters(
                   left.TypeParameters,
                   right.TypeParameters);
    }

    private static bool EqualTypeParameters(
        IReadOnlyList<ConstructionPlan.ConstructionTypeParameterModel> left,
        IReadOnlyList<ConstructionPlan.ConstructionTypeParameterModel> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!StringComparer.Ordinal.Equals(
                    left[index].Name,
                    right[index].Name) ||
                left[index].RequiresNullableAnnotationsDisabled !=
                    right[index].RequiresNullableAnnotationsDisabled ||
                !left[index].Constraints.SequenceEqual(
                    right[index].Constraints,
                    StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
