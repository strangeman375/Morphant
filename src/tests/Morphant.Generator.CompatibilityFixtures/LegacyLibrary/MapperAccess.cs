namespace LegacyLibrary;

public static class MapperAccess
{
    public static bool IsMapper(object value) => value is Morphant.IMapper;
}
