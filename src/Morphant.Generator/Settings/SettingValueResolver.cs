namespace Morphant.Generator.Settings;

internal static class SettingValueResolver
{
    public static TValue? Resolve<TValue>(
        TValue? assemblyValue,
        IEnumerable<TValue?> configuredValues,
        TValue libraryDefault)
        where TValue : struct, Enum
    {
        foreach (var configuredValue in configuredValues)
        {
            if (configuredValue is not { } value)
            {
                return null;
            }

            if (!IsDefault(value))
            {
                return value;
            }
        }

        if (assemblyValue is not { } assembly)
        {
            return null;
        }

        return IsDefault(assembly)
            ? libraryDefault
            : assembly;
    }

    private static bool IsDefault<TValue>(TValue value)
        where TValue : struct, Enum
    {
        return EqualityComparer<TValue>.Default.Equals(
            value,
            default);
    }
}
