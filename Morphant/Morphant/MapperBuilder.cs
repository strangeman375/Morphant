using Morphant.Exceptions;

namespace Morphant;

public abstract class MapperBuilderBase<T>
    where T : MapperBuilderBase<T>
{
    private protected MapperBuilderBase()
    {
    }

    public T NullSourceHandling(NullSourceHandling nullSourceHandling) =>
        throw new RuntimeInvocationNotSupportedException();

    public T NullDestinationHandling(NullDestinationHandling nullDestinationHandling) =>
        throw new RuntimeInvocationNotSupportedException();

    public T NotNullableMembersValidation(NotNullableMembersValidation notNullableMembersValidation) =>
        throw new RuntimeInvocationNotSupportedException();

    public T IgnoreNullSourceValues(bool value = true) =>
        throw new RuntimeInvocationNotSupportedException();

    public T ConstructorSelection(ConstructorSelection constructorSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    public T MembersSelection(MembersSelection membersSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    public T MembersValidation(MembersValidation membersValidation) =>
        throw new RuntimeInvocationNotSupportedException();
}

public abstract class MapperBuilder : MapperBuilderBase<MapperBuilder>
{
    private MapperBuilder()
    {
    }

    public MapperBuilder MappingMode(MappingMode mappingMode) =>
        throw new RuntimeInvocationNotSupportedException();

    public MapperBuilder<TSource, TDestination> Map<TSource, TDestination>(MappingMode mappingMode = Morphant.MappingMode.Default) =>
        throw new RuntimeInvocationNotSupportedException();
}

public abstract class MapperBuilder<TSource, TDestination> : MapperBuilderBase<MapperBuilder<TSource, TDestination>>
{
    private MapperBuilder()
    {
    }

    public MapperBuilder<TSource, TDestination> IncludeMembers(Func<TSource, object?> membersFunc) =>
        throw new RuntimeInvocationNotSupportedException();
}
