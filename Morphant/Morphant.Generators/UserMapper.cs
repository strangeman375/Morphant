using Morphant.Exceptions;

namespace Morphant.Generators;

public partial class UserMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder
            .Map<User, UserModel>()
            .Template(s => new(ByFactory<UserModel>(() => new UserModelNew()))
            {
                FullName = s.FirstName + " " + s.LastName,
                IsActive = true,
                DisplayName = Ignore(),
                LastLoginAt = Auto(),
                AddressDto = Map(s.Address)
            });
    }
}

public partial class UserMapper : ITypeMapper<User, UserModel>
{
    public UserModel Map(User source, MappingContext context)
    {
        return new UserModel()
        {
            Id = source.Id,
            FullName = source.FirstName + " " + source.LastName,
            Email = source.Email,
            CreatedAt = source.CreatedAt,
            LastLoginAt = source.LastLoginAt,
            IsActive = true,
            AddressDto = context.Mapper.Map<Address?, AddressModel?>(source.Address)
        };
    }

    public UserModel Map(User source, UserModel destination, MappingContext context)
    {
        destination.Id = source.Id;
        destination.FullName = source.FirstName + " " + source.LastName;
        destination.Email = source.Email;
        destination.CreatedAt = source.CreatedAt;
        destination.LastLoginAt = source.LastLoginAt;
        destination.IsActive = true;
        destination.AddressDto = context.Mapper.Map<Address?, AddressModel?>(source.Address);

        return destination;
    }
}

public static class UserMapperMorphantExtensions
{
    public static MapperBuilder<User, UserModel> Template(this MapperBuilder<User, UserModel> builder, Func<User, UserModelMorphantTemplate> templateFunc) =>
        throw new RuntimeInvocationNotSupportedException();

    public static MapperBuilder<User, UserModel> Template(this MapperBuilder<User, UserModel> builder, Func<User, UserModel, UserModelMorphantTemplate> templateFunc) =>
        throw new RuntimeInvocationNotSupportedException();
}
