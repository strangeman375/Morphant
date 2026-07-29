namespace Morphant.Generator.Settings;

internal static class SettingValueResolver
{
    public static TValue? Resolve<TValue>(
        TValue? assemblyValue,
        TValue? rootValue,
        TValue? mappingValue,
        TValue libraryDefault)
        where TValue : struct, Enum
    {
        if (mappingValue is not { } value)
        {
            return null;
        }

        if (!IsDefault(value))
        {
            return value;
        }

        if (rootValue is not { } root)
        {
            return null;
        }

        if (!IsDefault(root))
        {
            return root;
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
