using Morphant.Exceptions;

namespace Morphant.Generators;

public partial class UserMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder
            .Map<User, UserModel>()
            .Template(s => new(Auto())
            {
                FullName = s.FirstName + " " + s.LastName,
                IsActive = true,
                DisplayName = Ignore()
            });
    }
}

public partial class UserMapper : ITypeMapper<User, UserModel>
{
    public UserModel Map(User source, UserModel destination)
    {
        return new UserModel()
        {
            Id = source.Id,
            FullName = source.FirstName + " " + source.LastName,
            Email = source.Email,
            CreatedAt = source.CreatedAt,
            LastLoginAt = source.LastLoginAt,
            IsActive = true
        };
    }
}

public static class UserMapperExtensions
{
    public static MapperBuilder<User, UserModel> Template(this MapperBuilder<User, UserModel> builder, Func<User, UserModelMorphantTemplate> templateFunc) =>
        throw new RuntimeInvocationNotSupportedException();

    public static MapperBuilder<User, UserModel> Template(this MapperBuilder<User, UserModel> builder, Func<User, UserModel, UserModelMorphantTemplate> templateFunc) =>
        throw new RuntimeInvocationNotSupportedException();
}
