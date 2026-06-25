using System.Runtime.CompilerServices;

namespace Morphant.Generators;

[CompilerGenerated]
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
            AddressDto = context.Mapper.Map<Address?, AddressModel?>(source.Address, context)
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
        destination.AddressDto = context.Mapper.Map<Address?, AddressModel?>(source.Address, context);

        return destination;
    }
}
