using Morphant.Exceptions;

namespace Morphant;

public abstract class MapperBuilder
{
    private MapperBuilder()
    {
    }

    public MapperBuilder MappingMode(MappingMode mappingMode) =>
        throw new RuntimeInvocationNotSupportedException();

    public MapperBuilder ConstructorSelection(ConstructorSelection constructorSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    public MapperBuilder MembersSelection(MembersSelection membersSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    public MapperBuilder MembersValidation(MembersValidation membersValidation) =>
        throw new RuntimeInvocationNotSupportedException();

    public MapperBuilder<TSource, TDestination> Map<TSource, TDestination>(MappingMode mappingMode = Morphant.MappingMode.Default) =>
        throw new RuntimeInvocationNotSupportedException();
}

public abstract class MapperBuilder<TSource, TDestination>
{
    private MapperBuilder()
    {
    }

    public MapperBuilder<TSource, TDestination> IncludeMembers(Func<TSource, object?> membersFunc) =>
        throw new RuntimeInvocationNotSupportedException();

    public MapperBuilder<TSource, TDestination> ConstructorSelection(ConstructorSelection constructorSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    public MapperBuilder<TSource, TDestination> MembersSelection(MembersSelection membersSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    public MapperBuilder<TSource, TDestination> MembersValidation(MembersValidation membersValidation) =>
        throw new RuntimeInvocationNotSupportedException();
}
